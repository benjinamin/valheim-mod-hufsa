using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Server decides when and for whom (uniqueness, cooldowns, chance); the chosen client picks the spot
    /// near its own player and instantiates the prefab, since only it has that terrain loaded.
    /// </summary>
    internal class HufsaSpawner : MonoBehaviour
    {
        public const string RpcRequestSpawn = "Hufsa_RequestSpawn";
        public const string RpcWatcherGone = "Hufsa_WatcherGone";
        public const string RpcPreySafe = "Hufsa_PreySafe";
        public const string RpcPreyDied = "Hufsa_PreyDied";

        private static HufsaSpawner instance;
        // player uid -> time a replacement Watcher is due, after one vanished on them
        private readonly Dictionary<long, float> respawnAt = new Dictionary<long, float>();
        private float respawnTimer;

        private struct Target
        {
            public long Uid;
            public Vector3 Pos;
            public ZDOID CharId;
        }

        // ---- the hunt (server) ----
        // One hunt at a time. It follows its prey through portals, and moves on to another player if its prey
        // finds safety. It ends when the prey escapes on foot, at dawn, or when nobody is left to hunt.
        private bool huntActive;
        private long huntPrey;
        private float huntStarted;
        private bool hunterSeen;
        private Vector3 lastPreyPos;
        private float lastPreyJump = -999f;
        private float retargetAt = -1f;
        private long retargetExclude;
        private bool retargetNear;      // after a death: only players within RetargetAfterDeathRadius
        private Vector3 retargetCentre;
        private float followAt = -1f;

        private readonly Dictionary<long, float> watcherCooldownUntil = new Dictionary<long, float>();
        // player uid -> Watcher sightings, as reported by that player's client (HufsaDread)
        private readonly Dictionary<long, int> dread = new Dictionary<long, int>();
        private readonly List<ZDO> zdoBuffer = new List<ZDO>();
        private readonly List<Target> targets = new List<Target>();
        private float timer;
        private float hunterCooldownUntil;
        private bool wasNight;

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<string>(RpcRequestSpawn, (sender, prefab) => SpawnNearLocalPlayer(prefab, false));
            ZRoutedRpc.instance.Register(RpcWatcherGone, sender => instance?.OnWatcherGone(sender));
            ZRoutedRpc.instance.Register<long>(RpcPreySafe, (sender, preyUid) => instance?.OnPreySafe(preyUid));
            ZRoutedRpc.instance.Register<Vector3>(RpcPreyDied, (sender, where) => instance?.OnPreyDied(sender, where));
            ZRoutedRpc.instance.Register<int>(HufsaDread.RpcDread, (sender, count) =>
            {
                if (instance && ZNet.instance && ZNet.instance.IsServer()) instance.dread[sender] = count;
            });
        }

        private int DreadOf(long uid) => dread.TryGetValue(uid, out int d) ? d : 0;

        /// <summary>Client: the local player died with a Hunter nearby.</summary>
        internal static void ReportDeath(Vector3 where)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcPreyDied, where);
        }

        /// <summary>Server: a hunted player died. Go after someone else within the radius, or end the hunt.</summary>
        private void OnPreyDied(long uid, Vector3 where)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer() || !huntActive) return;
            HufsaPlugin.Log.LogInfo($"Hunt: {uid} died");
            retargetExclude = uid;
            retargetNear = true;
            retargetCentre = where;
            retargetAt = Time.time + 3f;
        }

        /// <summary>Of these, the player with the fewest other players within 50 m (ties at random).</summary>
        private Target PickLoneliest(List<Target> candidates)
        {
            Target best = candidates[Random.Range(0, candidates.Count)];
            int bestCount = int.MaxValue;
            foreach (Target c in candidates)
            {
                int friends = 0;
                foreach (Target o in targets)
                    if (o.Uid != c.Uid && Vector3.Distance(o.Pos, c.Pos) < 50f) friends++;
                if (friends < bestCount || (friends == bestCount && Random.value < 0.5f))
                {
                    bestCount = friends;
                    best = c;
                }
            }
            return best;
        }

        private void Awake() => instance = this;

        /// <summary>Hunter owner: everyone near it has been safe too long; ask the server for another prey.</summary>
        internal static void ReportPreySafe(long preyUid)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcPreySafe, preyUid);
        }

        private void OnPreySafe(long preyUid)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer() || !huntActive) return;
            HufsaPlugin.Log.LogInfo($"Hunt: prey {preyUid} is safe, looking for another");
            retargetExclude = preyUid;
            retargetNear = false;
            retargetAt = Time.time + 3f; // let the old Hunter finish vanishing first
        }

        private void StartHunt(long uid, Vector3 pos)
        {
            huntActive = true;
            huntPrey = uid;
            huntStarted = Time.time;
            hunterSeen = false;
            lastPreyPos = pos;
            lastPreyJump = -999f;
            retargetAt = -1f;
            followAt = -1f;
            hunterCooldownUntil = Time.time + HufsaConfig.HunterCooldownMinutes.Value * 60f;
            Send(uid, HufsaPrefabs.HunterName);
            // The hunt has come: their sightings start over.
            dread[uid] = 0;
            ZRoutedRpc.instance.InvokeRoutedRPC(uid, HufsaDread.RpcDreadReset);
            HufsaPlugin.Log.LogInfo($"Hunt: hunting {uid}");
        }

        /// <summary>A Hunter we didn't send (console 'hufsa hunter'): treat it as a hunt on the nearest player.</summary>
        private void AdoptHunter()
        {
            zdoBuffer.Clear();
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(HufsaPrefabs.HunterName, zdoBuffer, ref index)) { }
            if (zdoBuffer.Count == 0 || targets.Count == 0) return;
            Vector3 at = zdoBuffer[0].GetPosition();
            Target nearest = targets[0];
            foreach (Target t in targets)
                if (Vector3.Distance(t.Pos, at) < Vector3.Distance(nearest.Pos, at)) nearest = t;
            huntActive = true;
            huntPrey = nearest.Uid;
            huntStarted = Time.time;
            hunterSeen = true;
            lastPreyPos = nearest.Pos;
            lastPreyJump = -999f;
            retargetAt = followAt = -1f;
            hunterCooldownUntil = Time.time + HufsaConfig.HunterCooldownMinutes.Value * 60f;
            dread[huntPrey] = 0;
            HufsaPlugin.Log.LogInfo($"Hunt: adopted a Hunter (a Watcher turned, or the console), hunting {huntPrey}");
        }

        private void EndHunt(string why)
        {
            if (!huntActive) return;
            huntActive = false;
            retargetAt = -1f;
            followAt = -1f;
            hunterCooldownUntil = Time.time + HufsaConfig.HunterCooldownMinutes.Value * 60f;
            HufsaPlugin.Log.LogInfo($"Hunt over: {why}");
        }

        /// <summary>Can this player be hunted right now? Not indoors/at sea/in the Ashlands (their own client
        /// reports that), not in a dungeon, and it must be Hufsa time where they are.</summary>
        private static bool Huntable(Target t) =>
            t.Pos.y < 3000f && HufsaEntity.IsHufsaTime(t.Pos) &&
            HufsaHuntState.IsHuntable(ZDOMan.instance.GetZDO(t.CharId));

        private void HuntTick()
        {
            if (!huntActive)
            {
                AdoptHunter();
                return;
            }
            float now = Time.time;
            int pi = targets.FindIndex(x => x.Uid == huntPrey);
            if (pi >= 0)
            {
                if (Vector3.Distance(targets[pi].Pos, lastPreyPos) > 250f) lastPreyJump = now; // teleported
                lastPreyPos = targets[pi].Pos;
            }

            if (AnyExists(HufsaPrefabs.HunterName, Vector3.zero, float.MaxValue))
            {
                hunterSeen = true;
                return;
            }

            if (retargetAt > 0f)
            {
                if (now < retargetAt) return;
                retargetAt = -1f;
                float radius = HufsaConfig.RetargetAfterDeathRadius.Value;
                var others = targets.FindAll(t => t.Uid != retargetExclude && Huntable(t) &&
                                                  (!retargetNear || Vector3.Distance(t.Pos, retargetCentre) <= radius));
                if (others.Count == 0)
                {
                    EndHunt(retargetNear ? "prey died, nobody else near" : "everyone is safe");
                    return;
                }
                Target next = PickLoneliest(others);
                StartHunt(next.Uid, next.Pos);
                return;
            }

            // Its prey logged out: that counts as safe; move on to someone else.
            if (pi < 0 && followAt < 0f)
            {
                HufsaPlugin.Log.LogInfo($"Hunt: prey {huntPrey} left the game");
                retargetExclude = huntPrey;
                retargetNear = false;
                retargetAt = now + 3f;
                return;
            }

            if (followAt > 0f)
            {
                if (now < followAt) return;
                followAt = -1f;
                if (pi >= 0 && Huntable(targets[pi])) StartHunt(huntPrey, targets[pi].Pos);
                else EndHunt("followed prey can't be hunted there");
                return;
            }

            if (!hunterSeen)
            {
                if (now - huntStarted > 20f) EndHunt("Hunter never appeared");
                return;
            }

            // The Hunter is gone and nobody said why: either its prey ran away, it was dawn, or the prey
            // teleported and left it behind. Only the last one continues the hunt.
            if (pi >= 0 && now - lastPreyJump < 20f && HufsaConfig.FollowThroughPortals.Value)
            {
                HufsaPlugin.Log.LogInfo($"Hunt: prey {huntPrey} teleported, following in {HufsaConfig.FollowDelay.Value:0}s");
                followAt = now + HufsaConfig.FollowDelay.Value;
                hunterSeen = false;
                return;
            }
            EndHunt("prey escaped, or dawn");
        }

        /// <summary>Server: a Watcher slipped away from this player; queue a new one for them.</summary>
        private void OnWatcherGone(long uid)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            respawnAt[uid] = Time.time + HufsaConfig.RespawnAfterVanish.Value;
        }

        private void Update()
        {
            if (!ZNet.instance || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null || ZDOMan.instance == null) return;

            respawnTimer += Time.deltaTime;
            if (respawnTimer >= 1f)
            {
                respawnTimer = 0f;
                CollectTargets();
                HuntTick();
                if (respawnAt.Count > 0) ProcessRespawns();
            }

            timer += Time.deltaTime;
            if (timer < HufsaConfig.SpawnCheckInterval.Value) return;
            timer = 0f;

            CollectTargets();

            bool night = EnvMan.IsNight();
            bool nightfall = night && !wasNight;
            wasNight = night;
            if (nightfall && HufsaConfig.ForceEveryNight.Value && targets.Count > 0)
            {
                ForceNightSpawns();
                return;
            }

            foreach (Target t in targets) TrySpawnFor(t);
        }

        /// <summary>Sends the due replacement Watchers, if it's still Watcher time there and no Hunter is out.</summary>
        private void ProcessRespawns()
        {
            float now = Time.time;
            if (huntActive) return; // keep them queued until the hunt is over
            var due = new List<long>();
            foreach (KeyValuePair<long, float> kv in respawnAt) if (now >= kv.Value) due.Add(kv.Key);
            foreach (long uid in due)
            {
                respawnAt.Remove(uid);
                int i = targets.FindIndex(x => x.Uid == uid);
                if (i < 0) continue; // that player has left
                Vector3 pos = targets[i].Pos;
                if (pos.y > 3000f || !HufsaConfig.WatcherEnabled.Value || !HufsaEntity.WatcherMayAppear(pos)) continue;
                if (AnyExists(HufsaPrefabs.HunterName, pos, float.MaxValue)) continue;
                if (AnyExists(HufsaPrefabs.WatcherName, pos, 250f)) continue;
                watcherCooldownUntil[uid] = now + HufsaConfig.WatcherCooldown.Value;
                Send(uid, HufsaPrefabs.WatcherName);
            }
        }

        /// <summary>Test mode: guaranteed Hunter for one player and a Watcher for everyone at nightfall.</summary>
        private void ForceNightSpawns()
        {
            HufsaPlugin.Log.LogInfo($"Nightfall: forcing Hufsa spawns for {targets.Count} player(s)");
            var eligible = targets.FindAll(t => t.Pos.y < 3000f);
            if (eligible.Count == 0) return;

            // The Hunter takes priority; Watchers only come out on nights without one.
            if (huntActive || AnyExists(HufsaPrefabs.HunterName, Vector3.zero, float.MaxValue)) return;
            var prey = eligible.FindAll(Huntable);
            if (HufsaConfig.HunterEnabled.Value && prey.Count > 0)
            {
                Target t = PickLoneliest(prey);
                StartHunt(t.Uid, t.Pos);
            }
            else if (HufsaConfig.WatcherEnabled.Value)
            {
                foreach (Target t in eligible)
                {
                    watcherCooldownUntil[t.Uid] = Time.time + HufsaConfig.WatcherCooldown.Value;
                    Send(t.Uid, HufsaPrefabs.WatcherName);
                }
            }
        }

        private void CollectTargets()
        {
            targets.Clear();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers())
            {
                if (peer.IsReady() && !peer.m_characterID.IsNone())
                    targets.Add(new Target { Uid = peer.m_uid, Pos = peer.GetRefPos(), CharId = peer.m_characterID });
            }
            if (!ZNet.instance.IsDedicated() && Player.m_localPlayer)
                targets.Add(new Target
                {
                    Uid = ZNet.GetUID(), Pos = Player.m_localPlayer.transform.position, CharId = Player.m_localPlayer.GetZDOID(),
                });
        }

        private void TrySpawnFor(Target t)
        {
            if (t.Pos.y > 3000f) return; // dungeons / interiors
            bool hunterTime = HufsaEntity.IsHufsaTime(t.Pos);
            // The server can't see the player's weather; the client checks storm/fog before spawning.
            bool watcherTime = HufsaEntity.WatcherMayAppear(t.Pos);
            if (!hunterTime && !watcherTime) return;
            float now = Time.time;

            // While a hunt is on, nothing else appears: no second Hunter and no Watchers.
            if (huntActive || AnyExists(HufsaPrefabs.HunterName, t.Pos, float.MaxValue)) return;

            int seen = DreadOf(t.Uid);
            if (hunterTime && HufsaConfig.HunterEnabled.Value && now >= hunterCooldownUntil && Huntable(t))
            {
                // Seen it often enough: it comes for this player (not right away; within a minute or so).
                if (seen >= HufsaConfig.SightingsForHunt.Value && Random.value < 0.5f)
                {
                    StartHunt(t.Uid, t.Pos);
                    return;
                }
                // Optional hunts without warning: whoever is most alone.
                if (Random.value < HufsaConfig.HunterChance.Value)
                {
                    Target prey = PickLoneliest(targets.FindAll(Huntable));
                    StartHunt(prey.Uid, prey.Pos);
                    return;
                }
            }

            // Watchers come more often (and sooner after each other) the more this player has seen them.
            float escalate = 1f + seen * HufsaConfig.WatcherEscalation.Value;
            float chance = Mathf.Min(0.9f, HufsaConfig.WatcherChance.Value * escalate);
            if (watcherTime && HufsaConfig.WatcherEnabled.Value &&
                (!watcherCooldownUntil.TryGetValue(t.Uid, out float until) || now >= until) &&
                Random.value < chance && !AnyExists(HufsaPrefabs.WatcherName, t.Pos, 250f))
            {
                watcherCooldownUntil[t.Uid] = now + HufsaConfig.WatcherCooldown.Value / escalate;
                Send(t.Uid, HufsaPrefabs.WatcherName);
            }
        }

        private bool AnyExists(string prefab, Vector3 pos, float range)
        {
            zdoBuffer.Clear();
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, zdoBuffer, ref index)) { }
            foreach (ZDO zdo in zdoBuffer)
            {
                if (range == float.MaxValue || Vector3.Distance(zdo.GetPosition(), pos) < range) return true;
            }
            return false;
        }

        private static void Send(long uid, string prefab)
        {
            HufsaPlugin.Log.LogDebug($"Requesting {prefab} for peer {uid}");
            ZRoutedRpc.instance.InvokeRoutedRPC(uid, RpcRequestSpawn, prefab);
        }

        private static int treeMask = -1, losMask;
        private static readonly Collider[] spotHits = new Collider[96];
        private static readonly HashSet<TreeBase> spotTrees = new HashSet<TreeBase>();

        /// <summary>
        /// A Watcher spot must be visible from the player's eyes. In wooded biomes it must also stand between
        /// trees (at least two within 7 m, none where it would stand) or be a hilltop; in open biomes any visible
        /// spot will do. Used for spawning and for its look-away moves.
        /// </summary>
        internal static bool IsGoodWatcherSpot(Vector3 c, Vector3 eye, out bool hilltop)
        {
            if (treeMask < 0)
            {
                treeMask = LayerMask.GetMask("Default", "static_solid", "Default_small");
                losMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            }
            hilltop = IsHilltop(c);
            if (Physics.Linecast(eye, c + Vector3.up * 2.3f, losMask, QueryTriggerInteraction.Ignore)) return false;
            if (!HufsaEntity.IsWoodedBiome(c)) return true;

            spotTrees.Clear();
            Vector3 centre = c + Vector3.up * 1.5f;
            int n = Physics.OverlapSphereNonAlloc(centre, 7f, spotHits, treeMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                TreeBase tree = spotHits[i].GetComponentInParent<TreeBase>();
                if (!tree) continue;
                Vector3 nearest = spotHits[i].bounds.ClosestPoint(centre);
                nearest.y = c.y;
                if ((nearest - c).sqrMagnitude < 1.6f * 1.6f) return false; // a trunk where its body would be
                spotTrees.Add(tree);
            }
            return hilltop || spotTrees.Count >= 2;
        }

        /// <summary>Higher than everything 12 m around it, and clearly so (2 m above the average).</summary>
        private static bool IsHilltop(Vector3 c)
        {
            float sum = 0f;
            int n = 0;
            for (int k = 0; k < 8; k++)
            {
                Vector3 s = c + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 12f;
                if (!ZoneSystem.instance.GetGroundHeight(s, out float h)) continue;
                if (h > c.y + 0.5f) return false;
                sum += h;
                n++;
            }
            return n >= 6 && c.y - sum / n >= 2f;
        }

        /// <summary>Runs on the client that owns the target player. Returns the spawned entity or null.</summary>
        internal static HufsaEntity SpawnNearLocalPlayer(string prefabName, bool force)
        {
            Player player = Player.m_localPlayer;
            if (!player || player.IsDead() || player.InIntro() || player.IsTeleporting()) return null;
            Vector3 p = player.transform.position;
            if (p.y > 3000f) return null;
            bool hunter = prefabName == HufsaPrefabs.HunterName;
            if (!force && !(hunter ? HufsaEntity.IsHufsaTime(p) : HufsaEntity.IsWatcherTime(p))) return null;
            if (hunter && !force && !HufsaHuntState.IsHuntable(player)) return null; // indoors, at sea...

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (!prefab) return null;
            bool mist = !hunter && HufsaEntity.LowVisibility(p); // Mistlands or daytime storm/fog: stand closer
            Vector3 forward = GameCamera.instance ? GameCamera.instance.transform.forward : player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            // Hunter: somewhere behind the player.
            // Watcher: in front of the player, either standing between trees or on top of a hill, where the
            // player can actually see it; hilltops and spots at or above the player's height win.
            Vector3 eye = GameCamera.instance ? GameCamera.instance.transform.position : p + Vector3.up * 1.7f;
            Vector3 best = Vector3.zero, anyBest = Vector3.zero;
            bool found = false, anyFound = false;
            float bestScore = -1f;
            int good = 0;
            for (int i = 0; i < (hunter ? 25 : 100); i++)
            {
                float angle = hunter ? 180f + Random.Range(-70f, 70f) : Random.Range(-60f, 60f);
                float dist = hunter
                    ? Random.Range(HufsaConfig.HunterMinDistance.Value, HufsaConfig.HunterMaxDistance.Value)
                    : mist
                        ? Random.Range(HufsaConfig.MistlandsMinDistance.Value, HufsaConfig.MistlandsMaxDistance.Value)
                        : Random.Range(HufsaConfig.WatcherMinDistance.Value, HufsaConfig.WatcherMaxDistance.Value);
                if (!hunter && HufsaDread.Instance)
                {
                    // Each sighting brings the next one closer (up to 30% closer just before a hunt).
                    float dreadT = Mathf.Clamp01(HufsaDread.Instance.Count / (float)Mathf.Max(1, HufsaConfig.SightingsForHunt.Value));
                    dist = Mathf.Max(dist * Mathf.Lerp(1f, 0.7f, dreadT), HufsaConfig.WatcherVanishDistance.Value + 10f);
                }
                Vector3 c = p + Quaternion.Euler(0f, angle, 0f) * forward * dist;
                if (!ZoneSystem.instance.GetGroundHeight(c, out float h)) continue;
                if (h < ZoneSystem.instance.m_waterLevel + 0.3f) continue;
                c.y = h;
                if (HufsaEntity.BiomeAt(c) == Heightmap.Biome.AshLands) continue;
                if (EffectArea.IsPointInsideArea(c, EffectArea.Type.PlayerBase, 5f)) continue;

                if (hunter)
                {
                    best = c;
                    found = true;
                    break;
                }

                if (!anyFound || c.y > anyBest.y)
                {
                    anyBest = c; // fallback for forced (console) spawns only
                    anyFound = true;
                }
                if (!IsGoodWatcherSpot(c, eye, out bool hill)) continue;
                good++;
                float score = (hill ? 2f : 0f) + (c.y >= p.y - 1f ? 1f : 0f) + Random.value * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                    found = true;
                }
                if (hill || good >= 8) break;
            }
            if (!hunter && !found)
            {
                if (!force || !anyFound)
                {
                    HufsaPlugin.Log.LogInfo("Watcher skipped: no visible spot (between trees or on a hilltop in wooded biomes)");
                    return null;
                }
                best = anyBest;
                found = true;
            }
            if (!found) return null;
            if (!hunter && !force && best.y < p.y - 3f)
            {
                HufsaPlugin.Log.LogInfo("Watcher skipped: player is above every candidate spot");
                return null;
            }

            Vector3 look = p - best;
            look.y = 0f;
            GameObject go = Instantiate(prefab, best, Quaternion.LookRotation(look));
            HufsaPlugin.Log.LogInfo($"Spawned {prefabName} at {best} ({Vector3.Distance(p, best):0}m)");
            HufsaEntity entity = go.GetComponent<HufsaEntity>();
            if (force && entity) entity.MarkForced();
            return entity;
        }
    }
}
