using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Hufsa
{
    /// <summary>Client side: one looping track that fades in with the nearest Hufsa's distance.</summary>
    internal class HufsaAudio : MonoBehaviour
    {
        private const string FileName = "hufsa_loop.mp3";

        private AudioSource source;
        private float volume;

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
            if (!source.outputAudioMixerGroup && AudioMan.instance) source.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;

            float closeness = 0f;
            Player player = Player.m_localPlayer;
            if (player && !player.IsDead())
            {
                float range = HufsaConfig.AudioRange.Value;
                foreach (HufsaEntity g in HufsaEntity.Instances)
                {
                    if (!g.IsHunter || g.Vanishing) continue; // only the hunting Hufsa has music
                    float d = Vector3.Distance(g.transform.position, player.transform.position);
                    closeness = Mathf.Max(closeness, Mathf.Clamp01(1f - d / range));
                }
            }

            float target = HufsaConfig.AudioVolume.Value * Mathf.Pow(closeness, 0.6f);
            float fadeSeconds = target > volume ? 2f : 3f;
            volume = Mathf.MoveTowards(volume, target, Time.deltaTime / fadeSeconds);
            source.volume = volume;

            if (volume > 0.001f && !source.isPlaying) source.Play();
            else if (volume <= 0.001f && source.isPlaying) source.Stop();
        }
    }
}
