using System;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hufsa
{
    internal static class HufsaPrefabs
    {
        public const string HunterName = "Hufsa_Hunter";
        public const string WatcherName = "Hufsa_Watcher";
        public const string VisualName = "HufsaVisual";
        public const string BodyName = "Body";
        public const string NoseName = "Nose";
        public const string PupilName = "Pupil";
        public const string ToothName = "Tooth";

        private static Mesh bodyMesh;
        private static Mesh noseMesh;
        private static Material bodyMaterial;
        private static Material fingerMaterial;
        private static Material whiteMaterial;
        private static Material pupilMaterial;
        private static Material particleMaterial;
        private static Material glowMaterial;

        /// <summary>Unlit, transparent shader used for the eyes, teeth, frost patches and footprints.</summary>
        internal static Shader SpriteShader { get; private set; }

        public static void Register()
        {
            var chill = ScriptableObject.CreateInstance<SE_HufsaChill>();
            chill.name = SE_HufsaChill.SEName;
            chill.m_name = "Hufsa's chill";
            chill.m_tooltip = "Something cold is near. Get away from it.";
            chill.m_ttl = 1.5f;
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(chill, false));

            CreatureManager.OnVanillaCreaturesAvailable += CreateAll;
        }

        private static void CreateAll()
        {
            try
            {
                CreateMaterials();
                CreateHunter();
                CreateWatcher();
            }
            catch (Exception e)
            {
                HufsaPlugin.Log.LogError($"Failed to create Hufsa prefabs: {e}");
            }
            finally
            {
                CreatureManager.OnVanillaCreaturesAvailable -= CreateAll;
            }
        }

        private static void CreateHunter()
        {
            GameObject prefab = PrefabManager.Instance.CreateClonedPrefab(HunterName, "Blob");
            prefab.transform.localScale = Vector3.one;

            // Keep the Blob's skeleton (Character, MonsterAI for pathfinding, ZSyncAnimation) but hide its looks.
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var em = ps.emission;
                em.enabled = false;
            }
            DestroyIfPresent<CharacterDrop>(prefab);
            DestroyIfPresent<Tameable>(prefab);
            DestroyIfPresent<Procreation>(prefab);
            DestroyIfPresent<Growup>(prefab);
            DestroyIfPresent<LevelEffects>(prefab);

            var humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid)
            {
                humanoid.m_defaultItems = Array.Empty<GameObject>();
                humanoid.m_randomWeapon = Array.Empty<GameObject>();
                humanoid.m_randomArmor = Array.Empty<GameObject>();
                humanoid.m_randomShield = Array.Empty<GameObject>();
                humanoid.m_randomSets = Array.Empty<Humanoid.ItemSet>();
                humanoid.m_randomItems = Array.Empty<Humanoid.RandomItem>();
            }

            Character ch = prefab.GetComponent<Character>();
            ch.m_name = "Hufsa";
            ch.m_faction = Character.Faction.Boss;
            ch.m_health = 100000f;
            ch.m_turnSpeed = 60f;
            ch.m_acceleration = 0.6f;
            ch.m_staggerDamageFactor = 0f;
            ch.m_tolerateWater = true;
            ch.m_damageModifiers = new HitData.DamageModifiers
            {
                m_blunt = HitData.DamageModifier.Immune,
                m_slash = HitData.DamageModifier.Immune,
                m_pierce = HitData.DamageModifier.Immune,
                m_chop = HitData.DamageModifier.Immune,
                m_pickaxe = HitData.DamageModifier.Immune,
                m_fire = HitData.DamageModifier.Immune,
                m_frost = HitData.DamageModifier.Immune,
                m_lightning = HitData.DamageModifier.Immune,
                m_poison = HitData.DamageModifier.Immune,
                m_spirit = HitData.DamageModifier.Immune,
            };

            float tall = HufsaModel.Top * HufsaModel.Height;
            if (ch.m_eye) ch.m_eye.localPosition = new Vector3(0f, HufsaModel.EyeHeight * HufsaModel.Height, 0.5f);

            var capsule = prefab.GetComponent<CapsuleCollider>();
            if (capsule)
            {
                capsule.radius = 1.1f;
                capsule.height = tall;
                capsule.center = new Vector3(0f, tall / 2f, 0f);
            }

            var ai = prefab.GetComponent<BaseAI>();
            if (ai)
            {
                ai.m_viewRange = 150f;
                ai.m_hearRange = 150f;
                ai.m_avoidWater = false;
                ai.m_avoidFire = false;
                ai.m_afraidOfFire = false;
            }

            // Non-persistent: if every player leaves its area the ZDO is dropped, so a Hunter never lingers in the save.
            prefab.GetComponent<ZNetView>().m_persistent = false;

            BuildVisual(prefab.transform);
            prefab.AddComponent<HufsaEntity>().IsHunter = true;
            prefab.AddComponent<HufsaArms>();
            prefab.AddComponent<HunterBehaviour>();
            prefab.AddComponent<HufsaTrail>();
            prefab.AddComponent<HufsaSounds>();

            CreatureManager.Instance.AddCreature(new CustomCreature(prefab, false));
        }

        private static void CreateWatcher()
        {
            GameObject prefab = PrefabManager.Instance.CreateEmptyPrefab(WatcherName, true);
            DestroyIfPresent<MeshRenderer>(prefab);
            DestroyIfPresent<MeshFilter>(prefab);
            foreach (Collider c in prefab.GetComponents<Collider>()) Object.DestroyImmediate(c);

            var watcherView = prefab.GetComponent<ZNetView>();
            watcherView.m_persistent = false;
            watcherView.m_distant = true; // stays loaded far out, so it can stand 100+ m away on open land
            var sync = prefab.AddComponent<ZSyncTransform>();
            sync.m_syncPosition = true;
            sync.m_syncRotation = true;

            BuildVisual(prefab.transform, watcher: true);
            prefab.AddComponent<HufsaEntity>();
            prefab.AddComponent<HufsaArms>();
            prefab.AddComponent<WatcherBehaviour>();
            prefab.AddComponent<WatcherSounds>(); // only breathing, while it is behind you

            PrefabManager.Instance.AddPrefab(prefab);
        }

        private static void DestroyIfPresent<T>(GameObject go) where T : Component
        {
            foreach (T c in go.GetComponentsInChildren<T>(true)) Object.DestroyImmediate(c);
        }

        // ---------------------------------------------------------------- visual (from HufsaModel)

        /// <param name="watcher">
        /// Both get the silhouette glow; only the Hunter has the frost mist around its hem.
        /// </param>
        private static void BuildVisual(Transform parent, bool watcher = false)
        {
            var visual = new GameObject(VisualName).transform;
            visual.SetParent(parent, false);
            float hs = HufsaModel.Height;

            var body = new GameObject(BodyName);
            body.transform.SetParent(visual, false);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

            // Eyes: true spheres set 40% into the face, with a round pupil on each.
            foreach (float side in new[] { -1f, 1f })
            {
                float yB = HufsaModel.EyeHeight, x = HufsaModel.EyeSpacing * side;
                float re = 0.065f * HufsaModel.EyeSize;
                float z = HufsaModel.SurfaceZ(yB, x) - re * 0.4f;
                Primitive(PrimitiveType.Sphere, "Eye", visual, new Vector3(x, yB * hs, z), Vector3.one * 2f * re, whiteMaterial);

                float rp = 0.022f * HufsaModel.EyeSize * HufsaModel.PupilSize;
                float dy = HufsaModel.PupilLook * Mathf.Max(re - rp, 0f) * 0.8f;
                Primitive(PrimitiveType.Sphere, PupilName, visual,
                    new Vector3(x, yB * hs + dy, z + Mathf.Sqrt(Mathf.Max(re * re - dy * dy, 0f)) - rp * 0.5f),
                    Vector3.one * 2f * rp, pupilMaterial);
            }

            // Nose: sunk into the face far enough that its flat back doesn't float off the curved surface.
            {
                float yB = HufsaModel.NoseHeight;
                float inset = HufsaModel.SurfaceZ(yB) - HufsaModel.SurfaceZ(yB, HufsaModel.NoseWidth / 2f) + 0.02f;
                var nose = new GameObject(NoseName);
                nose.transform.SetParent(visual, false);
                nose.transform.localPosition = new Vector3(0f, yB * hs, HufsaModel.SurfaceZ(yB) - inset);
                nose.transform.localScale = new Vector3(HufsaModel.NoseWidth, HufsaModel.NoseTall, HufsaModel.NoseLength + inset);
                nose.transform.localRotation = Quaternion.Euler(-HufsaModel.NoseTilt, 0f, 0f);
                nose.AddComponent<MeshFilter>().sharedMesh = noseMesh;
                nose.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            }

            // Grin: a row of teeth following the face surface.
            for (int i = 0; i < HufsaModel.Teeth; i++)
            {
                float max = HufsaModel.GrinWidth;
                float a = Mathf.Lerp(-max, max, i / (float)(HufsaModel.Teeth - 1));
                float n = a / max;
                float tx = Mathf.Sin(a * Mathf.Deg2Rad) * HufsaModel.RadiusAt(HufsaModel.GrinHeight);
                var pos = new Vector3(tx, HufsaModel.GrinHeight * hs + HufsaModel.GrinCurve * n * n,
                    HufsaModel.SurfaceZ(HufsaModel.GrinHeight, tx) + 0.004f);
                Transform tooth = Primitive(PrimitiveType.Cube, ToothName, visual, pos,
                    new Vector3(0.035f, 0.05f, 0.022f) * HufsaModel.ToothSize, whiteMaterial);
                tooth.localRotation = Quaternion.Euler(0f, a, 0f);
            }

            AddArms(visual);
            AddSnow(visual);

            var glow = new GameObject("SilhouetteGlow");
            glow.transform.SetParent(visual, false);
            glow.AddComponent<MeshFilter>().sharedMesh = HufsaModel.BuildGlowShellMesh();
            var r = glow.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            glow.AddComponent<SilhouetteGlow>();

            if (!watcher) AddMist(visual);
        }

        public const string ArmPivotName = "ArmPivot";   // + 0 (left) / 1 (right)
        public const string ArmAxisName = "Axis", ArmTwistName = "Twist", ArmReachName = "Reach";
        public const string ArmName = "Arm", ArmGlowName = "ArmGlow", FingerName = "Finger";

        /// <summary>
        /// Both arms (see HufsaModel.BuildArms): ArmPivot (shoulder, swings) / Axis (along the arm) / Twist (about
        /// the arm) / Reach (slides along it) / the arm, its glow shell and the fingers. HufsaArms moves them.
        /// </summary>
        private static void AddArms(Transform visual)
        {
            if (HufsaModel.Arms == null) return;
            for (int n = 0; n < HufsaModel.Arms.Length; n++)
            {
                HufsaModel.ArmRig rig = HufsaModel.Arms[n];
                Transform pivot = Child(ArmPivotName + n, visual);
                pivot.localPosition = rig.Shoulder;
                Transform axis = Child(ArmAxisName, pivot);
                axis.localRotation = rig.Axis;
                Transform reach = Child(ArmReachName, Child(ArmTwistName, axis));

                Transform arm = Child(ArmName, reach);
                arm.gameObject.AddComponent<MeshFilter>().sharedMesh = rig.Mesh;
                arm.gameObject.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

                Transform glow = Child(ArmGlowName, reach);
                glow.gameObject.AddComponent<MeshFilter>().sharedMesh = HufsaModel.BuildShellMesh(rig.Mesh);
                var gr = glow.gameObject.AddComponent<MeshRenderer>();
                gr.sharedMaterial = glowMaterial;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                gr.receiveShadows = false;
                var sg = glow.gameObject.AddComponent<SilhouetteGlow>();
                sg.FollowsBody = false;
                sg.VertexFade = rig.GlowFade;

                for (int i = 0; i < rig.Fingers.Length; i++)
                {
                    Transform f = Child(FingerName + i, reach);
                    f.gameObject.AddComponent<MeshFilter>().sharedMesh = HufsaModel.FingerMesh;
                    f.gameObject.AddComponent<MeshRenderer>().sharedMaterial = fingerMaterial;
                }
            }
        }

        private static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private static void AddSnow(Transform parent)
        {
            var go = new GameObject("FrostSnow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = 5f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.gravityModifier = 0.03f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 7f;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
        }

        private static void AddMist(Transform parent)
        {
            var go = new GameObject("FrostMist");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 7f;
            main.startLifetime = 7f;
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.12f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3.5f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
        }

        // ---------------------------------------------------------------- materials

        private static void CreateMaterials()
        {
            bodyMesh = HufsaModel.BuildBodyMesh();
            noseMesh = HufsaModel.BuildNoseMesh();
            HufsaModel.BuildArms();

            // An opaque creature shader (the Blob's is translucent goo); fall back to the Blob's if none is found.
            Shader bodyShader = Shader.Find("Custom/Creature");
            foreach (string name in new[] { "Wolf", "Greydwarf", "Troll", "Blob" })
            {
                if (bodyShader) break;
                GameObject source = PrefabManager.Instance.GetPrefab(name);
                Renderer r = source ? source.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                if (r && r.sharedMaterial) bodyShader = r.sharedMaterial.shader;
            }
            if (!bodyShader) bodyShader = Shader.Find("Standard");

            bodyMaterial = new Material(bodyShader) { name = "HufsaBody" };
            SetIfPresent(bodyMaterial, "_MainTex", HufsaModel.CloakTexture());
            SetIfPresent(bodyMaterial, "_BumpMap", null);
            SetIfPresent(bodyMaterial, "_Color",
                bodyMaterial.HasProperty("_MainTex") ? Color.white : new Color(0.04f, 0.017f, 0.055f));
            SetIfPresent(bodyMaterial, "_EmissionColor", Color.black);
            SetIfPresent(bodyMaterial, "_Glossiness", 0.1f);
            SetIfPresent(bodyMaterial, "_Metallic", 0f);

            // Fingers: the body's cloak darkened to the nose colour.
            fingerMaterial = new Material(bodyMaterial) { name = "HufsaFingers" };
            SetIfPresent(fingerMaterial, "_Color", bodyMaterial.HasProperty("_MainTex")
                ? new Color(HufsaModel.NoseShade, HufsaModel.NoseShade, HufsaModel.NoseShade)
                : new Color(0.04f, 0.017f, 0.055f) * HufsaModel.NoseShade);

            SpriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            whiteMaterial = new Material(SpriteShader) { name = "HufsaWhite", color = new Color(0.92f, 0.94f, 1f) };
            pupilMaterial = new Material(SpriteShader) { name = "HufsaPupil", color = new Color(0.02f, 0.02f, 0.03f) };
            particleMaterial = new Material(SpriteShader) { name = "HufsaFrostParticle", mainTexture = SoftDot(64) };
            glowMaterial = new Material(SpriteShader) { name = "HufsaSilhouetteGlow", color = Color.white };

            HufsaPlugin.Log.LogInfo($"Hufsa shaders: body={bodyShader?.name}, unlit={SpriteShader?.name}");
        }

        private static void SetIfPresent(Material m, string prop, object value)
        {
            if (!m.HasProperty(prop)) return;
            switch (value)
            {
                case Color c: m.SetColor(prop, c); break;
                case float f: m.SetFloat(prop, f); break;
                default: m.SetTexture(prop, value as Texture); break;
            }
        }

        /// <summary>Local, unsynced burst of smoke and snow that hides Hufsa as it collapses.</summary>
        public static void SpawnVanishPuff(Vector3 position, float scale)
        {
            if (!particleMaterial) return;
            var go = new GameObject("HufsaVanishPuff");
            go.transform.position = position + Vector3.up * 1.2f * scale;

            Burst(go, "Smoke", 45, new Vector2(1.2f, 2.6f), new Vector2(1.2f, 3.2f), new Vector2(1.2f, 2f),
                new Color(0.75f, 0.78f, 0.85f, 0.55f), 0f, 1.2f * scale, 2.2f);
            Burst(go, "Snow", 120, new Vector2(0.05f, 0.14f), new Vector2(2f, 6f), new Vector2(1.2f, 2.4f),
                new Color(1f, 1f, 1f, 0.9f), 0.35f, 0.9f * scale, 1f);

            Object.Destroy(go, 3f);
        }

        private static void Burst(GameObject parent, string name, int count, Vector2 size, Vector2 speed,
            Vector2 lifetime, Color color, float gravity, float radius, float growTo)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growTo));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            ps.Play();
        }

        private static Texture2D SoftDot(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            return tex;
        }
    }
}
