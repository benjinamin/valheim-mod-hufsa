using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Console-only debug modes (see HufsaCommand):
    /// - "hufsa testmode": spawns a Watcher near you, and a new one 10 s after each one is gone, with on-screen
    ///   notes when one spawns and when you spot it. Sightings count for real, so the last one turns into the
    ///   Hunter (any time of day while testing); test mode then switches itself off.
    /// - "hufsa availableSpots": shows a still Hufsa on every spot around you where a Watcher could spawn
    ///   right now (same rules as real spawns, all directions). Local only: no network objects, no sightings.
    ///   Each one disappears the moment you come within its vanish distance. Run it again to clear.
    /// </summary>
    internal class HufsaDebug : MonoBehaviour
    {
        private const float TestRespawnSeconds = 10f;
        private const float SpotSpacing = 8f;
        private const int MaxPreviews = 400;

        internal static HufsaDebug Instance;
        public static bool TestMode => Instance && Instance.testMode;

        private bool testMode;
        private HufsaEntity current;
        private Minimap.PinData pin;
        private Vector3 pinPos;
        private bool hadCurrent;
        private float nextSpawnAt;

        private readonly List<GameObject> previews = new List<GameObject>();
        private float previewTimer;

        private void Awake() => Instance = this;

        // ---------------------------------------------------------------- test mode

        public void ToggleTestMode()
        {
            testMode = !testMode;
            current = null;
            ClearPin();
            hadCurrent = false;
            nextSpawnAt = Time.time;
            Say(testMode
                ? $"Test mode ON: a Watcher now, and another 10 s after each one is gone. Sightings {Sightings()}."
                : "Test mode OFF.");
        }

        public static void OnSpotted(int count)
        {
            if (TestMode) Instance.Say($"SPOTTED! Sightings {count}/{HufsaConfig.SightingsForHunt.Value}", true);
        }

        public static void OnHuntStarted()
        {
            if (!TestMode) return;
            Instance.testMode = false;
            Instance.ClearPin();
            Instance.Say("The last one turned on you: hunt started. Test mode OFF.", true);
        }

        private static string Sightings() =>
            $"{(HufsaDread.Instance ? HufsaDread.Instance.Count : 0)}/{HufsaConfig.SightingsForHunt.Value}";

        private void UpdateTestMode()
        {
            if (!testMode || !Player.m_localPlayer || HufsaEntity.AnyHunterLoaded()) return;

            if (current && !current.Vanishing)
            {
                UpdatePin(current.transform.position); // still out there; follow it if it moves
                return;
            }
            if (hadCurrent)
            {
                hadCurrent = false;
                ClearPin();
                nextSpawnAt = Time.time + TestRespawnSeconds;
                Say($"Watcher gone. Next one in {TestRespawnSeconds:0} s.");
            }
            if (Time.time < nextSpawnAt) return;

            current = HufsaSpawner.SpawnNearLocalPlayer(HufsaPrefabs.WatcherName, true);
            if (current)
            {
                hadCurrent = true;
                float d = Vector3.Distance(Player.m_localPlayer.transform.position, current.transform.position);
                UpdatePin(current.transform.position);
                Say($"Watcher spawned {d:0} m away (pinned on the map). Sightings {Sightings()}.");
            }
            else
            {
                nextSpawnAt = Time.time + 3f;
                Say("No spot for a Watcher here, trying again in 3 s.");
            }
        }

        /// <summary>Test mode: a temporary (unsaved) map pin on the current Watcher.</summary>
        private void UpdatePin(Vector3 at)
        {
            if (!Minimap.instance) return;
            if (pin != null && (at - pinPos).sqrMagnitude < 4f) return;
            ClearPin();
            pin = Minimap.instance.AddPin(at, Minimap.PinType.Icon3, "Hufsa", false, false, 0L);
            pinPos = at;
        }

        private void ClearPin()
        {
            if (pin != null && Minimap.instance) Minimap.instance.RemovePin(pin);
            pin = null;
        }

        // ---------------------------------------------------------------- available spots

        public void ToggleSpots()
        {
            if (previews.Count > 0)
            {
                ClearPreviews();
                Say("Spot previews cleared.");
                return;
            }

            Player p = Player.m_localPlayer;
            Camera cam = GameCamera.instance ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
            GameObject prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(HufsaPrefabs.WatcherName) : null;
            Transform source = prefab ? prefab.transform.Find(HufsaPrefabs.VisualName) : null;
            if (!p || !cam || !source) return;

            Vector3 me = p.transform.position;
            Vector3 eye = cam.transform.position;
            bool lowVis = HufsaEntity.LowVisibility(me);
            float open = lowVis ? 0f : HufsaEntity.Openness(me);
            float min = lowVis ? HufsaConfig.MistlandsMinDistance.Value
                : Mathf.Lerp(HufsaConfig.WatcherMinDistance.Value, HufsaConfig.OpenSpawnMinDistance.Value, open);
            float max = lowVis ? HufsaConfig.MistlandsMaxDistance.Value
                : Mathf.Lerp(HufsaConfig.WatcherMaxDistance.Value, HufsaConfig.OpenSpawnMaxDistance.Value, open);

            int tested = 0;
            for (float r = min; r <= max + 0.01f && previews.Count < MaxPreviews; r += SpotSpacing)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / SpotSpacing));
                for (int i = 0; i < steps && previews.Count < MaxPreviews; i++)
                {
                    tested++;
                    Vector3 c = me + Quaternion.Euler(0f, i * 360f / steps, 0f) * Vector3.forward * r;
                    if (!ZoneSystem.instance.GetGroundHeight(c, out float h) || h < ZoneSystem.instance.m_waterLevel + 0.3f) continue;
                    c.y = h;
                    if (HufsaEntity.BiomeAt(c) == Heightmap.Biome.AshLands) continue;
                    if (EffectArea.IsPointInsideArea(c, EffectArea.Type.PlayerBase, 5f)) continue;
                    if (!HufsaSpawner.IsGoodWatcherSpot(c, eye, out _)) continue;
                    previews.Add(MakePreview(source.gameObject, c, me));
                }
            }
            Say($"{previews.Count} possible Watcher spots {min:0}-{max:0} m around you ({tested} checked). " +
                "Walk toward them; run the command again to clear.");
        }

        /// <summary>A still copy of the Watcher's looks only: no network object, no particles, no glow.</summary>
        private static GameObject MakePreview(GameObject visual, Vector3 at, Vector3 lookAt)
        {
            Vector3 look = lookAt - at;
            look.y = 0f;
            GameObject go = Instantiate(visual, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            go.name = "HufsaSpotPreview";
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) Destroy(ps.gameObject);
            foreach (SilhouetteGlow glow in go.GetComponentsInChildren<SilhouetteGlow>(true)) Destroy(glow.gameObject);
            return go;
        }

        /// <summary>Each preview disappears as soon as you are within its (openness-dependent) vanish distance.</summary>
        private void UpdatePreviews()
        {
            if (previews.Count == 0) return;
            previewTimer -= Time.deltaTime;
            if (previewTimer > 0f) return;
            previewTimer = 0.15f;

            Player p = Player.m_localPlayer;
            if (!p)
            {
                ClearPreviews();
                return;
            }
            Vector3 eye = p.transform.position + Vector3.up * 1.6f;
            float open = HufsaConfig.WatcherVanishDistance.Value;
            for (int i = previews.Count - 1; i >= 0; i--)
            {
                GameObject v = previews[i];
                if (!v)
                {
                    previews.RemoveAt(i);
                    continue;
                }
                float d = Vector3.Distance(v.transform.position, p.transform.position);
                if (d > open + 1f) continue; // can't be within any vanish distance yet
                if (d < HufsaEntity.WatcherVanishDistance(v.transform.position, eye))
                {
                    Destroy(v);
                    previews.RemoveAt(i);
                }
            }
        }

        private void ClearPreviews()
        {
            foreach (GameObject v in previews) if (v) Destroy(v);
            previews.Clear();
        }

        private void Update()
        {
            UpdateTestMode();
            UpdatePreviews();
        }

        private GUIStyle readoutStyle;

        /// <summary>Test mode: what the sighting check sees of the current Watcher, live, at the top of the screen.</summary>
        private void OnGUI()
        {
            if (!testMode || !current || current.Vanishing) return;
            if (readoutStyle == null)
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, richText = true };
            float min = HufsaConfig.SightMinVisible.Value;
            string range = current.SightRangeNow > 10000f ? "-" : $"{current.SightRangeNow:0} m";
            string text =
                $"<color=#9FD4FF>[Hufsa sight · {(HufsaSight.UsesGpu ? "probe" : "rays")}]</color>  " +
                $"body {Mark(current.SightVisible, min)}  face {Mark(current.SightFace, min)}  " +
                $"size {Mark(current.SightScreenHeight, HufsaConfig.SightMinScreenHeight.Value, "0.000")}  " +
                $"range {range}  " +
                $"{(current.SightLooking ? "<color=#7CFF7C>LOOKING</color>" : "<color=#FF8080>not looking</color>")} " +
                $"{current.SpotProgress * 100f:0}%";
            GUI.Label(new Rect(0f, 8f, Screen.width, 30f), text, readoutStyle);
            GUI.Label(new Rect(0f, 32f, Screen.width, 30f), $"<color=#9FD4FF>{HufsaSight.Stats}</color>", readoutStyle);
        }

        private static string Mark(float value, float need, string format = "0%") =>
            $"<color={(value >= need ? "#7CFF7C" : "#FF8080")}>{value.ToString(format)}</color>";

        private void Say(string text, bool center = false)
        {
            HufsaPlugin.Log.LogInfo($"[debug] {text}");
            if (MessageHud.instance)
                MessageHud.instance.ShowMessage(center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft,
                    $"<color=#9FD4FF>[Hufsa debug] {text}</color>");
        }
    }
}
