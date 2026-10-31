using UnityEngine;

namespace Janggi
{
    public static class BoardTextureGenerator
    {
        public static Texture2D CreateJanggiBoardTexture()
        {
            int width = 1024;
            int height = 1024;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // 1. 따뜻한 목재 바탕색 (유저 첨부 이미지의 나무 느낌)
            Color woodBg = new Color(0.92f, 0.82f, 0.70f);
            Color woodPattern = new Color(0.88f, 0.77f, 0.65f);
            Color lineCol = new Color(0.15f, 0.10f, 0.05f); // 짙은 먹선

            Color[] pixels = new Color[width * height];

            // 목결(Wood grain) 효과 생성
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float grain = Mathf.Sin(y * 0.05f + Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 10f) * 0.5f + 0.5f;
                    pixels[y * width + x] = Color.Lerp(woodBg, woodPattern, grain * 0.3f);
                }
            }

            tex.SetPixels(pixels);

            // 2. 8x9 격자선 및 궁성 대각선 그리기 (9개 세로선, 10개 가로선)
            float marginX = 80f;
            float marginY = 80f;
            float stepX = (width - marginX * 2f) / 8f;   // 8칸 (9개 선)
            float stepY = (height - marginY * 2f) / 9f;  // 9칸 (10개 선)

            // 가로선 (10개)
            for (int i = 0; i < 10; i++)
            {
                float y = marginY + i * stepY;
                DrawLine(tex, new Vector2(marginX, y), new Vector2(width - marginX, y), lineCol, 3);
            }

            // 세로선 (9개)
            for (int i = 0; i < 9; i++)
            {
                float x = marginX + i * stepX;
                DrawLine(tex, new Vector2(x, marginY), new Vector2(x, height - marginY), lineCol, 3);
            }

            // 초 궁성 대각선 (X자)
            Vector2 p1 = new Vector2(marginX + 3 * stepX, marginY + 0 * stepY);
            Vector2 p2 = new Vector2(marginX + 5 * stepX, marginY + 2 * stepY);
            Vector2 p3 = new Vector2(marginX + 5 * stepX, marginY + 0 * stepY);
            Vector2 p4 = new Vector2(marginX + 3 * stepX, marginY + 2 * stepY);
            DrawLine(tex, p1, p2, lineCol, 3);
            DrawLine(tex, p3, p4, lineCol, 3);

            // 한 궁성 대각선 (X자)
            Vector2 h1 = new Vector2(marginX + 3 * stepX, marginY + 7 * stepY);
            Vector2 h2 = new Vector2(marginX + 5 * stepX, marginY + 9 * stepY);
            Vector2 h3 = new Vector2(marginX + 5 * stepX, marginY + 7 * stepY);
            Vector2 h4 = new Vector2(marginX + 3 * stepX, marginY + 9 * stepY);
            DrawLine(tex, h1, h2, lineCol, 3);
            DrawLine(tex, h3, h4, lineCol, 3);

            // 외각선 테두리
            DrawLine(tex, new Vector2(marginX - 15, marginY - 15), new Vector2(width - marginX + 15, marginY - 15), lineCol, 4);
            DrawLine(tex, new Vector2(marginX - 15, height - marginY + 15), new Vector2(width - marginX + 15, height - marginY + 15), lineCol, 4);
            DrawLine(tex, new Vector2(marginX - 15, marginY - 15), new Vector2(marginX - 15, height - marginY + 15), lineCol, 4);
            DrawLine(tex, new Vector2(width - marginX + 15, marginY - 15), new Vector2(width - marginX + 15, height - marginY + 15), lineCol, 4);

            tex.Apply();
            return tex;
        }

        private static void DrawLine(Texture2D tex, Vector2 p1, Vector2 p2, Color col, int thickness)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(p1, p2));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 p = Vector2.Lerp(p1, p2, t);

                for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
                {
                    for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
                    {
                        int px = Mathf.Clamp((int)p.x + dx, 0, tex.width - 1);
                        int py = Mathf.Clamp((int)p.y + dy, 0, tex.height - 1);
                        tex.SetPixel(px, py, col);
                    }
                }
            }
        }
    }
}
