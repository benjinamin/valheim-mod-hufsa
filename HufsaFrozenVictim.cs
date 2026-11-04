using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Whoever Hufsa catches is left behind frozen solid: a statue of them, cowering, glazed with ice, with icy
    /// spikes jutting out of their back. It is a saved world object, so it stands there for good, for everyone,
    /// until someone breaks it, and then it falls apart into ice.
    /// The dying player copies their looks (body, hair, beard, gear) into the statue; every client rebuilds the
    /// statue from those with the game's own player model.
    /// </summary>
    internal class HufsaFrozenVictim : MonoBehaviour, Hoverable
    {
        public const string PrefabName = "Hufsa_FrozenVictim";
        public const string IceName = "HufsaIce";

        private const string ReadyKey = "hufsa_frozen_ready";
        private const string NameKey = "hufsa_frozen_name";
        private const string SeedKey = "hufsa_frozen_seed";

        private static readonly int[] LookInts =
        {
            ZDOVars.s_modelIndex, ZDOVars.s_hairItem, ZDOVars.s_beardItem,
            ZDOVars.s_helmetItem, ZDOVars.s_chestItem, ZDOVars.s_legItem, ZDOVars.s_shoulderItem,
            ZDOVars.s_shoulderItemVariant, ZDOVars.s_shoulderItemQuality, ZDOVars.s_utilityItem, ZDOVars.s_trinketItem,
            ZDOVars.s_rightItem, ZDOVars.s_rightItemQuality, ZDOVars.s_leftItem, ZDOVars.s_leftItemVariant,
            ZDOVars.s_leftItemQuality, ZDOVars.s_rightBackItem, ZDOVars.s_rightBackItemQuality, ZDOVars.s_leftBackItem,
            ZDOVars.s_leftBackItemVariant, ZDOVars.s_leftBackItemQuality,
        };

        private static float localFrozenAt = -1f;
        private static Mesh spikeMesh;
        private static Material iceMaterial;

        private ZNetView nview;
        private bool built;

        // ---------------------------------------------------------------- prefabs

        /// <summary>The statue (saved, breakable, drops ice) and the ice it drops.</summary>
        internal static void CreatePrefabs()
        {
            GameObject ice = CreateIce();

            GameObject prefab = PrefabManager.Instance.CreateEmptyPrefab(PrefabName, true);
            foreach (MeshRenderer r in prefab.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
            foreach (MeshFilter f in prefab.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
            foreach (Collider c in prefab.GetComponents<Collider>()) Object.DestroyImmediate(c);
            prefab.layer = LayerMask.NameToLayer("static_solid");

            var view = prefab.GetComponent<ZNetView>();
            view.m_persistent = true; // it stays in the world save

            var capsule = prefab.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.5f;

            var destructible = prefab.AddComponent<Destructible>();
            destructible.m_health = Mathf.Max(1f, HufsaConfig.FrozenVictimHealth.Value);
            destructible.m_minToolTier = 0;
            destructible.m_damages = new HitData.DamageModifiers { m_fire = HitData.DamageModifier.Weak };
            // Sounds and shards of breaking ice: borrowed from the Mountain's ice blocks.
            Destructible like = FindIceDestructible();
            if (like)
            {
                destructible.m_hitEffect = like.m_hitEffect;
                destructible.m_destroyedEffect = like.m_destroyedEffect;
                destructible.m_hitNoise = like.m_hitNoise;
                destructible.m_destroyNoise = like.m_destroyNoise;
            }
            else HufsaPlugin.Log.LogWarning("No ice block found to borrow breaking sounds from; the statue breaks silently.");

            if (ice)
            {
                var drop = prefab.AddComponent<DropOnDestroyed>();
                drop.m_dropWhenDestroyed = new DropTable
                {
                    m_dropMin = 1,
                    m_dropMax = 1,
                    m_drops = new List<DropTable.DropData>
                    {
                        new DropTable.DropData { m_item = ice, m_stackMin = 2, m_stackMax = 5, m_weight = 1f },
                    },
                };
            }

            prefab.AddComponent<HufsaFrozenVictim>();
            PrefabManager.Instance.AddPrefab(prefab);
        }

        private static Destructible FindIceDestructible()
        {
            foreach (string name in new[] { "IceBlocker", "ice_rock1", "ice1" })
            {
                GameObject go = PrefabManager.Instance.GetPrefab(name);
                Destructible d = go ? go.GetComponentInChildren<Destructible>(true) : null;
                if (d) return d;
            }
            return null;
        }

        /// <summary>Plain ice: what is left of a frozen statue. A crystal's shape, paler and bluer.</summary>
        private static GameObject CreateIce()
        {
            var item = new CustomItem(IceName, "Crystal");
            if (item.ItemDrop == null)
            {
                HufsaPlugin.Log.LogWarning("Couldn't make the ice item; frozen statues drop nothing.");
                return null;
            }
            ItemDrop.ItemData.SharedData shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "Ice";
            shared.m_description = "Ice from someone Hufsa caught. It's colder than it should be.";
            shared.m_weight = 0.5f;
            shared.m_value = 0;
            shared.m_maxStackSize = 50;
            shared.m_teleportable = true;
            foreach (Renderer r in item.ItemPrefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.8f, 0.92f, 1f, m.GetColor("_Color").a));
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            ItemManager.Instance.AddItem(item);
            return item.ItemPrefab;
        }

        // ---------------------------------------------------------------- freezing someone

        /// <summary>The local player is about to be killed by Hufsa: they are left standing here, frozen.</summary>
        internal static void MarkLocalDeath(Player player)
        {
            if (!HufsaConfig.FrozenVictims.Value || !player) return;
            if (Spawn(player, player.transform.position, player.transform.rotation)) localFrozenAt = Time.time;
        }

        /// <summary>The dying player's own ragdoll is left out: the statue stands there instead.</summary>
        internal static bool SkipRagdoll(Player player)
        {
            if (player != Player.m_localPlayer || localFrozenAt < 0f) return false;
            bool frozen = Time.time - localFrozenAt < 2f; // (god mode: no death came of it)
            localFrozenAt = -1f;
            return frozen;
        }

        /// <summary>Console "hufsa frozen": a statue of this player 2.5 m in front of them, facing them, without dying.</summary>
        internal static bool Preview(Player player)
        {
            if (!player) return false;
            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            return Spawn(player, player.transform.position + fwd * 2.5f, Quaternion.LookRotation(-fwd));
        }

        private static bool Spawn(Player player, Vector3 at, Quaternion rotation)
        {
            try
            {
                GameObject prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(PrefabName) : null;
                ZDO from = player.m_nview ? player.m_nview.GetZDO() : null;
                if (!prefab || from == null) return false;

                GameObject go = Instantiate(prefab, at, rotation);
                ZDO zdo = go.GetComponent<ZNetView>().GetZDO();
                foreach (int key in LookInts) zdo.Set(key, from.GetInt(key));
                zdo.Set(ZDOVars.s_skinColor, from.GetVec3(ZDOVars.s_skinColor, Vector3.one));
                zdo.Set(ZDOVars.s_hairColor, from.GetVec3(ZDOVars.s_hairColor, Vector3.one));
                // Bald stays bald (the game would otherwise give a model without hair some).
                zdo.Set(ZDOVars.s_noHair, from.GetInt(ZDOVars.s_hairItem) == 0);
                zdo.Set(ZDOVars.s_noBeard, from.GetInt(ZDOVars.s_beardItem) == 0);
                zdo.Set(NameKey, player.GetPlayerName());
                zdo.Set(SeedKey, Random.Range(int.MinValue, int.MaxValue));
                zdo.Set(ReadyKey, true);
                HufsaPlugin.Log.LogInfo($"{player.GetPlayerName()} was frozen by Hufsa");
                return true;
            }
            catch (System.Exception ex)
            {
                HufsaPlugin.Log.LogWarning($"Couldn't freeze the victim: {ex}");
                return false;
            }
        }

        // ---------------------------------------------------------------- the statue in the world

        private void Awake()
        {
            nview = GetComponent<ZNetView>();
        }

        private void Update()
        {
            if (built || !nview || !nview.IsValid() || !nview.GetZDO().GetBool(ReadyKey)) return;
            built = true;
            enabled = false;
            try
            {
                Build();
            }
            catch (System.Exception ex)
            {
                HufsaPlugin.Log.LogWarning($"Couldn't build the frozen statue: {ex}");
            }
        }

        public string GetHoverName()
        {
            string who = nview && nview.IsValid() ? nview.GetZDO().GetString(NameKey) : "";
            return string.IsNullOrEmpty(who) ? "Frozen viking" : $"Frozen {who}";
        }

        public string GetHoverText() => GetHoverName();

        public float GetHoverOffset() => 0f;

        private void OnDestroy()
        {
            // The statue's own material copies (the shared ice and the spike mesh stay).
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m && m != iceMaterial) Destroy(m);
        }

        /// <summary>
        /// The game's own player model, dressed from the saved looks by its own VisEquipment, then posed, stripped
        /// down to what draws it, iced over and given its spikes.
        /// </summary>
        private void Build()
        {
            GameObject playerPrefab = ZNetScene.instance.GetPrefab("Player");
            if (!playerPrefab) return;

            // Copied under an inactive holder, so none of the copied scripts wake up; all but the looks go first.
            var holder = new GameObject("Looks");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            GameObject body = Instantiate(playerPrefab, holder.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            StripToLooks(body, keepDressing: true);

            var dressing = body.GetComponent<VisEquipment>();
            if (dressing) dressing.m_nViewOverride = nview; // it reads the looks from the statue's own saved data
            holder.SetActive(true);
            if (dressing)
            {
                dressing.UpdateVisuals();
                dressing.UpdateVisuals(); // (the second pass settles what the helmet hides)
            }
            StripToLooks(body, keepDressing: false); // the gear it attached too

            // The cower emote, about a second in.
            Transform visual = body.transform.Find("Visual");
            Animator animator = visual ? visual.GetComponent<Animator>() : body.GetComponentInChildren<Animator>(true);
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

            Glaze(body);
            AddSpikes(holder.transform, body.transform, nview.GetZDO().GetInt(SeedKey));
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

        /// <summary>
        /// Only transforms, the animator (for the pose), what draws the body and gear, and (while dressing) the
        /// VisEquipment are kept. Some scripts need others, so it goes over them a few times.
        /// </summary>
        private static void StripToLooks(GameObject root, bool keepDressing)
        {
            bool Keep(Component c) => c is Transform || c is Animator || c is MeshRenderer || c is SkinnedMeshRenderer ||
                                      c is MeshFilter || c is LODGroup || (keepDressing && c is VisEquipment);
            for (int pass = 0; pass < 4; pass++)
            {
                bool left = false;
                // Dependants before what they depend on.
                left |= DestroyAll<MonoBehaviour>(root, Keep);
                left |= DestroyAll<Joint>(root, Keep);
                left |= DestroyAll<Cloth>(root, Keep);
                left |= DestroyAll<Component>(root, Keep);
                if (!left) break;
            }
        }

        /// <returns>Whether something couldn't go yet.</returns>
        private static bool DestroyAll<T>(GameObject root, System.Func<Component, bool> keep) where T : Component
        {
            bool left = false;
            foreach (T c in root.GetComponentsInChildren<T>(true))
            {
                if (!c || keep(c)) continue;
                try { DestroyImmediate(c); }
                catch { left = true; } // something else still needs it
            }
            return left;
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
        /// fanning a little up and out. Seeded from the statue's saved data, so everyone sees the same ones.
        /// </summary>
        private static void AddSpikes(Transform holder, Transform statue, int seed)
        {
            if (!spikeMesh) spikeMesh = BuildSpikeMesh();
            if (!iceMaterial) iceMaterial = BuildIceMaterial(statue);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in statue.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;

            var rng = new System.Random(seed);
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
