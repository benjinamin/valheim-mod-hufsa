using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Hufsa's shape, as tuned in Hufsa Shape Lab (tools/hufsa-preview.html). The body is built
    /// from two traced reference outlines: width from the front picture, depth and forward lean from
    /// the side picture. Heights below are "base units" (0-2.45) and are scaled by Height to metres.
    /// </summary>
    internal static class HufsaModel
    {
        // Shape lab values
        public const float Height = 1.15f;
        public const float Width = 1f;
        public const float Depth = 1f;
        public const float ExtraLean = -0.06f;
        public const float HemWave = 0.035f;
        public const float EyeSize = 1.18f, EyeSpacing = 0.125f, EyeHeight = 2.23f, PupilSize = 0.64f, PupilLook = 0f;
        public const float NoseLength = 0.38f, NoseWidth = 0.65f, NoseTop = 0.21f, NoseTall = 0.31f, NoseFlare = 1.9f;
        public const float NoseHeight = 2.11f, NoseTilt = 2.5f, NoseShade = 0.15f;
        public const float ToothSize = 1.27f, GrinWidth = 30f, GrinHeight = 1.88f, GrinCurve = 0.005f;
        public const int Teeth = 9;

        public const float Top = 2.45f;            // body top in base units
        public const float HemTop = 1.3f;          // base units; nothing above this moves when gliding
        public const float GroundTop = 0.6f;       // base units; how far up the body bends to meet sloping ground
        public const int Segments = 48;            // around the body
        private const float TraceScale = 2.82f;    // metres the traced fractions are relative to

        // Front trace: [height fraction, half-width fraction of total height]
        private static readonly float[,] FrontTrace =
        {
            { 0f, 0.47f }, { 0.019f, 0.494f }, { 0.045f, 0.477f }, { 0.079f, 0.456f }, { 0.113f, 0.437f },
            { 0.147f, 0.426f }, { 0.182f, 0.42f }, { 0.216f, 0.411f }, { 0.25f, 0.404f }, { 0.284f, 0.4f },
            { 0.318f, 0.396f }, { 0.353f, 0.389f }, { 0.387f, 0.383f }, { 0.421f, 0.374f }, { 0.455f, 0.364f },
            { 0.49f, 0.351f }, { 0.524f, 0.336f }, { 0.558f, 0.318f }, { 0.592f, 0.299f }, { 0.627f, 0.275f },
            { 0.661f, 0.251f }, { 0.695f, 0.224f }, { 0.729f, 0.201f }, { 0.747f, 0.191f }, { 0.764f, 0.184f },
            { 0.798f, 0.172f }, { 0.832f, 0.162f }, { 0.866f, 0.152f }, { 0.901f, 0.138f }, { 0.935f, 0.12f },
            { 0.952f, 0.105f }, { 0.969f, 0.087f }, { 0.986f, 0.059f }, { 0.995f, 0.035f }, { 1f, 0f },
        };

        // Side trace (facing +z, nose excluded): [height fraction, half-depth fraction, centre offset fraction]
        private static readonly float[,] SideTrace =
        {
            { 0f, 0.401f, 0.002f }, { 0.035f, 0.403f, 0.006f }, { 0.067f, 0.402f, 0.012f }, { 0.099f, 0.4f, 0.016f },
            { 0.131f, 0.396f, 0.022f }, { 0.163f, 0.391f, 0.028f }, { 0.195f, 0.388f, 0.034f }, { 0.226f, 0.384f, 0.042f },
            { 0.258f, 0.38f, 0.049f }, { 0.29f, 0.376f, 0.056f }, { 0.322f, 0.369f, 0.062f }, { 0.354f, 0.364f, 0.069f },
            { 0.386f, 0.358f, 0.077f }, { 0.418f, 0.35f, 0.083f }, { 0.45f, 0.339f, 0.091f }, { 0.482f, 0.327f, 0.1f },
            { 0.514f, 0.312f, 0.11f }, { 0.545f, 0.293f, 0.124f }, { 0.577f, 0.276f, 0.133f }, { 0.609f, 0.258f, 0.147f },
            { 0.641f, 0.238f, 0.163f }, { 0.673f, 0.22f, 0.176f }, { 0.705f, 0.203f, 0.19f }, { 0.737f, 0.189f, 0.204f },
            { 0.753f, 0.183f, 0.211f }, { 0.769f, 0.176f, 0.215f }, { 0.785f, 0.171f, 0.219f }, { 0.801f, 0.167f, 0.222f },
            { 0.817f, 0.163f, 0.226f }, { 0.833f, 0.159f, 0.23f }, { 0.848f, 0.154f, 0.233f }, { 0.864f, 0.151f, 0.236f },
            { 0.88f, 0.147f, 0.238f }, { 0.896f, 0.14f, 0.238f }, { 0.912f, 0.131f, 0.238f }, { 0.928f, 0.121f, 0.238f },
            { 0.944f, 0.112f, 0.24f }, { 0.96f, 0.097f, 0.243f }, { 0.976f, 0.078f, 0.246f }, { 0.992f, 0.049f, 0.25f },
            { 1f, 0f, 0.25f },
        };

        private static float Interp(float[,] table, float h, int col)
        {
            int n = table.GetLength(0);
            for (int i = 0; i < n - 1; i++)
            {
                float h0 = table[i, 0], h1 = table[i + 1, 0];
                if (h >= h0 && h <= h1) return table[i, col] + (table[i + 1, col] - table[i, col]) * ((h - h0) / (h1 - h0));
            }
            return table[n - 1, col];
        }

        /// <summary>Sideways (x) radius in metres at a base height.</summary>
        public static float RadiusAt(float yBase) => Interp(FrontTrace, yBase / Top, 1) * TraceScale * Width;

        /// <summary>Front-to-back (z) radius in metres at a base height.</summary>
        public static float DepthRadiusAt(float yBase) => Interp(SideTrace, yBase / Top, 1) * TraceScale * Depth;

        /// <summary>Forward offset of the body centre: traced lean plus the extra lean.</summary>
        public static float LeanAt(float yBase) =>
            Interp(SideTrace, yBase / Top, 2) * TraceScale + ExtraLean * (yBase / Top) * (yBase / Top);

        /// <summary>Front surface z for a point at sideways offset x on the elliptical cross-section.</summary>
        public static float SurfaceZ(float yBase, float x = 0f)
        {
            float r = RadiusAt(yBase);
            float u = r > 0f ? x / r : 1f;
            return DepthRadiusAt(yBase) * Mathf.Sqrt(Mathf.Max(1f - u * u, 0f)) + LeanAt(yBase);
        }

        // ------------------------------------------------------------------ body mesh

        /// <summary>Rest vertices and, per vertex, base height / angle / ring centre for the hem animation.</summary>
        public static Vector3[] BodyRest;
        public static float[] VertexHeight, VertexAngle, VertexCentre;
        public static int[] VertexAngleIndex; // 0..Segments around the body; the bottom ring is vertices 0..Segments

        /// <summary>
        /// Lathe with an elliptical ring at every height either trace has a point at, so both outlines are
        /// reproduced exactly. x = sin(angle), z = cos(angle): the face is at +z.
        /// </summary>
        public static Mesh BuildBodyMesh()
        {
            var hs = new SortedSet<float>();
            for (int i = 0; i < FrontTrace.GetLength(0); i++) hs.Add(FrontTrace[i, 0]);
            for (int i = 0; i < SideTrace.GetLength(0); i++) hs.Add(SideTrace[i, 0]);
            var heights = new List<float>(hs);

            const int seg = Segments;
            int ring = seg + 1;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var vi = new List<int>();
            var vh = new List<float>();
            var va = new List<float>();
            var vc = new List<float>();
            for (int j = 0; j < heights.Count; j++)
            {
                float yBase = heights[j] * Top;
                float rx = RadiusAt(yBase), rz = DepthRadiusAt(yBase), lean = LeanAt(yBase);
                for (int i = 0; i <= seg; i++)
                {
                    float ang = i / (float)seg * Mathf.PI * 2f;
                    float x = Mathf.Sin(ang) * rx, z = Mathf.Cos(ang) * rz;
                    if (yBase < 0.5f)
                    {
                        float f = 1f + HemWave * Mathf.Sin(ang * 7f + j) * (1f - yBase / 0.5f);
                        x *= f;
                        z *= f;
                    }
                    verts.Add(new Vector3(x, yBase * Height, z + lean));
                    uvs.Add(new Vector2(i / (float)seg, j / (float)(heights.Count - 1)));
                    vh.Add(yBase);
                    vi.Add(i);
                    va.Add(ang);
                    vc.Add(lean);
                }
            }

            var tris = new List<int>();
            for (int j = 0; j < heights.Count - 1; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * ring + i, b = a + 1, c = a + ring, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }

            BodyRest = verts.ToArray();
            VertexAngleIndex = vi.ToArray();
            VertexHeight = vh.ToArray();
            VertexAngle = va.ToArray();
            VertexCentre = vc.ToArray();

            var mesh = new Mesh { name = "HufsaBody" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            BodyNormals = mesh.normals;
            BodyTriangles = tris.ToArray();
            return mesh;
        }

        public static Vector3[] BodyNormals;
        private static int[] BodyTriangles;

        /// <summary>
        /// A copy of the body pushed a couple of centimetres out along its normals. SilhouetteGlow colours its
        /// vertices by viewing angle so only the outline glows.
        /// </summary>
        public static Mesh BuildGlowShellMesh()
        {
            var verts = new Vector3[BodyRest.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = BodyRest[i] + BodyNormals[i] * 0.025f;
            var mesh = new Mesh { name = "HufsaGlowShell" };
            mesh.vertices = verts;
            mesh.normals = BodyNormals;
            mesh.colors32 = new Color32[verts.Length];
            mesh.triangles = BodyTriangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Writes the gliding hem into <paramref name="work"/>: folds travel around the hem, the hem lifts a
        /// little in each fold and trails slightly behind. Nothing above HemTop moves.
        /// </summary>
        public static void AnimateHem(Vector3[] work, float time, float amount)
        {
            for (int k = 0; k < work.Length; k++)
            {
                float yb = VertexHeight[k];
                Vector3 rest = BodyRest[k];
                if (yb >= HemTop || amount <= 0f)
                {
                    work[k] = rest;
                    continue;
                }
                float w = (1f - yb / HemTop) * (1f - yb / HemTop) * amount;
                float ang = VertexAngle[k], cz = VertexCentre[k];
                float fold = Mathf.Sin(ang * 5f - time * 4.5f);
                float f = 1f + w * (0.05f * fold + 0.025f * Mathf.Sin(ang * 3f + time * 2.7f));
                work[k] = new Vector3(
                    rest.x * f,
                    rest.y + 0.05f * w * Mathf.Max(0f, fold) * (yb < 0.05f ? 1f : 0.4f),
                    cz + (rest.z - cz) * f - 0.12f * w);
            }
        }

        /// <summary>
        /// Bends the lower body onto sloping ground: groundOffset[i] is how far (local units, + up) the terrain
        /// is from the hem at angle step i. Full effect at the hem, fading out by GroundTop.
        /// </summary>
        public static void ConformToGround(Vector3[] work, float[] groundOffset)
        {
            for (int k = 0; k < work.Length; k++)
            {
                float yb = VertexHeight[k];
                if (yb >= GroundTop) continue;
                float w = 1f - yb / GroundTop;
                work[k].y += groundOffset[VertexAngleIndex[k]] * w * w;
            }
        }

        // ------------------------------------------------------------------ nose mesh

        /// <summary>
        /// The bottom half of an hourglass (narrow neck on top, flaring to a wide rounded bottom), cut in half
        /// lengthwise with the flat side against the face. Unit size: 1 wide, 1 tall (centred), 1 deep.
        /// </summary>
        public static Mesh BuildNoseMesh()
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f) };
            const int steps = 14;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float r = NoseTop + (1f - NoseTop) * Mathf.Pow(1f - t, NoseFlare);
                profile.Add(new Vector2(0.5f * r * (s == 0 ? 0.92f : 1f), s == 0 ? 0.04f : t));
            }
            profile.Add(new Vector2(0f, 1f));

            const int seg = 24;
            int ring = seg + 1;
            var verts = new List<Vector3>();
            for (int j = 0; j < profile.Count; j++)
            for (int i = 0; i <= seg; i++)
            {
                float phi = -Mathf.PI / 2f + i / (float)seg * Mathf.PI; // front half only (z >= 0)
                float r = profile[j].x;
                verts.Add(new Vector3(Mathf.Sin(phi) * r, profile[j].y - 0.5f, Mathf.Cos(phi) * r * 2f));
            }
            var tris = new List<int>();
            for (int j = 0; j < profile.Count - 1; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * ring + i, b = a + 1, c = a + ring, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            var mesh = new Mesh { name = "HufsaNose" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ textures

        /// <summary>
        /// Point-filtered cloak in one dark purple: no stripes, just blocky pixels varying slightly in
        /// brightness so it keeps the game's chunky look. U wraps around, V runs up. The colour is linear and
        /// converted to gamma, since that is how Unity reads texture pixels.
        /// </summary>
        public static Texture2D CloakTexture()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "HufsaCloak",
            };
            var purple = new Color(0.036f, 0.014f, 0.054f);
            var rng = new System.Random(1337);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float shade = 0.88f + (float)rng.NextDouble() * 0.24f; // +-12% per pixel, same hue
                Color c = (purple * shade).gamma;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>A roundish, ragged-edged frost patch with a few bare spots; each seed gives a different outline.</summary>
        public static Texture2D FrostPatchTexture(int seed)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HufsaFrostPatch",
            };
            var rng = new System.Random(seed);
            Color[] cols =
            {
                new Color(0.80f, 0.88f, 0.98f), new Color(0.88f, 0.93f, 1f), new Color(0.67f, 0.78f, 0.92f),
                new Color(0.96f, 0.98f, 1f),
            };
            float p0 = (float)rng.NextDouble() * 6.28f, p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                float edge = 0.84f + 0.05f * Mathf.Sin(ang * 2f + p0) + 0.03f * Mathf.Sin(ang * 3f + p1) + 0.015f * Mathf.Sin(ang * 5f + p2);
                float a = Mathf.Clamp01((edge - r) * 6f + ((float)rng.NextDouble() - 0.5f) * 0.35f);
                if (rng.NextDouble() < 0.06) a *= 0.4f;
                Color c = rng.NextDouble() < 0.05 ? cols[3] : cols[rng.Next(3)];
                c.a = a <= 0.05f ? 0f : a * 0.85f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A big webbed monster print modelled on a beaver's hind foot: five fanned toes with claw tips,
        /// webbing between them and a narrow heel. Toes point to +V (forward when laid on the ground).
        /// </summary>
        public static Texture2D FootprintTexture()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HufsaFootprint" };
            Vector2[] web =
            {
                new Vector2(40, 64), new Vector2(18, 38), new Vector2(22, 26), new Vector2(34, 28), new Vector2(40, 18),
                new Vector2(52, 20), new Vector2(64, 10), new Vector2(76, 20), new Vector2(88, 18), new Vector2(94, 28),
                new Vector2(106, 26), new Vector2(110, 38), new Vector2(88, 64),
            };
            Vector2[] sole =
            {
                new Vector2(42, 64), new Vector2(45, 85), new Vector2(50, 100), new Vector2(56, 112), new Vector2(64, 117),
                new Vector2(72, 112), new Vector2(78, 100), new Vector2(83, 85), new Vector2(86, 64),
            };
            float[,] toes = { { -50, 34 }, { -25, 44 }, { 0, 50 }, { 25, 44 }, { 50, 34 } }; // angle, length
            for (int ty = 0; ty < s; ty++)
            for (int tx = 0; tx < s; tx++)
            {
                // drawing coordinates: y grows downward, toes at the top
                var p = new Vector2(tx + 0.5f, s - 1 - ty + 0.5f);
                float a = InPolygon(p, web) ? 0.45f : 0f;
                if (InPolygon(p, sole)) a = 0.95f;
                for (int i = 0; i < toes.GetLength(0) && a < 0.95f; i++)
                {
                    float rad = toes[i, 0] * Mathf.Deg2Rad, len = toes[i, 1];
                    float dx = p.x - 64f, dy = p.y - 66f;
                    float lx = dx * Mathf.Cos(rad) + dy * Mathf.Sin(rad);
                    float ly = -dx * Mathf.Sin(rad) + dy * Mathf.Cos(rad);
                    float ex = lx / 6f, ey = (ly + len / 2f + 4f) / (len / 2f);
                    bool claw = InTriangle(new Vector2(lx, ly), new Vector2(-3, -len - 3), new Vector2(0, -len - 12), new Vector2(3, -len - 3));
                    if (ex * ex + ey * ey <= 1f || claw) a = 0.95f;
                }
                tex.SetPixel(tx, ty, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        private static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
