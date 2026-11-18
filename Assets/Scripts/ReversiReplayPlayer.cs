using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TabletopReversi
{
    /// <summary>Muted, looping captured gameplay with captions driven by the decoded video clock.</summary>
    public sealed class ReversiReplayPlayer : MonoBehaviour
    {
        VideoPlayer player;
        RawImage image;
        Text caption, chapterLabel;
        RenderTexture output;
        bool tutorial, configured, playRequested = true, failed;
        double pendingSeek = -1;
        float progressWidth = -1;
        public Image ProgressFill;

        public bool IsReady => player != null && player.isPrepared && !failed;
        public bool IsPlaying => IsReady && player.isPlaying;
        public bool WantsToPlay => playRequested;
        public double PlaybackTime => IsReady ? player.time : pendingSeek >= 0 ? pendingSeek : 0;
        public string CurrentCaption
        {
            get
            {
                if (!tutorial) return "";
                double time = PlaybackTime;
                var chapter = ReversiReplayTimeline.ChapterAt(time);
                return ReversiReplayTimeline.TransitionOpacity(time) > .001 || string.IsNullOrEmpty(chapter.Title) ? "" : chapter.Caption;
            }
        }

        public void Configure(RawImage target, bool isTutorial, Text captionText = null, Text stageText = null)
        {
            ReleaseMedia();
            image = target;
            tutorial = isTutorial;
            caption = captionText;
            chapterLabel = stageText;
            configured = true;
            failed = false;
            playRequested = true;
            pendingSeek = -1;
            if (image != null) { image.raycastTarget = false; image.color = new Color(.08f, .15f, .11f); }
            if (caption != null) caption.text = "プレイ映像を読み込み中…";
            if (chapterLabel != null) chapterLabel.text = "";
            player = GetComponent<VideoPlayer>();
            if (player == null) player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.isLooping = true;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.source = VideoSource.VideoClip;
            player.clip = Resources.Load<VideoClip>(tutorial ? "Video/ReversiGuide" : "Video/ReversiIntro");
            player.prepareCompleted += Prepared;
            player.errorReceived += VideoError;
            if (player.clip == null)
            {
                VideoError(player, "プレイ映像が見つかりません。");
                return;
            }
            output = new RenderTexture(1024, 640, 0, RenderTextureFormat.ARGB32)
            {
                name = tutorial ? "Reversi guide playback" : "Reversi selection playback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            output.Create();
            player.targetTexture = output;
            if (image != null) image.texture = output;
            player.Prepare();
            UpdatePlaybackLabels();
        }

        void Prepared(VideoPlayer source)
        {
            if (!configured || !isActiveAndEnabled || source != player) return;
            if (image != null) image.color = Color.white;
            if (pendingSeek >= 0) { source.time = ClampTime(pendingSeek); pendingSeek = -1; }
            if (playRequested) source.Play();
            else source.Pause();
            UpdatePlaybackLabels();
        }
        void VideoError(VideoPlayer source, string message)
        {
            if (failed) return;
            failed = true;
            if (caption != null) caption.text = "プレイ映像を読み込めませんでした。";
            if (chapterLabel != null) chapterLabel.text = "";
            Debug.LogWarning("Reversi replay: " + message, this);
        }
        void Update() { UpdatePlaybackLabels(); }
        public void SetCaptionTargets(Text captionText, Text stageText)
        {
            caption = captionText;
            chapterLabel = stageText;
            if (caption != null && !IsReady)
                caption.text = failed ? "プレイ映像を読み込めませんでした。" : "プレイ映像を読み込み中…";
            UpdatePlaybackLabels();
        }
        void UpdatePlaybackLabels()
        {
            if (!configured || failed) return;
            if (IsReady)
            {
                if (caption != null) caption.text = CurrentCaption;
                if (chapterLabel != null)
                {
                    double time = PlaybackTime;
                    chapterLabel.text = tutorial && ReversiReplayTimeline.TransitionOpacity(time) <= .001
                        ? ReversiReplayTimeline.ChapterAt(time).Title : "";
                }
            }
            if (ProgressFill != null)
            {
                if (progressWidth < 0) progressWidth = ProgressFill.rectTransform.sizeDelta.x;
                float progress = Mathf.Clamp01((float)(PlaybackTime / (tutorial ? ReversiReplayTimeline.Duration : ReversiReplayTimeline.SelectionDuration)));
                ProgressFill.rectTransform.sizeDelta = new Vector2(progressWidth * progress, ProgressFill.rectTransform.sizeDelta.y);
            }
        }
        public void SetPaused(bool paused)
        {
            playRequested = !paused;
            if (IsReady) { if (paused) player.Pause(); else player.Play(); }
            UpdatePlaybackLabels();
        }
        public void SeekTime(double time)
        {
            double target = ClampTime(time);
            pendingSeek = target;
            if (IsReady) { player.time = target; pendingSeek = -1; }
            UpdatePlaybackLabels();
        }
        double ClampTime(double time)
        {
            double duration = tutorial ? ReversiReplayTimeline.Duration : ReversiReplayTimeline.SelectionDuration;
            return System.Math.Max(0, System.Math.Min(duration - .05, time));
        }
        public void SeekChapter(int index)
        {
            if (!tutorial || ReversiReplayTimeline.Chapters.Count == 0) return;
            index = Mathf.Clamp(index, 0, ReversiReplayTimeline.Chapters.Count - 1);
            if (string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[index].Title))
            {
                int following = index + 1;
                while (following < ReversiReplayTimeline.Chapters.Count && string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[following].Title)) following++;
                if (following < ReversiReplayTimeline.Chapters.Count) index = following;
                else
                    while (index > 0 && string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[index].Title)) index--;
            }
            SeekTime(ReversiReplayTimeline.Chapters[index].Start);
        }
        public void StepChapter(int delta)
        {
            if (!tutorial || delta == 0) return;
            int current = 0;
            for (int i = 0; i < ReversiReplayTimeline.Chapters.Count; i++)
                if (PlaybackTime + .025 >= ReversiReplayTimeline.Chapters[i].Start) current = i;
            int direction = delta < 0 ? -1 : 1;
            int target = current;
            for (int step = 0; step < System.Math.Abs(delta); step++)
            {
                int next = target + direction;
                while (next >= 0 && next < ReversiReplayTimeline.Chapters.Count && string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[next].Title)) next += direction;
                if (next < 0 || next >= ReversiReplayTimeline.Chapters.Count) break;
                target = next;
            }
            SeekChapter(target);
        }
        void OnDisable() { ReleaseMedia(); }
        void OnDestroy() { ReleaseMedia(); }
        void ReleaseMedia()
        {
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.errorReceived -= VideoError;
                player.Stop();
                player.targetTexture = null;
            }
            if (image != null) image.texture = null;
            if (output != null)
            {
                output.Release();
                if (Application.isPlaying) Destroy(output); else DestroyImmediate(output);
                output = null;
            }
            configured = false;
        }
    }
}
