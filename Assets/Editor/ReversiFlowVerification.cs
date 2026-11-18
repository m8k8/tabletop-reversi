using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TabletopReversi
{
    /// <summary>
    /// Play-mode integration checks for navigation, configured player colours,
    /// asynchronous CPU turns, pause, and undo. No test-framework package required.
    /// </summary>
    public static class ReversiFlowVerification
    {
        enum Phase { Navigation, PausedOpening, CpuOpening, CpuReply, CancelledCpu, LocalUndo }
        static readonly HashSet<string> MatchLabels = new HashSet<string> { "あなた", "CPU", "1P", "2P", "メニュー", "の番です" };
        static ReversiGame game;
        static Phase phase;
        static bool running;
        static int assertions;
        static double startedAt, deadline, readyAt;
        static string result = "IDLE: navigation verification has not run.";

        [MenuItem("Tabletop Reversi/Verify Navigation (Play Mode)")]
        static void RunFromMenu() { Start(); }

        /// <summary>Starts on the initial game-selection screen in an already-running scene.</summary>
        public static string Start()
        {
            if (running) return Status();
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                return result = "FAIL: enter Play Mode and wait for compilation before starting navigation verification.";
            game = UnityEngine.Object.FindAnyObjectByType<ReversiGame>();
            if (game == null) return result = "FAIL: no ReversiGame exists in the playing scene.";
            assertions = 0;
            phase = Phase.Navigation;
            running = true;
            startedAt = EditorApplication.timeSinceStartup;
            deadline = startedAt + 15;
            readyAt = startedAt;
            result = "RUNNING: navigation verification.";
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            return Status();
        }

        public static string Status()
        {
            return running ? "RUNNING: " + phase + "; " + assertions + " assertions." : result;
        }

        static void Tick()
        {
            if (!running) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || game == null)
                    throw new InvalidOperationException("The playing scene stopped or recompiled during verification.");
                double now = EditorApplication.timeSinceStartup;
                if (now - startedAt > 45 || now > deadline)
                    throw new TimeoutException("Timed out waiting for " + phase + ".");
                if (now < readyAt) return;
                switch (phase)
                {
                    case Phase.Navigation: VerifyNavigation(); break;
                    case Phase.PausedOpening: VerifyPausedOpening(); break;
                    case Phase.CpuOpening: VerifyCpuOpening(); break;
                    case Phase.CpuReply: VerifyCpuReplyAndPause(); break;
                    case Phase.CancelledCpu: VerifyCancellationAndLocalStart(); break;
                    case Phase.LocalUndo: VerifyLocalUndo(); break;
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (running && state == PlayModeStateChange.ExitingPlayMode)
                Finish(false, "Play Mode stopped during verification");
        }

        static void BeforeAssemblyReload()
        {
            if (running) Finish(false, "Scripts reloaded during verification");
        }

        static void VerifyNavigation()
        {
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection, "startup shows game selection");
            Check(!game.CanHumanMove && game.BoardView == null, "game selection cannot accept match input");
            game.PlayCell(2, 3);
            Check(game.Rules.MoveCount == 0, "menu input cannot place a disc");
            Click("Choose Reversi");
            Check(game.CurrentScreen == ReversiGame.GameScreen.Settings, "choosing Reversi opens settings");
            Click("Back to games");
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection, "settings back returns to game selection");
            Click("Choose Reversi");
            game.ConfigureMatch(false, 0, ReversiRules.White, false, false);
            VerifyChoiceState("Opponent CPU", "Opponent Local");
            Click("Start Match");
            Check(game.CurrentScreen == ReversiGame.GameScreen.Match, "start button opens match");
            Check(!game.IsTwoPlayer && game.HumanPlayer == ReversiRules.White && game.Difficulty == 0,
                "configured CPU opponent, colour and difficulty are applied");
            Check(game.BoardView != null && !game.BoardView.ShowLegal, "configured assist setting is applied");
            Check(game.IsThinking && !game.CanHumanMove, "CPU starts automatically when human chooses white");
            Check(game.PlayerName(ReversiRules.White) == "あなた" && game.PlayerName(ReversiRules.Black) == "CPU",
                "CPU labels follow the selected colour");
            VerifyMatchHud("あなた", "CPU");
            Click("Open Match Menu");
            VerifyMenuLabels();
            Check(!game.CanHumanMove, "visible menu button pauses the opening");
            Next(Phase.PausedOpening, 1.2);
        }

        static void VerifyPausedOpening()
        {
            Check(game.Rules.MoveCount == 0, "CPU does not place its opening disc while paused");
            Click("Close Match Menu");
            Next(Phase.CpuOpening);
        }

        static void VerifyCpuOpening()
        {
            if (!game.CanHumanMove) return;
            Check(game.Rules.MoveCount == 1 && game.Rules.CurrentPlayer == ReversiRules.White,
                "CPU opening hands the turn to the white human");
            Check(!game.CanUndo, "CPU opening alone cannot be undone as a human move");
            Check(game.Rules.Count(ReversiRules.Empty) == 59, "one CPU opening adds one disc");
            Move move = game.Rules.LegalMoves()[0];
            game.PlayCell(move.Row, move.Col);
            Check(game.Rules.MoveCount == 2 && game.IsThinking, "human move schedules CPU reply");
            Next(Phase.CpuReply);
        }

        static void VerifyCpuReplyAndPause()
        {
            if (!game.CanHumanMove) return;
            Check(game.Rules.MoveCount == 3 && game.Rules.CurrentPlayer == ReversiRules.White,
                "CPU replies and returns control to white");
            Check(game.CanUndo, "human turn can be undone after CPU reply");
            game.UndoMove();
            Check(game.Rules.MoveCount == 1 && game.Rules.CurrentPlayer == ReversiRules.White && game.CanHumanMove,
                "undo restores the human's prior turn without removing CPU opening");
            Check(!game.CanUndo, "no human undo remains after returning to first human turn");
            VerifyMatchHud("あなた", "CPU");
            game.ShowPause();
            int movesBefore = game.Rules.MoveCount;
            Move move = game.Rules.LegalMoves()[0];
            game.PlayCell(move.Row, move.Col);
            Check(!game.CanHumanMove && game.Rules.MoveCount == movesBefore, "paused board ignores placement");
            game.CloseModal();
            Check(game.CanHumanMove, "closing pause resumes human control");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    game.BoardView.rectTransform.TransformPoint(game.BoardView.CellCenter(move.Row, move.Col)))
            };
            ExecuteEvents.Execute(game.BoardView.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(!game.CanHumanMove && game.Rules.MoveCount == movesBefore, "right-click opens menu without placing a disc");
            game.CloseModal();
            game.OpenReversiSettings();
            game.ConfigureMatch(false, 0, ReversiRules.Black, true, false);
            Click("Start Match");
            game.PlayCell(2, 3);
            Check(game.IsThinking, "black human move starts a pending CPU reply");
            game.ShowGameSelection();
            Next(Phase.CancelledCpu, 1.5);
        }

        static void VerifyCancellationAndLocalStart()
        {
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection && game.Rules.MoveCount == 0,
                "returning to selection cancels the old match");
            Check(!game.IsThinking && !game.CanHumanMove && game.BoardView == null,
                "stale CPU result cannot update the selection screen");
            Click("Choose Reversi");
            game.ConfigureMatch(true, 0, ReversiRules.White, true, false);
            foreach (Text text in game.GetComponentsInChildren<Text>())
            {
                Check(text.text != "CPUの強さ", "local settings omit the CPU difficulty heading");
                Check(!text.text.Contains("同じ画面で"), "local settings omit the turn-sharing explanation");
            }
            VerifyChoiceState("Opponent Local", "Opponent CPU");
            Click("Start Match");
            Check(game.IsTwoPlayer && game.HumanPlayer == ReversiRules.White && game.CanHumanMove,
                "local two-player match accepts either human turn");
            Check(game.PlayerName(ReversiRules.White) == "1P" && game.PlayerName(ReversiRules.Black) == "2P",
                "local player labels follow 1P's selected colour");
            Check(game.Rules.CurrentPlayer == ReversiRules.Black, "black remains first even when assigned to 2P");
            VerifyMatchHud("1P", "2P");
            Move move = game.Rules.LegalMoves()[0];
            game.PlayCell(move.Row, move.Col);
            Check(game.Rules.MoveCount == 1 && game.Rules.CurrentPlayer == ReversiRules.White,
                "local black move passes control to white");
            VerifyTurnIndicator();
            Next(Phase.LocalUndo);
        }

        static void VerifyLocalUndo()
        {
            if (!game.CanHumanMove) return;
            Check(!game.IsThinking && game.CanUndo, "local turn has no CPU search and permits undo");
            game.UndoMove();
            Check(game.Rules.MoveCount == 0 && game.Rules.CurrentPlayer == ReversiRules.Black && game.CanHumanMove,
                "local undo restores the prior black turn");
            Check(!game.CanUndo, "empty local history disables undo");
            VerifyMatchHud("1P", "2P");
            game.ShowGameSelection();
            Check(game.CurrentScreen == ReversiGame.GameScreen.GameSelection, "verification leaves game selection visible");
            Finish(true, "menu/settings routing, white CPU opening, HUD, pause, right-click, undo, async cancellation and local 2P");
        }

        static void VerifyMatchHud(string lowerName, string upperName)
        {
            int labelCount = 0;
            Text bottomName = null, topName = null;
            foreach (Text text in game.GetComponentsInChildren<Text>())
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                Check(MatchLabels.Contains(text.text), "match contains only player names, turn prompt and menu text: " + text.text);
                Check(text.text != "黒" && text.text != "白", "colour words are absent from match HUD");
                labelCount++;
                if (text.text == lowerName) bottomName = text;
                if (text.text == upperName) topName = text;
            }
            Check(labelCount == (game.IsTwoPlayer ? 4 : 3),
                "match displays two names and menu, with a turn prompt only for local two-player games");
            Check(bottomName != null && topName != null, "both player names remain visible");
            Check(bottomName.alignment == TextAnchor.MiddleRight, "the lower player name is right-aligned in its badge");
            Check(IsCorner(bottomName, false), "human or 1P badge appears in lower-left corner");
            Check(IsCorner(topName, true), "CPU or 2P badge appears in upper-right corner");
            bool hasMenu = false;
            foreach (Button button in game.GetComponentsInChildren<Button>())
                if (button.name == "Open Match Menu" && button.interactable) hasMenu = true;
            Check(hasMenu, "a visible match menu button is available");
            if (game.IsTwoPlayer) VerifyTurnIndicator();
            else
            {
                bool hasTurnIcon = false, hasTurnPanel = false;
                foreach (Image image in game.GetComponentsInChildren<Image>())
                    if (image.name == "Turn disc") hasTurnIcon = true;
                foreach (RectTransform rect in game.GetComponentsInChildren<RectTransform>())
                    if (rect.name == "Current turn") hasTurnPanel = true;
                Check(!hasTurnIcon && !hasTurnPanel, "CPU matches omit the turn indicator entirely");
            }
        }

        static void VerifyMenuLabels()
        {
            Button undo = FindButton("Undo Match");
            var permitted = new HashSet<string> { "まった", "初めから", "対局をやめる", "×" };
            Button[] actions = undo.transform.parent.GetComponentsInChildren<Button>();
            Check(actions.Length == 4, "match menu has exactly three actions and one close button");
            foreach (Button action in actions)
            {
                Text label = action.GetComponentInChildren<Text>();
                Check(label != null && permitted.Remove(label.text), "match menu contains only the requested actions");
            }
            Check(permitted.Count == 0, "match menu exposes undo, restart, quit and the top-right close button");
            Check(FindButton("Close Match Menu").interactable, "the close cross is available while CPU is paused");
        }

        static void VerifyChoiceState(string chosenName, string idleName)
        {
            Color chosen = FindButton(chosenName).GetComponent<Image>().color;
            Color idle = FindButton(idleName).GetComponent<Image>().color;
            float range = Mathf.Max(idle.r, Mathf.Max(idle.g, idle.b)) - Mathf.Min(idle.r, Mathf.Min(idle.g, idle.b));
            Check(range < .1f && idle.grayscale > .25f && idle.grayscale < .65f,
                "unselected settings use a readable neutral gray");
            Check(Mathf.Abs(chosen.r - idle.r) + Mathf.Abs(chosen.g - idle.g) + Mathf.Abs(chosen.b - idle.b) > .2f,
                "selected settings retain a visibly distinct theme color");
        }

        static void VerifyTurnIndicator()
        {
            // Navigation creates these RectTransforms in this same editor tick.
            // Resolve the canvas layout before projecting their positions to the game view.
            Canvas.ForceUpdateCanvases();
            Image disc = null;
            foreach (Image image in game.GetComponentsInChildren<Image>())
                if (image.name == "Turn disc") disc = image;
            Check(disc != null && disc.transform.parent.name == "Current turn", "current turn is represented by a disc icon");
            Text prompt = disc.transform.parent.GetComponentInChildren<Text>();
            Check(prompt != null && prompt.text == "の番です", "turn prompt uses the disc instead of a color word");
            // Editor Screen dimensions may include a different Game View size than this canvas.
            // Compare both rendered elements in board-local space instead of normalizing by Screen.
            RectTransform panel = (RectTransform)disc.transform.parent;
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float panelBottom = float.PositiveInfinity;
            foreach (Vector3 corner in corners)
                panelBottom = Mathf.Min(panelBottom, game.BoardView.rectTransform.InverseTransformPoint(corner).y);
            Vector3 panelCenter = game.BoardView.rectTransform.InverseTransformPoint(panel.TransformPoint(panel.rect.center));
            Vector2 first = game.BoardView.CellCenter(0, 0), last = game.BoardView.CellCenter(0, 7);
            float planeTop = first.y + (first.y - game.BoardView.CellCenter(1, 0).y) * .5f;
            Check(panelCenter.x > first.x && panelCenter.x < last.x && panelBottom > planeTop,
                "entire turn indicator sits above the board's playing plane");
            Check(game.Rules.CurrentPlayer == ReversiRules.Black ? disc.color.grayscale < .4f : disc.color.grayscale > .6f,
                "turn disc follows the current player's black or white color");
        }

        static Button FindButton(string name)
        {
            foreach (Button button in game.GetComponentsInChildren<Button>())
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            throw new InvalidOperationException("Visible button not found: " + name);
        }

        static bool IsCorner(Text text, bool upperRight)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, text.rectTransform.TransformPoint(text.rectTransform.rect.center));
            float x = point.x / Screen.width, y = point.y / Screen.height;
            return upperRight ? x > .65f && y > .75f : x < .35f && y < .25f;
        }

        static void Click(string name)
        {
            foreach (Button button in game.GetComponentsInChildren<Button>())
                if (button.name == name && button.gameObject.activeInHierarchy)
                {
                    Check(button.interactable, name + " button is enabled");
                    button.onClick.Invoke();
                    return;
                }
            throw new InvalidOperationException("Visible button not found: " + name);
        }

        static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Next(Phase next, double delay = 0)
        {
            phase = next;
            double now = EditorApplication.timeSinceStartup;
            readyAt = now + delay;
            deadline = now + 15;
        }

        static void Finish(bool passed, string message)
        {
            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            result = (passed ? "PASS: " : "FAIL: ") + assertions + " assertions; " + message
                + "; " + (EditorApplication.timeSinceStartup - startedAt).ToString("F2") + " seconds.";
            if (passed) Debug.Log(result);
            else Debug.LogError(result);
        }
    }
}
