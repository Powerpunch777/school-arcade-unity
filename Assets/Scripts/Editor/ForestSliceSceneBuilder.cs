using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulArcher.Editor
{
    public static class ForestSliceSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ForestSlice.unity";

        [MenuItem("Soul Archer/Rebuild Forest Slice Scene")]
        public static void Rebuild()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ResponsiveCamera2D));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .045f, .055f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            GameObject gameObject = new GameObject("SoulArcherGame", typeof(AudioSource), typeof(SoulArcherGame));
            AudioSource audioSource = gameObject.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt product-ready ForestSlice scene at " + ScenePath);
        }
    }
}
