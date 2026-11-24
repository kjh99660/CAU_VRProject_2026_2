using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRWebSwing.Editor
{
    // Edit-mode preview of the in-order corridor (cities + gimmicks laid out exactly as Play mode will), for balancing.
    // Preview objects are HideFlags.DontSave: never written to the scene file, never in builds, removed before Play.
    // The scene's pooled segments are hidden in the Scene view only (SceneVisibilityManager), so the scene file is unchanged.
    [InitializeOnLoad]
    public static class EndlessLayoutPreview
    {
        const string RootName = "Endless Layout Preview (editor only)";
        const string EnabledKey = "VRWebSwing.EndlessLayoutPreview.Enabled";
        sealed class Preview
        {
            public GameObject root;
            public readonly List<(Vector3 position, string label)> labels = new List<(Vector3, string)>();
            public Vector3 wallLineStart, wallLineEnd;
            public bool hasWallLine;
        }
        static readonly Dictionary<EndlessCity, Preview> previews = new Dictionary<EndlessCity, Preview>();
        static readonly List<EndlessCity.LayoutSlot> layout = new List<EndlessCity.LayoutSlot>();
        static bool refreshQueued;

        static bool Enabled { get => EditorPrefs.GetBool(EnabledKey, true); set => EditorPrefs.SetBool(EnabledKey, value); }

        static EndlessLayoutPreview()
        {
            EditorApplication.delayCall += QueueRefreshAll;
            EditorSceneManager.sceneOpened += (scene, mode) => QueueRefreshAll();
            EditorSceneManager.sceneClosing += (scene, removing) => ClearScene(scene);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
            PrefabStage.prefabSaved += prefab => QueueRefreshAll();
            Undo.postprocessModifications += OnModifications;
            Undo.undoRedoPerformed += QueueRefreshAll;
            SceneView.duringSceneGui += DrawLabels;
        }

        [MenuItem("CONTEXT/EndlessCity/Refresh Layout Preview")]
        static void RefreshFromMenu(MenuCommand command) { Enabled = true; Refresh((EndlessCity)command.context); }
        [MenuItem("CONTEXT/EndlessCity/Refresh Layout Preview", true)]
        static bool CanRefresh(MenuCommand command) => !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("CONTEXT/EndlessCity/Clear Layout Preview")]
        static void ClearFromMenu(MenuCommand command) { Enabled = false; ClearAll(); }
        [MenuItem("CONTEXT/EndlessCity/Clear Layout Preview", true)]
        static bool CanClear(MenuCommand command) => !EditorApplication.isPlayingOrWillChangePlaymode;

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // DontSave objects survive the Play-mode scene reload, so remove them first or they would overlap the live corridor.
            if (change == PlayModeStateChange.ExitingEditMode) ClearAll();
            else if (change == PlayModeStateChange.EnteredEditMode) QueueRefreshAll();
        }

        static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            foreach (var modification in modifications)
                if (modification.currentValue?.target is EndlessCity) { QueueRefreshAll(); break; }
            return modifications;
        }

        static void QueueRefreshAll()
        {
            if (refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += () => { refreshQueued = false; RefreshAll(); };
        }

        static void RefreshAll()
        {
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var city in Object.FindObjectsByType<EndlessCity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!EditorUtility.IsPersistent(city)) Refresh(city);
        }

        static void Refresh(EndlessCity city)
        {
            if (!city || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Clear(city);
            if (!city.course || !city.course.start || !city.segmentPrefab) return;
            city.GetOrderedLayout(layout, out int backWallSlot);

            var preview = new Preview { root = new GameObject(RootName) };
            SceneManager.MoveGameObjectToScene(preview.root, city.gameObject.scene);
            float startZ = city.course.start.position.z;
            for (int i = 0; i < layout.Count; ++i)
            {
                var slot = layout[i];
                bool isCity = slot.kind < 0;
                GameObject prefab = isCity ? city.segmentPrefab : city.gimmicks[slot.kind].prefab;
                float z = startZ + (float)slot.start + (isCity ? 0 : city.gimmicks[slot.kind].spaceBefore);
                // Plain copies, not prefab instances: edit the prefab assets (or the Endless City values), then refresh.
                var copy = Object.Instantiate(prefab, preview.root.transform);
                copy.name = $"{i:00} {(isCity ? "City" : prefab.name)}";
                copy.transform.position = new Vector3(0, 0, z);
                if (!isCity) preview.labels.Add((new Vector3(0, 22, z), $"{i:00} {prefab.name}  ({slot.length:0.#} m)"));
                if (i == backWallSlot)
                {
                    float end = startZ + (float)(slot.start + slot.length);
                    preview.wallLineStart = new Vector3(-12, 0, end); preview.wallLineEnd = new Vector3(12, 0, end);
                    preview.hasWallLine = true;
                }
            }
            foreach (var t in preview.root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.DontSave;
                foreach (var component in t.GetComponents<Component>()) if (component) component.hideFlags = HideFlags.DontSave;
            }
            previews[city] = preview;
            SetSegmentsHidden(city, true);
            SceneView.RepaintAll();
        }

        static void SetSegmentsHidden(EndlessCity city, bool hidden)
        {
            if (city.segments == null) return;
            foreach (var segment in city.segments)
            {
                if (!segment) continue;
                if (hidden) SceneVisibilityManager.instance.Hide(segment.gameObject, true);
                else SceneVisibilityManager.instance.Show(segment.gameObject, true);
            }
        }

        static void Clear(EndlessCity city)
        {
            if (!previews.TryGetValue(city, out var preview)) return;
            if (preview.root) Object.DestroyImmediate(preview.root);
            previews.Remove(city);
            if (city) SetSegmentsHidden(city, false);
        }

        static void ClearScene(Scene scene)
        {
            var cities = new List<EndlessCity>(previews.Keys);
            foreach (var city in cities)
                if (!city || city.gameObject.scene == scene) Clear(city);
        }

        static void ClearAll()
        {
            var cities = new List<EndlessCity>(previews.Keys);
            foreach (var city in cities) Clear(city);
            // A preview left over from a crash or an older domain is found by name.
            for (int s = 0; s < SceneManager.sceneCount; ++s)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == RootName && (root.hideFlags & HideFlags.DontSave) != 0) Object.DestroyImmediate(root);
            }
        }

        static void DrawLabels(SceneView view)
        {
            if (previews.Count == 0) return;
            foreach (var preview in previews.Values)
            {
                foreach (var (position, label) in preview.labels) Handles.Label(position, label, EditorStyles.whiteLargeLabel);
                if (!preview.hasWallLine) continue;
                Handles.color = Color.red;
                Handles.DrawLine(preview.wallLineStart, preview.wallLineEnd, 4);
                Handles.Label(preview.wallLineEnd + Vector3.up * 2, "Back wall starts after here", EditorStyles.whiteLargeLabel);
            }
        }
    }
}
