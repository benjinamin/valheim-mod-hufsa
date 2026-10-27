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
    /// Missing groups are simply silent; other files are ignored.
    /// </summary>
    internal static class HufsaSoundBank
    {
        public static readonly List<AudioClip> Voice = new List<AudioClip>();
        public static readonly List<AudioClip> Steps = new List<AudioClip>();
        public static readonly List<AudioClip> Creaks = new List<AudioClip>();

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
                    kind.StartsWith("creak") ? Creaks : null;
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
            HufsaPlugin.Log.LogInfo($"Hufsa sounds: {Voice.Count} voice, {Steps.Count} step, {Creaks.Count} creak");
        }

        public static AudioClip Pick(List<AudioClip> group) =>
            group.Count == 0 ? null : group[UnityEngine.Random.Range(0, group.Count)];

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
    /// On the Hunter only (the Watcher is silent), per client: slow footsteps in time with its glide and the
    /// odd wood creak while it moves; its voice now and then while you're near. All 3D, so you can hear where
    /// it is.
    /// </summary>
    internal class HufsaSounds : MonoBehaviour
    {
        private const float VoiceRange = 70f;

        private HufsaEntity entity;
        private AudioSource voice;
        private AudioSource move;
        private float stepTimer;
        private float creakTimer;
        private float voiceTimer;
        private bool greeted;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            voice = MakeSource(VoiceRange);
            voice.minDistance = 10f; // full volume within 10 m, so the voice carries further than its steps
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
            if (group && !voice.outputAudioMixerGroup)
            {
                voice.outputAudioMixerGroup = group;
                move.outputAudioMixerGroup = group;
            }

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

            // Voice: it greets you when it arrives and speaks every so often while you're near.
            if (dist > VoiceRange) return;
            if (!greeted)
            {
                greeted = true;
                voiceTimer = 1.5f;
            }
            voiceTimer -= dt;
            if (voiceTimer > 0f) return;
            Play(voice, HufsaSoundBank.Pick(HufsaSoundBank.Voice), HufsaConfig.VoiceVolume.Value * 1.4f, 0.92f, 1.02f);
            voiceTimer = UnityEngine.Random.Range(14f, 30f);
        }

        private static void Play(AudioSource s, AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            if (!clip || volume <= 0f) return;
            s.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            s.PlayOneShot(clip, volume);
        }
    }
}
