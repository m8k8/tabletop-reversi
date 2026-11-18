using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TabletopReversi
{
    // Vector-drawn tabletop: every board edge, piece and shadow is original geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletopBoardGraphic : MaskableGraphic, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public ReversiGame Owner;
        public int FocusRow = -1, FocusCol = -1;
        public int HintRow = -1, HintCol = -1;
        public bool ShowLegal = true;
        readonly Dictionary<int, float> animations = new Dictionary<int, float>();
        readonly Dictionary<int, int> oldColors = new Dictionary<int, int>();
        readonly Vector2 tl = new Vector2(130, 635), tr = new Vector2(858, 635);
        readonly Vector2 bl = new Vector2(47, 103), br = new Vector2(941, 103);
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
        Vector2 P(float row, float col) => Vector2.LerpUnclamped(Vector2.LerpUnclamped(tl, bl, row / 8), Vector2.LerpUnclamped(tr, br, row / 8), col / 8);
        public void Refresh() => SetVerticesDirty();
        public void AnimateMove(int row, int col, List<Move> flips, int player)
        {
            animations.Clear(); oldColors.Clear();
            animations[row * 8 + col] = Time.unscaledTime; oldColors[row * 8 + col] = 0;
            for (int i = 0; i < flips.Count; i++) { int k = flips[i].Row * 8 + flips[i].Col; animations[k] = Time.unscaledTime + .10f + i * .035f; oldColors[k] = -player; }
            SetVerticesDirty();
        }
        void Update() { if (animations.Count > 0) { SetVerticesDirty(); if (Time.unscaledTime > Owner.AnimationEnd) { animations.Clear(); oldColors.Clear(); } } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Owner == null || Owner.Rules == null) return;
            // A broad ambient shadow and a layered walnut frame give the board real weight.
            for (int n = 12; n >= 1; n--)
            {
                float e = n * 2.7f; Color s = new Color(.16f, .20f, .17f, .008f);
                Quad(vh, tl + new Vector2(-36-e, 23+e-24), tr + new Vector2(36+e,23+e-24), br + new Vector2(37+e,-35-e-24), bl + new Vector2(-37-e,-35-e-24),s);
            }
            Vector2 a=tl+new Vector2(-31,25), b=tr+new Vector2(31,25), c=br+new Vector2(34,-32), d=bl+new Vector2(-34,-32);
            Quad(vh,a+Vector2.down*15,b+Vector2.down*15,c+Vector2.down*15,d+Vector2.down*15,C("#4F3528"));
            Quad(vh,a,b,c,d,C("#80563B"));
            Quad(vh,a,b,tr+new Vector2(8,8),tl+new Vector2(-8,8),C("#B18A60"));
            Quad(vh,a,tl+new Vector2(-8,8),bl+new Vector2(-8,-8),d,C("#987048"));
            Quad(vh,bl+new Vector2(-8,-8),br+new Vector2(8,-8),c,d,C("#A77B50"));
            Quad(vh,tr+new Vector2(8,8),b,c,br+new Vector2(8,-8),C("#69472F"));
            for(int i=0;i<22;i++) { float t=i/22f; Line(vh, Vector2.Lerp(d,c,t),Vector2.Lerp(d,c,t)+new Vector2(110,1),.7f,new Color(1,.89f,.66f,.09f)); }
            Quad(vh,tl+new Vector2(-7,7),tr+new Vector2(7,7),br+new Vector2(7,-7),bl+new Vector2(-7,-7),C("#183E31"));
            Quad(vh,tl,tr,br,bl,C("#397A5D"));
            for(int row=0;row<8;row++) for(int col=0;col<8;col++)
            {
                float tone = ((row+col)%2 == 0 ? 0 : .014f) + row*.0012f;
                Quad(vh,P(row,col),P(row,col+1),P(row+1,col+1),P(row+1,col),new Color(.215f+tone,.485f+tone,.365f+tone));
            }
            // Fine weave, intentionally restrained so move markers remain clear.
            
            for(int i=0;i<=8;i++){Line(vh,P(i,0),P(i,8),1.6f,C("#234E3C"));Line(vh,P(0,i),P(8,i),1.6f,C("#234E3C"));}
            for(int r=2;r<=6;r+=4)for(int c0=2;c0<=6;c0+=4)Ellipse(vh,P(r,c0),3,2.4f,C("#203F31"));
            if(!Owner.Rules.IsFinished && Owner.CanHumanMove)
            {
                if(ShowLegal)foreach(Move m in Owner.Rules.LegalMoves()) {var p=P(m.Row+.5f,m.Col+.5f);Ellipse(vh,p,7.3f,5.5f,new Color(.89f,1,.85f,.53f));}
                if(FocusRow>=0 && Owner.Rules.GetFlips(FocusRow,FocusCol,Owner.Rules.CurrentPlayer).Count>0)
                {
                    Quad(vh,P(FocusRow+.04f,FocusCol+.04f),P(FocusRow+.04f,FocusCol+.96f),P(FocusRow+.96f,FocusCol+.96f),P(FocusRow+.96f,FocusCol+.04f),new Color(.86f,1,.77f,.13f));
                    DrawPiece(vh,P(FocusRow+.5f,FocusCol+.5f),FocusRow,Owner.Rules.CurrentPlayer,.40f,1);
                }
            }
            for(int row=0;row<8;row++)for(int col=0;col<8;col++)
            {
                int player=Owner.Rules.Board[row,col];if(player==0)continue;
                int key=row*8+col;float scale=1,raise=0;
                if(animations.TryGetValue(key,out float start))
                {
                    float t=Mathf.Clamp01((Time.unscaledTime-start)/.44f);
                    if(oldColors[key]==0){scale=Mathf.Lerp(.2f,1,Mathf.Sin(t*Mathf.PI*.5f));raise=Mathf.Sin(t*Mathf.PI)*16;}
                    else {scale=Mathf.Max(.045f,Mathf.Abs(Mathf.Cos(t*Mathf.PI)));if(t<.5f)player=oldColors[key];raise=Mathf.Sin(t*Mathf.PI)*15;}
                }
                DrawPiece(vh,P(row+.5f,col+.5f)+new Vector2(0,raise),row,player,1,scale);
                if(row==Owner.Rules.LastRow&&col==Owner.Rules.LastCol)Ellipse(vh,P(row+.5f,col+.5f)+Vector2.up*4,3.1f,2.8f,C("#E7BC60"));
            }
            if(HintRow>=0 && !Owner.Rules.IsFinished)
            {
                Vector2 p=P(HintRow+.5f,HintCol+.5f);float rx=35+HintRow*.7f;
                Ring(vh,p,rx,rx*.70f,3.0f,C("#F5CF77"));
            }
        }
        void DrawPiece(VertexHelper vh,Vector2 p,int row,int player,float alpha,float sy)
        {
            float rx=33.5f+row*.72f, ry=(24+row*.40f)*sy;
            Ellipse(vh,p+new Vector2(3,-6),rx+3,Mathf.Max(5,ry+2),new Color(.04f,.09f,.06f,.23f*alpha));
            Color edge=player==1?C("#101A1D"):C("#B5BDB6");edge.a=alpha;
            Ellipse(vh,p,rx,Mathf.Max(3,ry),edge);
            Color rim=player==1?C("#364248"):C("#F4F1E7");rim.a=alpha;
            Ellipse(vh,p+Vector2.up*4,rx,Mathf.Max(2.6f,ry),rim);
            Color center=player==1?C("#253239"):C("#FFFFF7");center.a=alpha;
            Color outer=player==1?C("#182429"):C("#DFE4D9");outer.a=alpha;
            GradientEllipse(vh,p+Vector2.up*5,rx-2,Mathf.Max(2,ry-1.5f),center,outer);
            Color shine=player==1?new Color(.6f,.69f,.71f,.13f*alpha):new Color(1,1,1,.73f*alpha);
            Arc(vh,p+Vector2.up*5,rx-5,Mathf.Max(2,ry-4),.35f,2.2f,1.3f,shine);
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {int i=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color){Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;Quad(vh,a+n,b+n,b-n,a-n,color);}
        static void Ellipse(VertexHelper vh,Vector2 p,float rx,float ry,Color col)=>GradientEllipse(vh,p,rx,ry,col,col);
        static void GradientEllipse(VertexHelper vh,Vector2 p,float rx,float ry,Color center,Color edge)
        {int k=vh.currentVertCount;vh.AddVert(p,center,Vector2.zero);const int n=56;for(int i=0;i<=n;i++){float t=i*Mathf.PI*2/n;vh.AddVert(p+new Vector2(Mathf.Cos(t)*rx,Mathf.Sin(t)*ry),edge,Vector2.zero);if(i>0)vh.AddTriangle(k,k+i,k+i+1);}}
        static void Ring(VertexHelper vh,Vector2 p,float rx,float ry,float w,Color col)=>Arc(vh,p,rx,ry,0,Mathf.PI*2,w,col);
        static void Arc(VertexHelper vh,Vector2 p,float rx,float ry,float from,float to,float w,Color col){Vector2 prev=p+new Vector2(Mathf.Cos(from)*rx,Mathf.Sin(from)*ry);for(int i=1;i<=56;i++){float a=Mathf.Lerp(from,to,i/56f);Vector2 q=p+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);Line(vh,prev,q,w,col);prev=q;}}
        public bool CellAt(Vector2 local,out int row,out int col)
        {
            float t=(tl.y-local.y)/(tl.y-bl.y);row=Mathf.FloorToInt(t*8);
            float left=Mathf.LerpUnclamped(tl.x,bl.x,t),right=Mathf.LerpUnclamped(tr.x,br.x,t);
            col=Mathf.FloorToInt((local.x-left)/(right-left)*8);return row>=0&&row<8&&col>=0&&col<8;
        }
        bool Pointer(PointerEventData e,out int row,out int col){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out Vector2 p);p+=rectTransform.pivot*rectTransform.rect.size;return CellAt(p,out row,out col);}
        public void OnPointerClick(PointerEventData e){EventSystem.current.SetSelectedGameObject(null);if(Pointer(e,out int r,out int c))Owner.PlayCell(r,c);}
        public void OnPointerMove(PointerEventData e){if(Pointer(e,out int r,out int c)){FocusRow=r;FocusCol=c;}else{FocusRow=-1;FocusCol=-1;}SetVerticesDirty();}
        public void OnPointerExit(PointerEventData e){FocusRow=-1;FocusCol=-1;SetVerticesDirty();}
    }
}
