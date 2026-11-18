using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TabletopReversi
{
    /// <summary>Renders the real board into deterministic frames for the bundled demonstration films.</summary>
    public static class ReversiVideoBaker
    {
        public const int FramesPerSecond = 24, Width = 960, Height = 600;
        static Scene previewScene;
        static GameObject root;
        static Camera camera;
        static RenderTexture target;
        static Texture2D readback;
        static TabletopBoardGraphic board;
        static GameObject annotation;
        static Text annotationText;
        static Image transition;
        static int index, count, first;
        static bool running;
        static string folder, result = "IDLE";

        public static string Start(int frameCount = 768, int firstFrame = 0)
        {
            if (running) return Status();
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                return "FAIL: stop Play Mode and wait for compilation before baking video.";
            try
            {
                folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Captures/ReversiFrames"));
                Directory.CreateDirectory(folder);
                int maximum = (int)(ReversiReplayTimeline.Duration * FramesPerSecond);
                index = first = Mathf.Clamp(firstFrame, 0, maximum - 1);
                count = Mathf.Min(maximum, first + Mathf.Max(1, frameCount));
                previewScene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Reversi video baking stage") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var cameraObject = new GameObject("Video camera"); cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
                camera.scene = previewScene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(previewScene);
                camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.84f, .69f, .49f);
                camera.cullingMask = 1 << 31; camera.enabled = false; camera.allowHDR = false;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Reversi video capture", hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                target.Create(); camera.targetTexture = target;
                readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
                var canvasObject = new GameObject("Film canvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                var canvasRect = canvasObject.GetComponent<RectTransform>(); canvasRect.sizeDelta = new Vector2(1600, 1000);
                canvasRect.localScale = Vector3.one * .01f;
                var floorObject = new GameObject("Floor", typeof(RectTransform), typeof(CanvasRenderer), typeof(WoodenFloorGraphic));
                floorObject.transform.SetParent(canvasObject.transform, false);
                var floorRect = floorObject.GetComponent<RectTransform>(); floorRect.anchorMin = Vector2.zero; floorRect.anchorMax = Vector2.one;
                floorRect.offsetMin = floorRect.offsetMax = Vector2.zero;
                var boardObject = new GameObject("Playing board", typeof(RectTransform), typeof(CanvasRenderer), typeof(TabletopBoardGraphic));
                boardObject.transform.SetParent(canvasObject.transform, false);
                var rect = boardObject.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(197, -935); rect.sizeDelta = new Vector2(990, 700); rect.localScale = Vector3.one * 1.22f;
                board = boardObject.GetComponent<TabletopBoardGraphic>(); board.raycastTarget = false; board.ShowLegal = false;
                annotation = new GameObject("Chapter card", typeof(RectTransform), typeof(Image)); annotation.transform.SetParent(canvasObject.transform, false);
                var tagRect = annotation.GetComponent<RectTransform>(); tagRect.sizeDelta = new Vector2(660, 138); tagRect.anchoredPosition = Vector2.zero;
                annotation.GetComponent<Image>().color = new Color(.98f, .98f, .94f, .96f);
                var labelObject = new GameObject("Chapter", typeof(RectTransform), typeof(Text)); labelObject.transform.SetParent(annotation.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(14, 12); labelRect.offsetMax = new Vector2(-14, -12);
                annotationText = labelObject.GetComponent<Text>(); annotationText.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansJP-Bold.ttf");
                annotationText.fontSize = 39; annotationText.color = new Color(.14f, .24f, .20f); annotationText.alignment = TextAnchor.MiddleCenter;
                annotationText.horizontalOverflow = HorizontalWrapMode.Wrap; annotationText.verticalOverflow = VerticalWrapMode.Overflow;
                var fadeObject = new GameObject("Scene transition", typeof(RectTransform), typeof(Image));
                fadeObject.transform.SetParent(canvasObject.transform, false);
                var fadeRect = fadeObject.GetComponent<RectTransform>(); fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one;
                fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
                transition = fadeObject.GetComponent<Image>(); transition.color = Color.clear; transition.raycastTarget = false;
                SetLayer(root.transform, 31);
                running = true; result = "RUNNING"; EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Cancel;
                return Status();
            }
            catch (Exception exception) { result = "FAIL: " + exception; Cleanup(); return result; }
        }
        public static string Status() => running ? "RUNNING: " + (index - first) + "/" + (count - first) + " frames; " + folder : result;
        static void SetLayer(Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform) SetLayer(child, layer);
        }
        static void Tick()
        {
            if (!running) return;
            try
            {
                for (int batch = 0; batch < 4 && index < count; batch++, index++)
                {
                    double time = index / (double)FramesPerSecond;
                    var frame = ReversiReplayTimeline.Sample(time);
                    board.SetReplayFrame(frame);
                    bool ending = time >= 29;
                    annotation.SetActive(ending);
                    if (ending) annotationText.text = "対局終了\n黒 " + ReversiReplayTimeline.FinalBlackCount + "  –  " + ReversiReplayTimeline.FinalWhiteCount + " 白";
                    transition.color = new Color(0, 0, 0, ReversiReplayTimeline.TransitionOpacity(time));
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); readback.Apply(false, false);
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(folder, "frame-" + index.ToString("D5") + ".png"), readback.EncodeToPNG());
                }
                if (index >= count)
                {
                    result = "PASS: rendered " + (count - first) + " frames at " + FramesPerSecond + " fps, " + Width + "x" + Height + "; " + folder;
                    Cleanup();
                }
            }
            catch (Exception exception) { result = "FAIL: " + exception; Cleanup(); }
        }
        static void Cancel() { result = "FAIL: video bake cancelled by script reload"; Cleanup(); }
        static void Cleanup()
        {
            running = false; EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            root = null; camera = null; board = null; target = null; readback = null;
        }
    }
}
