using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hufsa
{
    /// <summary>
    /// How much of a Watcher the local player really sees. A tiny probe camera renders the real scene (trees,
    /// leaves, rocks, terrain, each with its own shader, so cut-out foliage hides it just like on screen) cropped
    /// to Hufsa's upper body, with Hufsa drawn flat magenta; a second pass draws Hufsa alone. The visible share is
    /// magenta pixels in the scene over pixels alone, for the whole upper body and for the face band. Read back
    /// asynchronously, one measurement at a time. Without GPU readback it falls back to rays over the same area.
    /// Also: the canopy check (leaves have no colliders) and how far a sighting can reach through fog.
    /// </summary>
    internal static class HufsaSight
    {
        // Heights (m at scale 1): the upper body, from where the arms start to the top, and the face in it.
        public static readonly float RegionBottom = HufsaModel.ArmHeight * HufsaModel.Height;
        public static readonly float RegionTop = HufsaModel.Top * HufsaModel.Height;
        public static readonly float FaceBottom = (HufsaModel.GrinHeight - 0.12f) * HufsaModel.Height;
        public static readonly float FaceTop = (HufsaModel.EyeHeight + 0.12f) * HufsaModel.Height;
        private const float RegionRadius = 1.2f;
        private const float Interval = 0.2f;
        private const int Size = 64;
        private const int MinPixels = 6;

        public static bool UsesGpu => SystemInfo.supportsAsyncGPUReadback && Init();

        /// <summary>
        /// The upper body's box on screen (viewport units, may reach past the edges) and the face band's top and
        /// bottom in it. False when any of it is behind the camera.
        /// </summary>
        public static bool ScreenRegion(Camera cam, Vector3 pos, float scale, out Rect rect, out float faceLo, out float faceHi)
        {
            rect = default;
            faceLo = faceHi = 0f;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -RegionRadius : RegionRadius, (i & 2) == 0 ? RegionBottom : RegionTop,
                    (i & 4) == 0 ? -RegionRadius : RegionRadius);
                Vector3 vp = cam.WorldToViewportPoint(pos + corner * scale);
                if (vp.z < 0.1f) return false;
                x0 = Mathf.Min(x0, vp.x);
                x1 = Mathf.Max(x1, vp.x);
                y0 = Mathf.Min(y0, vp.y);
                y1 = Mathf.Max(y1, vp.y);
            }
            rect = Rect.MinMaxRect(x0, y0, x1, y1);
            faceLo = cam.WorldToViewportPoint(pos + Vector3.up * FaceBottom * scale).y;
            faceHi = cam.WorldToViewportPoint(pos + Vector3.up * FaceTop * scale).y;
            return true;
        }

        /// <summary>
        /// Measures again if it's time (every Interval per Watcher, one at a time). The result lands in the
        /// Watcher's SightVisible / SightFace a frame or two later.
        /// </summary>
        public static void Measure(HufsaEntity e, Transform visual, Camera cam, Rect rect, float faceLo, float faceHi)
        {
            if (e.SightPending || Time.time < e.SightNextAt) return;
            if (UsesGpu)
            {
                if (busy) return;
                e.SightNextAt = Time.time + Interval;
                MeasureGpu(e, visual, cam, rect, faceLo, faceHi);
            }
            else
            {
                e.SightNextAt = Time.time + Interval;
                MeasureRays(e, cam);
            }
        }

        // ---------------------------------------------------------------- GPU probe

        private static Camera probe;
        private static RenderTexture sceneRT, aloneRT;
        private static CommandBuffer sceneCb, aloneCb;
        private static Material magenta;
        private static bool busy, initFailed;
        private static readonly Color32[] sceneBuf = new Color32[Size * Size], aloneBuf = new Color32[Size * Size];
        private static readonly List<Renderer> hidden = new List<Renderer>();
        private static readonly List<bool> hiddenWas = new List<bool>();
        private static readonly Dictionary<Mesh, Mesh> whiteCopies = new Dictionary<Mesh, Mesh>();

        private static bool Init()
        {
            if (probe) return true;
            if (initFailed || !HufsaPrefabs.SpriteShader) return false;
            try
            {
                var go = new GameObject("HufsaSightProbe") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                probe = go.AddComponent<Camera>();
                probe.enabled = false;
                sceneRT = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "HufsaSightScene" };
                aloneRT = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "HufsaSightAlone" };
                // Sprites/Default: unlit, no fog, depth-tested, colour times vertex colour (so meshes with
                // vertex colours get a white copy, see WhiteCopy).
                magenta = new Material(HufsaPrefabs.SpriteShader) { name = "HufsaSightMagenta", color = Color.magenta };
                sceneCb = new CommandBuffer { name = "HufsaSightScene" };
                aloneCb = new CommandBuffer { name = "HufsaSightAlone" };
                probe.AddCommandBuffer(CameraEvent.AfterForwardAlpha, sceneCb);
                return true;
            }
            catch (System.Exception ex)
            {
                HufsaPlugin.Log.LogWarning($"Sight probe unavailable, using rays: {ex.Message}");
                initFailed = true;
                return false;
            }
        }

        private static void MeasureGpu(HufsaEntity e, Transform visual, Camera cam, Rect rect, float faceLo, float faceHi)
        {
            // Only what is on screen can be seen: crop to the screen, and count the part beyond it as hidden.
            Rect crop = Rect.MinMaxRect(Mathf.Max(rect.xMin, 0f), Mathf.Max(rect.yMin, 0f), Mathf.Min(rect.xMax, 1f), Mathf.Min(rect.yMax, 1f));
            if (crop.width <= 0f || crop.height <= 0f)
            {
                e.SightVisible = e.SightFace = 0f;
                return;
            }
            float onScreen = crop.width * crop.height / Mathf.Max(rect.width * rect.height, 1e-6f);
            int faceRow0 = Mathf.Clamp(Mathf.FloorToInt((faceLo - crop.yMin) / crop.height * Size), 0, Size);
            int faceRow1 = Mathf.Clamp(Mathf.CeilToInt((faceHi - crop.yMin) / crop.height * Size), 0, Size);

            probe.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
            probe.nearClipPlane = cam.nearClipPlane;
            probe.farClipPlane = cam.farClipPlane;
            probe.cullingMask = cam.cullingMask;
            probe.renderingPath = cam.renderingPath;
            probe.clearFlags = CameraClearFlags.SolidColor;
            probe.backgroundColor = Color.clear;
            probe.allowHDR = false;
            probe.allowMSAA = false;
            probe.useOcclusionCulling = cam.useOcclusionCulling;
            probe.projectionMatrix = Crop(cam.projectionMatrix, crop);

            sceneCb.Clear();
            aloneCb.Clear();
            aloneCb.SetRenderTarget(aloneRT);
            aloneCb.ClearRenderTarget(true, true, Color.clear);
            aloneCb.SetViewProjectionMatrices(probe.worldToCameraMatrix, probe.projectionMatrix);
            hidden.Clear();
            hiddenWas.Clear();
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                hidden.Add(r);
                hiddenWas.Add(r.forceRenderingOff);
                // What the player can see of it now: no glow shell (bigger than the body), no particles, nothing
                // faded out (teeth and arms far away).
                if (!(r is MeshRenderer) || !r.enabled || r.GetComponent<SilhouetteGlow>()) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                Mesh mesh = f ? WhiteCopy(f.sharedMesh) : null;
                if (!mesh) continue;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    sceneCb.DrawMesh(mesh, r.localToWorldMatrix, magenta, s);
                    aloneCb.DrawMesh(mesh, r.localToWorldMatrix, magenta, s);
                }
            }

            float shadows = QualitySettings.shadowDistance;
            try
            {
                QualitySettings.shadowDistance = 0f; // only the magenta counts, light doesn't matter
                foreach (Renderer r in hidden) r.forceRenderingOff = true; // its real self mustn't hide the magenta
                probe.targetTexture = sceneRT;
                probe.Render();
                Graphics.ExecuteCommandBuffer(aloneCb);
            }
            finally
            {
                QualitySettings.shadowDistance = shadows;
                for (int i = 0; i < hidden.Count; i++) if (hidden[i]) hidden[i].forceRenderingOff = hiddenWas[i];
                probe.targetTexture = null;
            }

            busy = true;
            e.SightPending = true;
            int done = 0;
            bool failed = false;
            void Finish(AsyncGPUReadbackRequest req, Color32[] buf)
            {
                Readbacks++;
                if (req.hasError)
                {
                    failed = true;
                    ReadbackErrors++;
                }
                else req.GetData<Color32>().CopyTo(buf);
                if (++done < 2) return;
                busy = false;
                if (!e) return;
                e.SightPending = false;
                if (failed) return;
                Count(faceRow0, faceRow1, out float all, out float face);
                e.SightVisible = all * onScreen;
                e.SightFace = face * onScreen;
                if (dumpNext) Dump(crop, faceRow0, faceRow1);
            }
            AsyncGPUReadback.Request(sceneRT, 0, TextureFormat.RGBA32, req => Finish(req, sceneBuf));
            AsyncGPUReadback.Request(aloneRT, 0, TextureFormat.RGBA32, req => Finish(req, aloneBuf));
        }

        /// <summary>Visible share of the whole upper body and of the face rows (0 when too few pixels to tell).</summary>
        private static void Count(int faceRow0, int faceRow1, out float all, out float face)
        {
            // The body goes on below the box, so its bottom row is always filled and the row above its head is
            // not. If the readback came out upside down, turn the face rows round.
            if (!RowFilled(0) && RowFilled(Size - 1))
            {
                int r0 = Size - faceRow1;
                faceRow1 = Size - faceRow0;
                faceRow0 = r0;
            }
            int seen = 0, total = 0, faceSeen = 0, faceTotal = 0;
            for (int y = 0; y < Size; y++)
            {
                bool inFace = y >= faceRow0 && y < faceRow1;
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    if (aloneBuf[i].r < 128) continue;
                    total++;
                    if (inFace) faceTotal++;
                    Color32 c = sceneBuf[i];
                    if (c.r < 150 || c.b < 150 || c.g > 100) continue; // not magenta: something in front of it
                    seen++;
                    if (inFace) faceSeen++;
                }
            }
            all = total >= MinPixels ? seen / (float)total : 0f;
            face = faceTotal >= MinPixels ? faceSeen / (float)faceTotal : all;
            LastPixels = $"{seen}/{total} px, face {faceSeen}/{faceTotal}";
        }

        // ---------------------------------------------------------------- debug

        /// <summary>Test-mode readout: the last count and how the readbacks are going.</summary>
        public static string Stats => $"{LastPixels}, readbacks {Readbacks} (errors {ReadbackErrors}){(busy ? ", busy" : "")}, " +
                                      $"fog {(RenderSettings.fog ? RenderSettings.fogMode.ToString() : "off")} density {RenderSettings.fogDensity:0.0000} " +
                                      $"start {RenderSettings.fogStartDistance:0} end {RenderSettings.fogEndDistance:0}";
        private static string LastPixels = "no count yet";
        private static int Readbacks, ReadbackErrors;
        private static bool dumpNext;

        /// <summary>"hufsa probeshot": saves the next measurement's two pictures next to BepInEx's config folder.</summary>
        public static void DumpNextMeasurement() => dumpNext = true;

        private static void Dump(Rect crop, int faceRow0, int faceRow1)
        {
            dumpNext = false;
            string dir = System.IO.Path.GetDirectoryName(BepInEx.Paths.ConfigPath);
            foreach (var (name, buf) in new[] { ("scene", sceneBuf), ("alone", aloneBuf) })
            {
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.SetPixels32(buf);
                string path = System.IO.Path.Combine(dir, $"hufsa_probe_{name}.png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.Destroy(tex);
                HufsaPlugin.Log.LogInfo($"[debug] probe picture: {path}");
            }
            HufsaPlugin.Log.LogInfo($"[debug] probe crop {crop}, face rows {faceRow0}-{faceRow1}, {Stats}, " +
                                    $"path {probe.actualRenderingPath}, sRGB {sceneRT.sRGB}, readback {SystemInfo.supportsAsyncGPUReadback}");
        }

        private static bool RowFilled(int y)
        {
            for (int x = 0; x < Size; x++) if (aloneBuf[y * Size + x].r >= 128) return true;
            return false;
        }

        /// <summary>The camera's projection zoomed onto one viewport rectangle.</summary>
        private static Matrix4x4 Crop(Matrix4x4 proj, Rect vp)
        {
            float sx = 1f / vp.width, sy = 1f / vp.height;
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = sx;
            m.m03 = sx - 1f - 2f * vp.xMin * sx;
            m.m11 = sy;
            m.m13 = sy - 1f - 2f * vp.yMin * sy;
            return m * proj;
        }

        /// <summary>Sprites/Default multiplies by vertex colour; a mesh that has some gets a white copy.</summary>
        private static Mesh WhiteCopy(Mesh src)
        {
            if (!src || !src.isReadable || src.colors32.Length == 0) return src;
            if (!whiteCopies.TryGetValue(src, out Mesh copy) || !copy)
            {
                if (whiteCopies.Count > 32)
                {
                    foreach (Mesh m in whiteCopies.Values) if (m) Object.Destroy(m);
                    whiteCopies.Clear();
                }
                copy = Object.Instantiate(src);
                var white = new Color32[copy.vertexCount];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                copy.colors32 = white;
                whiteCopies[src] = copy;
            }
            else if (copy.vertexCount == src.vertexCount)
            {
                copy.vertices = src.vertices; // the body's hem follows the ground
            }
            return copy;
        }

        // ---------------------------------------------------------------- ray fallback

        private static int losMask = -1, treeMask;

        private static void Masks()
        {
            if (losMask >= 0) return;
            losMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            treeMask = LayerMask.GetMask("Default", "static_solid", "Default_small");
        }

        private static readonly float[] RayHeights = { 1.5f, 1.8f, 2.1f, 2.4f, 2.65f };
        private static readonly float[] RayAcross = { -0.45f, 0f, 0.45f };

        /// <summary>Rays from the eye to points over the upper body; leaves count as in the way (CanopyBlocks).</summary>
        private static void MeasureRays(HufsaEntity e, Camera cam)
        {
            Masks();
            Vector3 eye = cam.transform.position;
            Vector3 pos = e.transform.position;
            Vector3 across = Vector3.Cross(Vector3.up, pos - eye).normalized;
            int seen = 0, total = 0, faceSeen = 0, faceTotal = 0;
            foreach (float h in RayHeights)
            {
                bool inFace = h >= FaceBottom && h <= FaceTop;
                foreach (float a in RayAcross)
                {
                    Vector3 p = pos + Vector3.up * h + across * a;
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    bool clear = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f &&
                                 !Physics.Linecast(eye, p, losMask, QueryTriggerInteraction.Ignore) && !CanopyBlocks(eye, p);
                    total++;
                    if (clear) seen++;
                    if (!inFace) continue;
                    faceTotal++;
                    if (clear) faceSeen++;
                }
            }
            e.SightVisible = seen / (float)total;
            e.SightFace = faceTotal > 0 ? faceSeen / (float)faceTotal : e.SightVisible;
        }

        // ---------------------------------------------------------------- canopy

        private static readonly RaycastHit[] treeHits = new RaycastHit[128];
        private static readonly HashSet<TreeBase> lineTrees = new HashSet<TreeBase>();
        private static readonly Dictionary<TreeBase, Bounds> canopies = new Dictionary<TreeBase, Bounds>();

        /// <summary>
        /// Leaves have no colliders, so rays pass through a tree's crown. This treats each tree near the line as
        /// a box of leaves (its looks' bounds, narrowed and lifted off the ground) and says whether one stands
        /// between the two points. A crown either point is inside doesn't count (you can look out from under a tree).
        /// </summary>
        public static bool CanopyBlocks(Vector3 from, Vector3 to)
        {
            Masks();
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1f) return false;
            var ray = new Ray(from, d / len);
            int n = Physics.SphereCastNonAlloc(ray, 5f, treeHits, len, treeMask, QueryTriggerInteraction.Ignore);
            lineTrees.Clear();
            for (int i = 0; i < n; i++)
            {
                TreeBase tree = treeHits[i].collider ? treeHits[i].collider.GetComponentInParent<TreeBase>() : null;
                if (!tree || !lineTrees.Add(tree) || !Canopy(tree, out Bounds b)) continue;
                if (b.Contains(from) || b.Contains(to)) continue;
                if (b.IntersectRay(ray, out float at) && at < len) return true;
            }
            return false;
        }

        private static bool Canopy(TreeBase tree, out Bounds crown)
        {
            if (canopies.TryGetValue(tree, out crown)) return crown.size.y > 0f;
            if (canopies.Count > 4000) canopies.Clear();
            bool any = false;
            Bounds all = default;
            foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer)) continue;
                if (!any) all = r.bounds;
                else all.Encapsulate(r.bounds);
                any = true;
            }
            crown = default;
            if (any)
            {
                // Crowns are round or conical, not boxes; leaves start some way up the trunk.
                float bottom = all.min.y + all.size.y * 0.25f;
                crown = new Bounds(new Vector3(all.center.x, (bottom + all.max.y) / 2f, all.center.z),
                    new Vector3(all.size.x * 0.6f, all.max.y - bottom, all.size.z * 0.6f));
            }
            canopies[tree] = crown;
            return any;
        }

        // ---------------------------------------------------------------- fog

        /// <summary>How far a sighting can reach through the current fog (SightFogContrast of it still showing).</summary>
        public static float FogRange()
        {
            float c = HufsaConfig.SightFogContrast.Value;
            if (!RenderSettings.fog || c <= 0f) return float.MaxValue;
            c = Mathf.Min(c, 0.99f);
            float density = RenderSettings.fogDensity;
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    return Mathf.Lerp(RenderSettings.fogEndDistance, RenderSettings.fogStartDistance, c);
                case FogMode.Exponential:
                    return density > 0f ? -Mathf.Log(c) / density : float.MaxValue;
                default:
                    return density > 0f ? Mathf.Sqrt(-Mathf.Log(c)) / density : float.MaxValue;
            }
        }
    }
}
