using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using UnityEngine.SceneManagement;

namespace VRWebSwing.Editor
{
    public static class SwingSceneBuilder
    {
        public const string ScenePath = "Assets/VRWebSwing/Scenes/WebSwingCourse.unity";
        const string Root = "Assets/VRWebSwing/";
        [MenuItem("VR Web Swing/Upgrade Comfort in Test Course")]
        public static void UpgradeComfort()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) { Generate(); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var motor = Object.FindFirstObjectByType<SwingMotor>();
            if (!motor) throw new System.InvalidOperationException("SwingMotor missing from test course.");
            var comfort = motor.GetComponent<SwingComfort>();
            if (!comfort) comfort = motor.gameObject.AddComponent<SwingComfort>();
            comfort.motor = motor; comfort.input = motor.input;
            if (!comfort.vignette) comfort.vignette = motor.input.head.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>(true);
            if (!comfort.vignette) throw new System.InvalidOperationException("Import/install the existing XRI Starter Assets vignette first.");
            ConfigureVignetteMaterial(comfort);
            EditorUtility.SetDirty(comfort);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Comfort comparison configured in " + ScenePath);
        }
        [MenuItem("VR Web Swing/Create or Open Test Course")]
        public static void CreateOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Generate();
        }
        // Batch entry point: existing scenes/assets are never overwritten.
        public static void Generate()
        {
            if (File.Exists(ScenePath)) { Debug.Log("WebSwing course already exists; generation skipped."); return; }
            foreach (string folder in new[] { "Settings", "Prefabs", "Scenes" }) Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            var settings = ScriptableObject.CreateInstance<SwingSettings>();
            string settingsPath = Root + "Settings/DefaultSwingSettings.asset";
            if (File.Exists(settingsPath)) { Object.DestroyImmediate(settings); settings = AssetDatabase.LoadAssetAtPath<SwingSettings>(settingsPath); }
            else AssetDatabase.CreateAsset(settings, settingsPath);
            Material gray = MaterialAsset("Gray", new Color(0.38f, 0.4f, 0.42f));
            Material pale = MaterialAsset("Anchor", new Color(0.65f, 0.68f, 0.72f));
            Material cyan = MaterialAsset("Web", Color.cyan);
            Material gold = MaterialAsset("Checkpoint", new Color(0.9f, 0.65f, 0.12f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WebSwingCourse";
            RenderSettings.ambientLight = new Color(0.7f, 0.7f, 0.7f);
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var environment = new GameObject("Graybox Course");
            Cube("Start Platform", new Vector3(0, 9.5f, 0), new Vector3(12, 1, 12), gray, environment.transform);
            // Layer 8 is local to the generated scene; do not alter project layer names.
            var block = Cube("Anchor Block", Vector3.zero, Vector3.one, pale, null); block.layer = 8;
            string prefabPath = Root + "Prefabs/AnchorBlock.prefab";
            if (!File.Exists(prefabPath)) PrefabUtility.SaveAsPrefabAsset(block, prefabPath);
            Object.DestroyImmediate(block);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            for (int i = 0; i < 3; ++i)
            {
                float z = 12 + i * 20;
                var anchor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                anchor.name = "Attachable Building " + (i + 1); anchor.transform.SetParent(environment.transform);
                anchor.transform.position = new Vector3(i % 2 == 0 ? 8 : -8, 16 + i * 2, z);
                anchor.transform.localScale = new Vector3(5, 16 + i * 4, 9);
                // Broad overhead targets provide a forgiving first attach and alternating hand route.
                var beam = Cube("Overhead Target " + (i + 1), new Vector3(0, 22 + i * 2, z),
                    new Vector3(14, 1, 5), pale, environment.transform); beam.layer = 8;
                Cube("Landing " + (i + 1), new Vector3(0, 9.5f, 18 + i * 20), new Vector3(12, 1, 10), gray, environment.transform);
            }
            var rig = new GameObject("Swing XR Origin"); rig.layer = 2; // Ignore Raycast: aiming / ground probes exclude own capsule.
            var controller = rig.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = 0.3f; controller.center = new Vector3(0, 0.9f, 0);
            controller.skinWidth = 0.02f; controller.stepOffset = 0.2f; controller.minMoveDistance = 0;
            var visualRoot = Child("Interpolated Tracking Group", rig.transform);
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(visualRoot, false);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false); cameraObject.transform.localPosition = Vector3.up * 1.65f;
            var camera = cameraObject.AddComponent<Camera>(); camera.nearClipPlane = 0.05f; camera.farClipPlane = 250;
            cameraObject.AddComponent<AudioListener>();
            var origin = rig.AddComponent<XROrigin>(); origin.Camera = camera; origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor; origin.CameraYOffset = 1.65f;
            Transform left = Child("Left Hand", offset.transform), right = Child("Right Hand", offset.transform);
            var input = rig.AddComponent<SwingInput>();
            input.head = camera.transform; input.leftHand = left; input.rightHand = right;
            input.headTracking = Tracking(cameraObject, "<XRHMD>/centerEye");
            input.leftTracking = Tracking(left.gameObject, "<XRController>{LeftHand}/device");
            input.rightTracking = Tracking(right.gameObject, "<XRController>{RightHand}/device");
            var rope = rig.AddComponent<WebRope>(); rope.settings = settings; rope.input = input; rope.attachMask = 1 << 8;
            var lineObject = new GameObject("Web Line"); lineObject.transform.SetParent(rig.transform, false);
            rope.line = lineObject.AddComponent<LineRenderer>(); rope.line.positionCount = 2;
            rope.line.useWorldSpace = true; rope.line.startWidth = rope.line.endWidth = 0.025f;
            rope.line.sharedMaterial = cyan; rope.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rope.line.receiveShadows = false; rope.line.enabled = false;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.name = "Aim Valid Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>()); marker.transform.localScale = Vector3.one * 0.22f;
            marker.GetComponent<Renderer>().sharedMaterial = gold; rope.aimMarker = marker.transform;
            var course = new GameObject("Course Progress").AddComponent<SwingCourse>();
            var motor = rig.AddComponent<SwingMotor>(); motor.settings = settings; motor.input = input; motor.rope = rope; motor.course = course;
            var smoothing = rig.AddComponent<SwingVisualSmoothing>(); smoothing.motor = motor; smoothing.visualRoot = visualRoot;
            const string vignettePath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";
            var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vignettePath);
            if (vignettePrefab)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab);
                instance.transform.SetParent(camera.transform, false);
                var comfort = rig.AddComponent<SwingComfort>(); comfort.motor = motor; comfort.input = input;
                comfort.vignette = instance.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>();
                ConfigureVignetteMaterial(comfort);
            }
            course.motor = motor; course.start = Child("Start Respawn", environment.transform);
            course.start.position = new Vector3(0, 10.1f, 0); rig.transform.position = course.start.position;
            course.checkpoints = new Transform[3]; course.respawnPoints = new Transform[3];
            for (int i = 0; i < 3; ++i)
            {
                float z = 18 + i * 20;
                course.checkpoints[i] = Child("Checkpoint " + (i + 1), environment.transform);
                course.checkpoints[i].position = new Vector3(0, 12, z);
                course.respawnPoints[i] = Child("Respawn " + (i + 1), environment.transform);
                course.respawnPoints[i].position = new Vector3(0, 10.1f, z);
                for (int side = -1; side <= 1; side += 2)
                    Cube("Gate " + (i + 1), new Vector3(side * 4, 13, z), new Vector3(0.2f, 6, 0.2f), gold, environment.transform);
                Cube("Gate Top " + (i + 1), new Vector3(0, 16, z), new Vector3(8, 0.2f, 0.2f), gold, environment.transform);
                var label = Child("Checkpoint Label " + (i + 1), environment.transform).gameObject.AddComponent<TextMesh>();
                label.transform.position = new Vector3(-1, 15, z - 0.2f); label.text = "CP " + (i + 1);
                label.characterSize = 0.35f; label.fontSize = 48;
            }
            var failure = Cube("Fall Volume (height fallback also active)", new Vector3(0, -10, 30), new Vector3(160, 4, 140), gray, environment.transform);
            failure.GetComponent<Collider>().isTrigger = true; failure.GetComponent<Renderer>().enabled = false;
            var hud = rig.AddComponent<SwingHud>(); hud.motor = motor; hud.course = course; hud.rope = rope; hud.input = input;
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Replace the template's enabled scenes, retaining them as disabled entries for recovery.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var old in EditorBuildSettings.scenes)
                if (old.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(old.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); Debug.Log("WebSwing course generated: " + ScenePath);
            EndlessSceneBuilder.Upgrade();
        }
        static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        static void ConfigureVignetteMaterial(SwingComfort comfort)
        {
            const string path = Root + "Settings/ComfortVignette.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("VRWebSwing/ComfortVignette");
            if (!shader) throw new System.InvalidOperationException("Comfort URP shader missing.");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    throw new System.InvalidOperationException("Comfort shader: " + message.message);
            if (!material)
            {
                material = new Material(shader) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            comfort.vignette.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        static TrackedPoseDriver Tracking(GameObject target, string path)
        {
            var driver = target.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, path + "Position", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, path + "Rotation", expectedControlType: "Quaternion"));
            string device = path.Substring(0, path.LastIndexOf('/'));
            driver.trackingStateInput = new InputActionProperty(new InputAction("Tracking", InputActionType.Value, device + "/trackingState", expectedControlType: "Integer"));
            driver.enabled = false; // enabled only once an HMD is detected; desktop never writes tracked poses
            return driver;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(parent, false); cube.transform.position = position; cube.transform.localScale = scale;
            var renderer = cube.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return cube;
        }
        static Material MaterialAsset(string name, Color color)
        {
            string path = Root + "Settings/" + name + ".mat";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Material>(path);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = color;
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
