using System.Collections.Generic;
using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Hufsa's arms, per client, as in the Shape Lab. Resting they hang tucked against the body, spread apart,
    /// shortened and with the knuckles turned out. When this player comes within RaiseFar..RaiseNear (6..4 m) the arms
    /// slowly rise: the shoulders stay put and each arm swings so its end (where the fingers start) points at the
    /// player's chest, following them, but never more than AimMaxYaw to the side of where Hufsa faces.
    /// </summary>
    internal class HufsaArms : MonoBehaviour
    {
        private class Arm
        {
            public HufsaModel.ArmRig Rig;
            public Transform Pivot, Twist, Reach;
            public Transform[] Fingers;
            public Mesh Mesh;
            public Vector3[] Verts, Normals;
            public SilhouetteGlow Glow;
            public Quaternion Aim;
            public bool HasAim;
        }

        private readonly List<Arm> arms = new List<Arm>();
        private HufsaEntity entity;
        private Transform visual;
        private float raise;      // 0 resting .. 1 raised, eased slowly
        private float shownRest = -1f;

        /// <summary>The arm and finger renderers, for the distance reveal (HufsaEntity).</summary>
        public readonly List<Renderer> ArmRenderers = new List<Renderer>();
        public readonly List<Renderer> FingerRenderers = new List<Renderer>();

        private void Awake()
        {
            entity = GetComponent<HufsaEntity>();
            visual = transform.Find(HufsaPrefabs.VisualName);
            if (!visual || HufsaModel.Arms == null) return;
            for (int n = 0; n < HufsaModel.Arms.Length; n++)
            {
                Transform pivot = visual.Find(HufsaPrefabs.ArmPivotName + n);
                if (!pivot) continue;
                Transform twist = pivot.Find(HufsaPrefabs.ArmAxisName + "/" + HufsaPrefabs.ArmTwistName);
                Transform reach = twist ? twist.Find(HufsaPrefabs.ArmReachName) : null;
                Transform armT = reach ? reach.Find(HufsaPrefabs.ArmName) : null;
                if (!armT) continue;
                var a = new Arm
                {
                    Rig = HufsaModel.Arms[n], Pivot = pivot, Twist = twist, Reach = reach,
                    Mesh = armT.GetComponent<MeshFilter>().mesh, // own copy: it bends as it rises
                    Glow = reach.Find(HufsaPrefabs.ArmGlowName)?.GetComponent<SilhouetteGlow>(),
                };
                a.Mesh.MarkDynamic();
                a.Verts = new Vector3[a.Rig.RaisedVertices.Length];
                a.Normals = new Vector3[a.Rig.RaisedVertices.Length];
                a.Fingers = new Transform[a.Rig.Fingers.Length];
                for (int i = 0; i < a.Fingers.Length; i++)
                {
                    a.Fingers[i] = reach.Find(HufsaPrefabs.FingerName + i);
                    if (a.Fingers[i]) FingerRenderers.Add(a.Fingers[i].GetComponent<Renderer>());
                }
                ArmRenderers.Add(armT.GetComponent<Renderer>());
                arms.Add(a);
            }
        }

        private void OnDestroy()
        {
            foreach (Arm a in arms) if (a.Mesh) Destroy(a.Mesh);
        }

        /// <summary>Scales the arms' outline glow (0 while they are hidden, rising as they fade in).</summary>
        public void SetGlowVisibility(float v)
        {
            foreach (Arm a in arms) if (a.Glow) a.Glow.Visibility = v;
        }

        private void LateUpdate()
        {
            if (arms.Count == 0) return;
            float dt = Time.deltaTime;
            Player me = Player.m_localPlayer;
            bool target = me && !me.IsDead() && !(entity && entity.Vanishing);

            float want = 0f;
            if (target)
            {
                Vector3 d = me.transform.position - transform.position;
                d.y = 0f;
                want = 1f - HufsaModel.Smooth(HufsaModel.RaiseNear, HufsaModel.RaiseFar, d.magnitude);
            }
            raise = Mathf.MoveTowards(raise, want, dt * HufsaModel.RaiseSpeed); // slowly
            float e = raise * raise * (3f - 2f * raise);
            float rest = 1f - e; // the resting tweaks fade out as the arms rise

            Vector3 aimAt = target ? visual.InverseTransformPoint(me.transform.position + Vector3.up * HufsaModel.AimHeight) : Vector3.zero;
            float follow = 1f - Mathf.Exp(-dt * 3f); // the aim eases after the player instead of snapping
            bool reshape = Mathf.Abs(rest - shownRest) > 0.001f;

            foreach (Arm a in arms)
            {
                HufsaModel.ArmRig rig = a.Rig;
                Quaternion restQ = Quaternion.AngleAxis(HufsaModel.RestTuck, Vector3.right) *
                                   Quaternion.AngleAxis(rig.Side * HufsaModel.RestSpread, Vector3.forward);
                if (target)
                {
                    Vector3 dir = (aimAt - rig.Shoulder).normalized;
                    // no further than AimMaxYaw to the side: the body turns, the arms don't chase
                    float yaw = Mathf.Atan2(dir.x, dir.z), flat = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                    float max = HufsaModel.AimMaxYaw * Mathf.Deg2Rad;
                    if (Mathf.Abs(yaw) > max)
                    {
                        float cy = Mathf.Sign(yaw) * max;
                        dir = new Vector3(Mathf.Sin(cy) * flat, dir.y, Mathf.Cos(cy) * flat);
                    }
                    Quaternion aim = Quaternion.FromToRotation(rig.End, dir);
                    a.Aim = a.HasAim ? Quaternion.Slerp(a.Aim, aim, follow) : aim;
                    a.HasAim = true;
                }
                a.Pivot.localRotation = a.HasAim ? Quaternion.Slerp(restQ, a.Aim, e) : restQ;
                a.Twist.localRotation = Quaternion.AngleAxis(rig.Side * e * HufsaModel.RaiseTwist, Vector3.up);
                a.Reach.localPosition = new Vector3(0f, (-e * HufsaModel.RaiseExtend + rest * HufsaModel.RestShorten) * rig.Len, 0f);

                if (reshape)
                {
                    // resting, the arm bends further in toward the middle
                    for (int k = 0; k < a.Verts.Length; k++)
                    {
                        a.Verts[k] = Vector3.LerpUnclamped(rig.RaisedVertices[k], rig.RestVertices[k], rest);
                        a.Normals[k] = Vector3.LerpUnclamped(rig.RaisedNormals[k], rig.RestNormals[k], rest).normalized;
                    }
                    a.Mesh.vertices = a.Verts;
                    a.Mesh.normals = a.Normals;
                    a.Mesh.RecalculateBounds();
                    if (a.Glow) a.Glow.SetShape(a.Verts, a.Normals);

                    // resting, the hand alone turns about the arm's end (knuckles out), without twisting the arm
                    float ht = rig.Side * rest * HufsaModel.RestTwist;
                    float cos = Mathf.Cos(ht * Mathf.Deg2Rad), sin = Mathf.Sin(ht * Mathf.Deg2Rad);
                    for (int i = 0; i < a.Fingers.Length; i++)
                    {
                        if (!a.Fingers[i]) continue;
                        HufsaModel.FingerPlace f = rig.Fingers[i];
                        float cx = Mathf.Lerp(f.RaisedCx, f.RestCx, rest);
                        a.Fingers[i].localPosition = new Vector3(cx + f.Dx * cos, f.Y, f.Cz - f.Dx * sin);
                        a.Fingers[i].localRotation = Quaternion.AngleAxis(f.Tilt, Vector3.right) *
                                                     Quaternion.AngleAxis(ht, Vector3.up) *
                                                     Quaternion.AngleAxis(Mathf.Lerp(f.RaisedRz, f.RestRz, rest), Vector3.forward);
                    }
                }
            }
            if (reshape) shownRest = rest;
        }
    }

    /// <summary>
    /// Fades parts in by distance instead of letting them pop into view. The body's lit shader can't be made
    /// see-through, so while fading they use an unlit transparent copy (same texture, colour dimmed to roughly
    /// the scene's light), and switch to the lit material once fully shown. Same trick as the vanish fade.
    /// </summary>
    internal class RevealFader
    {
        private readonly List<Renderer> renderers;
        public readonly Material Lit;
        private readonly Material see;
        private Material current;
        private bool shown = true;

        public RevealFader(List<Renderer> renderers)
        {
            this.renderers = renderers;
            if (renderers.Count == 0 || !renderers[0]) return;
            Lit = new Material(renderers[0].sharedMaterial);
            if (HufsaPrefabs.SpriteShader)
                see = new Material(HufsaPrefabs.SpriteShader) { mainTexture = Lit.HasProperty("_MainTex") ? Lit.mainTexture : null };
            Use(Lit);
        }

        public void Set(float k)
        {
            if (!Lit) return;
            bool show = k > 0.01f;
            if (show != shown)
            {
                foreach (Renderer r in renderers) if (r) r.enabled = show;
                shown = show;
            }
            if (!show) return;
            if (k >= 0.99f || !see)
            {
                Use(Lit);
                return;
            }
            Color c = Lit.HasProperty("_Color") ? Lit.color : Color.white;
            float light = Mathf.Clamp(RenderSettings.ambientLight.maxColorComponent * 1.5f, 0.15f, 1f);
            see.color = new Color(c.r * light, c.g * light, c.b * light, k);
            Use(see);
        }

        private void Use(Material m)
        {
            if (current == m) return;
            foreach (Renderer r in renderers) if (r) r.sharedMaterial = m;
            current = m;
        }

        public void Destroy()
        {
            if (Lit) Object.Destroy(Lit);
            if (see) Object.Destroy(see);
        }
    }
}
