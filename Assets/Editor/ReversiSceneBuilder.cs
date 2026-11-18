using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabletopReversi
{
    public static class ReversiSceneBuilder
    {
        [MenuItem("Tabletop Reversi/Create Game Scene")]
        public static void CreateScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraGo.tag="MainCamera";
            var camera=cameraGo.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.969f,.969f,.937f);camera.orthographic=true;cameraGo.transform.position=new Vector3(0,0,-10);
            var lightGo=new GameObject("Directional Light",typeof(Light));var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;lightGo.transform.rotation=Quaternion.Euler(50,-30,0);
            var root=new GameObject("Tabletop Reversi");var game=root.AddComponent<ReversiGame>();
            game.JapaneseFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP.ttf");game.BoldFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP-Bold.ttf");
            PlayerSettings.companyName="Tabletop Studio";PlayerSettings.productName="Tabletop Reversi";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Reversi.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Reversi.unity",true)};
            Selection.activeGameObject=root;
        }
        [MenuItem("Tabletop Reversi/Verify Game Rules")]
        public static void VerifyRules(){Debug.Log(ReversiRulesVerification.Run());}
    }
}
