using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrostboundFrontier.Editor
{
    [InitializeOnLoad]
    public static class FrostboundFrontierSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/FrostboundFrontier.unity";

        static FrostboundFrontierSceneBuilder()
        {
            EditorApplication.delayCall += EnsureScene;
        }

        [MenuItem("Frostbound Frontier/Rebuild Prototype Scene")]
        public static void EnsureScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Frostbound Frontier Prototype").AddComponent<FrostboundFrontierPrototype>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (!Application.isPlaying) EditorSceneManager.OpenScene(ScenePath);
            PlayerSettings.productName = "Frostbound Frontier";
            PlayerSettings.companyName = "Independent Prototype";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            AssetDatabase.SaveAssets();
        }
    }
}
