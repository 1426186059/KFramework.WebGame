using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System.Collections.Generic;

namespace MirGame.Tests.InputTest;

/// <summary>
/// 输入测试页：演示键盘 / 鼠标的三种状态（按下 Down、持续按住 Held、抬起 Up），
/// 以及用鼠标左键拖拽物体（方块 A / B）跟随光标移动。全部走轮询查询（GetKeyState / GetButtonState），
/// 事件日志由"本帧状态 vs 上帧状态"差分得出，不订阅静态事件，避免场景切换时的生命周期隐患。
/// </summary>
public sealed class InputTestScene : TestSceneBase
{
    // 要展示的键（带显示名）
    private static readonly (Keys key, string label)[] _keys =
    [
        (Keys.W, "W"), (Keys.A, "A"), (Keys.S, "S"), (Keys.D, "D"),
        (Keys.Space, "Space"), (Keys.Enter, "Enter"), (Keys.LeftShift, "Shift"), (Keys.Escape, "Esc"),
    ];

    private static readonly MouseButton[] _buttonsList = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

    // 每个键 / 按钮的"持续按住"帧数（用于展示 Held）
    private readonly Dictionary<Keys, int> _keyFrames = new();
    private readonly Dictionary<MouseButton, int> _btnFrames = new();

    // 上一帧的按住状态，用于差分出"按下 / 抬起"事件日志
    private readonly Dictionary<Keys, bool> _prevKey = new();
    private readonly Dictionary<MouseButton, bool> _prevBtn = new();
    private readonly List<string> _log = new();

    // 两个可拖拽方块 + 拖拽状态
    private Rectangle _boxA = Rectangle.Empty;
    private Rectangle _boxB = Rectangle.Empty;
    private Vector2 _offsetA;
    private Vector2 _offsetB;
    private bool _draggingA;
    private bool _draggingB;

    public override string Title
    {
        get { return "输入测试（鼠标 / 键盘）"; }
    }

    public override void Update()
    {
        base.Update();

        foreach ((Keys key, _) in _keys)
        {
            bool held = Input_KeyBoard.GetKey(key);
            if (held) _keyFrames[key] = _keyFrames.GetValueOrDefault(key) + 1;
            else _keyFrames[key] = 0;

            bool prev = _prevKey.GetValueOrDefault(key);
            if (held && !prev) LogEvent($"键盘按下 {key}");
            else if (!held && prev) LogEvent($"键盘抬起 {key}");
            _prevKey[key] = held;
        }

        foreach (MouseButton btn in _buttonsList)
        {
            bool held = Input_Mouse.GetButton(btn);
            if (held) _btnFrames[btn] = _btnFrames.GetValueOrDefault(btn) + 1;
            else _btnFrames[btn] = 0;

            bool prev = _prevBtn.GetValueOrDefault(btn);
            if (held && !prev) LogEvent($"鼠标按下 {btn}");
            else if (!held && prev) LogEvent($"鼠标抬起 {btn}");
            _prevBtn[btn] = held;
        }

        if (_boxA.IsEmpty)
        {
            int w = Device.Viewport.Width;
            _boxA = new Rectangle(w - 360, 330, 130, 86);
            _boxB = new Rectangle(w - 190, 450, 110, 110);
        }

        Drag(ref _boxA, ref _offsetA, ref _draggingA);
        Drag(ref _boxB, ref _offsetB, ref _draggingB);
    }

    private void Drag(ref Rectangle box, ref Vector2 offset, ref bool dragging)
    {
        Vector2 mp = Input_Mouse.Position;
        if (Input_Mouse.GetButtonDown(MouseButton.Left) && box.Contains(mp))
        {
            dragging = true;
            offset = new Vector2(box.X - mp.X, box.Y - mp.Y);
        }
        if (dragging && !Input_Mouse.GetButton(MouseButton.Left)) dragging = false;
        if (dragging) box = new Rectangle((int)(mp.X + offset.X), (int)(mp.Y + offset.Y), box.Width, box.Height);
    }

