using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Shared by Watcher and Hunter: registry, synced vanish, and the per-player visuals: the face reveal
    /// by distance, the hem moving while gliding, and the Watcher always facing whoever looks at it.
    /// Standing still Hufsa does not move at all.
    /// </summary>
    internal class HufsaEntity : MonoBehaviour
    {
        internal static readonly List<HufsaEntity> Instances = new List<HufsaEntity>();

        private const string VanishKey = "hufsa_vanish";
        private const string ForcedKey = "hufsa_forced";
        private const float VanishDuration = 1f;
        private const float ShrinkDuration = 0.35f;

        public bool IsHunter;
        internal ZNetView NView;

        private Transform visual;
        private Vector3 visualBasePos;
        private Vector3 visualBaseScale;
        private ParticleSystem[] particles;
        private float vanishTime = -1f;

        // face reveal
        private readonly List<Material> pupilMats = new List<Material>();
        private readonly List<Material> toothMats = new List<Material>();
        private readonly List<Renderer> toothRenderers = new List<Renderer>();
        private Renderer noseRenderer;
        private RevealFader noseFader, armFader, fingerFader;
        private HufsaArms arms;

        // hem
        private Mesh bodyMesh;
        private Vector3[] hemWork;
        private float motion;
        private bool hemDirty;
        private Vector3 lastPos;

        // mild biome (no snowfall, less frost), re-checked as it moves
        public bool Mild { get; private set; }
        private ParticleSystem snowfall;
        private float biomeCheck;

        /// <summary>Hunter: current ground speed (m/s), as seen on this client.</summary>
        public float GlideSpeed { get; private set; }

        /// <summary>The hem-animated body vertices while the Hunter glides (null when the body is at rest).</summary>
        public Vector3[] MovingBodyVertices => hemDirty ? hemWork : null;

        public bool Vanishing => vanishTime >= 0f || fadeTime >= 0f;
        public bool Forced => NView && NView.IsValid() && NView.GetZDO().GetBool(ForcedKey, false);

        private void Awake()
        {
            NView = GetComponent<ZNetView>();
            visual = transform.Find(HufsaPrefabs.VisualName);
            if (visual)
            {
                visualBasePos = visual.localPosition;
                visualBaseScale = visual.localScale;
                foreach (Transform child in visual)
                {
                    var r = child.GetComponent<Renderer>();
                    if (!r) continue;
                    if (child.name == HufsaPrefabs.PupilName) pupilMats.Add(r.material);
                    else if (child.name == HufsaPrefabs.ToothName) { toothMats.Add(r.material); toothRenderers.Add(r); }
                    else if (child.name == HufsaPrefabs.NoseName) noseRenderer = r;
                }
                if (HufsaModel.BodyRest != null)
                {
                    var filter = visual.Find(HufsaPrefabs.BodyName)?.GetComponent<MeshFilter>();
                    if (filter)
                    {
                        bodyMesh = filter.mesh; // own copy, so this Hufsa's hem moves on its own
                        hemWork = new Vector3[HufsaModel.BodyRest.Length];
                    }
                }
            }
            particles = GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particles) if (ps.name == "FrostSnow") snowfall = ps;
            if (NView && !IsHunter)
            {
                NView.Register(BecomeHunterRpc, sender =>
                {
                    if (NView.IsValid() && NView.IsOwner()) ZNetScene.instance.Destroy(gameObject);
                });
                NView.Register(FadeOutRpc, sender =>
                {
                    if (NView.IsValid() && NView.IsOwner()) VanishSilently();
                });
            }
            lastPos = transform.position;
            Instances.Add(this);
        }

        /// <summary>Raid-style warning, once per Hunter, for every player it shows up near.</summary>
        private void Start()
        {
            // after every Awake, so the arms have found their renderers
            if (noseRenderer) noseFader = new RevealFader(new List<Renderer> { noseRenderer });
            arms = GetComponent<HufsaArms>();
            if (arms)
            {
                armFader = new RevealFader(arms.ArmRenderers);
                fingerFader = new RevealFader(arms.FingerRenderers);
            }
            UpdateReveal();

            if (!IsHunter || !Player.m_localPlayer || !MessageHud.instance) return;
            if (NView && NView.IsValid() && NView.GetZDO().GetBool(VanishKey, false)) return;
            float d = Vector3.Distance(Player.m_localPlayer.transform.position, transform.position);
            if (d > HufsaConfig.EscapeDistance.Value) return;
            MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "<color=#E0301E>Someone is hunting you!</color>");
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            foreach (Material m in pupilMats) if (m) Destroy(m);
            foreach (Material m in toothMats) if (m) Destroy(m);
            foreach (Material m in fadeMats) if (m) Destroy(m);
            noseFader?.Destroy();
            armFader?.Destroy();
            fingerFader?.Destroy();
            if (bodyMesh) Destroy(bodyMesh);
        }

        public void MarkForced()
        {
            if (NView && NView.IsValid()) NView.GetZDO().Set(ForcedKey, true);
        }

        public void Vanish()
        {
            if (!NView || !NView.IsValid() || !NView.IsOwner()) return;
            NView.GetZDO().Set(VanishKey, true);
        }

        /// <summary>Gone without a puff: it quietly fades out (synced, so everyone sees it fade).</summary>
        public void VanishSilently()
        {
            if (!NView || !NView.IsValid() || !NView.IsOwner()) return;
            NView.GetZDO().Set(FadeKey, true);
        }

        /// <summary>1 normally, falling to 0 while fading out. The silhouette glow follows it.</summary>
        public float FadeAlpha { get; private set; } = 1f;

        private const string FadeKey = "hufsa_fade";
        private const float FadeDuration = 0.8f;
        private float fadeTime = -1f;
        private readonly List<Material> fadeMats = new List<Material>();
        private readonly List<float> fadeBaseAlpha = new List<float>();

        /// <summary>
        /// The body's shader can't be made see-through, so for the fade every part gets an unlit transparent
        /// copy of its material (same texture and colour), whose alpha then drops to nothing.
        /// </summary>
        private void BeginFade()
        {
            fadeTime = 0f;
            HufsaSoundBank.PlayDespawn(transform.position);
            foreach (ParticleSystem ps in particles)
            {
                var em = ps.emission;
                em.enabled = false;
            }
            if (!visual) return;
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponent<SilhouetteGlow>()) continue;
                Material m = r.material;
                if (m.shader != HufsaPrefabs.SpriteShader && HufsaPrefabs.SpriteShader)
                {
                    var see = new Material(HufsaPrefabs.SpriteShader)
                    {
                        mainTexture = m.HasProperty("_MainTex") ? m.mainTexture : null,
                        color = m.HasProperty("_Color") ? m.color : Color.white,
                    };
                    Destroy(m);
                    r.material = see;
                    m = see;
                }
                fadeMats.Add(m);
                fadeBaseAlpha.Add(m.color.a);
            }
        }

        private void UpdateFade()
        {
            if (fadeTime < 0f) return;
            fadeTime += Time.deltaTime;
            FadeAlpha = 1f - Mathf.Clamp01(fadeTime / FadeDuration);
            for (int i = 0; i < fadeMats.Count; i++)
            {
                if (!fadeMats[i]) continue;
                Color c = fadeMats[i].color;
                c.a = fadeBaseAlpha[i] * FadeAlpha;
                fadeMats[i].color = c;
            }
            if (fadeTime >= FadeDuration + 0.1f && NView.IsOwner()) ZNetScene.instance.Destroy(gameObject);
        }

        public static Heightmap.Biome BiomeAt(Vector3 pos) =>
            WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(pos) : Heightmap.Biome.None;

        /// <summary>Hunter: night, or daytime in the Deep North if allowed. Never in the Ashlands.</summary>
        public static bool IsHufsaTime(Vector3 pos)
        {
            Heightmap.Biome b = BiomeAt(pos);
            if (b == Heightmap.Biome.AshLands) return false;
            if (EnvMan.IsNight()) return true;
            return HufsaConfig.DeepNorthDaytime.Value && b == Heightmap.Biome.DeepNorth;
        }

        /// <summary>
        /// Watcher, without weather (the server can't see it): any night, by day in the any-weather biomes
        /// or possibly in the bad-weather biomes. Never in the Ashlands.
        /// </summary>
        public static bool WatcherMayAppear(Vector3 pos)
        {
            Heightmap.Biome b = BiomeAt(pos);
            if (b == Heightmap.Biome.AshLands) return false;
            return EnvMan.IsNight() || InList(HufsaConfig.WatcherDayBiomes.Value, b) ||
                   InList(HufsaConfig.WatcherBadWeatherBiomes.Value, b);
        }

        /// <summary>Watcher, full check on a client that knows its own weather.</summary>
        public static bool IsWatcherTime(Vector3 pos)
        {
            if (!WatcherMayAppear(pos)) return false;
            if (EnvMan.IsNight() || InList(HufsaConfig.WatcherDayBiomes.Value, BiomeAt(pos))) return true;
            return IsBadWeather();
        }

        public static string CurrentWeather() =>
            EnvMan.instance ? EnvMan.instance.GetCurrentEnvironment()?.m_name ?? "" : "";

        public static bool IsBadWeather()
        {
            string env = CurrentWeather().ToLowerInvariant();
            if (env.Length == 0) return false;
            foreach (string k in HufsaConfig.BadWeatherKeywords.Value.Split(','))
            {
                string key = k.Trim().ToLowerInvariant();
                if (key.Length > 0 && env.Contains(key)) return true;
            }
            return false;
        }

        /// <summary>Swamp / Mistlands by default: cover blocks the view so often that one block isn't enough.</summary>
        public static bool IsDenseBiome(Vector3 pos) => InList(HufsaConfig.DenseBiomes.Value, BiomeAt(pos));

        /// <summary>Biomes with plenty of trees: the Watcher stands between trees there (or on a hilltop).</summary>
        public static bool IsWoodedBiome(Vector3 pos) => InList(HufsaConfig.WoodedBiomes.Value, BiomeAt(pos));

        /// <summary>Meadows / Black Forest by default: no snowfall and only a light frost there.</summary>
        public static bool IsMildBiome(Vector3 pos) => InList(HufsaConfig.MildBiomes.Value, BiomeAt(pos));

        /// <summary>Frost strength for this Hufsa's surroundings (1 normally, less in the mild biomes).</summary>
        public float FrostFactor => Mild ? Mathf.Clamp01(HufsaConfig.MildFrostFactor.Value) : 1f;

        private static bool InList(string list, Heightmap.Biome biome)
        {
            foreach (string part in list.Split(','))
            {
                if (string.Equals(part.Trim(), biome.ToString(), System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// You can't see far: in the Mistlands, or in daytime storm/fog. The Watcher then stands closer and
        /// lets you get nearer. Uses this client's weather, so call it on the client that owns the Watcher.
        /// </summary>
        public static bool LowVisibility(Vector3 pos) =>
            BiomeAt(pos) == Heightmap.Biome.Mistlands || (!EnvMan.IsNight() && IsBadWeather());

        private static readonly RaycastHit[] lineHits = new RaycastHit[32];
        private static readonly HashSet<Transform> lineRoots = new HashSet<Transform>();
        private static int obstacleMask = -1;

        /// <summary>How many separate things (trees, rocks, buildings; not the ground) stand between two points.</summary>
        public static int ObstaclesBetween(Vector3 a, Vector3 b)
        {
            if (obstacleMask < 0) obstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.1f) return 0;
            int n = Physics.RaycastNonAlloc(a, d / len, lineHits, len, obstacleMask, QueryTriggerInteraction.Ignore);
            lineRoots.Clear();
            for (int i = 0; i < n; i++)
                if (lineHits[i].collider) lineRoots.Add(lineHits[i].collider.transform.root);
            return lineRoots.Count;
        }

        /// <summary>
        /// How close a player may come before the Watcher leaves: further across open ground (VanishDistanceOpen),
        /// closer when trees and rocks stand between (VanishDistanceDense at 4 or more), capped in low visibility.
        /// </summary>
        public static float WatcherVanishDistance(Vector3 watcherPos, Vector3 playerEye)
        {
            int obstacles = ObstaclesBetween(playerEye, watcherPos + Vector3.up * 1.5f);
            float d = Mathf.Lerp(HufsaConfig.WatcherVanishDistance.Value, HufsaConfig.WatcherVanishDense.Value,
                Mathf.Clamp01(obstacles / 4f));
            if (LowVisibility(watcherPos)) d = Mathf.Min(d, HufsaConfig.MistlandsVanishDistance.Value);
            return d;
        }

        private static readonly Collider[] openHits = new Collider[256];
        private static readonly HashSet<TreeBase> openTrees = new HashSet<TreeBase>();

        /// <summary>
        /// How open the land around a point is: 1 = open grass or bare hills (no trees within 30 m),
        /// 0 = forest (8 or more trees).
        /// </summary>
        public static float Openness(Vector3 pos)
        {
            if (obstacleMask < 0) obstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
            int n = Physics.OverlapSphereNonAlloc(pos, 30f, openHits, obstacleMask, QueryTriggerInteraction.Ignore);
            openTrees.Clear();
            for (int i = 0; i < n; i++)
            {
                TreeBase t = openHits[i] ? openHits[i].GetComponentInParent<TreeBase>() : null;
                if (t) openTrees.Add(t);
            }
            return 1f - Mathf.Clamp01(openTrees.Count / 8f);
        }

        /// <summary>The open-ground (largest) vanish distance here; spawns and moves stay outside it.</summary>
        public static float WatcherVanishDistanceAt(Vector3 pos) =>
            LowVisibility(pos) ? HufsaConfig.MistlandsVanishDistance.Value : HufsaConfig.WatcherVanishDistance.Value;

        public static bool AnyHunterWithin(Vector3 pos, float radius)
        {
            foreach (HufsaEntity g in Instances)
            {
                if (g.IsHunter && !g.Vanishing && (g.transform.position - pos).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }

        public static bool AnyHunterLoaded()
        {
            foreach (HufsaEntity g in Instances) if (g.IsHunter && !g.Vanishing) return true;
            return false;
        }

        public static Player FindNearestPlayer(Vector3 pos, out float distance)
        {
            Player best = null;
            distance = float.MaxValue;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (!p || p.IsDead()) continue;
                float d = Vector3.Distance(p.transform.position, pos);
                if (d < distance)
                {
                    distance = d;
                    best = p;
                }
            }
            return best;
        }

        private void Update()
        {
            if (!NView || !NView.IsValid()) return;

            if (vanishTime < 0f && NView.GetZDO().GetBool(VanishKey, false))
            {
                vanishTime = 0f;
                foreach (ParticleSystem ps in particles)
                {
                    var em = ps.emission;
                    em.enabled = false;
                }
                HufsaPrefabs.SpawnVanishPuff(transform.position, visualBaseScale.x);
                HufsaSoundBank.PlayDespawn(transform.position);
            }

            if (vanishTime >= 0f && visual)
            {
                vanishTime += Time.deltaTime;
                float k = Mathf.Clamp01(vanishTime / ShrinkDuration);
                float shrink = 1f - k * k;
                // Collapse toward the body's centre so it disappears inside the puff rather than into the ground.
                visual.localPosition = visualBasePos + new Vector3(0f, (1f - shrink) * 1.2f * visualBaseScale.y, 0f);
                visual.localScale = visualBaseScale * shrink;
            }

            if (fadeTime < 0f && vanishTime < 0f && NView.GetZDO().GetBool(FadeKey, false)) BeginFade();

            UpdateBiome();
            UpdateHem();
            UpdateReveal();
            UpdateSighting();
            UpdateFadeRequest();
            UpdateFade(); // after the reveal, so the fade has the last word on alpha

            if (vanishTime >= VanishDuration && NView.IsOwner())
            {
                ZNetScene.instance.Destroy(gameObject);
            }
        }

        private float spotTime;
        private bool spotted;
        private static int sightMask = -1;

        /// <summary>
        /// Watcher, on every client: did this player notice it? It must be in the middle of the screen (the
        /// centre SightScreenWidth of its width, e.g. the middle third), within SightRange, with nothing in the
        /// way, for SightSeconds (looking off it slowly lets the timer run back). Each Watcher counts once per player.
        /// </summary>
        private void UpdateSighting()
        {
            if (IsHunter || spotted || Vanishing || !HufsaDread.Instance) return;
            Player me = Player.m_localPlayer;
            Camera cam = GameCamera.instance ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
            if (!me || me.IsDead() || !cam) return;
            if (sightMask < 0) sightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

            // In the middle band of the screen (and on screen top to bottom), in range, nothing in the way.
            Vector3 head = transform.position + Vector3.up * 1.6f;
            Vector3 toHead = head - cam.transform.position;
            Vector3 vp = cam.WorldToViewportPoint(head);
            float half = Mathf.Clamp01(HufsaConfig.SightScreenWidth.Value) / 2f;
            bool looking = vp.z > 0f && Mathf.Abs(vp.x - 0.5f) <= half && vp.y >= 0f && vp.y <= 1f &&
                           toHead.magnitude < HufsaConfig.SightRange.Value &&
                           !Physics.Linecast(cam.transform.position, head + Vector3.up * 0.7f, sightMask, QueryTriggerInteraction.Ignore);
            spotTime = looking ? spotTime + Time.deltaTime : Mathf.Max(0f, spotTime - Time.deltaTime * 0.5f);
            if (spotTime < HufsaConfig.SightSeconds.Value) return;
            spotted = true;
            HufsaDread.Instance.AddSighting();
            HufsaDebug.OnSpotted(HufsaDread.Instance.Count);

            // The sighting that fills the meter: this very Watcher comes for you, if it's a night you can be hunted
            // (test mode: any time).
            bool timeOk = IsHufsaTime(transform.position) || HufsaDebug.TestMode;
            if (HufsaDread.Instance.Count >= HufsaConfig.SightingsForHunt.Value && HufsaConfig.HunterEnabled.Value &&
                timeOk && HufsaHuntState.IsHuntable(me) && !AnyHunterLoaded())
            {
                BecomeHunter(me);
                return;
            }

            // Otherwise it has been seen: a few seconds later it quietly fades away.
            if (HufsaConfig.SpottedFadeSeconds.Value > 0f) fadeRequestAt = Time.time + HufsaConfig.SpottedFadeSeconds.Value;
        }

        private float fadeRequestAt = -1f;
        private const string FadeOutRpc = "Hufsa_FadeOut";

        /// <summary>Fades this Watcher out for everyone; only its owner may, so others ask the owner.</summary>
        private void UpdateFadeRequest()
        {
            if (fadeRequestAt < 0f || Time.time < fadeRequestAt) return;
            fadeRequestAt = -1f;
            if (Vanishing || !NView || !NView.IsValid()) return;
            if (NView.IsOwner()) VanishSilently();
            else NView.InvokeRPC(FadeOutRpc);
        }

        private const string BecomeHunterRpc = "Hufsa_BecomeHunter";

        /// <summary>
        /// Swaps this Watcher for a Hunter on the same spot, facing the player who saw it. The spotter's client
        /// spawns the Hunter (it has the ground loaded); the Watcher's owner removes the Watcher. The server then
        /// picks the Hunter up as a hunt on this player.
        /// </summary>
        private void BecomeHunter(Player me)
        {
            GameObject prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(HufsaPrefabs.HunterName) : null;
            if (!prefab) return;
            Vector3 look = me.transform.position - transform.position;
            look.y = 0f;
            GameObject hunter = Instantiate(prefab, transform.position,
                look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : transform.rotation);
            // It lunges at you first (HunterBehaviour), then settles into the slow chase.
            var hunterView = hunter.GetComponent<ZNetView>();
            if (hunterView && hunterView.IsValid()) hunterView.GetZDO().Set(HunterBehaviour.LungeKey, true);
            if (HufsaDebug.TestMode) hunter.GetComponent<HufsaEntity>()?.MarkForced(); // don't leave at daylight
            HufsaDebug.OnHuntStarted();
            HufsaDread.Instance.ResetAfterHunt();
            HufsaPlugin.Log.LogInfo("The Watcher you kept seeing has started hunting you");

            if (visual) visual.gameObject.SetActive(false); // hide at once; the owner removes it for everyone
            if (NView.IsOwner()) ZNetScene.instance.Destroy(gameObject);
            else NView.InvokeRPC(BecomeHunterRpc);
        }

        /// <summary>The Watcher turns to whoever is looking: every player sees it staring straight at them.</summary>
        private void LateUpdate()
        {
            if (IsHunter || !visual || !Player.m_localPlayer) return;
            Vector3 dir = Player.m_localPlayer.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) visual.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>Turns the falling snow around it off in the mild biomes (and back on when it leaves them).</summary>
        private void UpdateBiome()
        {
            biomeCheck -= Time.deltaTime;
            if (biomeCheck > 0f) return;
            biomeCheck = 1f;
            Mild = IsMildBiome(transform.position);
            if (snowfall && !Vanishing)
            {
                var em = snowfall.emission;
                em.enabled = !Mild;
            }
        }

        /// <summary>
        /// The lower body: the hem moves only while gliding (Hunter), and on sloping ground the hem bends down
        /// or up to rest on the terrain all the way round (both). Standing still on flat ground: no change.
        /// </summary>
        private void UpdateHem()
        {
            if (!bodyMesh) return;
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            delta.y = 0f;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            GlideSpeed = Mathf.Lerp(GlideSpeed, speed < 20f ? speed : 0f, Mathf.Min(1f, Time.deltaTime * 4f));
            float target = IsHunter && speed > 0.3f && speed < 20f && !Vanishing ? 1f : 0f;
            motion = Mathf.Lerp(motion, target, Mathf.Min(1f, Time.deltaTime * 2f));

            groundTimer -= Time.deltaTime;
            if (groundTimer <= 0f)
            {
                groundChanged = SampleGround();
                groundTimer = IsHunter ? 0.1f : 0.25f;
            }

            bool moving = motion > 0.001f;
            if (!moving && !groundChanged && !(hemDirty && !onSlope)) return;
            HufsaModel.AnimateHem(hemWork, Time.time, motion * HufsaConfig.HemSway.Value);
            if (onSlope) HufsaModel.ConformToGround(hemWork, groundOffset);
            bodyMesh.vertices = hemWork;
            bodyMesh.RecalculateNormals();
            bodyMesh.RecalculateBounds();
            hemDirty = moving || onSlope;
            groundChanged = false;
        }

        private readonly float[] groundOffset = new float[HufsaModel.Segments + 1];
        private float groundTimer;
        private bool groundChanged;
        private bool onSlope;

        /// <summary>
        /// Terrain height under each point of the hem, relative to where the hem would be. Returns whether
        /// anything changed noticeably since last time.
        /// </summary>
        private bool SampleGround()
        {
            if (!visual || ZoneSystem.instance == null || HufsaModel.BodyRest == null) return false;
            float scale = Mathf.Max(visual.lossyScale.y, 0.01f);
            bool changed = false, slope = false;
            for (int i = 0; i <= HufsaModel.Segments; i++)
            {
                Vector3 world = visual.TransformPoint(HufsaModel.BodyRest[i]); // bottom ring
                float off = 0f;
                // over a river or lake the hem rests on the water (as it glides there), not on the bottom
                if (ZoneSystem.instance.GetGroundHeight(world, out float h))
                    off = Mathf.Clamp((Mathf.Max(h, ZoneSystem.instance.m_waterLevel - 0.3f) - world.y) / scale, -1.2f, 0.8f);
                if (Mathf.Abs(off) > 0.03f) slope = true;
                if (Mathf.Abs(off - groundOffset[i]) > 0.01f) changed = true;
                groundOffset[i] = off;
            }
            changed |= slope != onSlope;
            onSlope = slope;
            return changed;
        }

        /// <summary>
        /// Far away only the white eyes show. Closer, the smile fades in first, then the pupils, the near-black
        /// nose and the arms (hidden entirely until then, so they never show as shapes from afar; they fade in
        /// rather than pop up).
        /// </summary>
        private void UpdateReveal()
        {
            Transform cam = GameCamera.instance ? GameCamera.instance.transform : Camera.main ? Camera.main.transform : null;
            if (!cam) return;
            Vector3 face = transform.position + Vector3.up * HufsaModel.EyeHeight * HufsaModel.Height * visualBaseScale.y;
            float d = Vector3.Distance(cam.position, face);
            float face01 = Reveal(d, HufsaConfig.FaceRevealNear.Value, HufsaConfig.FaceRevealFar.Value);
            float smile01 = IsHunter
                ? Reveal(d, HufsaConfig.HunterSmileRevealNear.Value, HufsaConfig.HunterSmileRevealFar.Value)
                : Reveal(d, HufsaConfig.SmileRevealNear.Value, HufsaConfig.SmileRevealFar.Value);

            foreach (Material m in pupilMats) SetAlpha(m, face01);
            foreach (Material m in toothMats) SetAlpha(m, smile01);
            foreach (Renderer r in toothRenderers) r.enabled = smile01 > 0.01f;
            if (fadeTime >= 0f) return; // fading out: the vanish fade owns these materials now
            if (noseFader != null && noseFader.Lit && noseFader.Lit.HasProperty("_Color"))
            {
                float v = Mathf.Lerp(1f, HufsaModel.NoseShade, face01);
                noseFader.Lit.color = new Color(v, v, v, 1f);
            }
            noseFader?.Set(face01);
            armFader?.Set(face01);
            fingerFader?.Set(face01);
            if (arms) arms.SetGlowVisibility(face01);
        }

        private static float Reveal(float d, float near, float far)
        {
            near = Mathf.Min(near, far - 0.5f);
            float t = Mathf.Clamp01((d - near) / (far - near));
            return 1f - t * t * (3f - 2f * t);
        }

        private static void SetAlpha(Material m, float a)
        {
            Color c = m.color;
            c.a = a;
            m.color = c;
        }
    }
}
