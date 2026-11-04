using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Console-only debug modes (see HufsaCommand):
    /// - "hufsa testmode": a live readout at the top of the screen (what the sight check sees of the Watcher you
    ///   look at or the nearest one, the stare meter, and your dread state). Readout only: it spawns nothing and
    ///   changes nothing. Spawn Watchers with "hufsa watcher", set the state with "hufsa dread". Every Hufsa out
    ///   gets a temporary pin on the map.
    /// - "hufsa fadetest [distance] [seconds]": a Watcher in front of you that counts for nothing and fades away
    ///   after a few seconds, to look at the fade.
    /// - "hufsa availableSpots": shows a still Hufsa on every spot around you where a Watcher could spawn
    ///   right now (same rules as real spawns, all directions). Local only: no network objects, no sightings.
    ///   Each one disappears the moment you come within its vanish distance. Run it again to clear.
    /// </summary>
    internal class HufsaDebug : MonoBehaviour
    {
        private const float SpotSpacing = 8f;
        private const int MaxPreviews = 400;

        internal static HufsaDebug Instance;
        public static bool TestMode => Instance && Instance.testMode;

        private bool testMode;

        private readonly List<GameObject> previews = new List<GameObject>();
        private float previewTimer;

        private void Awake() => Instance = this;

        private readonly Dictionary<HufsaEntity, Minimap.PinData> pins = new Dictionary<HufsaEntity, Minimap.PinData>();
        private readonly List<HufsaEntity> pinGone = new List<HufsaEntity>();

        /// <summary>Test mode: a temporary (unsaved) map pin on every Hufsa out, following it.</summary>
        private void UpdatePins()
        {
            if (!Minimap.instance) return;
            pinGone.Clear();
            foreach (KeyValuePair<HufsaEntity, Minimap.PinData> kv in pins)
                if (!testMode || !kv.Key || kv.Key.Vanishing) pinGone.Add(kv.Key);
            foreach (HufsaEntity e in pinGone)
            {
                Minimap.instance.RemovePin(pins[e]);
                pins.Remove(e);
            }
            if (!testMode) return;
            foreach (HufsaEntity e in HufsaEntity.Instances)
            {
                if (e.Vanishing) continue;
                if (pins.TryGetValue(e, out Minimap.PinData pin))
                {
                    if ((pin.m_pos - e.transform.position).sqrMagnitude < 4f) continue;
                    Minimap.instance.RemovePin(pin);
                }
                pins[e] = Minimap.instance.AddPin(e.transform.position, Minimap.PinType.Icon3,
                    e.IsHunter ? "Hufsa (hunting)" : "Hufsa", false, false, 0L);
            }
        }

        /// <summary>Console "hufsa fadetest": a Watcher this far in front of you that fades after this many seconds.</summary>
        public bool FadeTest(float distance, float seconds)
        {
            Player me = Player.m_localPlayer;
            Transform cam = GameCamera.instance ? GameCamera.instance.transform : null;
            GameObject prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(HufsaPrefabs.WatcherName) : null;
            if (!me || !cam || !prefab) return false;
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return false;
            fwd.Normalize();
            Vector3 at = me.transform.position + fwd * Mathf.Max(3f, distance);
            if (ZoneSystem.instance && ZoneSystem.instance.GetGroundHeight(at, out float h)) at.y = h;
            GameObject go = Instantiate(prefab, at, Quaternion.LookRotation(-fwd));
            HufsaEntity e = go.GetComponent<HufsaEntity>();
            if (!e) return false;
            e.MarkDebug(seconds);
            e.MarkForced();
            return true;
        }

        // ---------------------------------------------------------------- test mode

        public void ToggleTestMode()
        {
            testMode = !testMode;
            Say(testMode ? $"Test mode ON: readout only. {State()}" : "Test mode OFF.");
        }

        public static void OnNoticed(HufsaEntity e)
        {
            if (TestMode) Instance.Say($"NOTICED{(e.HarmlessLesson ? " (the first: harmless)" : "")}. Its limit: {e.StareLimit:0.0} s");
        }

        internal static string State()
        {
            HufsaDread d = HufsaDread.Instance;
            if (!d) return "";
            string night = !d.NightHuntsUnlocked ? "night hunts locked (no hunt yet)"
                : d.RestNights > 0 ? $"resting {d.RestNights} more night(s)"
                : $"tonight {d.TonightChance:P0}{(d.NoticedTonight ? " (noticed one)" : "")}, {d.QuietNights} quiet night(s)" +
                  (d.HuntAt > 0f ? $", coming in {Mathf.Max(0f, d.HuntAt - Time.time):0} s" : "");
            return $"Noticed {d.Encounters} ever, {d.Count} since the last hunt. Hunts {d.Hunts}. {night}.";
        }

        /// <summary>The Watcher the readout shows: the one being stared at, else the nearest.</summary>
        private static HufsaEntity Watched()
        {
            HufsaEntity t = HufsaStare.Instance ? HufsaStare.Instance.Target : null;
            if (t && !t.Vanishing) return t;
            Player me = Player.m_localPlayer;
            HufsaEntity best = null;
            float bestD = float.MaxValue;
            foreach (HufsaEntity e in HufsaEntity.Instances)
            {
                if (e.IsHunter || e.Vanishing || !me) continue;
                float d = (e.transform.position - me.transform.position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
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
            UpdatePreviews();
            UpdatePins();
        }

        private GUIStyle readoutStyle;

        /// <summary>Test mode: what the sighting check sees of the current Watcher, live, at the top of the screen.</summary>
        private void OnGUI()
        {
            if (!testMode) return;
            if (readoutStyle == null)
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, richText = true };
            HufsaEntity current = Watched();
            if (!current)
            {
                string m = HufsaStare.Instance ? $"stare {HufsaStare.Instance.Meter:0.0} s  (no Watcher out)" : "(no Watcher out)";
                GUI.Label(new Rect(0f, 8f, Screen.width, 30f), $"<color=#9FD4FF>{m}</color>", readoutStyle);
                GUI.Label(new Rect(0f, 32f, Screen.width, 30f), $"<color=#9FD4FF>{State()}</color>", readoutStyle);
                return;
            }
            float min = HufsaConfig.SightMinVisible.Value;
            string range = current.SightRangeNow > 10000f ? "-" : $"{current.SightRangeNow:0} m";
            HufsaStare stare = HufsaStare.Instance;
            string text =
                $"<color=#9FD4FF>[Hufsa sight · {(HufsaSight.UsesGpu ? "probe" : "rays")}]</color>  " +
                $"body {Mark(current.SightVisible, min)}  face {Mark(current.SightFace, min)}  " +
                $"size {Mark(current.SightScreenHeight, HufsaConfig.SightMinScreenHeight.Value, "0.000")}  " +
                $"range {range}  angle {Mark(HufsaConfig.StareCone.Value - current.SightAngle, 0f, "0.0")}  " +
                $"rate {current.LookRate:0.00}";
            string meter = stare
                ? $"stare {stare.Meter:0.0}/{current.StareLimit:0.0} s  " +
                  $"{(stare.Busy ? "<color=#FF8080>busy</color>" : "free")}  " +
                  $"{(current.Noticed ? "<color=#7CFF7C>noticed</color>" : "not noticed")}" +
                  $"{(current.HarmlessLesson ? " (first: harmless)" : "")}"
                : "";
            GUI.Label(new Rect(0f, 8f, Screen.width, 30f), text, readoutStyle);
            GUI.Label(new Rect(0f, 32f, Screen.width, 30f), $"<color=#9FD4FF>{meter}</color>", readoutStyle);
            GUI.Label(new Rect(0f, 56f, Screen.width, 30f), $"<color=#9FD4FF>{State()}</color>", readoutStyle);
            GUI.Label(new Rect(0f, 80f, Screen.width, 30f), $"<color=#9FD4FF>{HufsaSight.Stats}</color>", readoutStyle);
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
