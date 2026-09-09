using System.Text.Json;

namespace KFramework.Content.Pipeline;

/// <summary>
/// 把矢量描述（.sprite.json）离线光栅化成位图。
/// 目的：让美术资源以纯文本方式入库，任何人都能用编辑器改颜色/形状，
/// 打包时再烘焙成像素，不需要往仓库里塞二进制图片。
/// </summary>
public static class ShapeImporter
{
    /// <summary>支持的类型：rect / circle / ellipse / tri / poly / line。</summary>
    public static Bitmap Import(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        JsonElement root = document.RootElement;
        int width = root.TryGetProperty("width", out JsonElement w) ? w.GetInt32() : 32;
        int height = root.TryGetProperty("height", out JsonElement h) ? h.GetInt32() : 32;

        var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height));

        if (!root.TryGetProperty("shapes", out JsonElement shapes) || shapes.ValueKind != JsonValueKind.Array)
            return bitmap;

        foreach (JsonElement shape in shapes.EnumerateArray())
        {
            string type = shape.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";
            Rgba color = Rgba.Parse(shape.TryGetProperty("color", out JsonElement c) ? c.GetString() : null);

            switch (type)
            {
                case "rect":
                    bitmap.FillRect(ReadInt(shape, "x"), ReadInt(shape, "y"),
                                    ReadInt(shape, "w"), ReadInt(shape, "h"), color);
                    break;

                case "circle":
                    bitmap.FillCircle(ReadFloat(shape, "cx"), ReadFloat(shape, "cy"),
                                      ReadFloat(shape, "r"), color);
                    break;

                case "ellipse":
                    bitmap.FillEllipse(ReadFloat(shape, "cx"), ReadFloat(shape, "cy"),
                                       ReadFloat(shape, "rx"), ReadFloat(shape, "ry"), color);
                    break;

                case "tri":
                    bitmap.FillTriangle(
                        new Vec2f(ReadFloat(shape, "x1"), ReadFloat(shape, "y1")),
                        new Vec2f(ReadFloat(shape, "x2"), ReadFloat(shape, "y2")),
                        new Vec2f(ReadFloat(shape, "x3"), ReadFloat(shape, "y3")), color);
                    break;

                case "poly":
                {
                    if (!shape.TryGetProperty("points", out JsonElement points) ||
                        points.ValueKind != JsonValueKind.Array) break;

                    var list = new List<Vec2f>();
                    float[] buffer = new float[2];
                    int cursor = 0;
                    foreach (JsonElement value in points.EnumerateArray())
                    {
                        buffer[cursor++] = value.GetSingle();
                        if (cursor == 2)
                        {
                            list.Add(new Vec2f(buffer[0], buffer[1]));
                            cursor = 0;
                        }
                    }
                    if (list.Count >= 3) bitmap.FillPolygon(list.ToArray(), color);
                    break;
                }

                case "line":
                    bitmap.DrawLine(
                        new Vec2f(ReadFloat(shape, "x1"), ReadFloat(shape, "y1")),
                        new Vec2f(ReadFloat(shape, "x2"), ReadFloat(shape, "y2")),
                        ReadFloat(shape, "width", 1f), color);
                    break;
            }
        }

        return bitmap;
    }

    private static int ReadInt(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) ? value.GetInt32() : 0;

    private static float ReadFloat(JsonElement element, string name, float fallback = 0f)
        => element.TryGetProperty(name, out JsonElement value) ? value.GetSingle() : fallback;
}
