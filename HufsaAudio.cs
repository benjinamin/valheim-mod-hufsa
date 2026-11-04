using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Hufsa
{
    /// <summary>
    /// Client side: one looping track that fades in with the nearest Hunter's distance, once it has started
    /// moving (not while it stands there before the chase). It plays as music: on the
    /// game's music mixer, at the music volume setting, and while it plays the game's own music (the biome's)
    /// fades out for it, and fades back in as Hufsa's fades to nothing (Patches: MusicMan.UpdateMusic).
    /// </summary>
    internal class HufsaAudio : MonoBehaviour
    {
        private const string FileName = "hufsa_loop.mp3";

        private AudioSource source;
        private float volume;

        /// <summary>How much of the game's own music to keep right now (1 = all of it, 0 = silenced for Hufsa's).</summary>
        internal static float GameMusicFactor = 1f;

        private IEnumerator Start()
        {
            StartCoroutine(HufsaSoundBank.Load());
            string path = Path.Combine(Path.GetDirectoryName(typeof(HufsaAudio).Assembly.Location) ?? "", FileName);
            if (!File.Exists(path))
            {
                HufsaPlugin.Log.LogWarning($"No Hufsa audio at {path}");
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    HufsaPlugin.Log.LogError($"Failed to load Hufsa audio: {req.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = "HufsaLoop";
                source = gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0f;
            }
        }

        private void Update()
        {
            if (!source) return;
            bool asMusic = HufsaConfig.PlayAsMusic.Value && MusicMan.instance;
            var group = asMusic ? MusicMan.instance.m_musicMixer : AudioMan.instance ? AudioMan.instance.m_ambientMixer : null;
            if (group && source.outputAudioMixerGroup != group) source.outputAudioMixerGroup = group;

            float closeness = 0f;
            Player player = Player.m_localPlayer;
            if (player && !player.IsDead())
            {
                float range = HufsaConfig.AudioRange.Value;
                foreach (HufsaEntity g in HufsaEntity.Instances)
                {
                    if (!g.IsHunter || g.Vanishing || !g.HasMoved) continue; // only the hunting Hufsa, once it moves
                    float d = Vector3.Distance(g.transform.position, player.transform.position);
                    closeness = Mathf.Max(closeness, Mathf.Clamp01(1f - d / range));
                }
            }

            float target = HufsaConfig.AudioVolume.Value * Mathf.Pow(closeness, 0.6f);
            float fadeSeconds = target > volume ? 2f : 3f;
            volume = Mathf.MoveTowards(volume, target, Time.deltaTime / fadeSeconds);
            source.volume = volume * (asMusic ? MusicMan.m_masterMusicVolume : 1f);

            // The game's music gives way as Hufsa's comes in: gone by the time Hufsa's is at a third of its
            // loudness, and back in full only once Hufsa's has faded to nothing. Both fade, never cut.
            float share = HufsaConfig.AudioVolume.Value > 0f ? volume / HufsaConfig.AudioVolume.Value : 0f;
            float keep = asMusic && HufsaConfig.OverrideGameMusic.Value ? 1f - Mathf.Clamp01(share * 3f) : 1f;
            if (volume <= 0.001f) keep = 1f;
            GameMusicFactor = Mathf.MoveTowards(GameMusicFactor, keep, Time.deltaTime / 2f);

            if (volume > 0.001f && !source.isPlaying) source.Play();
            else if (volume <= 0.001f && source.isPlaying) source.Stop();
        }
    }
}
