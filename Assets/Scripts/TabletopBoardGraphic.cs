using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TabletopReversi
{
    /// <summary>Original textured tabletop geometry; the playing plane also drives hit testing.</summary>
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
        const int CornerSegments = 10;
        const int ContourPoints = (CornerSegments + 1) * 4;
        public override Texture mainTexture => TabletopSurfaceAtlas.Texture;

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
        Vector2 P(float row, float col) => Vector2.LerpUnclamped(Vector2.LerpUnclamped(tl, bl, row / 8), Vector2.LerpUnclamped(tr, br, row / 8), col / 8);
        public Vector2 CellCenter(int row, int col) => P(row + .5f, col + .5f);
        public void Refresh() => SetVerticesDirty();
        public void AnimateMove(int row, int col, List<Move> flips, int player)
        {
            animations.Clear(); oldColors.Clear();
            animations[row * 8 + col] = Time.unscaledTime; oldColors[row * 8 + col] = 0;
            for (int i = 0; i < flips.Count; i++)
            {
                int key = flips[i].Row * 8 + flips[i].Col;
                animations[key] = Time.unscaledTime + .10f + i * .035f;
                oldColors[key] = -player;
            }
            SetVerticesDirty();
        }
        void Update()
        {
            if (animations.Count == 0 || Owner == null) return;
            SetVerticesDirty();
            if (Time.unscaledTime > Owner.AnimationEnd) { animations.Clear(); oldColors.Clear(); }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Owner == null || Owner.Rules == null) return;
            DrawFrame(vh);
            DrawCloth(vh);
            if (!Owner.Rules.IsFinished && Owner.CanHumanMove)
            {
                if (ShowLegal)
                    foreach (Move move in Owner.Rules.LegalMoves())
                        Ellipse(vh, CellCenter(move.Row, move.Col), 5.2f, 4.2f, new Color(.79f, .94f, .70f, .56f));
                if (FocusRow >= 0 && Owner.Rules.GetFlips(FocusRow, FocusCol, Owner.Rules.CurrentPlayer).Count > 0)
                {
                    DrawPiece(vh, CellCenter(FocusRow, FocusCol), FocusRow, Owner.Rules.CurrentPlayer, .25f, 1);
                    DrawSelection(vh, FocusRow, FocusCol);
                }
            }
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    int player = Owner.Rules.Board[row, col];
                    if (player == 0) continue;
                    int key = row * 8 + col;
                    float scale = 1, raise = 0;
                    if (animations.TryGetValue(key, out float start))
                    {
                        float t = Mathf.Clamp01((Time.unscaledTime - start) / .44f);
                        if (oldColors[key] == 0)
                        {
                            scale = Mathf.Lerp(.2f, 1, Mathf.Sin(t * Mathf.PI * .5f));
                            raise = Mathf.Sin(t * Mathf.PI) * 16;
                        }
                        else
                        {
                            scale = Mathf.Max(.045f, Mathf.Abs(Mathf.Cos(t * Mathf.PI)));
                            if (t < .5f) player = oldColors[key];
                            raise = Mathf.Sin(t * Mathf.PI) * 15;
                        }
                    }
                    DrawPiece(vh, CellCenter(row, col) + Vector2.up * raise, row, player, 1, scale);
                    if (row == Owner.Rules.LastRow && col == Owner.Rules.LastCol)
                        Ellipse(vh, CellCenter(row, col) + Vector2.up * 3, 2.8f, 2.4f, C("#DEBF6F"));
                }
            if (HintRow >= 0 && !Owner.Rules.IsFinished)
            {
                float rx = 36 + HintRow * .7f;
                Arc(vh, CellCenter(HintRow, HintCol), rx, rx * .77f, 0, Mathf.PI * 2, 2.7f, C("#F5CF77"));
            }
        }
        void DrawFrame(VertexHelper vh)
        {
            // Rounded profiles form a dark walnut crown, a fine inner bead and a recessed lip.
            for (int layer = 7; layer >= 1; layer--)
            {
                float extent = layer * .035f;
                FillContour(vh, Contour(.42f + extent, .52f + extent, .26f + extent, .34f + extent, -15 - layer * 1.1f),
                    new Color(.12f, .12f, .08f, .018f));
            }
            FillContour(vh, Contour(.43f, .52f, .27f, .35f, -12), C("#241B17"));
            var outer = Contour(.43f, .52f, .27f, .35f, 0);
            var lower = Contour(.43f, .52f, .27f, .35f, -12);
            for (int i = 0; i < ContourPoints; i++)
            {
                int j = (i + 1) % ContourPoints;
                Quad(vh, outer[i], outer[j], lower[j], lower[i], C("#30231E"));
            }
            // These samples are the cross-section of the molded wooden rail, outer to inner.
            float[] offsets = { 1, .965f, .91f, .82f, .71f, .60f, .51f, .44f, .35f, .26f, .19f, .12f, .055f, 0 };
            float[] light = { .44f, .73f, .99f, 1.0f, .92f, .80f, .66f, .77f, .95f, 1.0f, .85f, .61f, .44f, .28f };
            Vector2[] previous = outer;
            for (int band = 1; band < offsets.Length; band++)
            {
                float extent = offsets[band];
                var next = Contour(Mathf.Lerp(.018f, .43f, extent), Mathf.Lerp(.024f, .52f, extent),
                    Mathf.Lerp(.014f, .27f, extent), Mathf.Lerp(.019f, .35f, extent), 0);
                for (int i = 0; i < ContourPoints; i++)
                {
                    int j = (i + 1) % ContourPoints;
                    Vector2 tangent = previous[j] - previous[i];
                    bool horizontal = Mathf.Abs(tangent.x) > Mathf.Abs(tangent.y);
                    float sideLight = horizontal ? (tangent.x > 0 ? .92f : .85f) : (tangent.y > 0 ? 1f : .82f);
                    Color c0 = WoodLight(light[band - 1] * sideLight), c1 = WoodLight(light[band] * sideLight);
                    AddQuad(vh, previous[i], previous[j], next[j], next[i],
                        WoodUV(previous[i], horizontal, 1 - offsets[band - 1]), WoodUV(previous[j], horizontal, 1 - offsets[band - 1]),
                        WoodUV(next[j], horizontal, 1 - extent), WoodUV(next[i], horizontal, 1 - extent), c0, c0, c1, c1);
                }
                previous = next;
            }
            FillContour(vh, Contour(.018f, .024f, .014f, .019f, 0), C("#12331B"));
        }
        static Color WoodLight(float light) => new Color(light, light, light, 1);
        static Vector2 WoodUV(Vector2 p, bool horizontal, float across)
        {
            float along = horizontal ? (p.x - 6) / 980f : (p.y - 65) / 610f;
            return TabletopSurfaceAtlas.WoodUV(Mathf.Clamp01(along), across);
        }
        Vector2[] Contour(float ex, float ey, float rx, float ry, float yOffset)
        {
            var points = new Vector2[ContourPoints];
            for (int corner = 0; corner < 4; corner++)
            {
                float col = corner == 0 || corner == 3 ? -ex + rx : 8 + ex - rx;
                float row = corner < 2 ? -ey + ry : 8 + ey - ry;
                float from = corner == 0 ? Mathf.PI : corner == 1 ? Mathf.PI * 1.5f : corner == 2 ? 0 : Mathf.PI * .5f;
                for (int i = 0; i <= CornerSegments; i++)
                {
                    float angle = from + i * Mathf.PI * .5f / CornerSegments;
                    points[corner * (CornerSegments + 1) + i] = P(row + Mathf.Sin(angle) * ry, col + Mathf.Cos(angle) * rx) + Vector2.up * yOffset;
                }
            }
            return points;
        }
        static void FillContour(VertexHelper vh, Vector2[] points, Color color)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in points) center += point;
            center /= points.Length;
            int first = vh.currentVertCount;
            vh.AddVert(center, color, TabletopSurfaceAtlas.SolidUV);
            for (int i = 0; i < points.Length; i++) vh.AddVert(points[i], color, TabletopSurfaceAtlas.SolidUV);
            for (int i = 0; i < points.Length; i++) vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % points.Length);
        }
        void DrawCloth(VertexHelper vh)
        {
            // Shared, continuous cloth UVs preserve the weave across all sixty-four squares.
            for (int row = 0; row < 16; row++)
                for (int col = 0; col < 16; col++)
                {
                    float r0 = row * .5f, c0 = col * .5f, r1 = r0 + .5f, c1 = c0 + .5f;
                    AddQuad(vh, P(r0, c0), P(r0, c1), P(r1, c1), P(r1, c0),
                        TabletopSurfaceAtlas.FeltUV(c0 / 8, 1 - r0 / 8), TabletopSurfaceAtlas.FeltUV(c1 / 8, 1 - r0 / 8),
                        TabletopSurfaceAtlas.FeltUV(c1 / 8, 1 - r1 / 8), TabletopSurfaceAtlas.FeltUV(c0 / 8, 1 - r1 / 8),
                        ClothLight(r0, c0), ClothLight(r0, c1), ClothLight(r1, c1), ClothLight(r1, c0));
                }
            // Printed dark lines have a soft darker inset, rather than a bright checkerboard effect.
            Color grid = C("#123E23");
            for (int i = 0; i <= 8; i++)
            {
                float width = i == 0 || i == 8 ? 3.3f : 2.25f;
                Line(vh, P(i, 0), P(i, 8), width, grid, true);
                Line(vh, P(0, i), P(8, i), width, grid, true);
            }
            for (int i = 0; i < 6; i++)
            {
                float distance = i * .035f;
                float a = .11f * (1 - i / 6f);
                Quad(vh, P(distance, 0), P(distance, 8), P(distance + .035f, 8), P(distance + .035f, 0), new Color(.02f, .06f, .025f, a));
                Quad(vh, P(0, distance), P(8, distance), P(8, distance + .035f), P(0, distance + .035f), new Color(.02f, .06f, .025f, a));
            }
            for (int r = 2; r <= 6; r += 4)
                for (int col = 2; col <= 6; col += 4) Ellipse(vh, P(r, col), 3.4f, 2.7f, grid);
        }
        static Color ClothLight(float row, float col)
        {
            float edge = Mathf.Min(Mathf.Min(row, 8 - row), Mathf.Min(col, 8 - col));
            float shade = Mathf.Lerp(.79f, 1, Mathf.Clamp01(edge * 3));
            return new Color(shade, shade, shade, 1);
        }
        void DrawSelection(VertexHelper vh, int row, int col)
        {
            Vector2[] points = { P(row + .06f, col + .06f), P(row + .06f, col + .94f), P(row + .94f, col + .94f), P(row + .94f, col + .06f) };
            Color cyan = C("#4AD5F4");
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = points[i], prev = points[(i + 3) % 4], next = points[(i + 1) % 4];
                Line(vh, p + Vector2.down * 1.5f, Vector2.Lerp(p, prev, .23f) + Vector2.down * 1.5f, 4.1f, new Color(0, .18f, .20f, .25f));
                Line(vh, p + Vector2.down * 1.5f, Vector2.Lerp(p, next, .23f) + Vector2.down * 1.5f, 4.1f, new Color(0, .18f, .20f, .25f));
                Line(vh, p, Vector2.Lerp(p, prev, .23f), 3.6f, cyan);
                Line(vh, p, Vector2.Lerp(p, next, .23f), 3.6f, cyan);
            }
        }
        void DrawPiece(VertexHelper vh, Vector2 p, int row, int player, float alpha, float sy)
        {
            float rx = 33.5f + row * .72f, ry = (27.0f + row * .40f) * sy;
            for (int i = 3; i >= 1; i--)
                Ellipse(vh, p + new Vector2(2.2f, -4.0f), rx + i * 1.1f, Mathf.Max(3, ry + i * .7f), new Color(.025f, .07f, .035f, .065f * alpha), i == 3);
            Color side = player == 1 ? C("#101D18") : C("#919C89"); side.a = alpha;
            Ellipse(vh, p + Vector2.down * .9f, rx, Mathf.Max(2.6f, ry), side);
            Color bevel = player == 1 ? C("#405047") : C("#E0E6D5"); bevel.a = alpha;
            Ellipse(vh, p + Vector2.up * 2.9f, rx, Mathf.Max(2.2f, ry), bevel, true);
            TexturedDisc(vh, p + Vector2.up * 3.25f, rx - 1.1f, Mathf.Max(1.7f, ry - 1.0f), player == 1, alpha);
            Color highlight = player == 1 ? new Color(.71f, .78f, .70f, .10f * alpha) : new Color(1, 1, .94f, .16f * alpha);
            Arc(vh, p + Vector2.up * 3, rx - 1.7f, Mathf.Max(1.5f, ry - 1.3f), .48f, 2.75f, .65f, highlight);
        }
        static void TexturedDisc(VertexHelper vh, Vector2 p, float rx, float ry, bool black, float alpha)
        {
            const int segments = 56;
            int first = vh.currentVertCount;
            Color color = new Color(1, 1, 1, alpha);
            vh.AddVert(p, color, TabletopSurfaceAtlas.DiscUV(.5f, .5f, black));
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments, x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                vh.AddVert(p + new Vector2(x * rx, y * ry), color, TabletopSurfaceAtlas.DiscUV(x * .5f + .5f, y * .5f + .5f, black));
                if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
            }
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            Vector2 uv = TabletopSurfaceAtlas.SolidUV;
            AddQuad(vh, a, b, c, d, uv, uv, uv, uv, color, color, color, color);
        }
        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Color ca, Color cb, Color cc, Color cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, ua); vh.AddVert(b, cb, ub); vh.AddVert(c, cc, uc); vh.AddVert(d, cd, ud);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color, bool smooth = false)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(vh, a + n, b + n, b - n, a - n, color);
            if (!smooth) return;
            Vector2 fringe = n.normalized * .7f, uv = TabletopSurfaceAtlas.SolidUV;
            Color transparent = new Color(color.r, color.g, color.b, 0);
            AddQuad(vh, a + n + fringe, b + n + fringe, b + n, a + n,
                uv, uv, uv, uv, transparent, transparent, color, color);
            AddQuad(vh, a - n, b - n, b - n - fringe, a - n - fringe,
                uv, uv, uv, uv, color, color, transparent, transparent);
        }
        static void Ellipse(VertexHelper vh, Vector2 p, float rx, float ry, Color color, bool smooth = false)
        {
            const int segments = 48;
            int first = vh.currentVertCount;
            Vector2 uv = TabletopSurfaceAtlas.SolidUV;
            vh.AddVert(p, color, uv);
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2 / segments;
                vh.AddVert(p + new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry), color, uv);
                if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
            }
            if (!smooth) return;
            int edge = vh.currentVertCount;
            Color transparent = new Color(color.r, color.g, color.b, 0);
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2 / segments, x = Mathf.Cos(t), y = Mathf.Sin(t);
                vh.AddVert(p + new Vector2(x * rx, y * ry), color, uv);
                vh.AddVert(p + new Vector2(x * (rx + .7f), y * (ry + .7f)), transparent, uv);
                if (i == 0) continue;
                int k = edge + i * 2;
                vh.AddTriangle(k - 2, k - 1, k + 1);
                vh.AddTriangle(k - 2, k + 1, k);
            }
        }
        static void Arc(VertexHelper vh, Vector2 p, float rx, float ry, float from, float to, float width, Color color)
        {
            const int segments = 32;
            Vector2 prev = p + new Vector2(Mathf.Cos(from) * rx, Mathf.Sin(from) * ry);
            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments);
                Vector2 q = p + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Line(vh, prev, q, width, color); prev = q;
            }
        }
        public bool CellAt(Vector2 local, out int row, out int col)
        {
            float t = (tl.y - local.y) / (tl.y - bl.y); row = Mathf.FloorToInt(t * 8);
            float left = Mathf.LerpUnclamped(tl.x, bl.x, t), right = Mathf.LerpUnclamped(tr.x, br.x, t);
            col = Mathf.FloorToInt((local.x - left) / (right - left) * 8);
            return row >= 0 && row < 8 && col >= 0 && col < 8;
        }
        bool Pointer(PointerEventData e, out int row, out int col)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out Vector2 p);
            p += rectTransform.pivot * rectTransform.rect.size;
            return CellAt(p, out row, out col);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { Owner.ShowPause(); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (Pointer(e, out int r, out int c)) Owner.PlayCell(r, c);
        }
        public void OnPointerMove(PointerEventData e) { if (Pointer(e, out int r, out int c)) { FocusRow = r; FocusCol = c; } else { FocusRow = -1; FocusCol = -1; } SetVerticesDirty(); }
        public void OnPointerExit(PointerEventData e) { FocusRow = -1; FocusCol = -1; SetVerticesDirty(); }
    }
}
