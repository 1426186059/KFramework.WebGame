using KFramework.MonoGame;

namespace MirGame.Tests;

/// <summary>
/// 测试页共用的程序化纹理与配色工具：不依赖任何图片资源与打包流程，
/// 谁需要「几千个精灵」的画面就直接用它。
/// </summary>
public static class TestSpriteTexture
{
    /// <summary>
    /// 程序化生成一张带光照 + 高光的球（白色，绘制时用 Color 着色），RGBA8。
    /// </summary>
    /// <param name="device">用于创建纹理的设备。</param>
    /// <param name="size">边长（像素）。</param>
    public static Texture2D MakeBall(GraphicsDevice device, int size)
    {
        byte[] pixels = new byte[size * size * 4];

        // 光方向（左上前）与 Blinn 半程向量
        var light = Vector3.Normalize(new Vector3(-0.45f, -0.55f, 0.70f));
        var half = Vector3.Normalize(light + new Vector3(0f, 0f, 1f));

        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - radius) / radius;
                float ny = (y + 0.5f - radius) / radius;
                float dist = MathF.Sqrt(nx * nx + ny * ny);

                // 边缘一个像素的抗锯齿
                float alpha = Math.Clamp((1f - dist) * size * 0.5f, 0f, 1f);

                float nz = MathF.Sqrt(MathF.Max(0f, 1f - MathF.Min(dist, 1f) * MathF.Min(dist, 1f)));
                var normal = Vector3.Normalize(new Vector3(nx, ny, nz));

                float diffuse = MathF.Max(0f, Vector3.Dot(normal, light));
                float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, half)), 28f);
                float v = 0.28f + 0.68f * diffuse + 0.85f * specular;

                int offset = (y * size + x) * 4;
                byte c = (byte)Math.Clamp(v * 255f, 0f, 255f);
                pixels[offset + 0] = c;
                pixels[offset + 1] = c;
                pixels[offset + 2] = c;
                pixels[offset + 3] = (byte)(alpha * 255f);
            }
        }

        return device.CreateTexture(size, size, pixels, SurfaceFormat.Color);
    }

    /// <summary>HSV → Color（h: 0..360，s / v: 0..1）。</summary>
    public static Color Hsv(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        float m = v - c;
        float r = 0f, g = 0f, b = 0f;

        if (h < 60f) { r = c; g = x; }
        else if (h < 120f) { r = x; g = c; }
        else if (h < 180f) { g = c; b = x; }
        else if (h < 240f) { g = x; b = c; }
        else if (h < 300f) { r = x; b = c; }
        else { r = c; b = x; }

        return new Color((int)((r + m) * 255f), (int)((g + m) * 255f), (int)((b + m) * 255f));
    }
}
