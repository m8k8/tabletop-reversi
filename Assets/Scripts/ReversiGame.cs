using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TabletopReversi
{
    public sealed partial class ReversiGame : MonoBehaviour
    {
        public enum GameScreen { GameSelection, Settings, Match }

        public Font JapaneseFont;
        public Font BoldFont;
        public ReversiRules Rules { get; private set; }
        public GameScreen CurrentScreen { get; private set; }
        public float AnimationEnd { get; private set; }
        public bool IsTwoPlayer => twoPlayers;
        public bool IsThinking => thinking;
        public int Difficulty => difficulty;
        public int HumanPlayer => humanPlayer;
        public TabletopBoardGraphic BoardView => board;
        public bool IsHumanTurn => CurrentScreen == GameScreen.Match && !thinking && Rules != null && !Rules.IsFinished
            && (twoPlayers || Rules.CurrentPlayer == humanPlayer);
        public bool CanHumanMove => IsHumanTurn && Time.unscaledTime >= AnimationEnd && modal == null;
        public bool CanUndo => CurrentScreen == GameScreen.Match && Rules != null
            && (twoPlayers ? Rules.UndoCount > 0 : humanTurnStarts.Count > 0);

        bool twoPlayers, thinking, sound = true, showLegal = true, previousCanMove;
        int difficulty = 1, humanPlayer = ReversiRules.Black, generation;
        bool pendingTwoPlayers, pendingAssist = true, pendingSound = true;
        int pendingDifficulty = 1, pendingPlayer = ReversiRules.Black;
        readonly Stack<int> humanTurnStarts = new Stack<int>();
        string toast = "";
        float toastUntil;
        AudioSource audioSource;
        AudioClip placeSound, flipSound, clickSound;

        void Awake() { Initialize(); }
        void OnEnable() { if (Application.isPlaying && Rules == null) Initialize(); }
        void Initialize()
        {
            CancelPendingActivity();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            Rules = new ReversiRules();
            CurrentScreen = GameScreen.GameSelection;
            if (JapaneseFont == null) JapaneseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ReleaseAudio();
            placeSound = Tone(390, .13f, .18f);
            flipSound = Tone(610, .10f, .10f);
            clickSound = Tone(810, .055f, .065f);
            BuildInterface();
            RenderScreen();
            UpdateInterface();
        }
        void OnDestroy() { generation++; ReleaseAudio(); }
        void ReleaseAudio()
        {
            if (placeSound) Destroy(placeSound);
            if (flipSound) Destroy(flipSound);
            if (clickSound) Destroy(clickSound);
        }
        void CancelPendingActivity()
        {
            StopAllCoroutines();
            generation++;
            thinking = false;
            AnimationEnd = 0;
            toast = "";
            previousCanMove = false;
            humanTurnStarts.Clear();
        }
        public string PlayerName(int player)
        {
            if (twoPlayers) return player == humanPlayer ? "1P" : "2P";
            return player == humanPlayer ? "あなた" : "CPU";
        }
        public void ShowGameSelection()
        {
            CancelPendingActivity();
            Rules.Reset();
            CurrentScreen = GameScreen.GameSelection;
            RenderScreen();
            UpdateInterface();
            Play(clickSound);
        }
        public void OpenReversiSettings()
        {
            CancelPendingActivity();
            Rules.Reset();
            CurrentScreen = GameScreen.Settings;
            RenderScreen();
            UpdateInterface();
            Play(clickSound);
        }
        public void ConfigureMatch(bool localTwoPlayer, int level, int playerColor, bool assist, bool enableSound)
        {
            if (playerColor != ReversiRules.Black && playerColor != ReversiRules.White)
                throw new System.ArgumentOutOfRangeException(nameof(playerColor));
            pendingTwoPlayers = localTwoPlayer;
            pendingDifficulty = Mathf.Clamp(level, 0, 2);
            pendingPlayer = playerColor;
            pendingAssist = assist;
            pendingSound = enableSound;
            if (CurrentScreen == GameScreen.Settings) { RenderScreen(); UpdateInterface(); }
        }
        public void StartMatch()
        {
            twoPlayers = pendingTwoPlayers;
            difficulty = pendingDifficulty;
            humanPlayer = pendingPlayer;
            showLegal = pendingAssist;
            sound = pendingSound;
            BeginMatch();
        }
        public void NewGame() { BeginMatch(); }
        void BeginMatch()
        {
            CancelPendingActivity();
            Rules.Reset();
            CurrentScreen = GameScreen.Match;
            RenderScreen();
            board.ShowLegal = showLegal;
            UpdateInterface();
            Play(clickSound);
            ScheduleComputerIfNeeded();
        }
        void RequestReset()
        {
            if (Rules.MoveCount == 0) { NewGame(); return; }
            ShowConfirm("はじめからあそぶ？", "今の対局をリセットして、新しくはじめます。", NewGame);
        }
        void RequestSettings()
        {
            if (Rules.MoveCount == 0 || Rules.IsFinished) { OpenReversiSettings(); return; }
            ShowConfirm("設定を変える？", "今の対局を終了して、リバーシの設定に戻ります。", OpenReversiSettings);
        }
        void RequestGameSelection()
        {
            if (Rules.MoveCount == 0 || Rules.IsFinished) { ShowGameSelection(); return; }
            ShowConfirm("ゲーム選択に戻る？", "今の対局を終了して、ゲーム選択に戻ります。", ShowGameSelection);
        }
        void Update()
        {
            if (Rules == null) return;
            bool canMove = CanHumanMove;
            if (canMove != previousCanMove) { previousCanMove = canMove; UpdateInterface(); }
            if (toast != "" && Time.unscaledTime > toastUntil) { toast = ""; UpdateInterface(); }
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (modal != null) CloseModal();
                else if (CurrentScreen == GameScreen.Match) ShowPause();
                else if (CurrentScreen == GameScreen.Settings) ShowGameSelection();
                return;
            }
            if (modal != null) return;
            if (CurrentScreen != GameScreen.Match)
            {
                if (keyboard.enterKey.wasPressedThisFrame)
                {
                    if (CurrentScreen == GameScreen.GameSelection) OpenReversiSettings();
                    else StartMatch();
                }
                return;
            }
            if (keyboard.hKey.wasPressedThisFrame) ShowHint();
            if (keyboard.zKey.wasPressedThisFrame) UndoMove();
            if (keyboard.rKey.wasPressedThisFrame) RequestReset();
            if (!CanHumanMove) return;
            int dr = 0, dc = 0;
            if (keyboard.leftArrowKey.wasPressedThisFrame) dc = -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame) dc = 1;
            if (keyboard.upArrowKey.wasPressedThisFrame) dr = -1;
            if (keyboard.downArrowKey.wasPressedThisFrame) dr = 1;
            if (dr != 0 || dc != 0)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                if (board.FocusRow < 0)
                {
                    Move first = Rules.LegalMoves()[0];
                    board.FocusRow = first.Row; board.FocusCol = first.Col;
                }
                else
                {
                    board.FocusRow = (board.FocusRow + dr + 8) % 8;
                    board.FocusCol = (board.FocusCol + dc + 8) % 8;
                }
                board.Refresh();
            }
            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) && board.FocusRow >= 0)
                PlayCell(board.FocusRow, board.FocusCol);
        }
        public void PlayCell(int row, int col)
        {
            if (!CanHumanMove) return;
            if (Rules.GetFlips(row, col, Rules.CurrentPlayer).Count == 0)
            {
                Notify("相手のコマをはさめる場所に置こう。");
                return;
            }
            if (!twoPlayers) humanTurnStarts.Push(Rules.MoveCount);
            ApplyMove(row, col);
        }
        bool ApplyMove(int row, int col)
        {
            if (CurrentScreen != GameScreen.Match) return false;
            int player = Rules.CurrentPlayer;
            var flips = Rules.GetFlips(row, col, player);
            if (!Rules.TryMove(row, col)) return false;
            board.HintRow = board.HintCol = -1;
            AnimationEnd = Time.unscaledTime + .64f + flips.Count * .035f;
            board.AnimateMove(row, col, flips, player);
            Play(placeSound);
            StartCoroutine(FlipAudio(generation));
            UpdateInterface();
            if (Rules.LastPassedPlayer != 0)
                Notify(PlayerName(Rules.LastPassedPlayer) + "はパス。" + PlayerName(Rules.CurrentPlayer) + "の番です。");
            if (Rules.IsFinished) StartCoroutine(ResultAfterAnimation(generation));
            else ScheduleComputerIfNeeded();
            return true;
        }
        IEnumerator FlipAudio(int version)
        {
            yield return new WaitForSecondsRealtime(.19f);
            if (IsCurrentMatch(version)) Play(flipSound);
        }
        bool IsCurrentMatch(int version) => generation == version && CurrentScreen == GameScreen.Match;
        void ScheduleComputerIfNeeded()
        {
            if (!twoPlayers && !thinking && !Rules.IsFinished && Rules.CurrentPlayer != humanPlayer)
                StartCoroutine(ComputerTurn(generation));
        }
        IEnumerator ComputerTurn(int version)
        {
            thinking = true;
            UpdateInterface();
            float readyAt = Mathf.Max(Time.unscaledTime + .45f, AnimationEnd + .30f);
            while (Time.unscaledTime < readyAt || modal != null)
            {
                if (!IsCurrentMatch(version)) yield break;
                yield return null;
            }
            if (!IsCurrentMatch(version)) yield break;
            var snapshot = new ReversiRules(Rules.Board, Rules.CurrentPlayer);
            Move move;
#if UNITY_WEBGL && !UNITY_EDITOR
            move = snapshot.BestMove(difficulty);
#else
            int level = difficulty;
            Task<Move> search = Task.Run(() => snapshot.BestMove(level));
            while (!search.IsCompleted)
            {
                if (!IsCurrentMatch(version)) yield break;
                yield return null;
            }
            if (!IsCurrentMatch(version)) yield break;
            if (search.IsFaulted)
            {
                Debug.LogException(search.Exception);
                var legal = Rules.LegalMoves();
                move = legal.Count == 0 ? new Move(-1, -1) : legal[0];
            }
            else move = search.Result;
#endif
            while (modal != null)
            {
                if (!IsCurrentMatch(version)) yield break;
                yield return null;
            }
            if (!IsCurrentMatch(version)) yield break;
            thinking = false;
            if (move.Row >= 0) ApplyMove(move.Row, move.Col);
            UpdateInterface();
        }
        IEnumerator ResultAfterAnimation(int version)
        {
            while (Time.unscaledTime < AnimationEnd + .3f || modal != null)
            {
                if (!IsCurrentMatch(version)) yield break;
                yield return null;
            }
            if (IsCurrentMatch(version)) ShowResult();
        }
        public void UndoMove()
        {
            if (!CanUndo) return;
            CloseModal();
            generation++;
            StopAllCoroutines();
            thinking = false;
            AnimationEnd = 0;
            if (twoPlayers) Rules.Undo();
            else
            {
                int target = humanTurnStarts.Pop();
                while (Rules.MoveCount > target && Rules.UndoCount > 0) Rules.Undo();
            }
            board.HintRow = board.FocusRow = -1;
            toast = "";
            board.Refresh();
            Play(clickSound);
            UpdateInterface();
        }
        public void ShowHint()
        {
            if (!IsHumanTurn || modal != null) return;
            Move move = Rules.BestMove(2);
            board.HintRow = move.Row;
            board.HintCol = move.Col;
            board.Refresh();
            Notify("おすすめの場所を囲みました。");
            Play(clickSound);
        }
        void Notify(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 4.2f;
            UpdateInterface();
        }
        static AudioClip Tone(float frequency, float duration, float volume)
        {
            const int rate = 22050;
            int length = (int)(rate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Exp(-t * 35) * Mathf.Min(1, t * 900);
                samples[i] = (Mathf.Sin(t * frequency * 2 * Mathf.PI) + .28f * Mathf.Sin(t * frequency * 4.13f * Mathf.PI)) * envelope * volume;
            }
            var clip = AudioClip.Create("Tabletop tap", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        void Play(AudioClip clip) { if (sound && audioSource && clip) audioSource.PlayOneShot(clip); }
    }
}
