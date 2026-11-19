using System.Collections.Generic;
using UnityEngine;

namespace HighPowerReactors
{
    // Emissive glow for a part: its own materials that have an emissive colour are set directly, from 0 (grey) to 1 (full glow).
    // No ModuleColorChanger (or any other glow module) is involved, so nothing else can fight over the value.
    public class ReactorGlow
    {
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        readonly List<Material> materials = new List<Material>();   // This part's own copies of its emissive materials.
        bool collected;
        float level;
        float sent = -1f;

        // Forget the materials, so they are gathered again on the next update (the module calls this when it starts).
        public void Reset()
        {
            Release();
            collected = false;
        }

        // Moves the glow toward full (on) or grey (off) and hands the result to the materials.
        public void Update(Part part, bool on, float riseSeconds, float fallSeconds)
        {
            if (!collected) Collect(part);
            if (materials.Count == 0) return;

            float target = on ? 1f : 0f;
            float seconds = target > level ? riseSeconds : fallSeconds;
            level = seconds > 0f ? Mathf.MoveTowards(level, target, Time.deltaTime / seconds) : target;

            // Only touch the materials when the value has actually moved.
            if (Mathf.Abs(level - sent) < 0.002f && !(level == 0f && sent != 0f) && !(level == 1f && sent != 1f)) return;
            sent = level;

            Color emissive = new Color(level, level, level, level);   // The shader multiplies this with the part's emissive texture.
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i] != null) materials[i].SetColor(EmissiveColorId, emissive);
            }
        }

        // Finds every material on this part that has an emissive colour and takes a private copy of it, so only this part glows
        // (the part's materials are shared with every other copy of it). The copies are made once and kept. A part with no such
        // material simply never glows.
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

                Material own = r.material;   // Makes this renderer's own copy.
                materials.Add(own);
                found++;
            }
            if (found == 0) Debug.Log(ReactorText.LogNoGlowMaterial);
            else Debug.Log(ReactorText.LogGlowMaterials(found, renderers.Length));
        }

        // Destroys the copies. Call when the part goes away.
        public void Release()
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) UnityEngine.Object.Destroy(materials[i]);
            materials.Clear();
        }
    }
}