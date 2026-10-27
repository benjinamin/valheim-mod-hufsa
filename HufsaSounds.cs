using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace Hufsa
{
    /// <summary>
    /// Loads Hufsa's sounds from BepInEx/plugins/Hufsa/sounds. Any number of .ogg, .wav or .mp3 files,
    /// grouped by the word in their name (an optional "Hufsa " in front is ignored, e.g. "Hufsa step 3.mp3"):
    /// - voice : Hufsa's voice; one is picked at random each time it "speaks"
    /// - step  : one slow footstep in snow, played in time with its glide
    /// - creak : wood cracking/creaking, now and then while it moves
    /// - breath: the Watcher breathing, heard while it is behind you
    /// - despawn: Hufsa leaving (vanishing or fading away)
    /// Missing groups are simply silent; other files are ignored.
    /// </summary>
    internal static class HufsaSoundBank
    {
        public static readonly List<AudioClip> Voice = new List<AudioClip>();
        public static readonly List<AudioClip> Steps = new List<AudioClip>();
        public static readonly List<AudioClip> Creaks = new List<AudioClip>();
        public static readonly List<AudioClip> Breaths = new List<AudioClip>();
        public static readonly List<AudioClip> Despawns = new List<AudioClip>();

        public static string Folder => Path.Combine(Path.GetDirectoryName(typeof(HufsaSoundBank).Assembly.Location) ?? "", "sounds");

        public static IEnumerator Load()
        {
            if (!Directory.Exists(Folder))
            {
                HufsaPlugin.Log.LogInfo($"No Hufsa sounds folder at {Folder}");
                yield break;
            }
            foreach (string file in Directory.GetFiles(Folder))
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                string kind = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Trim();
                if (kind.StartsWith("hufsa") || kind.StartsWith("groke")) kind = kind.Substring(5).TrimStart(' ', '_', '-');
                List<AudioClip> group = kind.StartsWith("voice") ? Voice : kind.StartsWith("step") ? Steps :
                    kind.StartsWith("creak") ? Creaks : kind.StartsWith("breath") ? Breaths :
                    kind.StartsWith("despawn") ? Despawns : null;
                AudioType type = name.EndsWith(".ogg") ? AudioType.OGGVORBIS : name.EndsWith(".wav") ? AudioType.WAV :
                    name.EndsWith(".mp3") ? AudioType.MPEG : AudioType.UNKNOWN;
                if (group == null || type == AudioType.UNKNOWN) continue;

                using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, type))
                {
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        HufsaPlugin.Log.LogWarning($"Could not load sound {name}: {req.error}");
                        continue;
                    }
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    clip.name = Path.GetFileNameWithoutExtension(file);
                    group.Add(clip);
                }
            }
            HufsaPlugin.Log.LogInfo($"Hufsa sounds: {Voice.Count} voice, {Steps.Count} step, {Creaks.Count} creak, " +
                                    $"{Breaths.Count} breath, {Despawns.Count} despawn");
        }

        public static AudioClip Pick(List<AudioClip> group) =>
            group.Count == 0 ? null : group[UnityEngine.Random.Range(0, group.Count)];

        /// <summary>
        /// Plays a sound from Hufsa's direction at a steady loudness however far it stands: the source is put a
        /// few metres from the listener, toward Hufsa, so you hear where it is but not how far.
        /// </summary>
        public static void PlayFrom(Vector3 hufsaPos, AudioClip clip, float volume, float pitch = 1f)
        {
            if (!clip || volume <= 0f || !HufsaConfig.SoundsEnabled.Value) return;
            Transform listener = GameCamera.instance ? GameCamera.instance.transform : Camera.main ? Camera.main.transform : null;
            if (!listener) return;
            Vector3 dir = hufsaPos - listener.position;
            float d = dir.magnitude;
            var go = new GameObject("HufsaSound");
            go.transform.position = listener.position + (d > 6f ? dir / d * 6f : dir);
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 10f; // the listener is always inside this: full volume, direction only
            s.maxDistance = 40f;
            s.dopplerLevel = 0f;
            s.pitch = pitch;
            if (SfxGroup) s.outputAudioMixerGroup = SfxGroup;
            s.PlayOneShot(clip, volume);
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(pitch, 0.1f) + 0.3f);
        }

        /// <summary>
        /// Hufsa leaving: heard only by players within the Watcher's leave distance of it, louder the closer they
        /// are (up to 40% louder right next to it).
        /// </summary>
        public static void PlayDespawn(Vector3 pos)
        {
            Player me = Player.m_localPlayer;
            if (!me) return;
            float radius = HufsaEntity.WatcherVanishDistanceAt(pos);
            float d = Vector3.Distance(me.transform.position, pos);
            if (d > radius) return;
            float near = 1f - d / radius;
            PlayFrom(pos, Pick(Despawns), HufsaConfig.DespawnVolume.Value * (1f + 0.4f * near), UnityEngine.Random.Range(0.95f, 1.03f));
        }

        private static AudioMixerGroup sfx;

        /// <summary>The game's sound-effects channel, so the in-game volume sliders apply.</summary>
        public static AudioMixerGroup SfxGroup
        {
            get
            {
                if (sfx || !AudioMan.instance) return sfx;
                AudioMixer master = AudioMan.instance.m_masterMixer;
                AudioMixerGroup[] found = master ? master.FindMatchingGroups("SFX") : null;
                sfx = found != null && found.Length > 0 ? found[0] : AudioMan.instance.m_ambientMixer;
                return sfx;
            }
        }
    }

    /// <summary>
    /// On the Hunter only (the Watcher only breathes, see WatcherSounds), per client: slow footsteps in time with
    /// its glide and the odd wood creak while it moves (3D, nearby only); its voice now and then, heard from its
    /// direction at any distance like the Watcher's breath, more often the closer it is.
    /// </summary>
    internal class HufsaSounds : MonoBehaviour
    {
        private const float VoiceRange = 70f; // the voice comes most often within this

        private HufsaEntity entity;
        private AudioSource move;
        private float stepTimer;
        private float creakTimer;
        private float voiceTimer;
        private bool greeted;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            move = MakeSource(45f);
            creakTimer = UnityEngine.Random.Range(4f, 10f);
            voiceTimer = UnityEngine.Random.Range(14f, 30f);
        }

        private AudioSource MakeSource(float maxDistance)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            return s;
        }

        private void Update()
        {
            if (!HufsaConfig.SoundsEnabled.Value || entity.Vanishing) return;
            AudioMixerGroup group = HufsaSoundBank.SfxGroup;
            if (group && !move.outputAudioMixerGroup) move.outputAudioMixerGroup = group;

            float dt = Time.deltaTime;
            Player me = Player.m_localPlayer;
            float dist = me ? Vector3.Distance(me.transform.position, transform.position) : float.MaxValue;

            // Movement: a footstep per stride, quicker as it speeds up; a creak every so often.
            float speed = entity.GlideSpeed;
            if (speed > 0.3f)
            {
                stepTimer -= dt;
                if (stepTimer <= 0f)
                {
                    Play(move, HufsaSoundBank.Pick(HufsaSoundBank.Steps), HufsaConfig.MoveVolume.Value * 1.4f, 0.85f, 1f);
                    stepTimer = Mathf.Clamp(1.6f / speed, 0.35f, 1.2f);
                }
                creakTimer -= dt;
                if (creakTimer <= 0f)
                {
                    Play(move, HufsaSoundBank.Pick(HufsaSoundBank.Creaks), HufsaConfig.MoveVolume.Value, 0.8f, 1.05f);
                    creakTimer = UnityEngine.Random.Range(7f, 16f);
                }
            }

            // Voice: it takes over from the Watcher's breathing. Like the breath it is heard from Hufsa's direction
            // at any distance; it greets you when the hunt starts and speaks more often the closer it gets.
            if (!greeted)
            {
                greeted = true;
                voiceTimer = 1.5f;
            }
            voiceTimer -= dt;
            if (voiceTimer > 0f) return;
            HufsaSoundBank.PlayFrom(transform.position, HufsaSoundBank.Pick(HufsaSoundBank.Voice),
                HufsaConfig.VoiceVolume.Value * 1.4f, UnityEngine.Random.Range(0.92f, 1.02f));
            float near = Mathf.Clamp01(1f - dist / VoiceRange); // 0 beyond 70 m, 1 right on you
            voiceTimer = UnityEngine.Random.Range(14f, 30f) / (1f + 2f * near);
        }

        private static void Play(AudioSource s, AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            if (!clip || volume <= 0f) return;
            s.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            s.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// The Watcher, per client: it always appears out of sight, so you hear it breathe from its direction right
    /// after it arrives, and then now and then while it stands behind you (behind the camera), more often the
    /// more you have seen it. In view it is silent.
    /// </summary>
    internal class WatcherSounds : MonoBehaviour
    {
        private const float BehindAngle = 110f; // degrees from where the camera looks

        private HufsaEntity entity;
        private float breathTimer;
        private bool announced;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            breathTimer = UnityEngine.Random.Range(0.4f, 1.2f); // the first breath: right after it arrives
        }

        /// <summary>9-18 s between breaths with no sightings, down to about a third of that near a hunt.</summary>
        private static float NextBreath()
        {
            int dread = HufsaDread.Instance ? HufsaDread.Instance.Count : 0;
            float need = Mathf.Max(1, HufsaConfig.SightingsForHunt.Value);
            return UnityEngine.Random.Range(9f, 18f) / (1f + 2f * Mathf.Clamp01(dread / need));
        }

        private void Update()
        {
            if (!HufsaConfig.SoundsEnabled.Value || !entity || entity.Vanishing) return;
            Player me = Player.m_localPlayer;
            Transform cam = GameCamera.instance ? GameCamera.instance.transform : null;
            if (!me || me.IsDead() || !cam) return;

            Vector3 look = cam.forward, to = transform.position - cam.position;
            look.y = 0f;
            to.y = 0f;
            bool behind = to.sqrMagnitude > 1f && Vector3.Angle(look, to) > BehindAngle;
            if (!announced)
            {
                // The first breath plays wherever it is, to tell you it has come.
                breathTimer -= Time.deltaTime;
                if (breathTimer > 0f) return;
                announced = true;
                HufsaSoundBank.PlayFrom(transform.position, HufsaSoundBank.Pick(HufsaSoundBank.Breaths),
                    HufsaConfig.BreathVolume.Value, UnityEngine.Random.Range(0.94f, 1.04f));
                breathTimer = NextBreath();
                return;
            }
            if (!behind)
            {
                breathTimer = Mathf.Max(breathTimer, UnityEngine.Random.Range(2f, 4f)); // turn away: a moment, then a breath
                return;
            }
            breathTimer -= Time.deltaTime;
            if (breathTimer > 0f) return;
            HufsaSoundBank.PlayFrom(transform.position, HufsaSoundBank.Pick(HufsaSoundBank.Breaths),
                HufsaConfig.BreathVolume.Value, UnityEngine.Random.Range(0.94f, 1.04f));
            breathTimer = NextBreath();
        }
    }
}
