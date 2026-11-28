using System.Collections.Generic;
using CrunchDrives.Core;
using UnityEngine;
namespace CrunchDrives
{
    public class CrunchTube
    {
        const int CornerCount = 8;
        const int MaxMeshVertices = 60000;
        const float EndMargin = 1f;
        const int MaxRefineVertices = 65000;
        struct ScanCorner
        {
            public float along;
            public float radial;
            public int part;
        }
        GameObject root;
        MeshFilter forwardFilter;
        MeshFilter backwardFilter;
        Renderer forwardRenderer;
        Renderer backwardRenderer;
        Transform forwardTransform;
        Transform backwardTransform;
        Light lamp;
        Material material;
        Material glowMaterial;
        MeshFilter burstForwardFilter;
        MeshFilter burstBackwardFilter;
        Renderer burstForwardRenderer;
        Renderer burstBackwardRenderer;
        Transform burstForwardTransform;
        Transform burstBackwardTransform;
        Material burstMaterial;
        Glow flashGlow;
        string colorProperty;
        float colorBase = 1f;
        readonly List<ScanCorner> scan = new List<ScanCorner>();
        readonly List<string> scanParts = new List<string>();
        readonly List<string> scanIgnored = new List<string>();
        float scanWidestRadius, scanMaxAlong, scanMinAlong, scanBoundRadius;
        string scanWidestPart = "";
        string scanWidestSource = "";
        int scanWidestTotal, scanWidestOuter, scanWidestMid;
        int scanRenderers;
        bool scanned;
        float radius = 1f;
        float forward = 2f;
        float backward = 2f;
        float flashRadius = 1f;
        float level;
        float clock;
        float flashClock = -1f;
        float flashAge;
        float[] forwardAlpha, backwardAlpha;
        Color[] forwardWork, backwardWork;
        float[] burstForwardAlpha, burstBackwardAlpha;
        Color[] burstForwardWork, burstBackwardWork;
        float burstLevel;
        bool burstShown;
        bool shown;
        bool flashShown;
        bool wasOn;
        bool built;
        public bool IsBuilt { get { return built; } }
        static Shader shader;
        static Texture2D glowTexture;
        static readonly Vector3[] corners = new Vector3[CornerCount];
        static readonly float[] cornerAlong = new float[CornerCount];
        static readonly float[] cornerRadial = new float[CornerCount];
        static readonly List<Vector3> vertexBuffer = new List<Vector3>();
        static readonly List<float> radialSqr = new List<float>();
        static int meshTotal, meshOuter, meshMid;
        static readonly string[] ShaderNames =
        {
            "KSP/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "KSP/Particles/Additive",
            "Legacy Shaders/Particles/Additive"
        };
        public void Attach(Part part)
        {
            Dispose();
            if (part == null) return;
            if (shader == null)
            {
                for (int i = 0; i < ShaderNames.Length && shader == null; i++) shader = Shader.Find(ShaderNames[i]);
                if (shader == null) { Debug.Log(CrunchText.LogNoTubeShader); return; }
                if (shader.name.Contains("Additive")) Debug.Log(CrunchText.LogTubeAdditive);
            }
            material = new Material(shader);
            material.mainTexture = Texture2D.whiteTexture;
            if (material.HasProperty("_TintColor")) { colorProperty = "_TintColor"; colorBase = 0.5f; }
            else if (material.HasProperty("_Color")) { colorProperty = "_Color"; colorBase = 1f; }
            else colorProperty = null;
            burstMaterial = new Material(shader);
            burstMaterial.mainTexture = Texture2D.whiteTexture;
            glowMaterial = new Material(shader);
            glowMaterial.mainTexture = GlowTexture();
            if (colorProperty != null) glowMaterial.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase));
            root = new GameObject("CrunchDriveTube");
            root.layer = part.gameObject.layer;
            root.transform.SetParent(part.transform, false);
            forwardRenderer = MakeHalf("Forward", out forwardFilter, material);
            backwardRenderer = MakeHalf("Backward", out backwardFilter, material);
            burstForwardRenderer = MakeHalf("BurstForward", out burstForwardFilter, burstMaterial);
            burstBackwardRenderer = MakeHalf("BurstBackward", out burstBackwardFilter, burstMaterial);
            burstForwardTransform = burstForwardFilter.transform;
            burstBackwardTransform = burstBackwardFilter.transform;
            forwardTransform = forwardFilter.transform;
            backwardTransform = backwardFilter.transform;
            flashGlow = new Glow("Flash", root.transform, glowMaterial, root.layer);
            lamp = root.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;
            shown = true;
            flashShown = true;
            burstShown = true;
            built = false;
            scanned = false;
            wasOn = false;
            flashClock = -1f;
            level = 0f;
            burstLevel = 0f;
            SetShown(false, false, false);
        }
        Renderer MakeHalf(string name, out MeshFilter filter, Material sharedMaterial)
        {
            GameObject go = new GameObject(name);
            go.layer = root.layer;
            go.transform.SetParent(root.transform, false);
            filter = go.AddComponent<MeshFilter>();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
        public void Dispose()
        {
            DestroyMesh(forwardFilter);
            DestroyMesh(backwardFilter);
            DestroyMesh(burstForwardFilter);
            DestroyMesh(burstBackwardFilter);
            if (flashGlow != null) flashGlow.Dispose();
            if (root != null) Object.Destroy(root);
            if (material != null) Object.Destroy(material);
            if (glowMaterial != null) Object.Destroy(glowMaterial);
            if (burstMaterial != null) Object.Destroy(burstMaterial);
            root = null;
            burstMaterial = null;
            burstForwardFilter = null;
            burstBackwardFilter = null;
            burstForwardRenderer = null;
            burstBackwardRenderer = null;
            burstForwardTransform = null;
            burstBackwardTransform = null;
            material = null;
            glowMaterial = null;
            flashGlow = null;
            forwardFilter = null;
            backwardFilter = null;
            forwardTransform = null;
            backwardTransform = null;
            scan.Clear();
            scanParts.Clear();
            scanned = false;
            shown = false;
            flashShown = false;
            burstShown = false;
            burstLevel = 0f;
            wasOn = false;
            flashClock = -1f;
            built = false;
        }
        static void DestroyMesh(MeshFilter f)
        {
            if (f != null && f.sharedMesh != null) Object.Destroy(f.sharedMesh);
        }
        public void Measure(Part drive, Vessel vessel)
        {
            if (root == null || drive == null || vessel == null) return;
            Scan(drive, vessel);
            Rebuild();
        }
        void Scan(Part drive, Vessel vessel)
        {
            Transform pt = drive.transform;
            Vector3 center = pt.position;
            Vector3 axis = pt.up;
            scan.Clear();
            scanParts.Clear();
            scanIgnored.Clear();
            scanWidestRadius = 0f;
            scanWidestPart = "";
            scanWidestSource = "";
            scanWidestTotal = 0;
            scanWidestOuter = 0;
            scanWidestMid = 0;
            scanMaxAlong = 0f;
            scanMinAlong = 0f;
            scanBoundRadius = 0f;
            scanRenderers = 0;
            for (int p = 0; p < vessel.parts.Count; p++)
            {
                scanParts.Add(vessel.parts[p].name);
                bool ignoreWidth = CrunchSettings.IsTubeIgnored(vessel.parts[p].name);
                if (ignoreWidth) scanIgnored.Add(vessel.parts[p].name);
                foreach (Renderer r in MeshRenderers(vessel.parts[p]))
                {
                    if (!Usable(r)) continue;
                    scanRenderers++;
                    int n = ObbCorners(r);
                    float boxRadial = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 offset = corners[i] - center;
                        float along = Vector3.Dot(offset, axis);
                        cornerAlong[i] = along;
                        cornerRadial[i] = Mathf.Sqrt(Mathf.Max(0f, offset.sqrMagnitude - along * along));
                        boxRadial = Mathf.Max(boxRadial, cornerRadial[i]);
                        if (along > scanMaxAlong) scanMaxAlong = along;
                        if (along < scanMinAlong) scanMinAlong = along;
                        scanBoundRadius = Mathf.Max(scanBoundRadius, offset.magnitude);
                    }
                    float radial = boxRadial;
                    string how = "box";
                    meshTotal = 0;
                    if (ignoreWidth) radial = 0f;
                    else if (boxRadial > scanWidestRadius) radial = MeshRadial(r, center, axis, boxRadial, out how);
                    if (!ignoreWidth && radial > scanWidestRadius)
                    {
                        scanWidestRadius = radial;
                        scanWidestPart = vessel.parts[p].name;
                        scanWidestSource = r.name + " / " + r.sharedMaterial.shader.name + ", " + how;
                        scanWidestTotal = meshTotal;
                        scanWidestOuter = meshOuter;
                        scanWidestMid = meshMid;
                    }
                    for (int i = 0; i < n; i++) scan.Add(new ScanCorner { along = cornerAlong[i], radial = ignoreWidth ? cornerRadial[i] : Mathf.Min(cornerRadial[i], radial), part = p });
                }
            }
            scanned = true;
        }
        void Rebuild()
        {
            if (root == null || !scanned) return;
            float curve = (float)CrunchTuning.TubeCurve;
            float ringR = Mathf.Max(0.5f, scanWidestRadius * (float)CrunchTuning.TubeRadiusFactor);
            float fitMax = (float)CrunchTuning.TubeFitMaxFactor;
            float needForward = 0f;
            float needBackward = 0f;
            int poking = 0;
            string forwardPart = "", backwardPart = "";
            float forwardAlong = 0f, forwardRadial = 0f, backwardAlong = 0f, backwardRadial = 0f;
            for (int i = 0; i < scan.Count; i++)
            {
                ScanCorner c = scan[i];
                bool capped = false;
                if (c.along > 0f)
                {
                    float need = (float)CrunchMath.TubeRequiredLength(c.along, c.radial, ringR, fitMax, curve, out capped);
                    if (need > needForward) { needForward = need; forwardPart = scanParts[c.part]; forwardAlong = c.along; forwardRadial = c.radial; }
                }
                else if (c.along < 0f)
                {
                    float need = (float)CrunchMath.TubeRequiredLength(-c.along, c.radial, ringR, fitMax, curve, out capped);
                    if (need > needBackward) { needBackward = need; backwardPart = scanParts[c.part]; backwardAlong = c.along; backwardRadial = c.radial; }
                }
                if (capped) poking++;
            }
            float maxAlong = scanMaxAlong;
            float minAlong = scanMinAlong;
            float craftLength = maxAlong - minAlong;
            radius = ringR;
            float minHalf = Mathf.Max(2f, scanWidestRadius * 2f);
            forward = Mathf.Max(minHalf, maxAlong + EndMargin, needForward);
            backward = Mathf.Max(minHalf, -minAlong + EndMargin, needBackward);
            float length = Mathf.Max(forward, backward);
            forward = length;
            backward = length;
            float solid = (float)CrunchMath.TubeSolidDistance(Mathf.Max(maxAlong, -minAlong), length, CrunchTuning.TubeFadeStart);
            int segments = CrunchTuning.TubeSegments;
            DestroyMesh(forwardFilter);
            DestroyMesh(backwardFilter);
            float curveNow = (float)CrunchTuning.TubeCurve;
            forwardFilter.sharedMesh = BuildTube(radius, forward, solid, segments, 1f, curveNow);
            backwardFilter.sharedMesh = BuildTube(radius, backward, solid, segments, -1f, curveNow);
            CacheColors(forwardFilter.sharedMesh, out forwardAlpha, out forwardWork);
            CacheColors(backwardFilter.sharedMesh, out backwardAlpha, out backwardWork);
            float burstSolid = (float)CrunchMath.TubeSolidDistance(Mathf.Max(maxAlong, -minAlong), length, CrunchTuning.TubeBurstFadeStart);
            DestroyMesh(burstForwardFilter);
            DestroyMesh(burstBackwardFilter);
            burstForwardFilter.sharedMesh = BuildTube(radius, length, burstSolid, segments, 1f, curveNow);
            burstBackwardFilter.sharedMesh = BuildTube(radius, length, burstSolid, segments, -1f, curveNow);
            CacheColors(burstForwardFilter.sharedMesh, out burstForwardAlpha, out burstForwardWork);
            CacheColors(burstBackwardFilter.sharedMesh, out burstBackwardAlpha, out burstBackwardWork);
            flashRadius = Mathf.Max(scanBoundRadius, radius);
            built = true;
            lamp.range = Mathf.Max(radius, craftLength) * (float)CrunchTuning.TubeLightRangeFactor;
            Debug.Log(CrunchText.LogTubeMeasured(radius, forward, backward, craftLength));
            Debug.Log(CrunchText.LogTubeWidest(scanWidestPart, scanWidestSource, scanWidestRadius));
            if (scanWidestTotal > 0) Debug.Log(CrunchText.LogTubeWidestVertices(scanWidestTotal, scanWidestOuter, scanWidestMid));
            if (scanIgnored.Count > 0) Debug.Log(CrunchText.LogTubeIgnored(string.Join(", ", scanIgnored.ToArray())));
            if (poking > 0) Debug.Log(CrunchText.LogTubePoking(poking));
            Debug.Log(CrunchText.LogTubeSources(scanRenderers, scanParts.Count, forwardPart, forwardAlong, forwardRadial, backwardPart, backwardAlong, backwardRadial));
        }
        static void CacheColors(Mesh mesh, out float[] alphas, out Color[] work)
        {
            work = mesh.colors;
            alphas = new float[work.Length];
            for (int i = 0; i < work.Length; i++) alphas[i] = work[i].a;
        }
        static void FadeColors(Mesh mesh, float[] alphas, Color[] work, float fade)
        {
            for (int i = 0; i < work.Length; i++) work[i].a = alphas[i] * fade;
            mesh.colors = work;
        }
        static IEnumerable<Renderer> MeshRenderers(Part part)
        {
            List<MeshRenderer> meshes = part.FindModelComponents<MeshRenderer>();
            for (int i = 0; i < meshes.Count; i++) yield return meshes[i];
            List<SkinnedMeshRenderer> skinned = part.FindModelComponents<SkinnedMeshRenderer>();
            for (int i = 0; i < skinned.Count; i++) yield return skinned[i];
        }
        bool Usable(Renderer r)
        {
            return r != null && r.enabled && r.gameObject.activeInHierarchy && !r.transform.IsChildOf(root.transform) && !IsEffect(r);
        }
        static bool IsEffect(Renderer r)
        {
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null) return true;
            string n = m.shader.name;
            return n.StartsWith("Waterfall") || n.Contains("Particles") || m.renderQueue >= 3000;
        }
        static float MeshRadial(Renderer r, Vector3 center, Vector3 axis, float boxRadial, out string how)
        {
            how = "box";
            if (r is SkinnedMeshRenderer) return boxRadial;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0 || mesh.vertexCount > MaxRefineVertices) return boxRadial;
            how = "mesh";
            mesh.GetVertices(vertexBuffer);
            Matrix4x4 toWorld = r.transform.localToWorldMatrix;
            radialSqr.Clear();
            float widest = 0f;
            for (int i = 0; i < vertexBuffer.Count; i++)
            {
                Vector3 offset = toWorld.MultiplyPoint3x4(vertexBuffer[i]) - center;
                float along = Vector3.Dot(offset, axis);
                float sqr = offset.sqrMagnitude - along * along;
                radialSqr.Add(sqr);
                widest = Mathf.Max(widest, sqr);
            }
            float outerSqr = widest * 0.9025f;
            float midSqr = widest * 0.5625f;
            meshTotal = radialSqr.Count;
            meshOuter = 0;
            meshMid = 0;
            for (int i = 0; i < radialSqr.Count; i++)
            {
                if (radialSqr[i] >= outerSqr) meshOuter++;
                if (radialSqr[i] >= midSqr) meshMid++;
            }
            return Mathf.Min(boxRadial, Mathf.Sqrt(Mathf.Max(0f, widest)));
        }
        static int ObbCorners(Renderer r)
        {
            Bounds b;
            SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
            if (skinned != null)
            {
                b = skinned.localBounds;
            }
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return 0;
                b = mf.sharedMesh.bounds;
            }
            Transform t = skinned != null && skinned.rootBone != null ? skinned.rootBone : r.transform;
            Vector3 c = b.center, e = b.extents;
            int k = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners[k++] = t.TransformPoint(c + new Vector3(e.x * x, e.y * y, e.z * z));
            return CornerCount;
        }
        public void Tick(float dt, bool on, float intensity)
        {
            if (root == null) return;
            if (!built) on = false;
            intensity = Mathf.Clamp01(intensity);
            if (on != wasOn)
            {
                wasOn = on;
                flashClock = 0f;
                if (!on) burstLevel = 1f;
            }
            if (on) burstLevel = 0f;
            else
            {
                float burstSeconds = (float)CrunchTuning.TubeFadeOutSeconds;
                burstLevel = Mathf.MoveTowards(burstLevel, 0f, burstSeconds > 0f ? dt / burstSeconds : 1f);
            }
            float burstFade = burstLevel * burstLevel;
            bool burstVisible = burstFade > 0.0005f;
            if (on)
            {
                level = 1f;
            }
            else
            {
                float fadeSeconds = (float)CrunchTuning.TubeFadeOutSeconds;
                level = Mathf.MoveTowards(level, 0f, fadeSeconds > 0f ? dt / fadeSeconds : 1f);
            }
            float fade = level * level * intensity;
            bool visible = fade > 0.0005f;
            float flash = TickFlash(dt);
            SetShown(visible, flash > 0f, burstVisible);
            if (!visible && !burstVisible && flash <= 0f) return;
            Camera cam = ViewCamera();
            Quaternion faceCamera = cam != null ? cam.transform.rotation : root.transform.rotation;
            float shade = 0f;
            if (visible)
            {
                clock += dt;
                float hz = (float)CrunchTuning.TubeFlickerHz;
                float amount = (float)CrunchTuning.TubeFlickerAmount;
                float noise = Mathf.PerlinNoise(clock * hz, 0.37f) * 0.6f
                            + Mathf.PerlinNoise(clock * hz * 2.3f, 7.1f) * 0.4f;
                float flicker = 1f - amount * (1f - noise);
                float lengthScale = StretchFactor(noise, (float)CrunchTuning.TubeStretchAmount);
                Vector3 scale = new Vector3(1f, lengthScale, 1f);
                forwardTransform.localScale = scale;
                backwardTransform.localScale = scale;
                shade = fade * flicker;
                if (colorProperty != null)
                {
                    material.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase * shade));
                }
                else
                {
                    FadeColors(forwardFilter.sharedMesh, forwardAlpha, forwardWork, shade);
                    FadeColors(backwardFilter.sharedMesh, backwardAlpha, backwardWork, shade);
                }
            }
            if (burstVisible)
            {
                Vector3 burstScale = new Vector3(1f, Mathf.Max(0.0001f, Mathf.SmoothStep(0f, 1f, burstLevel)), 1f);
                burstForwardTransform.localScale = burstScale;
                burstBackwardTransform.localScale = burstScale;
                if (colorProperty != null)
                {
                    burstMaterial.SetColor(colorProperty, new Color(colorBase, colorBase, colorBase, colorBase * burstFade));
                }
                else
                {
                    FadeColors(burstForwardFilter.sharedMesh, burstForwardAlpha, burstForwardWork, burstFade);
                    FadeColors(burstBackwardFilter.sharedMesh, burstBackwardAlpha, burstBackwardWork, burstFade);
                }
            }
            PlaceFlash(flash, cam, faceCamera);
            lamp.intensity = (float)CrunchTuning.TubeLightIntensity * Mathf.Max(shade, burstFade) + (float)CrunchTuning.FlashLightIntensity * flash;
        }
        float TickFlash(float dt)
        {
            if (flashClock < 0f) return 0f;
            float rise = (float)CrunchTuning.FlashRiseSeconds;
            float fall = (float)CrunchTuning.FlashFadeSeconds;
            flashClock += dt;
            if (flashClock >= rise + fall) { flashClock = -1f; return 0f; }
            flashAge = flashClock / (rise + fall);
            return FlashEnvelope(flashClock, rise, fall);
        }
        void PlaceFlash(float flash, Camera cam, Quaternion faceCamera)
        {
            if (flash <= 0f) return;
            Vector3 position = Vector3.zero;
            if (cam != null)
            {
                Vector3 toCamera = root.transform.InverseTransformPoint(cam.transform.position);
                float distance = toCamera.magnitude;
                if (distance > 0.001f)
                {
                    float standoff = Mathf.Min(flashRadius * 1.05f, distance * 0.5f);
                    position = toCamera * (standoff / distance);
                }
            }
            float size = 2f * flashRadius * (float)CrunchTuning.FlashSizeFactor * (1f + (float)CrunchTuning.FlashGrowth * flashAge);
            flashGlow.Place(position, faceCamera, size, Mathf.Clamp01(flash * (float)CrunchTuning.FlashStrength));
        }
        void SetShown(bool tubeOn, bool flashOn, bool burstOn)
        {
            if (root != null && shown == tubeOn && flashShown == flashOn && burstShown == burstOn) return;
            shown = tubeOn;
            flashShown = flashOn;
            burstShown = burstOn;
            if (burstForwardRenderer != null) burstForwardRenderer.enabled = burstOn;
            if (burstBackwardRenderer != null) burstBackwardRenderer.enabled = burstOn;
            if (forwardRenderer != null) forwardRenderer.enabled = tubeOn;
            if (backwardRenderer != null) backwardRenderer.enabled = tubeOn;
            if (flashGlow != null) flashGlow.Visible = flashOn;
            if (lamp != null) lamp.enabled = tubeOn || flashOn || burstOn;
        }
        static Camera ViewCamera()
        {
            Camera c = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            return c != null ? c : Camera.main;
        }
        sealed class Glow
        {
            readonly GameObject go;
            readonly Transform tf;
            readonly MeshRenderer renderer;
            readonly Mesh mesh;
            readonly Color[] colors = new Color[4];
            float lastAlpha = -1f;
            public Glow(string name, Transform parent, Material material, int layer)
            {
                go = new GameObject(name);
                go.layer = layer;
                tf = go.transform;
                tf.SetParent(parent, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                mesh = BuildQuad();
                filter.sharedMesh = mesh;
                renderer.enabled = false;
            }
            public bool Visible { set { renderer.enabled = value; } }
            public void Place(Vector3 localPosition, Quaternion worldRotation, float size, float alpha)
            {
                tf.localPosition = localPosition;
                tf.rotation = worldRotation;
                tf.localScale = new Vector3(size, size, 1f);
                if (Mathf.Approximately(alpha, lastAlpha)) return;
                lastAlpha = alpha;
                Color c = new Color(1f, 1f, 1f, alpha);
                colors[0] = c; colors[1] = c; colors[2] = c; colors[3] = c;
                mesh.colors = colors;
            }
            public void Dispose()
            {
                if (mesh != null) Object.Destroy(mesh);
                if (go != null) Object.Destroy(go);
            }
            static Mesh BuildQuad()
            {
                Mesh m = new Mesh();
                m.name = "CrunchDriveGlow";
                m.vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                m.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                Color clear = new Color(1f, 1f, 1f, 0f);
                m.colors = new Color[] { clear, clear, clear, clear };
                m.triangles = new int[] { 0, 2, 1, 1, 2, 3, 0, 1, 2, 1, 3, 2 };
                m.RecalculateBounds();
                return m;
            }
        }
        static Texture2D GlowTexture()
        {
            if (glowTexture != null) return glowTexture;
            const int size = 64;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(255f * GlowAlpha(Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            t.SetPixels32(pixels);
            t.Apply();
            glowTexture = t;
            return t;
        }
        public static float GlowAlpha(float r)
        {
            if (r >= 1f) return 0f;
            float s = Mathf.Clamp01((r - 0.15f) / 0.85f);
            return 1f - Mathf.SmoothStep(0f, 1f, s);
        }
        public static float FlashEnvelope(float t, float rise, float fall)
        {
            if (t <= 0f) return 0f;
            if (t < rise) return Mathf.SmoothStep(0f, 1f, t / rise);
            float s = Mathf.Clamp01((t - rise) / Mathf.Max(0.0001f, fall));
            return (1f - s) * (1f - s);
        }
        public static float StretchFactor(float noise, float amount)
        {
            return 1f + Mathf.Max(0f, amount) * Mathf.Clamp01(noise);
        }
        public static float HaloShellAlpha(int s, int shells, float edgeOpacity, float falloff)
        {
            if (shells < 1 || s < 1 || s > shells) return 0f;
            float k = Mathf.Max(0.05f, falloff);
            float end = Bell(1f, k);
            float inner = (Bell((float)(s - 1) / shells, k) - end) / (1f - end);
            float outer = (Bell((float)s / shells, k) - end) / (1f - end);
            float added = Mathf.Clamp01(edgeOpacity) * Mathf.Max(0f, inner - outer);
            return 1f - Mathf.Sqrt(1f - Mathf.Min(added, 0.999f));
        }
        static float Bell(float d, float k)
        {
            return Mathf.Exp(-(d * d) / (k * k));
        }
        static Mesh BuildTube(float radius, float length, float solid, int segments, float direction, float curve)
        {
            int count = CrunchTuning.TubeRings;
            List<float> us = new List<float>();
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                us.Add(Mathf.Lerp(t, 1f - (1f - t) * (1f - t), Mathf.Clamp01(curve)));
            }
            float solidU = Mathf.Clamp01(solid / length);
            us.Add(solidU);
            us.Sort();
            for (int i = us.Count - 1; i > 0; i--) if (us[i] - us[i - 1] < 0.0005f) us.RemoveAt(i);
            int ringVerts = segments + 1;
            int shells = Mathf.Min(CrunchTuning.TubeHaloShells, Mathf.Max(0, MaxMeshVertices / (us.Count * ringVerts) - 1));
            float spread = (float)CrunchTuning.TubeHaloSpread;
            float edgeOpacity = (float)CrunchTuning.TubeHaloStrength;
            float falloff = (float)CrunchTuning.TubeHaloFalloff;
            List<Vector3> verts = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            for (int s = 0; s <= shells; s++)
            {
                float widen = 1f + spread * s / Mathf.Max(1, shells);
                float fade = s == 0 ? 1f : HaloShellAlpha(s, shells, edgeOpacity, falloff);
                int first = verts.Count;
                for (int r = 0; r < us.Count; r++)
                {
                    float y = length * us[r];
                    float rad = radius * widen * (float)CrunchMath.TubeRadiusScale(us[r], curve);
                    Color c = new Color(1f, 1f, 1f, (float)CrunchMath.TubeEndAlpha(y, solid, length) * fade);
                    for (int i = 0; i < ringVerts; i++)
                    {
                        float a = (float)i / segments * Mathf.PI * 2f;
                        verts.Add(new Vector3(Mathf.Cos(a) * rad, y * direction, Mathf.Sin(a) * rad));
                        colors.Add(c);
                        uvs.Add(new Vector2((float)i / segments, 0.5f));
                    }
                }
                for (int r = 0; r < us.Count - 1; r++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        int a = first + r * ringVerts + i, b = a + 1, c = a + ringVerts, d = c + 1;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
                }
            }
            Mesh mesh = new Mesh();
            mesh.name = "CrunchDriveTube";
            mesh.vertices = verts.ToArray();
            mesh.colors = colors.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}