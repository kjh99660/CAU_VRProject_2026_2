using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRWebSwing.Editor
{
    public static class DangerVignetteSetup
    {
        const string MaterialPath = "Assets/VRWebSwing/Settings/DangerVignette.mat";
        const string XriVignettePath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";
        const string ObjectName = "Danger Vignette";

        [MenuItem("VR Web Swing/Add Danger Vignette")]
        public static void AddToCourse()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(SwingSceneBuilder.ScenePath)) throw new System.InvalidOperationException("Create the test course first.");
            var scene = EditorSceneManager.OpenScene(SwingSceneBuilder.ScenePath);
            var motor = Object.FindFirstObjectByType<SwingMotor>();
            if (!motor) throw new System.InvalidOperationException("SwingMotor missing from test course.");
            var city = Object.FindFirstObjectByType<EndlessCity>();
            if (!city) throw new System.InvalidOperationException("EndlessCity missing; run Upgrade to Endless City first.");

            // Reuse XRI's head-locked hemisphere mesh: the standard VR vignette geometry, already used by the comfort vignette.
            var xri = AssetDatabase.LoadAssetAtPath<GameObject>(XriVignettePath);
            var xriFilter = xri ? xri.GetComponent<MeshFilter>() : null;
            var mesh = xriFilter ? xriFilter.sharedMesh : null;
            if (!mesh) throw new System.InvalidOperationException("XRI Starter Assets TunnelingVignette mesh missing.");

            Transform head = motor.input.head;
            Transform existing = head.Find(ObjectName);
            GameObject go = existing ? existing.gameObject : new GameObject(ObjectName);
            go.transform.SetParent(head, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            var filter = go.GetComponent<MeshFilter>();
            if (!filter) filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            if (!renderer) renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;

            var vignette = go.GetComponent<DangerVignette>();
            if (!vignette) vignette = go.AddComponent<DangerVignette>();
            vignette.city = city; vignette.mask = renderer;
            EditorUtility.SetDirty(vignette);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Danger vignette added under " + head.name + " and linked to " + city.name + ".");
        }

        static Material EnsureMaterial()
        {
            var shader = Shader.Find("VRWebSwing/DangerVignette");
            if (!shader) throw new System.InvalidOperationException("DangerVignette shader missing.");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new System.InvalidOperationException("Danger vignette shader: " + message.message);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }
            return material;
        }
    }
}
