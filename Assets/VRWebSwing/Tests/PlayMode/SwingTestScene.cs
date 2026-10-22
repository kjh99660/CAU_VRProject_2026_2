namespace VRWebSwing.Tests
{
    public static class SwingTestScene
    {
        public static void LoadValidation()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/VRWebSwing/Scenes/WebSwingValidation.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            NUnit.Framework.Assert.Ignore("Legacy course fixture is loaded directly in the Unity Editor only.");
#endif
        }
    }
}
