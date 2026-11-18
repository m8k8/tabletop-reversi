using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TabletopReversi
{
    public sealed partial class ReversiGame
    {
        TabletopBoardGraphic board;
        RectTransform canvasRoot, screenRoot, modal;
        RectTransform toastPanel;
        Text toastText;
        UiPlayerBadge lowerPlayer, upperPlayer;
        static Sprite uiRounded, uiCircle;
        static readonly Color Ink = C("#29413F"), Teal = C("#379C9E"), Muted = C("#7B8B80"), Paper = C("#F7F7EF");
        static readonly string[] UiDifficultyLabels = { "やさしい", "ふつう", "つよい" };

        sealed class UiPlayerBadge
        {
            public Image Outline;
            public Text Name;
            public Text Color;
            public int Player;
        }

        public void BuildInterface()
        {
            if (uiRounded == null) uiRounded = MakeUiSprite(false);
            if (uiCircle == null) uiCircle = MakeUiSprite(true);
            var canvasObject = new GameObject("Tabletop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var floorObject = new GameObject("Wooden floor", typeof(RectTransform), typeof(CanvasRenderer), typeof(WoodenFloorGraphic));
            floorObject.transform.SetParent(canvasObject.transform, false);
            var floorRect = floorObject.GetComponent<RectTransform>();
            floorRect.anchorMin = Vector2.zero; floorRect.anchorMax = Vector2.one;
            floorRect.offsetMin = floorRect.offsetMax = Vector2.zero;
            var floor = floorObject.GetComponent<WoodenFloorGraphic>();
            floor.Owner = this; floor.raycastTarget = true;
            canvasRoot = Rect(canvasObject.transform, "Centered layout", 0, 0, 1600, 1000);
            canvasRoot.anchorMin = canvasRoot.anchorMax = new Vector2(.5f, .5f);
            canvasRoot.anchoredPosition = new Vector2(-800, 500);
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void RenderScreen()
        {
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; }
            if (screenRoot != null) { screenRoot.gameObject.SetActive(false); Destroy(screenRoot.gameObject); }
            board = null; lowerPlayer = null; upperPlayer = null; toastPanel = null; toastText = null;
            screenRoot = Rect(canvasRoot, "Screen " + CurrentScreen, 0, 0, 1600, 1000);
            switch (CurrentScreen)
            {
                case GameScreen.GameSelection: BuildGameSelection(); break;
                case GameScreen.Settings: BuildSettings(); break;
                case GameScreen.Match: BuildMatch(); break;
            }
            UpdateInterface();
        }

        void BuildGameSelection()
        {
            Panel(screenRoot, "Ivory menu surface", 0, 0, 1600, 1000, new Color(.969f, .969f, .937f, .94f), 0);
            Brand(screenRoot, 80, 46);
            Label(screenRoot, "1つのあそび", 1210, 48, 310, 36, 14, Muted, false, TextAnchor.MiddleRight);
            Label(screenRoot, "ゲームを選ぶ", 200, 134, 1200, 76, 49, Ink, true, TextAnchor.MiddleCenter);
            Label(screenRoot, "今日は、どのあそびにする？", 300, 211, 1000, 36, 18, Muted, false, TextAnchor.MiddleCenter);
            Panel(screenRoot, "Game card shadow", 454, 259, 692, 568, new Color(.16f, .25f, .20f, .055f), 26).raycastTarget = false;
            var card = Action(screenRoot, "", 454, 250, 692, 568, OpenReversiSettings, C("#FFFFF8"), Ink, 18);
            card.name = "Choose Reversi";
            CreateBoard(card.transform, "Reversi preview", 50, 425, .60f, false);
            Label(card.transform, "リバーシ", 36, 429, 420, 53, 34, Ink, true);
            Label(card.transform, "1–2人  /  ボードゲーム", 38, 489, 430, 31, 15, Muted);
            var choose = Panel(card.transform, "Choose badge", 514, 451, 142, 54, Teal, 25);
            choose.raycastTarget = false;
            Label(choose.transform, "選ぶ  →", 0, 0, 142, 54, 18, Color.white, true, TextAnchor.MiddleCenter);
            Label(screenRoot, "ゲームを選ぶと、対戦方法を設定できます。", 260, 864, 1080, 38, 16, Muted, false, TextAnchor.MiddleCenter);
        }

        void BuildSettings()
        {
            Panel(screenRoot, "Ivory settings surface", 0, 0, 1600, 1000, new Color(.969f, .969f, .937f, .94f), 0);
            var back = Action(screenRoot, "←  ゲーム選択", 80, 46, 211, 45, ShowGameSelection, C("#E9EEE3"), Ink, 16);
            back.name = "Back to games";
            Label(screenRoot, "あそびのテーブル", 1160, 48, 360, 36, 19, Muted, true, TextAnchor.MiddleRight);
            Label(screenRoot, "リバーシの設定", 82, 137, 710, 73, 46, Ink, true);
            Label(screenRoot, "あそびかたを選んで、さあ、ひと勝負。", 86, 215, 705, 43, 18, Muted);
            CreateBoard(screenRoot, "Settings board preview", 70, 790, .72f, false);
            Label(screenRoot, "黒から交互にコマを置き、\n最後に自分の色を多く残そう。", 150, 798, 620, 65, 18, Muted, false, TextAnchor.MiddleCenter);
            var settings = Panel(screenRoot, "Match settings", 860, 128, 660, 784, C("#FFFFF8"), 25).rectTransform;
            Label(settings, "対戦の準備", 40, 24, 580, 44, 25, Ink, true);
            Label(settings, "対戦相手", 40, 89, 580, 30, 15, Muted);
            Option(settings, "CPUと対戦", "Opponent CPU", 40, 127, 282, 51, !pendingTwoPlayers, () => { pendingTwoPlayers = false; RenderScreen(); });
            Option(settings, "ふたりで", "Opponent Local", 338, 127, 282, 51, pendingTwoPlayers, () => { pendingTwoPlayers = true; RenderScreen(); });
            Label(settings, "CPUの強さ", 40, 197, 580, 30, 15, pendingTwoPlayers ? C("#ACB5A7") : Muted);
            if (pendingTwoPlayers)
                Label(settings, "同じ画面で、交互に操作します。", 40, 238, 580, 45, 17, Muted);
            else
                for (int i = 0; i < 3; i++)
                {
                    int value = i;
                    Option(settings, UiDifficultyLabels[i], "Difficulty " + i, 40 + i * 199, 238, 182, 47, pendingDifficulty == i,
                        () => { pendingDifficulty = value; RenderScreen(); });
                }
            Label(settings, pendingTwoPlayers ? "1Pのコマ" : "あなたのコマ", 40, 305, 580, 30, 15, Muted);
            ColorOption(settings, ReversiRules.Black, 40, 344, pendingPlayer == ReversiRules.Black);
            ColorOption(settings, ReversiRules.White, 338, 344, pendingPlayer == ReversiRules.White);
            Label(settings, "アシスト", 40, 421, 300, 30, 18, Ink, true);
            Label(settings, "置ける場所を表示", 40, 454, 300, 25, 13, Muted);
            Option(settings, "ON", "Assist On", 410, 426, 99, 47, pendingAssist, () => { pendingAssist = true; RenderScreen(); });
            Option(settings, "OFF", "Assist Off", 521, 426, 99, 47, !pendingAssist, () => { pendingAssist = false; RenderScreen(); });
            Label(settings, "サウンド", 40, 494, 300, 30, 18, Ink, true);
            Label(settings, "コマの効果音", 40, 527, 300, 25, 13, Muted);
            Option(settings, "ON", "Sound On", 410, 499, 99, 47, pendingSound, () => { pendingSound = true; RenderScreen(); });
            Option(settings, "OFF", "Sound Off", 521, 499, 99, 47, !pendingSound, () => { pendingSound = false; RenderScreen(); });
            Panel(settings, "Settings separator", 40, 578, 580, 1, C("#E0E6DA"), 0).raycastTarget = false;
            var start = Action(settings, "対局をはじめる  →", 40, 605, 580, 64, StartMatch, Teal, Color.white, 22);
            start.name = "Start Match";
            Label(settings, "黒が先手です。\n対局中は Esc / 右クリックでメニューを開けます。", 40, 690, 580, 53, 13, Muted, false, TextAnchor.MiddleCenter);
        }

        void BuildMatch()
        {
            board = CreateBoard(screenRoot, "Reversi board", 197, 935, 1.22f, true);
            board.ShowLegal = showLegal;
            lowerPlayer = PlayerBadge(screenRoot, "Lower player", 56, 885, humanPlayer, false);
            upperPlayer = PlayerBadge(screenRoot, "Upper player", 1320, 40, -humanPlayer, true);
            toastPanel = Panel(screenRoot, "Transient message", 430, 914, 740, 47, new Color(.16f, .23f, .19f, .86f), 23).rectTransform;
            toastPanel.GetComponent<Image>().raycastTarget = false;
            toastText = Label(toastPanel, "", 16, 0, 708, 47, 16, Color.white, false, TextAnchor.MiddleCenter);
            toastPanel.gameObject.SetActive(false);
        }

        UiPlayerBadge PlayerBadge(Transform parent, string name, float x, float y, int player, bool right)
        {
            var outline = Panel(parent, name, x, y, 224, 78, Color.clear, 37);
            outline.raycastTarget = false;
            var background = Panel(outline.transform, "Nameplate", 2, 2, 220, 74, new Color(.13f, .20f, .17f, .73f), 35);
            background.raycastTarget = false;
            float discX = right ? 156 : 13;
            Disc(background.transform, discX, 10, 54, player == ReversiRules.Black ? C("#9DAD9C") : C("#7E8D7C"));
            Disc(background.transform, discX + 1.5f, 10, 51, player == ReversiRules.Black ? C("#23322A") : C("#CED7C6"));
            float textX = right ? 21 : 80;
            return new UiPlayerBadge
            {
                Outline = outline,
                Player = player,
                Name = Label(background.transform, "", textX, 9, 126, 34, 24, Color.white, true),
                Color = Label(background.transform, "", textX + 1, 44, 119, 21, 13, C("#E2E8DC"))
            };
        }

        void UpdateInterface()
        {
            if (CurrentScreen != GameScreen.Match || Rules == null) return;
            UpdateBadge(lowerPlayer); UpdateBadge(upperPlayer);
            if (toastPanel != null)
            {
                bool visible = !string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil;
                toastPanel.gameObject.SetActive(visible);
                if (visible) toastText.text = toast;
            }
            if (board != null) { board.ShowLegal = showLegal; board.Refresh(); }
        }

        void UpdateBadge(UiPlayerBadge badge)
        {
            if (badge == null) return;
            badge.Name.text = PlayerName(badge.Player);
            badge.Color.text = badge.Player == ReversiRules.Black ? "黒" : "白";
            badge.Outline.color = !Rules.IsFinished && Rules.CurrentPlayer == badge.Player ? C("#4AC9C8") : Color.clear;
        }

        public void ShowPause()
        {
            if (CurrentScreen != GameScreen.Match) return;
            if (modal != null) { CloseModal(); return; }
            var p = CreateModal("対局メニュー", 600);
            Action(p, "つづける", 40, 104, 580, 57, CloseModal, Teal, Color.white, 21).name = "Resume Match";
            var hint = Action(p, "✦  ヒント", 40, 179, 282, 53, () => { CloseModal(); ShowHint(); }, C("#EAF0E3"), Ink, 18);
            hint.interactable = IsHumanTurn;
            var undo = Action(p, "↶  待った", 338, 179, 282, 53, () => { CloseModal(); UndoMove(); }, C("#EAF0E3"), Ink, 18);
            undo.interactable = CanUndo;
            Action(p, "あそびかた", 40, 250, 580, 49, ShowRules, C("#EEF1E8"), Ink, 17);
            Action(p, "はじめから", 40, 313, 580, 49, RequestReset, C("#EEF1E8"), Ink, 17);
            Action(p, "設定を変える", 40, 376, 580, 49, RequestSettings, C("#EEF1E8"), Ink, 17);
            Action(p, "ゲーム選択へ", 40, 439, 580, 49, RequestGameSelection, C("#EEF1E8"), Ink, 17);
            Label(p, "Escでメニューを閉じる", 40, 529, 580, 30, 13, Muted, false, TextAnchor.MiddleCenter);
        }

        RectTransform CreateModal(string title, int height)
        {
            CloseModal();
            modal = Panel(canvasRoot, "Modal shade", 0, 0, 1600, 1000, new Color(.08f, .14f, .11f, .42f), 0).rectTransform;
            var p = Panel(modal, "Dialog", 470, (1000 - height) * .5f, 660, height, C("#FCFCF4"), 25).rectTransform;
            Label(p, title, 40, 27, 518, 53, 29, Ink, true);
            Action(p, "×", 580, 28, 43, 43, CloseModal, C("#EDF1E7"), Muted, 25);
            Play(clickSound);
            return p;
        }

        public void CloseModal()
        {
            if (modal == null) return;
            modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null;
            UpdateInterface();
        }

        void ShowConfirm(string title, string message, Action yes)
        {
            var p = CreateModal(title, 312);
            Label(p, message, 40, 111, 580, 69, 18, Muted);
            Action(p, "キャンセル", 40, 220, 282, 54, CloseModal, C("#EAF0E3"), Ink, 18);
            string accept = title.Contains("戻") ? "戻る" : title.Contains("はじめ") ? "はじめる" : "変更する";
            Action(p, accept, 338, 220, 282, 54, () => { CloseModal(); yes(); }, Teal, Color.white, 18);
        }

        public void ShowRules()
        {
            var p = CreateModal("リバーシのあそびかた", 636);
            Label(p, "はさんで、ひっくり返そう。", 40, 109, 580, 39, 23, Teal, true);
            Label(p, "01", 40, 176, 56, 44, 24, Teal, true);
            Label(p, "黒から交互に、コマを1つずつ置きます。", 111, 174, 509, 51, 17, Ink);
            Label(p, "02", 40, 250, 56, 44, 24, Teal, true);
            Label(p, "タテ・ヨコ・ナナメに相手のコマをはさむと、\nはさんだコマをすべて自分の色に返せます。", 111, 244, 509, 73, 17, Ink);
            Label(p, "03", 40, 345, 56, 44, 24, Teal, true);
            Label(p, "両者が置けなくなったら、おしまい。\n自分の色のコマが多いほうの勝ち！", 111, 339, 509, 73, 17, Ink);
            Label(p, "置ける場所がなければ、自動でパスします。", 40, 435, 580, 33, 14, Muted);
            Label(p, "矢印＋Enter：着手 / H：ヒント / Z：待った\nEsc・右クリック：対局メニュー", 40, 477, 580, 46, 13, Muted);
            Action(p, "わかった", 40, 556, 580, 51, CloseModal, Teal, Color.white, 19);
        }

        void ShowResult()
        {
            int lower = Rules.Count(humanPlayer), upper = Rules.Count(-humanPlayer);
            string winner = lower == upper ? "引き分け！" : PlayerName(lower > upper ? humanPlayer : -humanPlayer) + "の勝ち！";
            var p = CreateModal(winner, 449);
            Label(p, lower.ToString("00") + "  –  " + upper.ToString("00"), 40, 108, 580, 95, 65, Ink, true, TextAnchor.MiddleCenter);
            Label(p, PlayerName(humanPlayer) + "（" + (humanPlayer == 1 ? "黒" : "白") + "）", 40, 210, 282, 31, 17, Muted, false, TextAnchor.MiddleCenter);
            Label(p, PlayerName(-humanPlayer) + "（" + (humanPlayer == 1 ? "白" : "黒") + "）", 338, 210, 282, 31, 17, Muted, false, TextAnchor.MiddleCenter);
            Label(p, "いい勝負でした。また、ひと勝負。", 40, 268, 580, 35, 18, Muted, false, TextAnchor.MiddleCenter);
            Action(p, "ゲーム選択", 40, 348, 282, 56, ShowGameSelection, C("#EAF0E3"), Ink, 18);
            Action(p, "もう一度あそぶ", 338, 348, 282, 56, NewGame, Teal, Color.white, 18);
        }

        void Brand(Transform parent, float x, float y)
        {
            Disc(parent, x, y + 1, 15, C("#2B423A")); Disc(parent, x + 19, y + 1, 15, C("#FFFFF6"));
            Disc(parent, x, y + 20, 15, C("#FFFFF6")); Disc(parent, x + 19, y + 20, 15, Teal);
            Label(parent, "あそびのテーブル", x + 53, y - 1, 355, 40, 23, Ink, true);
        }

        TabletopBoardGraphic CreateBoard(Transform parent, string name, float x, float bottom, float scale, bool interactive)
        {
            var r = Rect(parent, name, x, bottom, 990, 700);
            r.pivot = Vector2.zero; r.localScale = Vector3.one * scale;
            var view = r.gameObject.AddComponent<TabletopBoardGraphic>();
            view.Owner = this; view.raycastTarget = interactive; view.ShowLegal = interactive && showLegal;
            return view;
        }

        Button Option(Transform parent, string text, string name, float x, float y, float w, float h, bool selected, Action callback)
        {
            var result = Action(parent, text, x, y, w, h, callback, selected ? Teal : C("#EDF1E7"), selected ? Color.white : C("#6C8173"), 17);
            result.name = name;
            return result;
        }

        void ColorOption(Transform parent, int player, float x, float y, bool selected)
        {
            var b = Action(parent, "", x, y, 282, 54, () => { pendingPlayer = player; RenderScreen(); }, selected ? Teal : C("#EDF1E7"), Ink, 17);
            b.name = player == ReversiRules.Black ? "Color Black" : "Color White";
            Disc(b.transform, 20, 12, 31, player == ReversiRules.Black ? C("#223129") : C("#CED7C6"));
            Label(b.transform, player == ReversiRules.Black ? "黒（先手）" : "白（後手）", 63, 0, 202, 54, 17, selected ? Color.white : Ink, true);
        }

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
        static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        static Image Panel(Transform parent, string name, float x, float y, float width, float height, Color color, float radius)
        {
            var rect = Rect(parent, name, x, y, width, height); var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            if (radius > 0) { image.sprite = uiRounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 20 / radius; }
            return image;
        }
        static Image Disc(Transform parent, float x, float y, float size, Color color)
        {
            var result = Panel(parent, "Disc", x, y, size, size, color, 0); result.sprite = uiCircle; result.raycastTarget = false; return result;
        }
        Text Label(Transform parent, string text, float x, float y, float width, float height, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, text, x, y, width, height); var label = rect.gameObject.AddComponent<Text>();
            label.font = bold && BoldFont != null ? BoldFont : JapaneseFont; label.fontSize = size;
            label.fontStyle = bold && BoldFont == null ? FontStyle.Bold : FontStyle.Normal;
            label.color = color; label.text = text; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false; label.supportRichText = false; label.lineSpacing = 1.15f; return label;
        }
        Button Action(Transform parent, string text, float x, float y, float width, float height, Action callback, Color background, Color foreground, int size)
        {
            var image = Panel(parent, "Button " + text, x, y, width, height, background, 21);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.93f, .98f, .95f);
            colors.pressedColor = new Color(.80f, .91f, .85f); colors.selectedColor = Color.white; colors.disabledColor = new Color(1, 1, 1, .35f); colors.fadeDuration = .13f;
            button.colors = colors; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); callback(); });
            Label(image.transform, text, 0, 0, width, height, size, foreground, true, TextAnchor.MiddleCenter); return button;
        }
        static Sprite MakeUiSprite(bool circle)
        {
            const int size = 64; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "UI circle" : "UI soft rectangle", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float distance;
                if (circle) distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) - 31;
                else { var q = new Vector2(Mathf.Abs(x + .5f - 32) - 12, Mathf.Abs(y + .5f - 32) - 12); distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude - 19; }
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - distance));
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
        }
    }
}
