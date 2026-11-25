using System.Collections.Generic;
using UnityEngine;
namespace HighPowerReactors
{
    public class ReactorGlow
    {
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        readonly List<Material> materials = new List<Material>();
        bool collected;
        float level;
        float sent = -1f;
        public void Reset()
        {
            Release();
            collected = false;
        }
        public void Update(Part part, bool on, float riseSeconds, float fallSeconds)
        {
            if (!collected) Collect(part);
            if (materials.Count == 0) return;
            float target = on ? 1f : 0f;
            float seconds = target > level ? riseSeconds : fallSeconds;
            level = seconds > 0f ? Mathf.MoveTowards(level, target, Time.deltaTime / seconds) : target;
            if (Mathf.Abs(level - sent) < 0.002f && !(level == 0f && sent != 0f) && !(level == 1f && sent != 1f)) return;
            sent = level;
            Color emissive = new Color(level, level, level, level);
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null) materials[i].SetColor(EmissiveColorId, emissive);
            }
        }
        void Collect(Part part)
        {
            Release();
            collected = true;
            sent = -1f;
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            int found = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty(EmissiveColorId)) continue;
                Material own = r.material;
                materials.Add(own);
                found++;
            }
            if (found == 0) Debug.Log(ReactorText.LogNoGlowMaterial);
            else Debug.Log(ReactorText.LogGlowMaterials(found, renderers.Length));
        }
        public void Release()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) UnityEngine.Object.Destroy(materials[i]);
            materials.Clear();
        }
    }
}