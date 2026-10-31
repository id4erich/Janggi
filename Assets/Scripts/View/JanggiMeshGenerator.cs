using UnityEngine;

namespace Janggi
{
    public static class JanggiMeshGenerator
    {
        public static Mesh CreateOctagonalPieceMesh(float radius, float height)
        {
            Mesh mesh = new Mesh();
            mesh.name = "OctagonalJanggiPiece";

            int sides = 8;
            Vector3[] vertices = new Vector3[sides * 4];
            Vector2[] uvs = new Vector2[sides * 4];

            int[] topTriangles = new int[(sides - 2) * 3];
            int[] sideBottomTriangles = new int[(sides - 2) * 3 + sides * 6];

            float angleOffset = Mathf.PI / 8f; // 정8각형 배치

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides + angleOffset;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Top (0..7)
                vertices[i] = new Vector3(cos * radius, height * 0.5f, sin * radius);
                uvs[i] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

                // Bottom (8..15)
                vertices[i + sides] = new Vector3(cos * radius, -height * 0.5f, sin * radius);
                uvs[i + sides] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

                // Side Top (16..23)
                vertices[i + sides * 2] = new Vector3(cos * radius, height * 0.5f, sin * radius);
                uvs[i + sides * 2] = new Vector2((float)i / sides, 1f);

                // Side Bottom (24..31)
                vertices[i + sides * 3] = new Vector3(cos * radius, -height * 0.5f, sin * radius);
                uvs[i + sides * 3] = new Vector2((float)i / sides, 0f);
            }

            // Top Face Submesh
            int topIdx = 0;
            for (int i = 1; i < sides - 1; i++)
            {
                topTriangles[topIdx++] = 0;
                topTriangles[topIdx++] = i + 1;
                topTriangles[topIdx++] = i;
            }

            // Side & Bottom Submesh
            int sideIdx = 0;
            for (int i = 1; i < sides - 1; i++)
            {
                sideBottomTriangles[sideIdx++] = sides;
                sideBottomTriangles[sideIdx++] = sides + i;
                sideBottomTriangles[sideIdx++] = sides + i + 1;
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int topCurr = sides * 2 + i;
                int topNext = sides * 2 + next;
                int botCurr = sides * 3 + i;
                int botNext = sides * 3 + next;

                sideBottomTriangles[sideIdx++] = topCurr;
                sideBottomTriangles[sideIdx++] = topNext;
                sideBottomTriangles[sideIdx++] = botCurr;

                sideBottomTriangles[sideIdx++] = topNext;
                sideBottomTriangles[sideIdx++] = botNext;
                sideBottomTriangles[sideIdx++] = botCurr;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sideBottomTriangles, 0); // Submesh 0: 옆면 & 밑면
            mesh.SetTriangles(topTriangles, 1);         // Submesh 1: 윗면 표면

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static Texture2D CreateJanggiPieceTexture(string hanjaText, Color textCol, Color bgCol, Color borderColor)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[size * size];

            // 8각 영역 마스크 및 테두리 칠하기
            float center = size * 0.5f;
            float maxR = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (dist > maxR - 8f)
                    {
                        pixels[y * size + x] = borderColor;
                    }
                    else
                    {
                        pixels[y * size + x] = bgCol;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
