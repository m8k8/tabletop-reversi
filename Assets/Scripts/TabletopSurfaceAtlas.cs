using UnityEngine;

namespace TabletopReversi
{
    /// <summary>
    /// Original, deterministic surface textures for the tabletop mesh. UVs use
    /// u=0 at the left and v=0 at the bottom, matching Unity texture coordinates.
    /// </summary>
    public static class TabletopSurfaceAtlas
    {
        const int Size = 1024;
        const int Padding = 8;
        const float Tau = Mathf.PI * 2f;

        // Each rectangle describes the usable interior, excluding its gutter.
        static readonly RectInt Felt = new RectInt(8, 520, 496, 496);
        static readonly RectInt Wood = new RectInt(520, 520, 496, 496);
        static readonly RectInt BlackDisc = new RectInt(8, 8, 240, 240);
        static readonly RectInt IvoryDisc = new RectInt(264, 8, 240, 240);
        static Texture2D texture;

        public static Texture2D Texture
        {
            get
            {
                if (texture == null) texture = CreateTexture();
                return texture;
            }
        }

        // A large unused white area keeps plain geometry white at every useful mip.
        public static Vector2 SolidUV => new Vector2(896.5f / Size, 256.5f / Size);
        public static Vector2 FeltUV(float u, float v) => Map(Felt, u, v);
        public static Vector2 WoodUV(float u, float v) => Map(Wood, u, v);
        public static Vector2 DiscUV(float u, float v, bool black) => Map(black ? BlackDisc : IvoryDisc, u, v);

        static Vector2 Map(RectInt region, float u, float v)
        {
            // Sample texel centers, never the boundary shared with another region.
            return new Vector2(
                (region.x + .5f + Mathf.Clamp01(u) * (region.width - 1)) / Size,
                (region.y + .5f + Mathf.Clamp01(v) * (region.height - 1)) / Size);
        }

        static Texture2D CreateTexture()
        {
            var pixels = new Color32[Size * Size];
            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;

            Fill(pixels, Felt, 0);
            Fill(pixels, Wood, 1);
            Fill(pixels, BlackDisc, 2);
            Fill(pixels, IvoryDisc, 3);

            var result = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "Tabletop original felt, walnut and matte plastic",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            result.SetPixels32(pixels);
            result.Apply(true, true);
            return result;
        }

        static void Fill(Color32[] pixels, RectInt region, int surface)
        {
            for (int y = -Padding; y < region.height + Padding; y++)
            {
                int sampleY = Mathf.Clamp(y, 0, region.height - 1);
                float v = sampleY / (float)(region.height - 1);
                int row = (region.y + y) * Size;
                for (int x = -Padding; x < region.width + Padding; x++)
                {
                    int sampleX = Mathf.Clamp(x, 0, region.width - 1);
                    float u = sampleX / (float)(region.width - 1);
                    Color32 color;
                    if (surface == 0) color = FeltPixel(u, v, sampleX, sampleY);
                    else if (surface == 1) color = WoodPixel(u, v, sampleX, sampleY);
                    else color = DiscPixel(u, v, sampleX, sampleY, surface == 2);
                    pixels[row + region.x + x] = color;
                }
            }
        }

        static Color32 FeltPixel(float u, float v, int x, int y)
        {
            // Narrow, slanted fabric ribs. Mipmaps soften them as the board shrinks.
            float warp = Mathf.Sin((u + v * .34f) * Tau * 130f);
            float weft = Mathf.Sin((v - u * .21f) * Tau * 148f);
            float rib = Mathf.Pow(.5f + .5f * warp, 5f);
            float crossing = Mathf.Pow(.5f + .5f * weft, 4f);
            float fiber = (Hash(x, y) - .5f) * .014f;
            float broad = (Noise(u * 4.5f, v * 4.5f) - .5f) * .025f;
            float light = 1f + (rib - .25f) * .043f + (crossing - .27f) * .023f
                + fiber + broad + (v - u) * .018f;
            return Pixel(11f, 139f, 56f, light);
        }

        static Color32 WoodPixel(float u, float v, int x, int y)
        {
            // The grain follows U along each rail; V moves across growth rings.
            float flow = v + Mathf.Sin(u * 6.3f + v * 8f) * .008f
                + Mathf.Sin(u * 15f + v * 23f) * .0025f;
            float broad = Mathf.Sin(flow * Tau * 10.3f + Mathf.Sin(u * 3f) * .3f);
            float fine = Mathf.Sin(flow * Tau * 46f + Mathf.Sin(u * 8f) * .65f);
            float pores = Mathf.Pow(.5f + .5f * Mathf.Sin(flow * Tau * 93f
                + Mathf.Sin(u * 13f + flow * 11f)), 10f);
            float softNoise = (Noise(u * 11f, v * 75f) - .5f) * .07f;
            float fineNoise = (Hash(x + 719, y + 193) - .5f) * .021f;
            float light = 1f + broad * .105f + fine * .052f - pores * .145f
                + softNoise + fineNoise;
            float warmth = Mathf.Max(0, broad) * 2.3f;
            return Pixel(78f + warmth, 48f + warmth * .38f, 40f, light);
        }

        static Color32 DiscPixel(float u, float v, int x, int y, bool black)
        {
            // A small planar light falloff reads as matte plastic, not a domed gem.
            float micro = (Hash(x + 311, y + 991) - .5f) * .010f;
            float molding = (Noise(u * 7f, v * 7f) - .5f) * .008f;
            float light = 1f + (v - u) * .027f + micro + molding;
            return black ? Pixel(32f, 43f, 40f, light) : Pixel(206f, 215f, 198f, light);
        }

        static Color32 Pixel(float r, float g, float b, float light)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * light), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * light), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * light), 0, 255), 255);
        }

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
