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
        const string PrefabsDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/Game.unity";

        const float RoadWidth = 10f;
        const float ShoulderWidth = 60f;
        const float DashLength = 3f;
        const float DashSpacing = 10f;
        static readonly Color FogColor = new(0.42f, 0.45f, 0.5f);

        [MenuItem("Tornado Chaser/Build Prototype Scene")]
        static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        public static void Build()
        {
            Directory.CreateDirectory(MaterialsDir);
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
            BuildScene(scene, roadPrefab, carPrefab);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
        }

        static Material CreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
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

        static void BuildScene(Scene scene, RoadSegment roadPrefab, CarController carPrefab)
        {
            RenderSettings.skybox = null;
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

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            SetReference(cameraObject.AddComponent<ChaseCamera>(), "target", car.GetComponent<CarController>());

            var road = new GameObject("Road").AddComponent<RoadGenerator>();
            SetReference(road, "segmentPrefab", roadPrefab);
            SetReference(road, "target", car.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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
