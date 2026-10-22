using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRWebSwing.Editor
{
    public static class EndlessSceneBuilder
    {
        public const string ValidationScenePath = "Assets/VRWebSwing/Scenes/WebSwingValidation.unity";
        const string PrefabPath = "Assets/VRWebSwing/Prefabs/EndlessCitySegment.prefab";

        [MenuItem("VR Web Swing/Upgrade to Endless City")]
        public static void Upgrade()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(SwingSceneBuilder.ScenePath)) { SwingSceneBuilder.Generate(); return; }
            var scene = EditorSceneManager.OpenScene(SwingSceneBuilder.ScenePath);
            var motor = UnityEngine.Object.FindFirstObjectByType<SwingMotor>();
            if (!motor) throw new InvalidOperationException("SwingMotor missing.");
            var course = motor.course;
            if (!course.IsEndless && !File.Exists(ValidationScenePath))
                if (!AssetDatabase.CopyAsset(SwingSceneBuilder.ScenePath, ValidationScenePath))
                    throw new IOException("Could not preserve the original course validation scene.");
            var environment = GameObject.Find("Graybox Course");
            if (!environment) throw new InvalidOperationException("Graybox Course root missing.");
            for (int i = environment.transform.childCount - 1; i >= 0; --i)
            {
                GameObject child = environment.transform.GetChild(i).gameObject;
                string name = child.name;
                if (name.StartsWith("Landing ", StringComparison.Ordinal) || name.StartsWith("Attachable Building ", StringComparison.Ordinal)
                    || name.StartsWith("Overhead Target ", StringComparison.Ordinal) || name.StartsWith("Checkpoint ", StringComparison.Ordinal)
                    || name.StartsWith("Respawn ", StringComparison.Ordinal) || name.StartsWith("Gate ", StringComparison.Ordinal)
                    || name.StartsWith("Fall Volume", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(child);
            }
            var city = course.endlessCity;
            if (!city) city = new GameObject("Endless City").AddComponent<EndlessCity>();
            city.course = course; city.startArea = environment.transform; city.segmentPrefab = EnsureSegmentPrefab();
            course.endlessCity = city; course.checkpoints = Array.Empty<Transform>(); course.respawnPoints = Array.Empty<Transform>();
            int count = Mathf.Clamp(city.poolSize, 4, 32);
            if (city.segments == null || city.segments.Length != count)
            {
                if (city.segments != null)
                    foreach (var old in city.segments) if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                city.segments = new Transform[count];
                for (int i = 0; i < count; ++i)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(city.segmentPrefab);
                    instance.name = "City Segment " + i;
                    instance.transform.SetParent(city.transform, false);
                    instance.transform.localPosition = Vector3.forward * (i * city.segmentLength);
                    instance.SetActive(i < count - 1);
                    city.segments[i] = instance.transform;
                }
            }
            var hud = motor.GetComponent<SwingHud>();
            Transform legacyHud = motor.input.head.Find("World HUD");
            if (legacyHud) UnityEngine.Object.DestroyImmediate(legacyHud.gameObject);
            if (!hud.distanceText)
            {
                var text = new GameObject("Distance HUD").AddComponent<TextMesh>();
                text.transform.SetParent(motor.input.head, false);
                text.transform.localPosition = new Vector3(0.95f, 0.58f, 1.5f);
                text.characterSize = 0.025f; text.fontSize = 64;
                text.anchor = TextAnchor.UpperRight; text.alignment = TextAlignment.Right;
                text.text = "DISTANCE\n0.0 m\nBEST 0.0 m | LAST 0.0 m";
                hud.distanceText = text;
            }
            motor.input.head.GetComponent<Camera>().farClipPlane = 180;
            EditorUtility.SetDirty(city); EditorUtility.SetDirty(course); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Endless city configured; fixed pool=" + count + ", original course=" + ValidationScenePath);
        }
        static GameObject EnsureSegmentPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing) return existing;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRWebSwing/Settings/Anchor.mat");
            var root = new GameObject("Endless City Segment");
            Block("Left Building", root.transform, new Vector3(-9, -14, 12), new Vector3(6, 72, 12), material);
            Block("Right Building", root.transform, new Vector3(9, -14, 12), new Vector3(6, 72, 12), material);
            Block("Overhead Anchor", root.transform, new Vector3(0, 22, 12), new Vector3(12, 1, 5), material);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
        static void Block(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = name; block.layer = 8;
            block.transform.SetParent(parent, false); block.transform.localPosition = position; block.transform.localScale = scale;
            var renderer = block.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
    }
}
