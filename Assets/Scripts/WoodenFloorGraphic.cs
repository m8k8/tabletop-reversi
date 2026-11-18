using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TabletopReversi
{
    /// <summary>Original light ash floor, shared as one texture behind the playing table.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WoodenFloorGraphic : MaskableGraphic, IPointerClickHandler
    {
        public ReversiGame Owner;
        const int TextureSize = 1024;
        const float Tau = Mathf.PI * 2f;
        static Texture2D floorTexture;
        static int activeInstances;
        bool registered;

        public override Texture mainTexture
        {
            get
            {
                if (floorTexture == null) floorTexture = CreateFloorTexture();
                return floorTexture;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!registered) { registered = true; activeInstances++; }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!registered) return;
            registered = false;
            activeInstances = Mathf.Max(0, activeInstances - 1);
            if (activeInstances != 0 || floorTexture == null) return;
            Texture2D textureToRelease = floorTexture;
            floorTexture = null;
            if (Application.isPlaying) Destroy(textureToRelease);
            else DestroyImmediate(textureToRelease);
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
            float dx = (x - .46f) * .82f, dy = (y - .54f) * .83f;
            float vignette = Mathf.Clamp01((dx * dx + dy * dy) * .47f);
            float light = Mathf.Clamp01(.995f + (y - x) * .035f - vignette);
            Color tint = new Color(color.r * light, color.g * light, color.b * light, color.a);
            // The reference canvas is 16:10. Wider windows reveal more grain instead of stretching it.
            float u = .5f + (x - .5f) * aspect / 1.6f;
            vh.AddVert(new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height), tint, new Vector2(u, y));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && Owner != null)
                Owner.ShowPause();
        }

        static Texture2D CreateFloorTexture()
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                float v = y / (float)(TextureSize - 1);
                float plankPosition = v * 8f;
                int plank = Mathf.Min(7, Mathf.FloorToInt(plankPosition));
                float across = Mathf.Clamp01(plankPosition - plank);
                float seed = Hash(plank + 137, 719) * Tau;
                float plankTone = (Hash(plank + 347, 233) - .5f) * .045f;
                float seamDistance = Mathf.Min(across, 1 - across);
                float seam = 1f - Mathf.Clamp01(seamDistance / .010f);
                float lip = Mathf.Exp(-Mathf.Abs(across - .019f) * 130f) * .022f;
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)(TextureSize - 1);
                    // Long gently wandering growth rings, all flowing horizontally across a plank.
                    float flow = across + Mathf.Sin(u * 7.3f + seed) * .028f
                        + Mathf.Sin(u * 18.7f + seed * 1.9f) * .006f;
                    float broad = Mathf.Sin(flow * Tau * 7.2f + seed + u * .72f);
                    float fine = Mathf.Sin(flow * Tau * 28.7f + Mathf.Sin(u * 12f + seed) * .35f);
                    float pores = Mathf.Pow(.5f + .5f * Mathf.Sin(flow * Tau * 51.3f + seed), 8f);
                    float soft = (Noise(u * 7f + plank * 13f, across * 28f) - .5f) * .025f;
                    float fibers = (Hash(x + 37, y + 911) - .5f) * .012f;
                    float tone = 1f + plankTone + broad * .036f + fine * .020f - pores * .025f
                        + soft + fibers - seam * .082f + lip;
                    float warm = Mathf.Max(0, broad) * 2.0f;
                    pixels[y * TextureSize + x] = new Color32(
                        Byte((216 + warm) * tone), Byte((179 + warm * .5f) * tone), Byte(132 * tone), 255);
                }
            }
            var result = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, false)
            {
                name = "Original warm ash floor",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Trilinear,
                wrapModeU = TextureWrapMode.Mirror,
                wrapModeV = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            result.SetPixels32(pixels);
            result.Apply(true, true);
            return result;
        }

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = x - ix, ty = y - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), tx),
                Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), tx), ty);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)x * 374761393u + (uint)y * 668265263u + 2246822519u;
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (n & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
