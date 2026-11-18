using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TabletopReversi
{
    public sealed class ReversiGame : MonoBehaviour
    {
        public Font JapaneseFont;
        public Font BoldFont;
        public ReversiRules Rules { get; private set; }
        public float AnimationEnd { get; private set; }
        public bool CanHumanMove => !thinking && !Rules.IsFinished && (twoPlayers || Rules.CurrentPlayer == ReversiRules.Black) && Time.unscaledTime >= AnimationEnd && modal == null;
        public bool IsTwoPlayer => twoPlayers;
        public bool IsThinking => thinking;
        public int Difficulty => difficulty;
        public TabletopBoardGraphic BoardView => board;
        TabletopBoardGraphic board;
        RectTransform canvasRoot, modal;
        Text blackScore, whiteScore, turnLabel, statusLabel, moveLabel, difficultyLabel, guideLabel, soundLabel;
        Image blackCard, whiteCard, blackDot, whiteDot;
        Button soloButton, duoButton, undoButton, hintButton;
        bool twoPlayers, thinking, sound=true, showLegal=true, previousCanMove;
        int difficulty=1, generation;
        string toast=""; float toastUntil;
        AudioSource audioSource; AudioClip placeSound, flipSound, clickSound;
        static Sprite rounded, circle;
        static readonly Color Ink=C("#29413F"), Teal=C("#379C9E"), Muted=C("#84918A"), Paper=C("#F7F7EF");
        static readonly string[] Levels={"やさしい", "ふつう", "つよい"};
        static Color C(string hex){ColorUtility.TryParseHtmlString(hex,out Color c);return c;}
        void Awake(){Initialize();}
        void OnEnable(){if(Application.isPlaying && Rules==null)Initialize();}
        void Initialize()
        {
            StopAllCoroutines();generation++;thinking=false;AnimationEnd=0;toast="";modal=null;previousCanMove=false;
            for(int i=transform.childCount-1;i>=0;i--){var child=transform.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            Rules=new ReversiRules();
            if(JapaneseFont==null)JapaneseFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            audioSource=GetComponent<AudioSource>();if(audioSource==null)audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;
            placeSound=Tone(390,.13f,.18f);flipSound=Tone(610,.10f,.10f);clickSound=Tone(810,.055f,.065f);
            BuildInterface();UpdateInterface();
        }
        void OnDestroy(){generation++;if(placeSound)Destroy(placeSound);if(flipSound)Destroy(flipSound);if(clickSound)Destroy(clickSound);}
        public void BuildInterface()
        {
            if(rounded==null)rounded=MakeSprite(false);if(circle==null)circle=MakeSprite(true);
            var canvasGo=new GameObject("Tabletop Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);
            canvasRoot=canvasGo.GetComponent<RectTransform>();var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var content=Rect(canvasRoot,"Centered game layout",0,0,1600,1000);content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.anchoredPosition=new Vector2(-800,500);canvasRoot=content;
            if(FindAnyObjectByType<EventSystem>()==null){new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));}
            var bg=Panel(canvasRoot,"Warm paper background",0,0,1600,1000,Paper,0);bg.raycastTarget=false;
            // Subtle original diamond weave ties the calm interface to a physical tabletop.
            for(int x=-950;x<2600;x+=45){var line=Panel(canvasRoot,"Paper weave",x,0,1,1750,new Color(.28f,.47f,.41f,.022f),0);line.rectTransform.localEulerAngles=new Vector3(0,0,42);line.raycastTarget=false;}
            Panel(canvasRoot,"Header rule",56,96,1488,1,C("#DDE4DB"),0);
            Disc(canvasRoot,66,31,17,C("#263B3B"));Disc(canvasRoot,86,31,17,Color.white);Disc(canvasRoot,66,51,17,Color.white);Disc(canvasRoot,86,51,17,Teal);
            Label(canvasRoot,"あそびのテーブル",121,25,350,40,25,Ink,true);
            Label(canvasRoot,"ひと息、ひと勝負。",124,64,350,22,11,Muted);
            Label(canvasRoot,"T A B L E T O P   C O L L E C T I O N",1100,34,435,26,12,Muted,false,TextAnchor.MiddleRight);
            Label(canvasRoot,"01  /  BOARD GAME",1210,62,325,22,11,Teal,false,TextAnchor.MiddleRight);
            Label(canvasRoot,"リバーシ",67,119,520,86,61,Ink,true);
            Label(canvasRoot,"はさんで、返して。最後の一手まで、おもしろい。",73,211,850,35,17,C("#7B8A80"));
            var tag=Panel(canvasRoot,"Board game category",790,147,215,36,C("#E9EEE3"),18);Label(tag.rectTransform,"ボードゲーム   /   1–2人",0,0,215,36,12,C("#74846F"),false,TextAnchor.MiddleCenter);
            // Left: the board, rendered as a single mesh with perspective and animated pieces.
            var boardGo=new GameObject("Walnut & felt board",typeof(RectTransform),typeof(CanvasRenderer),typeof(TabletopBoardGraphic));boardGo.transform.SetParent(canvasRoot,false);
            var rt=boardGo.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=Vector2.zero;rt.anchoredPosition=new Vector2(38,-910);rt.sizeDelta=new Vector2(990,700);
            board=boardGo.GetComponent<TabletopBoardGraphic>();board.Owner=this;board.raycastTarget=true;
            for(int c=0;c<8;c++)Label(canvasRoot,((char)('A'+c)).ToString(),197+c*91,249,34,24,11,C("#E0D3B2"),false,TextAnchor.MiddleCenter);
            for(int r=0;r<8;r++)Label(canvasRoot,(r+1).ToString(),133-r*10.4f,291+r*66.5f,25,26,11,C("#80907D"),false,TextAnchor.MiddleCenter);
            Label(canvasRoot,"●",348,875,24,25,17,C("#85A38A"));Label(canvasRoot,"置けるところ",376,876,160,26,13,C("#849080"));
            Label(canvasRoot,"●",560,875,22,25,15,C("#D6B462"));Label(canvasRoot,"最後に置いたコマ",588,876,210,26,13,C("#849080"));
            // Right: a single quiet player panel, with a clear hierarchy and generous spacing.
            var side=Panel(canvasRoot,"Match panel",1084,125,460,740,new Color(1,1,1,.78f),24);
            Label(side.rectTransform,"さあ、あそぼう。",28,25,395,36,23,Ink,true);
            Panel(side.rectTransform,"Mode rail",28,80,404,51,C("#EEF2EC"),25);
            soloButton=Action(side.rectTransform,"ひとりで",34,85,197,41,()=>ChangeMode(false),Teal,Color.white,16);
            duoButton=Action(side.rectTransform,"ふたりで",231,85,195,41,()=>ChangeMode(true),Color.clear,C("#8B998E"),16);
            Label(side.rectTransform,"対戦相手",29,154,170,30,12,Muted);
            difficultyLabel=Label(side.rectTransform,"",192,153,198,32,15,Ink,true,TextAnchor.MiddleRight);
            Action(side.rectTransform,"›",394,151,37,35,CycleDifficulty,Color.clear,Teal,25);
            blackCard=Panel(side.rectTransform,"Black player",27,206,406,111,C("#E7F2ED"),15);
            blackDot=Disc(blackCard.rectTransform,23,27,54,C("#26363A"));Disc(blackCard.rectTransform,32,32,31,C("#34464A"));
            Label(blackCard.rectTransform,"黒",94,23,90,25,12,C("#82938A"));Label(blackCard.rectTransform,"あなた",94,49,180,37,23,Ink,true);
            blackScore=Label(blackCard.rectTransform,"02",289,19,91,73,49,Ink,true,TextAnchor.MiddleRight);
            whiteCard=Panel(side.rectTransform,"White player",27,329,406,111,C("#F1F3EE"),15);
            whiteDot=Disc(whiteCard.rectTransform,23,27,54,C("#D1D9D0"));Disc(whiteCard.rectTransform,25,25,50,C("#FFFFF8"));
            Label(whiteCard.rectTransform,"白",94,23,90,25,12,C("#82938A"));Label(whiteCard.rectTransform,"コンピューター",94,49,210,37,21,Ink,true);
            whiteScore=Label(whiteCard.rectTransform,"02",289,19,91,73,49,Ink,true,TextAnchor.MiddleRight);
            turnLabel=Label(side.rectTransform,"",28,469,404,37,24,Teal,true,TextAnchor.MiddleCenter);
            statusLabel=Label(side.rectTransform,"",28,516,404,48,13,C("#859284"),false,TextAnchor.MiddleCenter);
            Panel(side.rectTransform,"Action separator",28,586,404,1,C("#E4EADF"),0);
            undoButton=Action(side.rectTransform,"↶  待った",28,606,193,51,UndoMove,C("#F0F3EC"),Ink,16);
            hintButton=Action(side.rectTransform,"✦  ヒント",236,606,196,51,ShowHint,C("#EDF4E9"),C("#71946C"),16);
            moveLabel=Label(side.rectTransform,"",28,678,404,30,11,Muted,false,TextAnchor.MiddleCenter);
            // Utility controls use the same soft pill shape as the primary UI.
            Panel(canvasRoot,"Footer rule",56,931,1488,1,C("#DBE3D7"),0);
            Action(canvasRoot,"?  あそびかた",62,949,168,36,ShowRules,Color.clear,C("#7E8F80"),13);
            var guideButton=Action(canvasRoot,"",258,949,174,36,ToggleGuide,Color.clear,C("#7E8F80"),13);guideLabel=guideButton.GetComponentInChildren<Text>();
            var soundButton=Action(canvasRoot,"",463,949,145,36,ToggleSound,Color.clear,C("#7E8F80"),13);soundLabel=soundButton.GetComponentInChildren<Text>();
            Label(canvasRoot,"のんびり、じっくり。",690,948,250,36,13,C("#A0AC98"),false,TextAnchor.MiddleCenter);
            Action(canvasRoot,"↻  はじめから",1323,948,215,39,RequestReset,C("#E6ECE0"),C("#5F7966"),14);
        }
        void Update()
        {
            if(Rules==null)return;
            bool canMove=CanHumanMove;if(canMove!=previousCanMove){previousCanMove=canMove;UpdateInterface();}
            if(toast!="" && Time.unscaledTime>toastUntil){toast="";UpdateInterface();}
            var k=Keyboard.current;if(k==null)return;
            if(k.escapeKey.wasPressedThisFrame){CloseModal();return;}
            if(modal!=null)return;
            if(k.hKey.wasPressedThisFrame)ShowHint();if(k.zKey.wasPressedThisFrame)UndoMove();if(k.rKey.wasPressedThisFrame)RequestReset();
            if(!CanHumanMove)return;
            int dr=0,dc=0;if(k.leftArrowKey.wasPressedThisFrame)dc=-1;if(k.rightArrowKey.wasPressedThisFrame)dc=1;if(k.upArrowKey.wasPressedThisFrame)dr=-1;if(k.downArrowKey.wasPressedThisFrame)dr=1;
            if(dr!=0||dc!=0){EventSystem.current.SetSelectedGameObject(null);if(board.FocusRow<0){var moves=Rules.LegalMoves();board.FocusRow=moves[0].Row;board.FocusCol=moves[0].Col;}else{board.FocusRow=(board.FocusRow+dr+8)%8;board.FocusCol=(board.FocusCol+dc+8)%8;}board.Refresh();}
            if((k.enterKey.wasPressedThisFrame||k.spaceKey.wasPressedThisFrame)&&board.FocusRow>=0)PlayCell(board.FocusRow,board.FocusCol);
        }
        public void PlayCell(int row,int col)
        {
            if(!CanHumanMove)return;
            if(Rules.GetFlips(row,col,Rules.CurrentPlayer).Count==0){Notify("相手のコマをはさめる場所に置こう。");return;}
            ApplyMove(row,col);
        }
        void ApplyMove(int row,int col)
        {
            int player=Rules.CurrentPlayer;var flips=Rules.GetFlips(row,col,player);if(!Rules.TryMove(row,col))return;
            board.HintRow=-1;board.HintCol=-1;
            AnimationEnd=Time.unscaledTime+.64f+flips.Count*.035f;
            board.AnimateMove(row,col,flips,player);Play(placeSound);
            StartCoroutine(FlipAudio(generation));UpdateInterface();
            if(Rules.IsFinished)StartCoroutine(ResultAfterAnimation(generation));
            else if(!twoPlayers&&Rules.CurrentPlayer==ReversiRules.White)StartCoroutine(ComputerTurn(generation));
            if(Rules.LastPassedPlayer!=0)Notify((Rules.LastPassedPlayer==1?"黒":"白")+"は置けないためパス。続けて置こう。");
        }
        IEnumerator FlipAudio(int version){yield return new WaitForSecondsRealtime(.19f);if(generation==version)Play(flipSound);}
        IEnumerator ComputerTurn(int version)
        {
            thinking=true;UpdateInterface();
            while(Time.unscaledTime<AnimationEnd+.30f){if(version!=generation)yield break;yield return null;}
            var snapshot=new ReversiRules(Rules.Board,Rules.CurrentPlayer);int level=difficulty;
            Task<Move> search=Task.Run(()=>snapshot.BestMove(level));
            while(!search.IsCompleted){if(version!=generation)yield break;yield return null;}
            if(version!=generation)yield break;
            thinking=false;
            if(search.IsFaulted){Debug.LogException(search.Exception);var legal=Rules.LegalMoves();if(legal.Count>0)ApplyMove(legal[0].Row,legal[0].Col);}
            else{Move m=search.Result;if(m.Row>=0)ApplyMove(m.Row,m.Col);}
            UpdateInterface();
        }
        IEnumerator ResultAfterAnimation(int version)
        {while(Time.unscaledTime<AnimationEnd+.3f){if(version!=generation)yield break;yield return null;}if(version==generation)ShowResult();}
        public void UndoMove()
        {
            if(Rules.UndoCount==0)return;
            CloseModal();generation++;thinking=false;AnimationEnd=0;
            Rules.Undo();if(!twoPlayers)while(Rules.CurrentPlayer==ReversiRules.White&&Rules.UndoCount>0)Rules.Undo();
            board.HintRow=-1;board.FocusRow=-1;toast="";board.Refresh();Play(clickSound);UpdateInterface();
        }
        public void ShowHint()
        {
            if(!CanHumanMove)return;Move move=Rules.BestMove(2);board.HintRow=move.Row;board.HintCol=move.Col;board.Refresh();
            Notify(((char)('A'+move.Col)).ToString()+(move.Row+1)+" がおすすめ。次の展開も考えてみよう。");Play(clickSound);
        }
        void ChangeMode(bool duo)
        {
            if(twoPlayers==duo)return;
            if(Rules.MoveCount>0&&!Rules.IsFinished){ShowConfirm("あそびかたを変える？","対局をリセットして、"+(duo?"ふたりで":"ひとりで")+"あそびます。",()=>{twoPlayers=duo;NewGame();});return;}
            twoPlayers=duo;NewGame();
        }
        void CycleDifficulty(){if(twoPlayers){Notify("ふたりで交代しながらあそべます。");return;}difficulty=(difficulty+1)%3;UpdateInterface();Play(clickSound);}
        public void NewGame()
        {generation++;thinking=false;AnimationEnd=0;Rules.Reset();toast="";board.HintRow=-1;board.FocusRow=-1;CloseModal();board.Refresh();UpdateInterface();Play(clickSound);}
        void RequestReset(){if(Rules.MoveCount==0){NewGame();return;}ShowConfirm("もう一度、はじめから？","今の対局をリセットして、新しくはじめます。",NewGame);}
        void ToggleGuide(){showLegal=!showLegal;board.ShowLegal=showLegal;board.Refresh();UpdateInterface();Play(clickSound);}
        void ToggleSound(){sound=!sound;UpdateInterface();Play(clickSound);}
        void Notify(string message){toast=message;toastUntil=Time.unscaledTime+4.2f;UpdateInterface();}
        void UpdateInterface()
        {
            if(board==null)return;
            blackScore.text=Rules.Count(1).ToString("00");whiteScore.text=Rules.Count(-1).ToString("00");
            blackCard.color=Rules.CurrentPlayer==1&&!Rules.IsFinished?C("#E1F0E8"):C("#F0F3ED");whiteCard.color=Rules.CurrentPlayer==-1&&!Rules.IsFinished?C("#E1F0E8"):C("#F0F3ED");
            blackCard.transform.Find("あなた").GetComponent<Text>().text=twoPlayers?"プレイヤー 1":"あなた";
            whiteCard.transform.Find("コンピューター").GetComponent<Text>().text=twoPlayers?"プレイヤー 2":"コンピューター";
            soloButton.image.color=twoPlayers?Color.clear:Teal;soloButton.GetComponentInChildren<Text>().color=twoPlayers?C("#8B998E"):Color.white;
            duoButton.image.color=twoPlayers?Teal:Color.clear;duoButton.GetComponentInChildren<Text>().color=twoPlayers?Color.white:C("#8B998E");
            difficultyLabel.text=twoPlayers?"おなじ画面で交代":"CPU  ·  "+Levels[difficulty];
            if(Rules.IsFinished){turnLabel.text="対局終了";statusLabel.text="最後まであそんでくれて、ありがとう。";}
            else if(thinking){turnLabel.text="コンピューターの番";statusLabel.text="次の一手を考えています…";}
            else{turnLabel.text=twoPlayers?(Rules.CurrentPlayer==1?"黒の番です":"白の番です"):"あなたの番です";statusLabel.text="相手のコマをはさめる場所に置こう。";}
            if(toast!="")statusLabel.text=toast;
            moveLabel.text=Rules.MoveCount.ToString("00")+" 手目     ·     空きマス "+Rules.Count(0).ToString("00")+"     ·     "+(twoPlayers?"2 PLAYERS":"VS COMPUTER");
            guideLabel.text="◉  ガイド "+(showLegal?"ON":"OFF");soundLabel.text="♪  サウンド "+(sound?"ON":"OFF");
            undoButton.interactable=Rules.UndoCount>0;hintButton.interactable=CanHumanMove;board.Refresh();
        }
        RectTransform CreateModal(string title,int height)
        {
            CloseModal();var shade=Panel(canvasRoot,"Modal shade",0,0,1600,1000,new Color(.09f,.18f,.16f,.38f),0);modal=shade.rectTransform;
            var p=Panel(modal,"Dialog",475,(1000-height)/2f,650,height,C("#FCFCF5"),24);
            Label(p.rectTransform,title,42,32,566,58,30,Ink,true);
            Action(p.rectTransform,"×",570,28,45,45,CloseModal,C("#EDF1E8"),Muted,25);
            Play(clickSound);return p.rectTransform;
        }
        public void CloseModal(){if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;UpdateInterface();}}
        void ShowConfirm(string title,string message,Action yes)
        {var p=CreateModal(title,300);Label(p,message,42,112,566,48,17,Muted);Action(p,"つづける",42,207,266,54,CloseModal,C("#E9EEE4"),Ink,17);Action(p,"はじめる",326,207,282,54,()=>{CloseModal();yes();},Teal,Color.white,17);}
        public void ShowRules()
        {
            var p=CreateModal("リバーシのあそびかた",625);
            Label(p,"はさんで、ひっくり返そう。",42,110,566,38,22,Teal,true);
            Label(p,"01",43,178,55,45,24,Teal,true);Label(p,"黒から交互に、コマを1つずつ置きます。",111,176,485,56,17,Ink);
            Label(p,"02",43,251,55,45,24,Teal,true);Label(p,"タテ・ヨコ・ナナメに相手のコマをはさむと、\nはさんだコマをすべて自分の色に返せます。",111,245,485,73,17,Ink);
            Label(p,"03",43,345,55,45,24,Teal,true);Label(p,"両者が置けなくなったら、おしまい。\n自分の色のコマが多いほうの勝ち！",111,339,485,73,17,Ink);
            Label(p,"置ける場所がなければ、自動でパスします。",43,438,566,37,14,Muted);
            Label(p,"キーボード：矢印＋Enter / H ヒント / Z 待った",43,482,566,32,12,Muted);
            Action(p,"わかった、あそぼう！",42,547,566,49,CloseModal,Teal,Color.white,18);
        }
        void ShowResult()
        {
            int black=Rules.Count(1),white=Rules.Count(-1);string title=black==white?"引き分け！":twoPlayers?(black>white?"黒の勝ち！":"白の勝ち！"):(black>white?"あなたの勝ち！":"コンピューターの勝ち！");
            var p=CreateModal(title,420);Label(p,black.ToString("00")+"  –  "+white.ToString("00"),42,116,566,91,65,Ink,true,TextAnchor.MiddleCenter);
            Label(p,"黒                                      白",42,213,566,30,14,Muted,false,TextAnchor.MiddleCenter);
            Label(p,"いい勝負でした。また、ひと勝負。",42,272,566,32,17,Muted,false,TextAnchor.MiddleCenter);
            Action(p,"盤面を見る",42,335,266,52,CloseModal,C("#E9EEE4"),Ink,17);Action(p,"もう一度あそぶ",326,335,282,52,NewGame,Teal,Color.white,17);
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        static Image Panel(Transform parent,string name,float x,float y,float w,float h,Color color,float radius)
        {var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color;if(radius>0){image.sprite=rounded;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=20/radius;}return image;}
        static Image Disc(Transform parent,float x,float y,float size,Color color){var im=Panel(parent,"Disc",x,y,size,size,color,0);im.sprite=circle;im.raycastTarget=false;return im;}
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft)
        {var r=Rect(parent,text,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=bold&&BoldFont?BoldFont:JapaneseFont;t.fontSize=size;t.fontStyle=bold&&BoldFont==null?FontStyle.Bold:FontStyle.Normal;t.color=color;t.text=text;t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;t.supportRichText=false;t.lineSpacing=1.15f;return t;}
        Button Action(Transform parent,string text,float x,float y,float w,float h,Action callback,Color bg,Color fg,int size)
        {var im=Panel(parent,"Button "+text,x,y,w,h,bg,20);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.92f,.97f,.94f);colors.pressedColor=new Color(.80f,.90f,.85f);colors.selectedColor=Color.white;colors.disabledColor=new Color(1,1,1,.35f);colors.fadeDuration=.12f;b.colors=colors;b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>{EventSystem.current.SetSelectedGameObject(null);callback();});Label(im.rectTransform,text,0,0,w,h,size,fg,true,TextAnchor.MiddleCenter);return b;}
        static Sprite MakeSprite(bool round)
        {int s=64;var tex=new Texture2D(s,s,TextureFormat.RGBA32,false);tex.name=round?"Original circle":"Original soft rectangle";tex.filterMode=FilterMode.Bilinear;var pixels=new Color[s*s];for(int y=0;y<s;y++)for(int x=0;x<s;x++){float d;if(round)d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))-31;else{var q=new Vector2(Mathf.Abs(x+.5f-32)-12,Mathf.Abs(y+.5f-32)-12);d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude-19;}pixels[y*s+x]=new Color(1,1,1,Mathf.Clamp01(.5f-d));}tex.SetPixels(pixels);tex.Apply();return Sprite.Create(tex,new Rect(0,0,s,s),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(22,22,22,22));}
        static AudioClip Tone(float frequency,float duration,float volume)
        {int rate=22050,n=(int)(rate*duration);var samples=new float[n];for(int i=0;i<n;i++){float t=i/(float)rate,env=Mathf.Exp(-t*35)*Mathf.Min(1,t*900);samples[i]=(Mathf.Sin(t*frequency*2*Mathf.PI)+.28f*Mathf.Sin(t*frequency*4.13f*Mathf.PI))*env*volume;}var clip=AudioClip.Create("Tabletop tap",n,1,rate,false);clip.SetData(samples,0);return clip;}
        void Play(AudioClip clip){if(sound&&audioSource&&clip)audioSource.PlayOneShot(clip);}
    }
}
