using System.Collections.Generic;
using UnityEngine;

namespace Janggi
{
    public static class JanggiMeshGenerator
    {
        // 윗면 테두리를 비스듬히 깎은(모따기) 8각 장기말.
        // Submesh 0: 모따기·옆면·밑면(나뭇결), Submesh 1: 윗면(글자 텍스처가 윗면 8각에 꽉 차게 UV 배치)
        public static Mesh CreateOctagonalPieceMesh(float radius, float height)
        {
            const int sides = 8;
            float chamfer = Mathf.Min(height * 0.3f, radius * 0.12f);
            float topY = height * 0.5f, chamferY = topY - chamfer, bottomY = -height * 0.5f;
            float innerR = radius - chamfer;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var body = new List<int>();

            Vector3 Ring(float r, float y, int i)
            {
                float a = i * Mathf.PI * 2f / sides + Mathf.PI / 8f; // 정8각형 배치
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }

            // 윗면: 중심 + 8각 (면마다 정점을 따로 두어 각진 면 법선을 유지)
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, topY, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                Vector3 v = Ring(innerR, topY, i);
                vertices.Add(v); uvs.Add(new Vector2(0.5f + v.x / innerR * 0.5f, 0.5f + v.z / innerR * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                top.Add(topCenter); top.Add(topCenter + 1 + (i + 1) % sides); top.Add(topCenter + 1 + i);
            }

            // 모따기 띠와 옆면: 위쪽 링 → 아래쪽 링 사각형 8개
            void Band(float rUpper, float yUpper, float rLower, float yLower, float vUpper, float vLower)
            {
                for (int i = 0; i < sides; i++)
                {
                    int b = vertices.Count;
                    vertices.Add(Ring(rUpper, yUpper, i)); uvs.Add(new Vector2((float)i / sides, vUpper));
                    vertices.Add(Ring(rUpper, yUpper, i + 1)); uvs.Add(new Vector2((float)(i + 1) / sides, vUpper));
                    vertices.Add(Ring(rLower, yLower, i)); uvs.Add(new Vector2((float)i / sides, vLower));
                    vertices.Add(Ring(rLower, yLower, i + 1)); uvs.Add(new Vector2((float)(i + 1) / sides, vLower));
                    body.Add(b); body.Add(b + 1); body.Add(b + 2);
                    body.Add(b + 1); body.Add(b + 3); body.Add(b + 2);
                }
            }
            Band(innerR, topY, radius, chamferY, 1f, 0.9f);
            Band(radius, chamferY, radius, bottomY, 0.9f, 0f);

            // 밑면
            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                Vector3 v = Ring(radius, bottomY, i);
                vertices.Add(v); uvs.Add(new Vector2(0.5f + v.x / radius * 0.5f, 0.5f + v.z / radius * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                body.Add(bottomCenter); body.Add(bottomCenter + 1 + i); body.Add(bottomCenter + 1 + (i + 1) % sides);
            }

            Mesh mesh = new Mesh { name = "OctagonalJanggiPiece" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(top, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // 윗면 음각 노멀맵용
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
