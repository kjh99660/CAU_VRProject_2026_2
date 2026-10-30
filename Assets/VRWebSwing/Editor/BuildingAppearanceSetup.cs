using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRWebSwing.Editor
{
    public static class BuildingAppearanceSetup
    {
        const string TexturePath = "Assets/VRWebSwing/Art/Buildings/BILUDING.png";
        const string ArtPath = "Assets/VRWebSwing/Art/Buildings/";
        const string SegmentPath = "Assets/VRWebSwing/Prefabs/EndlessCitySegment.prefab";
        static readonly string[] Names = { "Original", "Small", "Medium", "Tall" };
        static readonly string[] Variants = { "Original", "Silver", "Warm", "Slate" };
        static readonly Vector3[] Sizes = { new Vector3(7, 82, 11), new Vector3(6, 72, 10), new Vector3(8, 90, 13), new Vector3(10, 110, 16) };

        [MenuItem("VR Web Swing/Apply Building Texture and Variants")]
        public static void ApplyToDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(SegmentPath)) throw new InvalidOperationException("Create the endless city first.");
            EnsureSurfaceLayerName();
            ImportTexture(TexturePath);
            var shaders = new System.Collections.Generic.HashSet<Shader>();
            var prefabs = new GameObject[Names.Length];
            for (int i = 0; i < Names.Length; ++i)
            {
                var shader = Shader.Find("VRWebSwing/Building Facade " + Variants[i]);
                if (!shader) throw new InvalidOperationException("Facade variant shader missing: " + Variants[i]);
                shaders.Add(shader);
                string texturePath = i == 0 ? TexturePath : ArtPath + "BILUDING_" + Variants[i] + ".png";
                ImportTexture(texturePath);
                string materialPath = ArtPath + "BuildingFacade_" + Names[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(shader) { name = "Building Facade " + Names[i], enableInstancing = true };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                EditorUtility.SetDirty(material);
                prefabs[i] = EnsureBuilding(Names[i], Sizes[i], material);
            }
            UpdateSegment(prefabs);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(SwingSceneBuilder.ScenePath);
            var city = UnityEngine.Object.FindFirstObjectByType<EndlessCity>();
            if (!city) throw new InvalidOperationException("EndlessCity missing from demo.");
            if (city.course.motor.rope.attachMask.value == 0) city.course.motor.rope.attachMask = 1 << 8;
            foreach (var segment in city.segments)
            {
                int count = 0;
                var textures = new System.Collections.Generic.HashSet<Texture>();
                foreach (var renderer in segment.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.name.StartsWith("Facade Tower ", StringComparison.Ordinal) && renderer.sharedMaterial && shaders.Contains(renderer.sharedMaterial.shader))
                    { ++count; textures.Add(renderer.sharedMaterial.GetTexture("_BaseMap")); }
                if (count != 6 || textures.Count != 4) throw new InvalidOperationException("Expected 6 towers / 4 facade textures per segment; got " + count + "/" + textures.Count);
            }
            EditorSceneManager.SaveScene(scene);
            foreach (var shader in shaders) CheckShader(shader);
            Debug.Log("Four facade images, shaders, materials and prefabs applied; 6 towers per pooled segment.");
        }
        static void EnsureSurfaceLayerName()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tags = new SerializedObject(assets[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(8);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = "SwingSurface";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        static void ImportTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Facade image missing: " + path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None; importer.mipmapEnabled = true;
            importer.isReadable = false; importer.wrapMode = TextureWrapMode.Repeat;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
        }
        static GameObject EnsureBuilding(string name, Vector3 size, Material material)
        {
            string path = "Assets/VRWebSwing/Prefabs/Building_" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // Change the source renderer only; preserve the user's scale, geometry and collider edits.
                    contents.GetComponent<MeshRenderer>().sharedMaterial = material;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building " + name; building.layer = 8;
            building.transform.localScale = size;
            var renderer = building.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(building, path);
            UnityEngine.Object.DestroyImmediate(building);
            return prefab;
        }
        static void UpdateSegment(GameObject[] prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(SegmentPath);
            try
            {
                foreach (string name in new[] { "Left Building", "Right Building" })
                {
                    var old = root.transform.Find(name);
                    if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                // Keep the central 12m-wide swing corridor and the original overhead anchor unchanged.
                int[] types = { 1, 2, 3, 0, 2, 3 };
                Vector2[] positions = { new Vector2(-9, 12), new Vector2(10, 12), new Vector2(-23, 8),
                    new Vector2(21, 18), new Vector2(-37, 18), new Vector2(37, 5) };
                for (int i = 0; i < types.Length; ++i)
                {
                    string name = "Facade Tower " + i;
                    var tower = root.transform.Find(name);
                    if (tower && PrefabUtility.GetCorrespondingObjectFromOriginalSource(tower.gameObject) != prefabs[types[i]])
                    {
                        UnityEngine.Object.DestroyImmediate(tower.gameObject); tower = null;
                    }
                    if (!tower)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[types[i]], root.transform);
                        tower = instance.transform; tower.name = name;
                    }
                    Vector3 scale = prefabs[types[i]].transform.localScale;
                    tower.localScale = scale;
                    tower.localPosition = new Vector3(positions[i].x, -50 + scale.y * 0.5f, positions[i].y);
                }
                PrefabUtility.SaveAsPrefabAsset(root, SegmentPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void CheckShader(Shader shader)
        {
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new InvalidOperationException(message.message);
        }
        [MenuItem("VR Web Swing/Render Building Preview")]
        public static void RenderPreview()
        {
            ApplyToDemo();
            var city = UnityEngine.Object.FindFirstObjectByType<EndlessCity>();
            var distanceText = city.course.motor.GetComponent<SwingHud>().distanceText.gameObject;
            bool wasActive = distanceText.activeSelf; distanceText.SetActive(false);
            var cameraObject = new GameObject("Building Forest Preview");
            var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0, 16, -14); camera.transform.LookAt(new Vector3(0, 26, 65));
            camera.fieldOfView = 65; camera.farClipPlane = 220;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/BuildingForest-Preview.png", pixels.EncodeToPNG());
                CheckShader(Shader.Find("VRWebSwing/Building Facade"));
                Debug.Log("Desktop building preview rendered to Logs/BuildingForest-Preview.png");
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; distanceText.SetActive(wasActive);
                UnityEngine.Object.DestroyImmediate(cameraObject); target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
