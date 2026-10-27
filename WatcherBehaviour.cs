using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Stands still, stares (facing is per player, see HufsaEntity.LateUpdate) and is elusive, slenderman-style.
    /// Runs on the owning client, which is the player it was sent to:
    /// - once you have seen it and look away, it is often simply gone (and another comes back for you a little
    ///   later); otherwise it moves to a new spot just outside your view, a little closer each time;
    /// - optionally (off by default), if cover blocks your view of it while you close in, it is gone;
    /// - it leaves if you get too close (further across open ground, closer through forest), at dawn, after its
    ///   lifetime, or when a Hunter is out; and a few seconds after you have really spotted it (HufsaEntity).
    /// </summary>
    internal class WatcherBehaviour : MonoBehaviour
    {
        private const float CoverRange = 70f;
        private const float HeadHeight = 2.3f;

        private HufsaEntity entity;
        private float age;
        private bool seen;
        private float unseenFor;
        private float occludedFor;
        private int blinks;
        private int coverBlocks;
        private bool blockCounted;
        private int losMask;
        private float vanishCheck;
        private float vanishDistance = 25f;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            losMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
        }

        private void Update()
        {
            ZNetView nview = entity.NView;
            if (!nview || !nview.IsValid() || !nview.IsOwner() || entity.Vanishing) return;

            age += Time.deltaTime;
            Player nearest = HufsaEntity.FindNearestPlayer(transform.position, out float dist);

            // How close you may come depends on what stands between you: further across open ground.
            vanishCheck -= Time.deltaTime;
            if (nearest && vanishCheck <= 0f)
            {
                vanishDistance = HufsaEntity.WatcherVanishDistance(transform.position, nearest.transform.position + Vector3.up * 1.6f);
                vanishCheck = 0.5f;
            }

            // Leaves at dawn, or when the storm or fog it came with clears.
            bool timeOver = !entity.Forced && !HufsaEntity.IsWatcherTime(transform.position);
            if (!nearest || dist > 250f || dist < vanishDistance ||
                age > HufsaConfig.WatcherLifetime.Value || timeOver || HufsaEntity.AnyHunterLoaded())
            {
                entity.Vanish();
                return;
            }

            UpdateElusive();
        }

        private void UpdateElusive()
        {
            Player me = Player.m_localPlayer;
            Camera cam = GameCamera.instance ? GameCamera.instance.GetComponent<Camera>() : null;
            if (!cam) cam = Camera.main;
            if (!me || !cam) return;

            Vector3 head = transform.position + Vector3.up * HeadHeight;
            bool inFrustum = InView(cam, head);
            bool clear = !Physics.Linecast(cam.transform.position, head, losMask, QueryTriggerInteraction.Ignore);

            if (inFrustum && clear)
            {
                seen = true;
                unseenFor = 0f;
                occludedFor = 0f;
                blockCounted = false;
                return;
            }
            if (!seen) return; // it only plays games with you once you've noticed it

            if (inFrustum)
            {
                // Something is in the way. Approach it from behind cover and it won't be there any more.
                // In dense biomes (swamp roots and trees everywhere) it takes several blocked views.
                occludedFor += Time.deltaTime;
                float myDist = Vector3.Distance(me.transform.position, transform.position);
                if (HufsaConfig.WatcherHidesBehindCover.Value && myDist < CoverRange && occludedFor > 0.4f && !blockCounted)
                {
                    blockCounted = true;
                    coverBlocks++;
                    int needed = HufsaEntity.IsDenseBiome(transform.position) ? Mathf.Max(1, HufsaConfig.DenseCoverBlocks.Value) : 1;
                    if (coverBlocks >= needed) VanishAndComeBack();
                }
                return;
            }

            unseenFor += Time.deltaTime;
            if (unseenFor < HufsaConfig.WatcherLookAwaySeconds.Value) return;

            bool vanish = Random.value < HufsaConfig.LookAwayVanishChance.Value;
            if (!vanish && blinks < HufsaConfig.WatcherBlinks.Value && Relocate(cam, me))
            {
                blinks++;
                seen = false;
                unseenFor = 0f;
            }
            else
            {
                VanishAndComeBack();
            }
        }

        /// <summary>Gone without a trace; the server is told so a new one comes for this player again soon.</summary>
        private void VanishAndComeBack()
        {
            if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), HufsaSpawner.RpcWatcherGone);
            entity.VanishSilently();
        }

        private static bool InView(Camera cam, Vector3 point)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            return vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f;
        }

        /// <summary>
        /// Moves to a spot just outside the player's view, on whichever side, a bit closer than before and
        /// preferably no lower than the player, so turning your head finds it waiting there.
        /// </summary>
        private bool Relocate(Camera cam, Player me)
        {
            Vector3 p = me.transform.position;
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return false;
            fwd.Normalize();

            float current = Vector3.Distance(p, transform.position);
            bool mist = HufsaEntity.LowVisibility(p);
            float minDist = HufsaEntity.WatcherVanishDistanceAt(p) + 8f;
            float maxDist = mist ? HufsaConfig.MistlandsMaxDistance.Value : HufsaConfig.WatcherMaxDistance.Value;
            float newDist = Mathf.Clamp(current * 0.75f, minDist, Mathf.Max(minDist, maxDist));

            Vector3 best = Vector3.zero;
            bool found = false;
            for (int i = 0; i < 40; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float angle = side * Random.Range(55f, 85f);
                Vector3 c = p + Quaternion.Euler(0f, angle, 0f) * fwd * newDist;
                if (!ZoneSystem.instance.GetGroundHeight(c, out float h) || h < ZoneSystem.instance.m_waterLevel + 0.3f) continue;
                c.y = h;
                if (InView(cam, c + Vector3.up * HeadHeight)) continue;
                if (EffectArea.IsPointInsideArea(c, EffectArea.Type.PlayerBase, 5f)) continue;
                if (!HufsaSpawner.IsGoodWatcherSpot(c, cam.transform.position, out _)) continue; // trees or hilltop
                if (!found || (best.y < p.y - 1f && c.y > best.y))
                {
                    best = c;
                    found = true;
                }
                if (best.y >= p.y - 1f) break;
            }
            if (!found) return false;
            transform.position = best;
            return true;
        }
    }
}
