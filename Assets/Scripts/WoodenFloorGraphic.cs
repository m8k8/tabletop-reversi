using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TabletopReversi
{
    /// <summary>Full-size satin oak tabletop, with natural grain shared by matches and films.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WoodenFloorGraphic : MaskableGraphic, IPointerClickHandler
    {
        public ReversiGame Owner;
        static Texture2D tabletopTexture;

        public override Texture mainTexture
        {
            get
            {
                if (tabletopTexture == null)
                    tabletopTexture = Resources.Load<Texture2D>("Materials/OakTabletop");
                return tabletopTexture != null ? tabletopTexture : Texture2D.whiteTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            // A coarse grid carries broad lighting only. Every wood fiber is in the texture.
            const int columns = 6, rows = 4;
            float aspect = rect.width / rect.height;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    float x0 = col / (float)columns, x1 = (col + 1) / (float)columns;
                    float y0 = row / (float)rows, y1 = (row + 1) / (float)rows;
                    int first = vh.currentVertCount;
                    AddVertex(vh, rect, x0, y0, aspect);
                    AddVertex(vh, rect, x0, y1, aspect);
                    AddVertex(vh, rect, x1, y1, aspect);
                    AddVertex(vh, rect, x1, y0, aspect);
                    vh.AddTriangle(first, first + 1, first + 2);
                    vh.AddTriangle(first, first + 2, first + 3);
                }
        }

        void AddVertex(VertexHelper vh, Rect rect, float x, float y, float aspect)
        {
            float dx = x - .46f, dy = y - .54f;
            float vignette = Mathf.Clamp01((dx * dx + dy * dy) * .18f);
            float light = Mathf.Clamp01(1f + (y - x) * .018f - vignette);
            Color tint = new Color(color.r * light, color.g * light, color.b * light, color.a);
            // Cover the canvas without stretching the grain or exposing a repeated texture edge.
            float u = .5f + (x - .5f) * aspect / 1.6f;
            float v = y;
            if (aspect > 1.6f)
            {
                u = x;
                v = .5f + (y - .5f) * 1.6f / aspect;
            }
            vh.AddVert(new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height), tint, new Vector2(u, v));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && Owner != null)
                Owner.ShowPause();
        }

    }
}
