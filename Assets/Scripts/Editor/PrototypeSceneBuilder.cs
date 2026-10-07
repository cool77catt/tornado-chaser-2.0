using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TornadoChaser.Editor
{
    // Builds the prototype's materials, prefabs, and Game scene from code, so the
    // setup can be reproduced. Running it again overwrites what it built before.
    public static class PrototypeSceneBuilder
    {
        const string MaterialsDir = "Assets/Art/Materials";
        const string TexturesDir = "Assets/Art/Textures";
        const string MeshesDir = "Assets/Art/Meshes";
        const string UiDir = "Assets/Art/UI";
        const string PrefabsDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string FunnelShader = "TornadoChaser/TornadoFunnel";
        const string ParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        const float RoadWidth = 10f;
        const float ShoulderWidth = 60f;
        const float DashLength = 3f;
        const float DashSpacing = 10f;
        const float TornadoStartGap = 70f;
        // Overall tornado size. The funnel, dust cloud, and debris swirl all scale with these.
        const float TornadoWidth = 1.5f;
        const float TornadoHeight = 1.3f;
        // Gray with a faint green tint, like a sky ahead of a severe storm.
        static readonly Color FogColor = new(0.36f, 0.4f, 0.4f);

        [MenuItem("Tornado Chaser/Build Prototype Scene")]
        static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(TexturesDir);
            Directory.CreateDirectory(MeshesDir);
            Directory.CreateDirectory(UiDir);
            Directory.CreateDirectory(PrefabsDir);
            AssetDatabase.Refresh();

            // Create the scene first: opening a new scene unloads unreferenced assets,
            // which would invalidate the prefabs built below.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var asphalt = CreateMaterial("Asphalt", new Color(0.18f, 0.18f, 0.19f), 0.3f);
            var grass = CreateMaterial("Grass", new Color(0.32f, 0.42f, 0.2f), 0.1f);
            var paint = CreateMaterial("RoadPaint", new Color(0.9f, 0.85f, 0.6f), 0.2f);
            var carBody = CreateMaterial("CarBody", new Color(0.85f, 0.35f, 0.1f), 0.6f);
            var carGlass = CreateMaterial("CarGlass", new Color(0.1f, 0.12f, 0.15f), 0.9f);
            var tire = CreateMaterial("Tire", new Color(0.08f, 0.08f, 0.08f), 0.1f);

            var roadPrefab = BuildRoadSegmentPrefab(asphalt, grass, paint);
            var carPrefab = BuildCarPrefab(carBody, carGlass, tire);
            var tornadoPrefab = BuildTornadoPrefab();
            BuildScene(scene, roadPrefab, carPrefab, tornadoPrefab);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
        }

        static Material CreateMaterial(string name, Color color, float smoothness)
        {
            var material = LoadOrCreateMaterial(name, () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LoadOrCreateMaterial(string name, System.Func<Material> create)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = create();
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static Material CreateFunnelMaterial(string name, Texture2D streaks, Color light, Color dark, float opacity,
            float edgeSoftness, float contrast, float spinSpeed, int renderQueue,
            float tilingV = 8f, float fadeBottom = 0.04f, float fadeTop = 0.1f, float raggedness = 0f, float minDensity = 0.6f)
        {
            var material = LoadOrCreateMaterial(name, () => new Material(Shader.Find(FunnelShader)));
            material.SetTexture("_StreakTex", streaks);
            material.SetColor("_LightColor", light);
            material.SetColor("_DarkColor", dark);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_EdgeSoftness", edgeSoftness);
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_TilingU", 4f);
            material.SetFloat("_TilingV", tilingV);
            material.SetFloat("_FadeBottom", fadeBottom);
            material.SetFloat("_FadeTop", fadeTop);
            material.SetFloat("_Raggedness", raggedness);
            material.SetFloat("_MinDensity", minDensity);
            material.SetFloat("_SpinSpeed", spinSpeed);
            // Fog barely touches the funnel so it stays dark against the hazy sky.
            material.SetFloat("_FogStrength", 0.15f);
            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Tiling noise stretched sideways into long, thin streaks that wrap around the funnel.
        static Texture2D BuildStreakTexture()
        {
            const int size = 512;
            var pixels = new byte[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float value = 0f, amplitude = 0.5f, total = 0f;
                for (int octave = 0; octave < 4; octave++)
                {
                    int periodU = 3 << octave, periodV = 24 << octave;
                    value += amplitude * TilingValueNoise(u * periodU, v * periodV, periodU, periodV);
                    total += amplitude;
                    amplitude *= 0.6f;
                }
                pixels[y * size + x] = (byte)(Mathf.Clamp01(value / total) * 255f);
            }

            var texture = new Texture2D(size, size, TextureFormat.R8, true)
            {
                name = "TornadoStreaks",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(true);

            string path = $"{TexturesDir}/TornadoStreaks.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(texture, path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float TilingValueNoise(float x, float y, int periodX, int periodY)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, periodX), xb = Mod(x0 + 1, periodX);
            int ya = Mod(y0, periodY), yb = Mod(y0 + 1, periodY);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(xa, ya), Hash(xb, ya), fx),
                Mathf.Lerp(Hash(xa, yb), Hash(xb, yb), fx),
                fy);
        }

        static int Mod(int value, int period) => ((value % period) + period) % period;

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        static RoadSegment BuildRoadSegmentPrefab(Material asphalt, Material grass, Material paint)
        {
            var root = new GameObject("RoadSegment");
            float length = root.AddComponent<RoadSegment>().Length;
            float mid = length / 2f;

            Flat(Part("Asphalt", root.transform, new Vector3(0f, -0.1f, mid), new Vector3(RoadWidth, 0.2f, length), asphalt));
            float shoulderX = (RoadWidth + ShoulderWidth) / 2f;
            Flat(Part("GrassLeft", root.transform, new Vector3(-shoulderX, -0.12f, mid), new Vector3(ShoulderWidth, 0.2f, length), grass));
            Flat(Part("GrassRight", root.transform, new Vector3(shoulderX, -0.12f, mid), new Vector3(ShoulderWidth, 0.2f, length), grass));

            float edgeX = RoadWidth / 2f - 0.4f;
            Flat(Part("EdgeLineLeft", root.transform, new Vector3(-edgeX, 0.005f, mid), new Vector3(0.15f, 0.01f, length), paint));
            Flat(Part("EdgeLineRight", root.transform, new Vector3(edgeX, 0.005f, mid), new Vector3(0.15f, 0.01f, length), paint));
            for (float z = DashSpacing / 4f; z < length; z += DashSpacing)
                Flat(Part("CenterDash", root.transform, new Vector3(0f, 0.005f, z + DashLength / 2f), new Vector3(0.15f, 0.01f, DashLength), paint));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/RoadSegment.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RoadSegment>();
        }

        static CarController BuildCarPrefab(Material body, Material glass, Material tire)
        {
            var root = new GameObject("Car");
            root.AddComponent<CarInput>();
            root.AddComponent<CarController>();

            Part("Body", root.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.6f, 4.2f), body);
            Part("Cabin", root.transform, new Vector3(0f, 1.22f, -0.3f), new Vector3(1.6f, 0.55f, 2f), glass);
            foreach (float x in new[] { -0.85f, 0.85f })
            foreach (float z in new[] { -1.35f, 1.35f })
            {
                var wheel = Part("Wheel", root.transform, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.15f, 0.7f), tire, PrimitiveType.Cylinder);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/Car.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CarController>();
        }

        static Material CreateSkyMaterial()
        {
            var material = LoadOrCreateMaterial("StormSky", () => new Material(Shader.Find("TornadoChaser/StormSky")));
            material.SetTexture("_CloudTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/TornadoStreaks.asset"));
            material.SetColor("_HorizonColor", FogColor);
            EditorUtility.SetDirty(material);
            return material;
        }

        static TornadoController BuildTornadoPrefab()
        {
            var root = new GameObject("Tornado");
            root.AddComponent<TornadoController>();

            // The rings render after the core so they always sit on top of it.
            var streaks = BuildStreakTexture();
            var core = CreateFunnelMaterial("FunnelCore", streaks, new Color(0.55f, 0.55f, 0.56f), new Color(0.04f, 0.04f, 0.05f),
                opacity: 1f, edgeSoftness: 0.2f, contrast: 2f, spinSpeed: 0.25f, renderQueue: 3000, minDensity: 0.95f);
            // The rings spin as geometry, so their texture doesn't need to.
            var rings = CreateFunnelMaterial("FunnelRings", streaks, new Color(0.5f, 0.5f, 0.51f), new Color(0.05f, 0.05f, 0.06f),
                opacity: 1f, edgeSoftness: 0.45f, contrast: 2f, spinSpeed: 0f, renderQueue: 3001,
                tilingV: 0.6f, fadeBottom: 0.35f, fadeTop: 0.35f, raggedness: 0.5f, minDensity: 0.8f);
            var funnel = Funnel("FunnelCore", root.transform, core, radiusScale: 0.92f * TornadoWidth);
            FunnelRings(root.transform, rings, funnel);
            BuildCloudDeck(root.transform, funnel, streaks);
            BuildDustCloud(root.transform);
            BuildDebrisSwirl(root.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsDir}/Tornado.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<TornadoController>();
        }

        static TornadoFunnel Funnel(string name, Transform parent, Material material, float radiusScale)
        {
            var component = MeshObject(name, parent, material).AddComponent<TornadoFunnel>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("baseRadius").floatValue *= radiusScale;
            serialized.FindProperty("topRadius").floatValue *= radiusScale;
            serialized.FindProperty("height").floatValue *= TornadoHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        // The broad, rotating storm cloud layer the funnel bells out into. Its inner edge
        // overlaps the top of the funnel, and it rises gently and fades into the fog outward.
        static void BuildCloudDeck(Transform parent, TornadoFunnel funnel, Texture2D clouds)
        {
            const int rings = 28;
            const int segments = 64;
            // Far enough that the edge sits just above the horizon, where full fog hides it.
            const float outerRadius = 1500f;
            const float rise = 30f;
            float innerRadius = funnel.Radius(1f) * 0.6f;
            float baseHeight = funnel.Height * 0.97f;

            int columns = segments + 1;
            var vertices = new Vector3[rings * columns];
            var uvs = new Vector2[vertices.Length];
            for (int r = 0; r < rings; r++)
            {
                float v = (float)r / (rings - 1);
                // Rings are spaced exponentially: dense near the funnel, sparse far out.
                float radius = innerRadius * Mathf.Pow(outerRadius / innerRadius, v);
                float y = baseHeight + rise * (1f - Mathf.Exp(-(radius - innerRadius) / 250f));
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    vertices[r * columns + s] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    uvs[r * columns + s] = new Vector2((float)s / segments, v);
                }
            }

            var triangles = new int[(rings - 1) * segments * 6];
            int k = 0;
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = r * columns + s;
                triangles[k++] = i;
                triangles[k++] = i + columns;
                triangles[k++] = i + 1;
                triangles[k++] = i + 1;
                triangles[k++] = i + columns;
                triangles[k++] = i + columns + 1;
            }

            var mesh = new Mesh { name = "CloudDeck", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{MeshesDir}/CloudDeck.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var material = LoadOrCreateMaterial("CloudDeck", () => new Material(Shader.Find("TornadoChaser/StormCloudDeck")));
            material.SetTexture("_CloudTex", clouds);
            // Full fog makes the distant deck match the sky's horizon color, so there's no seam.
            material.SetFloat("_FogStrength", 1f);
            material.SetFloat("_EdgeFade", 0.5f);
            // Behind the funnel (3000+), so the funnel's top blends into the clouds.
            material.renderQueue = 2990;
            EditorUtility.SetDirty(material);

            var deck = MeshObject("CloudDeck", parent, material);
            deck.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static void FunnelRings(Transform parent, Material material, TornadoFunnel funnel) =>
            SetReference(MeshObject("FunnelRings", parent, material).AddComponent<TornadoBands>(), "funnel", funnel);

        static GameObject MeshObject(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // The debris cloud where the funnel touches the ground: a dense churning core
        // that hugs the funnel, plus a wider skirt of dust rolling outward along the ground.
        static void BuildDustCloud(Transform parent)
        {
            var material = LoadOrCreateMaterial("Dust",
                () => new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath)));
            material.SetTexture("_BaseMap", BuildPuffTexture());
            // Draw after the funnel (queues 3000-3001). Sorting by center would otherwise put the
            // tall funnel in front and hide the dust packed around its base. URP particle materials
            // derive their queue from _QueueOffset and overwrite a directly set renderQueue.
            material.SetFloat("_QueueOffset", 2f);
            material.renderQueue = 3002;
            EditorUtility.SetDirty(material);

            // Lighter and browner than the funnel so the cloud stands out against it.
            DustLayer("DustCore", parent, material,
                radius: 7f, radiusThickness: 1f, lifetime: new Vector2(3f, 5f), size: new Vector2(9f, 16f),
                darkColor: new Color(0.28f, 0.24f, 0.2f), lightColor: new Color(0.42f, 0.36f, 0.3f),
                rise: new Vector2(0.5f, 2.5f), orbit: new Vector2(0.8f, 1.3f), outward: new Vector2(-0.5f, 0.5f),
                rate: 50f, maxParticles: 220);
            DustLayer("DustSkirt", parent, material,
                radius: 14f, radiusThickness: 0.5f, lifetime: new Vector2(3f, 5f), size: new Vector2(9f, 18f),
                darkColor: new Color(0.24f, 0.21f, 0.18f), lightColor: new Color(0.38f, 0.33f, 0.28f),
                rise: new Vector2(0.3f, 1.5f), orbit: new Vector2(0.5f, 0.9f), outward: new Vector2(2f, 4f),
                rate: 25f, maxParticles: 120);
        }

        static void DustLayer(string name, Transform parent, Material material, float radius, float radiusThickness,
            Vector2 lifetime, Vector2 size, Color darkColor, Color lightColor, Vector2 rise, Vector2 orbit, Vector2 outward,
            float rate, int maxParticles)
        {
            var dust = new GameObject(name);
            dust.transform.SetParent(parent, false);
            // Raised so the puffs don't slice through the ground at their centers.
            radius *= TornadoWidth;
            size *= TornadoWidth;
            outward *= TornadoWidth;
            dust.transform.localPosition = new Vector3(0f, size.x * 0.4f, 0f);
            var particles = dust.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(darkColor, lightColor);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = rate;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = radiusThickness;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // Unity requires every axis to use the same curve mode, so the fixed axes are 0-0 ranges.
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(orbit.x, orbit.y);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(outward.x, outward.y);

            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            // Fade in quickly, stay dense, then thin out at the end.
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // A billowing puff with a lumpy edge, lit from above: lighter on top and
        // darker underneath. Particle colors tint it.
        static Texture2D BuildPuffTexture()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float lumps = TilingValueNoise(u * 5f, v * 5f, 5, 5) * 0.6f + TilingValueNoise(u * 11f, v * 11f, 11, 11) * 0.4f;
                float detail = TilingValueNoise(u * 23f, v * 23f, 23, 23);

                float edge = 0.62f + (lumps - 0.5f) * 0.5f;
                float alpha = Mathf.Clamp01((edge - distance) / 0.22f) * Mathf.Lerp(0.75f, 1f, detail);
                float brightness = Mathf.Clamp01(0.72f + dy * 0.28f + (lumps - 0.5f) * 0.3f + (detail - 0.5f) * 0.15f);
                byte b = (byte)(brightness * 255f);
                pixels[y * size + x] = new Color32(b, b, b, (byte)(alpha * 255f));
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "DustPuff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true);

            string path = $"{TexturesDir}/DustPuff.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(texture, path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Chunks of wreckage spiraling up and out around the base of the funnel.
        static void BuildDebrisSwirl(Transform parent)
        {
            var debris = new GameObject("DebrisSwirl");
            debris.transform.SetParent(parent, false);
            var particles = debris.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * TornadoWidth, 1.6f * TornadoWidth);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.19f, 0.16f), new Color(0.4f, 0.38f, 0.35f));
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = 15f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6f * TornadoWidth;
            shape.radiusThickness = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Unity requires every axis to use the same curve mode, so the fixed axes are 0-0 ranges.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(4f * TornadoHeight, 10f * TornadoHeight);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(1.5f * TornadoWidth);

            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            spin.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

            var renderer = debris.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.sharedMaterial = LoadOrCreateMaterial("DebrisChunk",
                () => new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Streaks of rain that surround the camera. RainController slants them by car speed.
        static void BuildRain(Transform camera, CarController car)
        {
            var rain = new GameObject("Rain");
            rain.transform.SetParent(camera, false);
            var particles = rain.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(0.75f, 0.8f, 0.85f, 0.35f);
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = 800f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 12f, 25f);
            shape.scale = new Vector3(30f, 1f, 50f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = LoadOrCreateMaterial("Rain",
                () => new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath)));
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            SetReference(rain.AddComponent<RainController>(), "car", car);
        }

        static void BuildLightning(Light sun, Transform tornado)
        {
            var lightning = new GameObject("Lightning");
            var bolt = lightning.AddComponent<LineRenderer>();
            bolt.positionCount = 16;
            bolt.useWorldSpace = true;
            bolt.widthCurve = AnimationCurve.Linear(0f, 2.5f, 1f, 1f);
            bolt.numCornerVertices = 2;
            bolt.shadowCastingMode = ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            var material = LoadOrCreateMaterial("LightningBolt",
                () => new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            material.SetColor("_BaseColor", new Color(0.9f, 0.93f, 1f));
            EditorUtility.SetDirty(material);
            bolt.sharedMaterial = material;

            var controller = lightning.AddComponent<LightningController>();
            SetReference(controller, "sun", sun);
            SetReference(controller, "tornado", tornado);
            SetReference(controller, "bolt", bolt);
        }

        static void BuildScene(Scene scene, RoadSegment roadPrefab, CarController carPrefab, TornadoController tornadoPrefab)
        {
            RenderSettings.skybox = CreateSkyMaterial();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 250f;

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.9f, 0.92f, 1f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab.gameObject, scene);
            var tornado = (GameObject)PrefabUtility.InstantiatePrefab(tornadoPrefab.gameObject, scene);
            tornado.transform.position = new Vector3(0f, 0f, TornadoStartGap);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            // Far enough to draw the storm cloud deck out to the horizon.
            camera.farClipPlane = 1600f;
            cameraObject.AddComponent<AudioListener>();
            BuildRain(cameraObject.transform, car.GetComponent<CarController>());
            BuildLightning(light, tornado.transform);
            SetReference(cameraObject.AddComponent<ChaseCamera>(), "target", car.GetComponent<CarController>());

            var road = new GameObject("Road").AddComponent<RoadGenerator>();
            SetReference(road, "segmentPrefab", roadPrefab);
            SetReference(road, "target", car.transform);

            var game = new GameObject("Game");
            var tracker = game.AddComponent<ChaseTracker>();
            SetReference(tracker, "car", car.GetComponent<CarController>());
            SetReference(tracker, "tornado", tornado.GetComponent<TornadoController>());
            SetReference(game.AddComponent<RunManager>(), "tracker", tracker);
            var hud = game.AddComponent<DebugHud>();
            SetReference(hud, "tracker", tracker);
            SetReference(hud, "car", car.GetComponent<CarController>());
            SetReference(hud, "tornado", tornado.GetComponent<TornadoController>());
            BuildHud(tracker, cameraObject.GetComponent<ChaseCamera>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // Portrait HUD: a vertical chase meter on the right edge and a camera view toggle
        // in the top-right corner. ChaseMeter draws the meter's fill at runtime from
        // ChaseTracker's zone limits.
        static void BuildHud(ChaseTracker tracker, ChaseCamera chaseCamera)
        {
            var rounded = BuildUiSprite("UIRounded", RoundedPixel, border: 31);
            var glow = BuildUiSprite("UIGlow", GlowPixel, border: 31);
            var cameraIcon = BuildUiSprite("UICameraIcon", CameraIconPixel, border: 0, size: 128);

            var canvasObject = new GameObject("Hud", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // Chase meter
            var meter = UiObject("ChaseMeter", canvasObject.transform);
            SetRect(meter, anchor: new Vector2(1f, 0.5f), pivot: new Vector2(1f, 0.5f),
                position: new Vector2(-72f, 150f), size: new Vector2(40f, 820f));
            var pulseRoot = UiObject("PulseRoot", meter);
            Stretch(pulseRoot, padding: 0f);

            var warningGlow = UiImage("WarningGlow", pulseRoot, new Color(1f, 1f, 1f, 0f), glow);
            Stretch(warningGlow, padding: 44f);
            Stretch(UiImage("Frame", pulseRoot, new Color(0f, 0f, 0f, 0.55f), rounded), padding: 8f);
            var fill = UiObject("Fill", pulseRoot);
            Stretch(fill, padding: 0f);
            var fillImage = fill.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            fillImage.raycastTarget = false;

            var marker = UiObject("Marker", pulseRoot);
            SetRect(marker, anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                position: Vector2.zero, size: new Vector2(72f, 8f));
            var markerGlow = UiImage("Glow", marker, Color.white, glow);
            SetRect(markerGlow, anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
                position: Vector2.zero, size: new Vector2(150f, 48f));
            SetRect(UiImage("Outline", marker, new Color(0f, 0f, 0f, 0.7f), rounded), anchor: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), position: Vector2.zero, size: new Vector2(78f, 14f));
            SetRect(UiImage("Line", marker, Color.white, rounded), anchor: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), position: Vector2.zero, size: new Vector2(72f, 8f));

            var chaseMeter = meter.gameObject.AddComponent<ChaseMeter>();
            SetReference(chaseMeter, "tracker", tracker);
            SetReference(chaseMeter, "pulseRoot", pulseRoot);
            SetReference(chaseMeter, "fill", fillImage);
            SetReference(chaseMeter, "warningGlow", warningGlow.GetComponent<UnityEngine.UI.Image>());
            SetReference(chaseMeter, "marker", marker);
            SetReference(chaseMeter, "markerGlow", markerGlow.GetComponent<UnityEngine.UI.Image>());

            // Camera view toggle
            var viewButton = UiImage("ViewButton", canvasObject.transform, new Color(0f, 0f, 0f, 0.45f), rounded);
            SetRect(viewButton, anchor: Vector2.one, pivot: Vector2.one, position: new Vector2(-40f, -60f), size: new Vector2(112f, 112f));
            viewButton.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var button = viewButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, chaseCamera.ToggleView);
            SetRect(UiImage("Icon", viewButton, new Color(1f, 1f, 1f, 0.9f), cameraIcon), anchor: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), position: Vector2.zero, size: new Vector2(64f, 64f));
        }

        static RectTransform UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform UiImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var rect = UiObject(name, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        // Generates a white UI sprite from a per-pixel alpha function and imports it as a
        // sprite asset. A nonzero border makes it 9-sliceable.
        static Sprite BuildUiSprite(string name, System.Func<float, float, float> alpha, int border, int size = 64)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size)) * 255f));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            string path = $"{UiDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // A circle that 9-slices into rounded rectangles and capsules.
        static float RoundedPixel(float u, float v) =>
            Mathf.Clamp01((0.5f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f))) * 64f + 0.5f);

        // Soft falloff from the center, for glows.
        static float GlowPixel(float u, float v)
        {
            float t = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            return t * t;
        }

        // A simple camera: a rounded body with a viewfinder bump and a ring-shaped lens.
        static float CameraIconPixel(float u, float v)
        {
            const float aa = 128f;
            float body = RoundedBox(u, v, new Vector2(0.5f, 0.45f), new Vector2(0.4f, 0.26f), 0.08f);
            float bump = RoundedBox(u, v, new Vector2(0.5f, 0.74f), new Vector2(0.13f, 0.06f), 0.03f);
            float shape = Mathf.Clamp01(-Mathf.Min(body, bump) * aa + 0.5f);
            float lensDistance = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.45f));
            float ring = Mathf.Clamp01((Mathf.Abs(lensDistance - 0.15f) - 0.045f) * -aa + 0.5f);
            return shape * (1f - ring);
        }

        // Signed distance to a rounded box: negative inside.
        static float RoundedBox(float u, float v, Vector2 center, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(u - center.x), Mathf.Abs(v - center.y)) - halfSize + new Vector2(radius, radius);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-padding, -padding);
            rect.offsetMax = new Vector2(padding, padding);
        }

        static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material,
            PrimitiveType type = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        static void Flat(GameObject part) => part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        static void SetReference(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
