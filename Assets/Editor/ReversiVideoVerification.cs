using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TabletopReversi
{
    /// <summary>Checks the shipped clips through the real decoder, UI controls and caption clock.</summary>
    public static class ReversiVideoVerification
    {
        enum Phase { SelectionReady, SelectionClock, GuideReady, BeforeCut, FirstBlackout, AfterCut, SecondBlackout, MiddleSeek, EndingSeek, PauseSeek, RebuiltGuide, ResumedClock, ReturnedSelection }
        static ReversiGame game;
        static ReversiReplayPlayer replay;
        static VideoPlayer watched;
        static Phase phase;
        static bool running;
        static int assertions, middleChapter;
        static ReversiReplayPlayer priorPlayer;
        static double startedAt, deadline, readyAt, expectedTime, previousTime;
        static string initialCaption, beforeCutCaption, result = "IDLE: video verification has not run.";

        [MenuItem("Tabletop Reversi/Verify Videos (Play Mode)")]
        static void RunFromMenu() { Start(); }

        public static string Start()
        {
            if (running) return Status();
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                return result = "FAIL: enter Play Mode and wait for compilation before verifying videos.";
            game = UnityEngine.Object.FindAnyObjectByType<ReversiGame>();
            if (game == null || game.CurrentScreen != ReversiGame.GameScreen.GameSelection)
                return result = "FAIL: begin video verification on the game-selection screen.";
            running = true;
            assertions = 0;
            phase = Phase.SelectionReady;
            startedAt = EditorApplication.timeSinceStartup;
            deadline = startedAt + 20;
            readyAt = startedAt;
            result = "RUNNING: video verification.";
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            return Status();
        }

        public static string Status() => running ? "RUNNING: " + phase + "; " + assertions + " assertions." : result;

        static void Tick()
        {
            if (!running) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || game == null)
                    throw new InvalidOperationException("The playing scene stopped or recompiled during video verification.");
                double now = EditorApplication.timeSinceStartup;
                if (now > deadline || now - startedAt > 90)
                    throw new TimeoutException("Timed out waiting for " + phase + ".");
                if (now < readyAt) return;
                switch (phase)
                {
                    case Phase.SelectionReady: VerifySelectionReady(); break;
                    case Phase.SelectionClock: VerifySelectionClock(); break;
                    case Phase.GuideReady: VerifyGuideReady(); break;
                    case Phase.BeforeCut: VerifyBeforeCut(); break;
                    case Phase.FirstBlackout: VerifyFirstBlackout(); break;
                    case Phase.AfterCut: VerifyAfterCut(); break;
                    case Phase.SecondBlackout: VerifySecondBlackout(); break;
                    case Phase.MiddleSeek: VerifyMiddleSeek(); break;
                    case Phase.EndingSeek: VerifyEndingSeek(); break;
                    case Phase.PauseSeek: VerifyPauseSeek(); break;
                    case Phase.RebuiltGuide: VerifyRebuiltGuide(); break;
                    case Phase.ResumedClock: VerifyResumedClock(); break;
                    case Phase.ReturnedSelection: VerifyReturnedSelection(); break;
                }
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        static bool Ready()
        {
            ReversiReplayPlayer[] players = game.GetComponentsInChildren<ReversiReplayPlayer>();
            if (players.Length == 0) return false;
            if (players.Length != 1) throw new InvalidOperationException("More than one replay component is active for the current screen.");
            replay = players[0];
            VideoPlayer[] decoders = game.GetComponentsInChildren<VideoPlayer>();
            if (decoders.Length == 0) return false;
            if (decoders.Length != 1) throw new InvalidOperationException("Old screens left more than one active video decoder.");
            Watch(decoders[0]);
            return replay.IsReady && watched.isPrepared && watched.frame >= 0 && watched.targetTexture != null;
        }

        static void VerifySelectionReady()
        {
            if (!Ready()) return;
            if (replay.PlaybackTime < .35 || replay.PlaybackTime > ReversiReplayTimeline.SelectionDuration - .5) return;
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection, "startup remains at game selection");
            Check(HasText("あそび選択"), "selection uses the requested heading");
            RejectText("あそびのテーブル", "ゲームを選ぶ", "今日は、どのあそびにする？", "今日はどの遊びにする");
            RejectContains("ゲームを選ぶと、");
            Check(game.GetComponentsInChildren<TabletopBoardGraphic>().Length == 0, "selection uses footage instead of a static board graphic");
            VerifyClip("ReversiIntro", ReversiReplayTimeline.SelectionDuration);
            Check(replay.IsPlaying, "selection footage autoplays");
            Check(watched.isLooping && watched.audioOutputMode == VideoAudioOutputMode.None, "selection footage loops silently");
            VerifyRenderedPixels(watched.targetTexture);
            previousTime = replay.PlaybackTime;
            Next(Phase.SelectionClock, .7);
        }

        static void VerifySelectionClock()
        {
            if (!Ready()) return;
            Check(Advanced(previousTime, replay.PlaybackTime, ReversiReplayTimeline.SelectionDuration), "selection clip's decoded clock advances");
            Click("Choose Reversi");
            Check(game.CurrentScreen == ReversiGame.GameScreen.Settings, "selection video card still opens settings");
            Next(Phase.GuideReady);
        }

        static void VerifyGuideReady()
        {
            if (!Ready()) return;
            Check(HasText("リバーシについて"), "settings uses the requested game-information heading");
            Check(HasText("駒を置ける場所を表示") && HasText("コマの効果音"), "settings labels describe the two toggles directly");
            RejectText("リバーシの設定", "あそびかたを選んで、さあ、ひと勝負。", "アシスト", "サウンド");
            RejectContains("黒が先手です。", "対局中は Esc /");
            Check(game.GetComponentsInChildren<TabletopBoardGraphic>().Length == 0, "settings uses the rules film instead of a static board graphic");
            VerifyClip("ReversiGuide", ReversiReplayTimeline.Duration);
            Check(replay.IsPlaying, "rules footage autoplays");
            Check(replay.CurrentCaption == ExpectedCaption(replay.PlaybackTime),
                "opening rules caption follows the actual video clock");
            initialCaption = replay.CurrentCaption;
            middleChapter = FindChapter("タテ・ヨコ・ナナメ");
            Check(string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[2].Title) && string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[2].Caption),
                "first cut has no transition title or explanatory text");
            Check(string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[4].Title) && string.IsNullOrEmpty(ReversiReplayTimeline.Chapters[4].Caption),
                "second cut has no transition title or explanatory text");
            Check(ReversiReplayTimeline.TransitionOpacity(9.2) <= .001f && ReversiReplayTimeline.TransitionOpacity(10) >= .99f
                && ReversiReplayTimeline.TransitionOpacity(11) <= .001f && ReversiReplayTimeline.TransitionOpacity(20) >= .99f,
                "cut transition opacity surrounds the authored cuts and returns to visible footage");
            replay.SetPaused(true);
            Seek(9.2, Phase.BeforeCut);
        }

        static void VerifyBeforeCut()
        {
            if (!AtDecodedTarget() || !HasText(replay.CurrentCaption)) return;
            beforeCutCaption = replay.CurrentCaption;
            Check(SampleBrightness(watched.targetTexture) > 15, "footage is visible before the first cut");
            Check(!string.IsNullOrEmpty(replay.CurrentCaption) && replay.CurrentCaption == ExpectedCaption(replay.PlaybackTime),
                "normal rules explanation is visible before the blackout");
            Seek(10, Phase.FirstBlackout);
        }

        static void VerifyFirstBlackout()
        {
            if (!AtDecodedTarget() || HasVisibleRulesCaption()) return;
            Check(SampleBrightness(watched.targetTexture) < 8, "decoded footage briefly blacks out at the first cut");
            Check(string.IsNullOrEmpty(replay.CurrentCaption) && !HasText(beforeCutCaption), "first blackout hides the displayed rule explanation");
            Seek(11, Phase.AfterCut);
        }

        static void VerifyAfterCut()
        {
            if (!AtDecodedTarget() || !HasText(replay.CurrentCaption)) return;
            Check(SampleBrightness(watched.targetTexture) > 15, "decoded footage resumes after the first cut");
            Check(!string.IsNullOrEmpty(replay.CurrentCaption) && replay.CurrentCaption == ExpectedCaption(replay.PlaybackTime),
                "rules explanation resumes with the new scene after blackout");
            Seek(20, Phase.SecondBlackout);
        }

        static void VerifySecondBlackout()
        {
            if (!AtDecodedTarget() || HasVisibleRulesCaption()) return;
            Check(SampleBrightness(watched.targetTexture) < 8, "decoded footage briefly blacks out at the second cut");
            Check(string.IsNullOrEmpty(replay.CurrentCaption), "second blackout hides the rule explanation");
            replay.SeekChapter(middleChapter);
            expectedTime = ReversiReplayTimeline.Chapters[middleChapter].Start;
            Next(Phase.MiddleSeek);
        }

        static void VerifyMiddleSeek()
        {
            if (!Ready() || Math.Abs(replay.PlaybackTime - expectedTime) > .25) return;
            Check(!replay.IsPlaying, "chapter seek preserves a paused player");
            Check(replay.CurrentCaption == ReversiReplayTimeline.Chapters[middleChapter].Caption,
                "middle-game seek changes the rule explanation");
            Check(replay.CurrentCaption != initialCaption, "middle-game caption differs from the opening explanation");
            Check(watched.frame > 0, "chapter seek reaches a decoded gameplay frame");
            int last = ReversiReplayTimeline.Chapters.Count - 1;
            replay.SeekChapter(last);
            expectedTime = ReversiReplayTimeline.Chapters[last].Start;
            Next(Phase.EndingSeek);
        }

        static void VerifyEndingSeek()
        {
            if (!Ready() || Math.Abs(replay.PlaybackTime - expectedTime) > .25) return;
            var ending = ReversiReplayTimeline.Chapters[ReversiReplayTimeline.Chapters.Count - 1];
            Check(replay.CurrentCaption == ending.Caption, "ending seek displays the final scoring explanation");
            var final = ReversiReplayTimeline.Sample(ReversiReplayTimeline.Duration - .1);
            Check(final.IsFinished, "edited rules film reaches an actual completed legal game");
            int black = 0, white = 0;
            foreach (int value in final.Board) { if (value == ReversiRules.Black) black++; if (value == ReversiRules.White) white++; }
            Check(black == ReversiReplayTimeline.FinalBlackCount && white == ReversiReplayTimeline.FinalWhiteCount,
                "ending counts match the full game's final board");
            Check(black + white == ReversiReplayTimeline.TotalGameMoves + 4, "edited ending accounts for every legal placement");
            Check(ending.Caption.Contains(black.ToString()) && ending.Caption.Contains(white.ToString()), "ending caption contains the actual final counts");
            Check(new ReversiRules(final.Board, final.CurrentPlayer).IsFinished, "final position independently satisfies the game's end condition");
            replay.SeekChapter(middleChapter);
            expectedTime = ReversiReplayTimeline.Chapters[middleChapter].Start;
            Next(Phase.PauseSeek);
        }

        static void VerifyPauseSeek()
        {
            if (!Ready() || Math.Abs(replay.PlaybackTime - expectedTime) > .25) return;
            Check(!replay.IsPlaying, "rules video remains paused before changing a setting");
            previousTime = replay.PlaybackTime;
            priorPlayer = replay;
            Click("Assist Off");
            Next(Phase.RebuiltGuide);
        }

        static void VerifyRebuiltGuide()
        {
            if (!Ready() || ReferenceEquals(replay, priorPlayer)) return;
            if (Math.Abs(replay.PlaybackTime - previousTime) > .35) return;
            Check(!replay.IsPlaying, "changing settings preserves the video's paused state");
            Check(Math.Abs(replay.PlaybackTime - previousTime) <= .35, "changing settings preserves the video's playback position");
            Check(replay.CurrentCaption == ExpectedCaption(replay.PlaybackTime), "restored video caption is synchronized");
            replay.SetPaused(false);
            previousTime = replay.PlaybackTime;
            Next(Phase.ResumedClock, .7);
        }

        static void VerifyResumedClock()
        {
            if (!Ready()) return;
            Check(replay.IsPlaying && Advanced(previousTime, replay.PlaybackTime, ReversiReplayTimeline.Duration),
                "rules footage resumes and advances after its settings are rebuilt");
            Click("Back to games");
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection, "settings can return to the selection video");
            Next(Phase.ReturnedSelection);
        }

        static void VerifyReturnedSelection()
        {
            if (!Ready()) return;
            VerifyClip("ReversiIntro", ReversiReplayTimeline.SelectionDuration);
            Check(replay.IsPlaying, "selection footage plays after returning from the guide");
            Finish(true, "real clips, decoded frames, silent blackout cuts, synchronized captions, completed-game ending and preserved playback state");
        }

        static void VerifyClip(string name, double duration)
        {
            Check(game.GetComponentsInChildren<ReversiReplayPlayer>().Length == 1 && game.GetComponentsInChildren<VideoPlayer>().Length == 1,
                "the current screen owns exactly one active replay decoder");
            Check(watched.clip != null && watched.clip.name == name, "the requested prerecorded clip is loaded: " + name);
            Check(Math.Abs(watched.clip.length - duration) < .5, "clip duration matches the authored timeline");
            Check(watched.targetTexture.IsCreated() && watched.targetTexture.width > 0 && watched.targetTexture.height > 0,
                "video decoder renders into a live texture");
        }

        static void VerifyRenderedPixels(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D sample = null;
            try
            {
                RenderTexture.active = target;
                sample = new Texture2D(12, 12, TextureFormat.RGB24, false);
                sample.ReadPixels(new Rect(target.width / 2 - 6, target.height / 2 - 6, 12, 12), 0, 0);
                sample.Apply();
                int visible = 0;
                foreach (Color32 pixel in sample.GetPixels32()) if (pixel.r + pixel.g + pixel.b > 45) visible++;
                Check(visible > 100, "decoded video texture contains visible board pixels rather than an empty black surface");
            }
            finally
            {
                RenderTexture.active = previous;
                if (sample != null) UnityEngine.Object.Destroy(sample);
            }
        }

        static string ExpectedCaption(double time)
        {
            return ReversiReplayTimeline.TransitionOpacity(time) > .001f ? "" : ReversiReplayTimeline.ChapterAt(time).Caption;
        }

        static void Seek(double time, Phase next)
        {
            replay.SeekTime(time);
            expectedTime = time;
            Next(next, .08);
        }

        static bool AtDecodedTarget()
        {
            if (!Ready() || Math.Abs(replay.PlaybackTime - expectedTime) > .1) return false;
            double fps = watched.clip != null ? watched.clip.frameRate : 0;
            return fps > 0 && Math.Abs(watched.frame / fps - expectedTime) < .065;
        }

        static double SampleBrightness(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D sample = null;
            try
            {
                RenderTexture.active = target;
                sample = new Texture2D(12, 12, TextureFormat.RGB24, false);
                sample.ReadPixels(new Rect(target.width / 2 - 6, target.height / 2 - 6, 12, 12), 0, 0);
                sample.Apply();
                double total = 0;
                foreach (Color32 pixel in sample.GetPixels32()) total += pixel.r + pixel.g + pixel.b;
                return total / (12 * 12 * 3);
            }
            finally
            {
                RenderTexture.active = previous;
                if (sample != null) UnityEngine.Object.Destroy(sample);
            }
        }

        static bool Advanced(double before, double after, double duration)
        {
            double delta = after - before;
            if (delta < 0) delta += duration;
            return delta > .2;
        }

        static int FindChapter(string title)
        {
            for (int i = 0; i < ReversiReplayTimeline.Chapters.Count; i++)
                if (ReversiReplayTimeline.Chapters[i].Title == title) return i;
            throw new InvalidOperationException("Expected authored chapter not found: " + title);
        }

        static bool HasVisibleRulesCaption()
        {
            foreach (var chapter in ReversiReplayTimeline.Chapters)
                if (!string.IsNullOrEmpty(chapter.Caption) && HasText(chapter.Caption)) return true;
            return false;
        }

        static bool HasText(string value)
        {
            foreach (Text text in game.GetComponentsInChildren<Text>()) if (text.text == value) return true;
            return false;
        }

        static void RejectText(params string[] values)
        {
            foreach (Text text in game.GetComponentsInChildren<Text>())
                foreach (string value in values) Check(text.text != value, "removed copy is absent: " + value);
        }

        static void RejectContains(params string[] values)
        {
            foreach (Text text in game.GetComponentsInChildren<Text>())
                foreach (string value in values) Check(!text.text.Contains(value), "removed instruction is absent: " + value);
        }

        static void Click(string name)
        {
            foreach (Button button in game.GetComponentsInChildren<Button>())
                if (button.name == name && button.interactable)
                {
                    button.onClick.Invoke();
                    return;
                }
            throw new InvalidOperationException("Visible enabled button not found: " + name);
        }

        static void Watch(VideoPlayer player)
        {
            if (watched == player) return;
            if (watched != null) watched.errorReceived -= OnVideoError;
            watched = player;
            watched.errorReceived += OnVideoError;
        }

        static void OnVideoError(VideoPlayer player, string error) { if (running) Finish(false, "Video decoding failed: " + error); }
        static void OnPlayModeChanged(PlayModeStateChange state) { if (running && state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Play Mode stopped"); }
        static void BeforeAssemblyReload() { if (running) Finish(false, "Scripts reloaded during verification"); }

        static void Next(Phase next, double delay = 0)
        {
            phase = next;
            double now = EditorApplication.timeSinceStartup;
            readyAt = now + delay;
            deadline = now + 20;
        }

        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Finish(bool passed, string message)
        {
            if (!running) return;
            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            if (watched != null) watched.errorReceived -= OnVideoError;
            watched = null;
            replay = null;
            result = (passed ? "PASS: " : "FAIL: ") + assertions + " assertions; " + message + "; "
                + (EditorApplication.timeSinceStartup - startedAt).ToString("F2") + " seconds.";
            if (passed) Debug.Log(result); else Debug.LogError(result);
        }
    }
}
