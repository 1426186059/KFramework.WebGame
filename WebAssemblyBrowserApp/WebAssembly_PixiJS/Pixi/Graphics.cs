namespace PixiJS;

/// <summary>
/// 对应 PixiJS Graphics：矢量绘图对象。
/// 默认即时提交；大量绘制可进入批模式（BeginBatch/EndBatch），
/// 命令在 C# 侧累积、EndBatch 时一次 [JSImport] 提交（跨边界 O(1)）。
/// </summary>
public sealed class Graphics : PixiObject
{
    private const int Stride = 11;          // 与 pixi-api.js 的 BATCH_STRIDE 一致
    private const int OpRect = 0, OpRound = 1, OpCircle = 2, OpClear = 3,
                      OpMoveTo = 4, OpLineTo = 5, OpStroke = 6, OpLine = 7,
                      OpTriangle = 8, OpEllipse = 9;

    private List<double>? _batch;

    public static Graphics Create() => new(PixiApi.Create("graphics"));
    internal Graphics(int handle) : base(handle) { }

    // ---- 批模式 ----
    public void BeginBatch() => _batch = new List<double>();
    public void EndBatch()
    {
        if (_batch is { Count: > 0 }) PixiApi.GfxBatch(Handle, _batch.ToArray());
        _batch = null;
    }

    // ---- 图形命令（即时或入批） ----
    public void Clear() => Push(OpClear);
    public void DrawRect(float x, float y, float w, float h, Color c) => Push(OpRect, x, y, w, h, c.R, c.G, c.B, c.A);
    public void DrawRoundedRect(float x, float y, float w, float h, float radius, Color c) => Push(OpRound, x, y, w, h, radius, c.R, c.G, c.B, c.A);
    public void DrawCircle(float cx, float cy, float r, Color c) => Push(OpCircle, cx, cy, r, 0, c.R, c.G, c.B, c.A);
    public void MoveTo(float x, float y) => Push(OpMoveTo, x, y);
    public void LineTo(float x, float y) => Push(OpLineTo, x, y);
    public void Stroke(float width, Color c) => Push(OpStroke, width, c.R, c.G, c.B, c.A);
    public void DrawLine(float x1, float y1, float x2, float y2, float width, Color c) => Push(OpLine, x1, y1, x2, y2, width, c.R, c.G, c.B, c.A);
    public void DrawTriangle(float x1, float y1, float x2, float y2, float x3, float y3, Color c) => Push(OpTriangle, x1, y1, x2, y2, x3, y3, c.R, c.G, c.B, c.A);
    public void DrawEllipse(float cx, float cy, float rx, float ry, Color c) => Push(OpEllipse, cx, cy, rx, ry, 0, c.R, c.G, c.B, c.A);

    private void Push(int op, params double[] a)
    {
        if (_batch is not null)
        {
            _batch.Add(op);
            for (int i = 0; i < a.Length; i++) _batch.Add(a[i]);
            for (int i = a.Length; i < Stride - 1; i++) _batch.Add(0);
        }
        else
        {
            PixiApi.Gfx(Handle, op,
                a.Length > 0 ? a[0] : 0, a.Length > 1 ? a[1] : 0, a.Length > 2 ? a[2] : 0,
                a.Length > 3 ? a[3] : 0, a.Length > 4 ? a[4] : 0, a.Length > 5 ? a[5] : 0,
                a.Length > 6 ? a[6] : 0, a.Length > 7 ? a[7] : 0, a.Length > 8 ? a[8] : 0,
                a.Length > 9 ? a[9] : 0);
        }
    }
}