    private void LogEvent(string s)
    {
        _log.Insert(0, s);
        if (_log.Count > 7) _log.RemoveAt(_log.Count - 1);
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float y = origin.Y;

        y += DrawSection(batch, "一、键盘 Keyboard：按下(黄) / 持续按住(绿) / 抬起(红) / 无(灰)", new Vector2(origin.X, y));
        foreach ((Keys key, string label) in _keys)
        {
            KPressState st = Input_KeyBoard.GetKeyState(key);
            int frames = _keyFrames.GetValueOrDefault(key);
            string suffix = st == KPressState.Held ? $" (按住 {frames} 帧)" : "";
            batch.DrawString(Font, $"{label,-6} {StateText(st)}{suffix}", new Vector2(origin.X, y), StateColor(st));
            y += Font.LineSpacing + 4f;
        }

        Vector2 axis = Input_KeyBoard.GetAxis();
        batch.DrawString(Font, $"WASD / 方向键 轴: ({axis.X:+0.##;-0.##;0}, {axis.Y:+0.##;-0.##;0})", new Vector2(origin.X, y), Color.LightGray);
        y += Font.LineSpacing + 10f;

        y += DrawSection(batch, "二、鼠标 Mouse", new Vector2(origin.X, y));
        batch.DrawString(Font,
            $"位置: ({Input_Mouse.X}, {Input_Mouse.Y})   位移: ({Input_Mouse.Delta.X}, {Input_Mouse.Delta.Y})   滚轮累计: {Input_Mouse.ScrollValue}",
            new Vector2(origin.X, y), Color.LightGray);
        y += Font.LineSpacing + 4f;
        foreach (MouseButton btn in _buttonsList)
        {
            KPressState st = Input_Mouse.GetButtonState(btn);
            int frames = _btnFrames.GetValueOrDefault(btn);
            string suffix = st == KPressState.Held ? $" (按住 {frames} 帧)" : "";
            batch.DrawString(Font, $"{btn}  {StateText(st)}{suffix}", new Vector2(origin.X, y), StateColor(st));
            y += Font.LineSpacing + 4f;
        }
        y += 8f;

        batch.DrawString(Font, "本帧事件日志（按下 / 抬起，由状态差分得出）:", new Vector2(origin.X, y), Color.LightGray);
        y += Font.LineSpacing + 4f;
        for (int i = 0; i < _log.Count; i++)
        {
            batch.DrawString(Font, _log[i], new Vector2(origin.X, y), new Color(200, 210, 230));
            y += Font.LineSpacing + 3f;
        }

        DrawDragArea(batch);
    }

    private void DrawDragArea(SpriteBatch batch)
    {
        int w = Device.Viewport.Width;
        Rectangle panel = new Rectangle(w - 380, 300, 360, 280);
        DrawRect(batch, panel, new Color(20, 26, 40));
        batch.DrawString(Font, "拖拽区域：左键按住方块拖动", new Vector2(panel.X + 10f, panel.Y + 8f), Color.LightGray);
        DrawBox(batch, _boxA, _draggingA, "方块 A");
        DrawBox(batch, _boxB, _draggingB, "方块 B");
    }

    private void DrawBox(SpriteBatch batch, Rectangle box, bool dragging, string label)
    {
        Color fill = dragging ? new Color(96, 190, 255) : new Color(60, 120, 200);
        DrawRect(batch, box, fill);
        float cx = box.X + box.Width / 2f - Font.Measure(label).X / 2f;
        float cy = box.Y + box.Height / 2f - Font.LineSpacing / 2f;
        batch.DrawString(Font, label, new Vector2(cx, cy), Color.White);
        if (dragging)
            batch.DrawString(Font, "拖拽中", new Vector2(box.X, box.Y - Font.LineSpacing - 2f), new Color(255, 206, 110));
    }

    private static Color StateColor(KPressState st)
    {
        if (st == KPressState.Down) return new Color(255, 206, 110);
        if (st == KPressState.Held) return new Color(120, 230, 140);
        if (st == KPressState.Up) return new Color(230, 120, 120);
        return new Color(120, 130, 150);
    }

    private static string StateText(KPressState st)
    {
        if (st == KPressState.Down) return "按下";
        if (st == KPressState.Held) return "按住";
        if (st == KPressState.Up) return "抬起";
        return "无";
    }
}
