using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Faint glow along Hufsa's silhouette, fading inward, like the Shape Lab's glow. Valheim can't load
    /// new shaders at runtime, so the fresnel is done on the CPU: every frame each vertex of a thin shell around
    /// the body gets an alpha from how far its surface turns away from this player's camera. Edges glow, the
    /// middle (facing you) stays clear. Per client, so everyone sees their own outline. On the Hunter the shell
    /// follows the moving hem.
    /// </summary>
    internal class SilhouetteGlow : MonoBehaviour
    {
        internal const float ShellOffset = 0.025f;
        private static readonly Color GlowColour = new Color(0.78f, 0.68f, 0.98f);

        /// <summary>Body shell: follows the moving hem. Arm shells are moved by HufsaArms (SetShape) instead.</summary>
        public bool FollowsBody = true;

        /// <summary>Optional 0..1 per vertex, scaling the glow (the arms fade it out where they meet the body).</summary>
        public float[] VertexFade;

        /// <summary>Scales the whole glow, e.g. the arms while they are still hidden or fading in.</summary>
        public float Visibility { get; set; } = 1f;

        private HufsaEntity entity;
        private Mesh mesh;
        private Vector3[] verts;
        private Vector3[] normals;
        private Color32[] colours;
        private Renderer rend;
        private bool shellMoved;

        private void Awake()
        {
            entity = GetComponentInParent<HufsaEntity>();
            var filter = GetComponent<MeshFilter>();
            rend = GetComponent<Renderer>();
            mesh = filter.mesh; // own copy: colours are per viewer
            mesh.MarkDynamic();
            verts = mesh.vertices;
            normals = mesh.normals;
            colours = new Color32[verts.Length];
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        private void LateUpdate()
        {
            float strength = entity && entity.IsHunter ? HufsaConfig.HunterGlow.Value : HufsaConfig.WatcherGlow.Value;
            if (entity) strength *= entity.FadeAlpha;
            strength *= Visibility * HufsaAuraManager.GlowWeatherFactor; // barely any in fog
            Camera cam = GameCamera.instance ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
            if (!cam || strength <= 0f)
            {
                if (rend.enabled) rend.enabled = false;
                return;
            }
            if (!rend.enabled) rend.enabled = true;
            if (!rend.isVisible) return;

            if (FollowsBody) FollowHem();

            // Strongest from afar, where the outline is all you see; up close it fades to a fifth.
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            float t = Mathf.Clamp01((dist - 8f) / 32f);
            strength *= Mathf.Lerp(0.2f, 1f, t * t * (3f - 2f * t));

            Vector3 eye = transform.InverseTransformPoint(cam.transform.position);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 view = eye - verts[i];
                float facing = Mathf.Abs(Vector3.Dot(normals[i], view.normalized));
                // Faint, reaching a little way in from the edge (gentler falloff than a hard rim).
                float rim = 1f - facing;
                float a = strength * 0.3f * Mathf.Pow(rim, 2.5f);
                if (VertexFade != null && i < VertexFade.Length) a *= VertexFade[i];
                colours[i] = new Color(GlowColour.r, GlowColour.g, GlowColour.b, Mathf.Clamp01(a));
            }
            mesh.colors32 = colours;
        }

        /// <summary>Moves the shell onto a new shape of what it wraps (an arm bending as it rises).</summary>
        public void SetShape(Vector3[] surface, Vector3[] surfaceNormals)
        {
            if (!mesh) return;
            for (int i = 0; i < verts.Length && i < surface.Length; i++)
            {
                normals[i] = surfaceNormals[i];
                verts[i] = surface[i] + surfaceNormals[i] * ShellOffset;
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        /// <summary>Keeps the shell on the Hunter's gliding hem, and puts it back when it stops.</summary>
        private void FollowHem()
        {
            Vector3[] body = entity ? entity.MovingBodyVertices : null;
            if (body == null && !shellMoved) return;
            Vector3[] source = body ?? HufsaModel.BodyRest;
            for (int i = 0; i < verts.Length && i < source.Length; i++) verts[i] = source[i] + normals[i] * ShellOffset;
            mesh.vertices = verts;
            shellMoved = body != null;
        }
    }
}
