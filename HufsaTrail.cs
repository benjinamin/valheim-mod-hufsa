using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side, on the Hunter: while it glides it leaves round frost patches about as wide as its hem,
    /// clustered toward the middle of its path, and big beaver-like footprints alternating left and right.
    /// The decals live in <see cref="FrostDecals"/> so they keep fading after Hufsa has gone.
    /// </summary>
    internal class HufsaTrail : MonoBehaviour
    {
        private HufsaEntity entity;
        private Vector3 lastPos;
        private float sincePatch, nextPatch, sinceFoot;
        private int footSide = 1;

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            lastPos = transform.position;
        }

        private void Update()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos;
            lastPos = pos;
            delta.y = 0f;
            float d = delta.magnitude;
            if (!HufsaConfig.TrailEnabled.Value || entity.Vanishing || d <= 0f || d > 3f) return;

            sincePatch += d;
            sinceFoot += d;
            Vector3 fwd = delta / d;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

            if (sincePatch >= nextPatch)
            {
                float hemW = HufsaModel.RadiusAt(0.05f) * 2f;
                float off = (Random.value + Random.value - 1f) * hemW * 0.2f;
                Vector3 at = pos - fwd * 0.5f + right * off + fwd * (Random.value - 0.5f) * 0.3f;
                float size = hemW * (0.65f + Random.value * 0.4f);
                FrostDecals.Instance?.PlacePatch(at, size, Random.value * 360f, entity.FrostFactor);
                sincePatch = 0f;
                nextPatch = 0.7f + Random.value * 0.6f;
            }

            float foot = HufsaConfig.FootprintSize.Value;
            if (sinceFoot >= foot * 1.9f)
            {
                // Prints appear just inside the back of the hem as Hufsa glides past, toed out a little.
                Vector3 at = pos + right * footSide * 0.34f - fwd * 0.4f;
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg + footSide * 11f;
                FrostDecals.Instance?.PlaceFootprint(at, foot, yaw, footSide < 0);
                footSide = -footSide;
                sinceFoot = 0f;
            }
        }
    }

    /// <summary>Owns and fades every frost patch and footprint on this client.</summary>
    internal class FrostDecals : MonoBehaviour
    {
        internal static FrostDecals Instance;

        private class Decal
        {
            public GameObject Go;
            public Material Mat;
            public Color Tint;
            public float Born, Peak;
        }

        private const int MaxDecals = 160;
        private readonly List<Decal> decals = new List<Decal>();
        private Mesh quad;
        private Texture2D[] patchTextures;
        private Texture2D footTexture;
        private int groundMask;
        private int layer;

        private void Awake()
        {
            Instance = this;
            groundMask = LayerMask.GetMask("terrain", "Default", "static_solid", "Default_small", "piece");
        }

        private void EnsureAssets()
        {
            if (quad) return;
            quad = new Mesh { name = "HufsaDecalQuad" };
            quad.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
            });
            quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            patchTextures = new[] { 11, 23, 37, 51, 67 }.Select(HufsaModel.FrostPatchTexture).ToArray();
            footTexture = HufsaModel.FootprintTexture();
        }

        /// <param name="strength">1 normally; lower in the mild biomes for a lighter, patchier frost.</param>
        public void PlacePatch(Vector3 at, float size, float yaw, float strength)
        {
            bool snowy = IsSnowy(at);
            Color tint = snowy ? new Color(0.66f, 0.77f, 0.91f) : Color.white;
            Place(at, new Vector3(size * (0.9f + Random.value * 0.2f), 1f, size), yaw,
                patchTextures?[Random.Range(0, 5)], tint, (0.6f + Random.value * 0.35f) * strength);
        }

        public void PlaceFootprint(Vector3 at, float length, float yaw, bool left)
        {
            bool snowy = IsSnowy(at);
            // Dark pressed-ice imprints, readable on frost, snow and grass alike.
            Color tint = snowy ? new Color(0.23f, 0.29f, 0.4f) : new Color(0.086f, 0.125f, 0.227f);
            Place(at, new Vector3((left ? -1f : 1f) * length * 0.9f, 1f, length), yaw, footTexture, tint, 0.85f);
        }

        private void Place(Vector3 at, Vector3 scale, float yaw, Texture2D tex, Color tint, float peak)
        {
            EnsureAssets();
            if (!tex || !HufsaPrefabs.SpriteShader) return;
            if (!Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, groundMask)) return;

            var go = new GameObject("HufsaFrostDecal");
            go.transform.position = hit.point + hit.normal * (0.03f + (layer++ % 8) * 0.004f);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(HufsaPrefabs.SpriteShader) { mainTexture = tex };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            decals.Add(new Decal { Go = go, Mat = mat, Tint = tint, Born = Time.time, Peak = peak });
            if (decals.Count > MaxDecals) Remove(0);
        }

        private void Update()
        {
            if (decals.Count == 0) return;
            float life = Mathf.Max(1f, HufsaConfig.TrailLifetime.Value);
            // Unlit decals would glow at night, so dim them with the scene's ambient light.
            float light = Mathf.Clamp(RenderSettings.ambientLight.grayscale * 2.5f, 0.2f, 1f);
            for (int i = decals.Count - 1; i >= 0; i--)
            {
                Decal d = decals[i];
                float left = 1f - (Time.time - d.Born) / life;
                if (left <= 0f || !d.Go)
                {
                    Remove(i);
                    continue;
                }
                Color c = d.Tint * light;
                c.a = d.Peak * Mathf.Pow(left, 0.7f);
                d.Mat.color = c;
            }
        }

        private void Remove(int i)
        {
            Decal d = decals[i];
            if (d.Go) Destroy(d.Go);
            if (d.Mat) Destroy(d.Mat);
            decals.RemoveAt(i);
        }

        private static bool IsSnowy(Vector3 at)
        {
            if (WorldGenerator.instance == null) return false;
            Heightmap.Biome b = WorldGenerator.instance.GetBiome(at);
            return b == Heightmap.Biome.Mountain || b == Heightmap.Biome.DeepNorth;
        }
    }
}
