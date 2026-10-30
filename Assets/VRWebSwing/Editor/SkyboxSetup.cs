using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRWebSwing.Editor
{
    public static class SkyboxSetup
    {
        const string TexturePath = "Assets/VRWebSwing/Art/Skybox/SKYBOX.jpg";
        public const string MaterialPath = "Assets/VRWebSwing/Art/Skybox/CloudSkybox.mat";

        [MenuItem("VR Web Swing/Apply Image Skybox")]
        public static void ApplyToDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(SwingSceneBuilder.ScenePath);
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Sky photograph is missing: " + TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var shader = Shader.Find("VRWebSwing/Image Skybox");
            if (!shader) throw new InvalidOperationException("Image Skybox shader was not imported.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "Cloud Skybox", enableInstancing = true };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            RenderSettings.skybox = material;
            var motor = UnityEngine.Object.FindFirstObjectByType<SwingMotor>();
            if (motor) motor.input.head.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(material); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new InvalidOperationException(message.message);
            Debug.Log("Image skybox applied: " + MaterialPath + ". Source is a mirrored photograph, not a captured 360 panorama.");
        }

        // Optional desktop render verification; does not require or simulate an HMD.
        public static void RenderPreview()
        {
            ApplyToDemo();
            var shader = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath).shader;
            var cameraObject = new GameObject("Skybox Preview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = 0;
            camera.transform.rotation = Quaternion.Euler(-12, 0, 0);
            var target = new RenderTexture(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply(); RenderTexture.active = previous;
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/Skybox-Preview.png", pixels.EncodeToPNG());
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        throw new InvalidOperationException(message.message);
                Debug.Log("Desktop skybox render saved to Logs/Skybox-Preview.png");
            }
            finally
            {
                camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
