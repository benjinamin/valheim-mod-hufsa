using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>Client side: tints vegetation and rocks near a Hufsa toward a frosty white, fading in and out.</summary>
    internal class FrostVisuals : MonoBehaviour
    {
        private class Entry
        {
            public float Amount;
            public Color[] BaseColors;
        }

        private const float FadeSeconds = 3f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        // renderer -> how frosted it should get (1, or less near a Hufsa in a mild biome)
        private readonly Dictionary<Renderer, float> targets = new Dictionary<Renderer, float>();
        private readonly HashSet<GameObject> scannedRoots = new HashSet<GameObject>();
        private readonly List<Renderer> removeBuffer = new List<Renderer>();
        private readonly Collider[] hits = new Collider[4096];
        private MaterialPropertyBlock mpb;
        private float scanTimer;

        private void Update()
        {
            if (!HufsaConfig.FoliageFrostEnabled.Value)
            {
                if (entries.Count > 0) ClearAll();
                return;
            }

            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 1f;
                Scan();
            }
            Fade(Time.deltaTime);
        }

        private void Scan()
        {
            targets.Clear();
            scannedRoots.Clear();
            Player player = Player.m_localPlayer;
            if (!player) return;

            foreach (HufsaEntity g in HufsaEntity.Instances)
            {
                if (g.Vanishing) continue;
                float radius = g.IsHunter ? HufsaConfig.ColdRadius.Value : HufsaConfig.WatcherFrostRadius.Value;
                if (radius <= 0f) continue;
                if ((g.transform.position - player.transform.position).sqrMagnitude > 200f * 200f) continue;

                float goal = g.FrostFactor;
                int n = Physics.OverlapSphereNonAlloc(g.transform.position, radius, hits);
                for (int i = 0; i < n; i++)
                {
                    GameObject root = FindFrostableRoot(hits[i]);
                    if (!root || !scannedRoots.Add(root)) continue;
                    foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    {
                        if (r is ParticleSystemRenderer) continue;
                        if (!targets.TryGetValue(r, out float cur) || goal > cur) targets[r] = goal;
                    }
                }
            }
        }

        private static GameObject FindFrostableRoot(Collider c)
        {
            if (!c || c.GetComponentInParent<Piece>() || c.GetComponentInParent<Character>()) return null;
            Component comp = c.GetComponentInParent<TreeBase>();
            if (!comp) comp = c.GetComponentInParent<TreeLog>();
            if (!comp) comp = c.GetComponentInParent<Pickable>();
            if (!comp) comp = c.GetComponentInParent<Destructible>();
            if (!comp) comp = c.GetComponentInParent<MineRock>();
            if (!comp) comp = c.GetComponentInParent<MineRock5>();
            return comp ? comp.gameObject : null;
        }

        private void Fade(float dt)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            Color frost = HufsaConfig.FrostColor.Value;
            float strength = HufsaConfig.FrostStrength.Value;

            foreach (Renderer r in targets.Keys)
            {
                if (r && !entries.ContainsKey(r)) entries[r] = new Entry { BaseColors = ReadBaseColors(r) };
            }

            removeBuffer.Clear();
            foreach (KeyValuePair<Renderer, Entry> kv in entries)
            {
                Renderer r = kv.Key;
                Entry e = kv.Value;
                if (!r)
                {
                    removeBuffer.Add(r);
                    continue;
                }

                float goal = targets.TryGetValue(r, out float g) ? g : 0f;
                if (Mathf.Approximately(e.Amount, goal) && goal > 0f) continue;
                e.Amount = Mathf.MoveTowards(e.Amount, goal, dt / FadeSeconds);

                if (e.Amount <= 0f)
                {
                    Restore(r, e);
                    removeBuffer.Add(r);
                    continue;
                }

                for (int i = 0; i < e.BaseColors.Length; i++)
                {
                    mpb.Clear();
                    mpb.SetColor(ColorId, Color.Lerp(e.BaseColors[i], frost, e.Amount * strength));
                    r.SetPropertyBlock(mpb, i);
                }
            }
            foreach (Renderer r in removeBuffer) entries.Remove(r);
        }

        private static Color[] ReadBaseColors(Renderer r)
        {
            Material[] mats = r.sharedMaterials;
            var colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                colors[i] = mats[i] && mats[i].HasProperty(ColorId) ? mats[i].GetColor(ColorId) : Color.white;
            }
            return colors;
        }

        private void Restore(Renderer r, Entry e)
        {
            mpb.Clear();
            for (int i = 0; i < e.BaseColors.Length; i++) r.SetPropertyBlock(mpb, i);
        }

        private void ClearAll()
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (KeyValuePair<Renderer, Entry> kv in entries)
            {
                if (kv.Key) Restore(kv.Key, kv.Value);
            }
            entries.Clear();
        }
    }
}
