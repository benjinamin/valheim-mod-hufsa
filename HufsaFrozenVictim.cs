using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Whoever Hufsa catches is left behind frozen solid: a statue of them, cowering, glazed with ice, with icy
    /// spikes jutting out of their back. Built on every client from that player's own looks (body, hair, beard,
    /// gear) in the instant they die, so it needs nothing saved or sent beyond "this one froze". It stands for
    /// FrozenVictimSeconds and then is gone; players who weren't nearby (or join later) don't see it.
    /// </summary>
    internal class HufsaFrozenVictim : MonoBehaviour
    {
        public const string RpcFrozen = "Hufsa_Frozen";

        private static readonly Dictionary<ZDOID, float> pending = new Dictionary<ZDOID, float>();
        private static float localFrozenAt = -1f;
        private static Mesh spikeMesh;
        private static Material iceMaterial;

        private float dieAt;

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<ZDOID>(RpcFrozen, (sender, id) => pending[id] = Time.time);
        }

        /// <summary>The local player is about to be killed by Hufsa: everyone gets ready to freeze them.</summary>
        internal static void MarkLocalDeath(Player player)
        {
            if (HufsaConfig.FrozenVictimSeconds.Value <= 0f || !player || ZRoutedRpc.instance == null) return;
            localFrozenAt = Time.time;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcFrozen, player.GetZDOID());
        }

        /// <summary>The dying player's own ragdoll is left out: the statue stands there instead.</summary>
        internal static bool SkipRagdoll(Player player)
        {
            if (player != Player.m_localPlayer || localFrozenAt < 0f) return false;
            bool frozen = Time.time - localFrozenAt < 2f; // (god mode: no death came of it)
            localFrozenAt = -1f;
            return frozen;
        }

        /// <summary>Every client, as a player dies (before their body is hidden): freeze them if Hufsa did it.</summary>
        internal static void OnPlayerDeath(Player player)
        {
            if (!player) return;
            ZDOID id = player.GetZDOID();
            if (!pending.TryGetValue(id, out float at)) return;
            pending.Remove(id);
            if (Time.time - at > 10f) return;
            try
            {
                Build(player, id);
            }
            catch (System.Exception ex)
            {
                HufsaPlugin.Log.LogWarning($"Couldn't freeze the victim: {ex}");
            }
        }

        /// <summary>Console "hufsa frozen": a local statue of this player 2.5 m in front of them, without dying.</summary>
        internal static bool Preview(Player player)
        {
            if (!player) return false;
            try
            {
                GameObject statue = Build(player, player.GetZDOID());
                if (!statue) return false;
                Vector3 fwd = player.transform.forward;
                fwd.y = 0f;
                statue.transform.position += fwd.normalized * 2.5f;
                statue.transform.rotation = Quaternion.LookRotation(-fwd.normalized); // facing you
                return true;
            }
            catch (System.Exception ex)
            {
                HufsaPlugin.Log.LogWarning($"Couldn't make the statue: {ex}");
                return false;
            }
        }

        private void Update()
        {
            if (Time.time >= dieAt) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // The statue's own material copies (the shared ice and the spike mesh stay).
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m && m != iceMaterial) Destroy(m);
        }

        // ---------------------------------------------------------------- the statue

        private static GameObject Build(Player player, ZDOID id)
        {
            GameObject visual = player.m_visual;
            if (!visual) return null;

            // Copied under an inactive holder, so none of the copied scripts wake up; they are stripped first.
            var holder = new GameObject("HufsaFrozenVictim");
            holder.SetActive(false);
            holder.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            GameObject statue = Instantiate(visual, holder.transform, false);
            statue.transform.localPosition = visual.transform.localPosition;
            statue.transform.localRotation = visual.transform.localRotation;
            statue.transform.localScale = visual.transform.localScale;
            StripToLooks(statue);
            CopyPropertyBlocks(visual, statue); // hair colour lives in a property block, which isn't copied

            // The cower emote, about a second in.
            Animator animator = statue.GetComponent<Animator>();
            holder.SetActive(true);
            if (animator)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);
                SetParam(animator, "onGround", true);
                SetParam(animator, "inWater", false);
                SetParam(animator, "forward_speed", 0f);
                SetParam(animator, "sideway_speed", 0f);
                animator.SetTrigger("emote_cower");
                float total = Mathf.Max(0f, HufsaConfig.FrozenPoseSeconds.Value);
                for (float t = 0f; t < total; t += 0.05f) animator.Update(Mathf.Min(0.05f, total - t));
                Destroy(animator); // the bones stay where the pose left them
            }

            Glaze(statue);
            AddSpikes(holder.transform, statue.transform, id);
            holder.AddComponent<HufsaFrozenVictim>().dieAt = Time.time + HufsaConfig.FrozenVictimSeconds.Value;
            HufsaPlugin.Log.LogInfo($"{player.GetPlayerName()} was frozen by Hufsa");
            return holder;
        }

        private static void SetParam(Animator a, string name, object value)
        {
            foreach (AnimatorControllerParameter p in a.parameters)
            {
                if (p.name != name) continue;
                if (value is bool b && p.type == AnimatorControllerParameterType.Bool) a.SetBool(name, b);
                else if (value is float f && p.type == AnimatorControllerParameterType.Float) a.SetFloat(name, f);
                return;
            }
        }

        /// <summary>Only transforms, the animator (for the pose) and what draws the body and gear are kept.</summary>
        private static void StripToLooks(GameObject root)
        {
            // Dependants before what they depend on.
            DestroyAll<MonoBehaviour>(root, null);
            DestroyAll<Joint>(root, null);
            DestroyAll<Cloth>(root, null);
            DestroyAll<ParticleSystem>(root, null);
            DestroyAll<ParticleSystemRenderer>(root, null);
            DestroyAll<TrailRenderer>(root, null);
            DestroyAll<LineRenderer>(root, null);
            DestroyAll<Collider>(root, null);
            DestroyAll<Rigidbody>(root, null);
            DestroyAll<Light>(root, null);
            DestroyAll<AudioSource>(root, null);
            DestroyAll<Component>(root, c => !(c is Transform || c is Animator || c is MeshRenderer ||
                                                 c is SkinnedMeshRenderer || c is MeshFilter || c is LODGroup));
        }

        private static void DestroyAll<T>(GameObject root, System.Func<Component, bool> which) where T : Component
        {
            foreach (T c in root.GetComponentsInChildren<T>(true))
            {
                if (!c || (which != null && !which(c))) continue;
                try { DestroyImmediate(c); }
                catch { /* something else still needs it; harmless */ }
            }
        }

        private static void CopyPropertyBlocks(GameObject from, GameObject to)
        {
            Renderer[] a = from.GetComponentsInChildren<Renderer>(true);
            Renderer[] b = to.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();
            var byPath = new Dictionary<string, Renderer>();
            foreach (Renderer r in a) byPath[PathOf(r.transform, from.transform)] = r;
            foreach (Renderer r in b)
            {
                if (!byPath.TryGetValue(PathOf(r.transform, to.transform), out Renderer src) || !src.HasPropertyBlock()) continue;
                src.GetPropertyBlock(block);
                r.SetPropertyBlock(block);
            }
        }

        private static string PathOf(Transform t, Transform root)
        {
            string path = t.name;
            for (Transform p = t.parent; p && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static readonly Color Ice = new Color(0.72f, 0.86f, 1f);

        /// <summary>Everything turns pale, blue-white and glossy, like it froze solid.</summary>
        private static void Glaze(GameObject statue)
        {
            float k = Mathf.Clamp01(HufsaConfig.FrozenTint.Value);
            foreach (Renderer r in statue.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]);
                    TintColor(m, "_Color", k);
                    TintColor(m, "_SkinColor", k);
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", Mathf.Max(m.GetFloat("_Glossiness"), 0.75f));
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), 0.1f));
                    if (m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", new Color(0.05f, 0.09f, 0.14f) * k);
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                if (r.HasPropertyBlock())
                {
                    // Hair: its own colour sits in the property block.
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    Vector4 c = block.GetVector("_SkinColor");
                    if (c != Vector4.zero) block.SetColor("_SkinColor", Color.Lerp((Color)c, Ice, k));
                    r.SetPropertyBlock(block);
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static void TintColor(Material m, string prop, float k)
        {
            if (!m.HasProperty(prop)) return;
            Color c = m.GetColor(prop);
            Color t = Color.Lerp(c, Ice * Mathf.Max(0.55f, c.grayscale + 0.35f), k);
            t.a = c.a;
            m.SetColor(prop, t);
        }

        // ---------------------------------------------------------------- ice spikes

        private static readonly string[] SpikeBones =
        {
            "Spine2", "Spine2", "Spine2", "Spine1", "Spine1", "Spine", "Neck", "Head", "Head",
            "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "Hips",
        };

        /// <summary>
        /// Icy spikes out of the back, shoulders and head, all pointing backwards (away from where they faced),
        /// fanning a little up and out. Seeded by the player, so everyone sees the same ones.
        /// </summary>
        private static void AddSpikes(Transform holder, Transform statue, ZDOID id)
        {
            if (!spikeMesh) spikeMesh = BuildSpikeMesh();
            if (!iceMaterial) iceMaterial = BuildIceMaterial(statue);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in statue.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            var rng = new System.Random(id.GetHashCode());
            float Rand(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            Vector3 back = -holder.forward, up = holder.up, right = holder.right;
            Vector3 chest = holder.position + up * 1.2f;
            int count = Mathf.Max(0, HufsaConfig.FrozenSpikes.Value);
            for (int i = 0; i < count; i++)
            {
                string boneName = SpikeBones[i % SpikeBones.Length];
                Vector3 at = bones.TryGetValue(boneName, out Transform bone) ? bone.position : chest + up * Rand(-0.4f, 0.5f);
                float side = Rand(-1f, 1f);
                if (boneName.StartsWith("Left")) side = -Mathf.Abs(side);
                if (boneName.StartsWith("Right")) side = Mathf.Abs(side);
                // Start inside the body, a little behind the bone, so the base is hidden.
                at += back * 0.08f + right * side * 0.12f;
                Vector3 dir = (back + up * Rand(0.05f, 0.7f) + right * side * 0.55f).normalized;
                float length = Rand(0.35f, 0.95f) * (boneName == "Head" ? 0.7f : 1f);
                float width = Rand(0.05f, 0.1f);

                var spike = new GameObject("IceSpike");
                spike.transform.SetParent(holder, false);
                spike.transform.position = at;
                spike.transform.rotation = Quaternion.LookRotation(dir, up) * Quaternion.Euler(0f, 0f, Rand(0f, 360f));
                spike.transform.localScale = new Vector3(width, width, length);
                spike.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
                spike.AddComponent<MeshRenderer>().sharedMaterial = iceMaterial;
            }
        }

        /// <summary>A six-sided shard along +Z: a short flared base and a long point, length 1, width 1.</summary>
        private static Mesh BuildSpikeMesh()
        {
            const int sides = 6;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.5f;
                Vector3 b1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.5f;
                Vector3 m0 = new Vector3(b0.x * 0.8f, b0.y * 0.8f, 0.18f);
                Vector3 m1 = new Vector3(b1.x * 0.8f, b1.y * 0.8f, 0.18f);
                Vector3 tip = new Vector3(0f, 0f, 1f);
                // flat-shaded faces: own vertices per triangle
                AddTri(verts, tris, b0, m0, b1);
                AddTri(verts, tris, b1, m0, m1);
                AddTri(verts, tris, m0, tip, m1);
                AddTri(verts, tris, Vector3.zero, b0, b1); // the base
            }
            var mesh = new Mesh { name = "HufsaIceSpike" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            t.Add(v.Count); v.Add(a);
            t.Add(v.Count); v.Add(b);
            t.Add(v.Count); v.Add(c);
        }

        /// <summary>Pale, glossy, faintly glowing ice, on the same lit shader the statue's body uses.</summary>
        private static Material BuildIceMaterial(Transform statue)
        {
            Shader shader = Shader.Find("Custom/Creature");
            if (!shader)
            {
                Renderer any = statue.GetComponentInChildren<Renderer>(true);
                shader = any && any.sharedMaterial ? any.sharedMaterial.shader : Shader.Find("Standard");
            }
            var m = new Material(shader) { name = "HufsaIce" };
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.78f, 0.9f, 1f));
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.9f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.05f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.12f, 0.2f, 0.3f));
            }
            return m;
        }
    }
}
