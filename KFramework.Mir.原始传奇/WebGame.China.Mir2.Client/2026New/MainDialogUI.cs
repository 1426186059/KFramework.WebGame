#if false // 方案2：暂时停用 MainDialogUI（基于 MonoGameExtend/KUIBase 的旧实现，待改为 MirControl Dialog 后启用）
using System;
using System.Threading;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using Client.MirObjects; // MapObject.User / UserObject（真实角色数据，与老 MainDialog 同源）
// Color 在 MirEngine / KFramework.MonoGame 都有，这里明确用 KFramework.MonoGame 版本（KImage.Color 同款）。
using Color = KFramework.MonoGame.Color;

/// <summary>
/// 用新 UI 框架（KFramework.MonoGameExtend：KUIBase / KImage / KButton / KLabel / KCanvas）重建的
/// “游戏主界面（MainDialogUI）”——PC 经典传奇 HUD 布局（横屏页游，随分辨率自适应）。
///
/// 布局对齐 PC 传奇经典 HUD（参考原始 main10.25341.1.min.js 的 MainUICtrl 主界面元素）：
///   - 左上：任务追踪栏（当前主线/支线任务）
///   - 右上：小地图（y_h_map1/2）+ 一排功能按钮（邮件/消息/组队/排行/交易/释放）
///   - 正下方居中：主条（bg_zhukuang_1 角色面板：头像 + 名字/等级 + 血/蓝/经验三条）+ 右侧菜单按钮（背包/自动/组队…）
///   - 主条上方居中：技能栏（F1~F8，用 y_h5h_img19 技能格底图）
///
/// 全部用【贴角锚定】（MinMaxAnchor 四角/中心 + 像素边距），随画布分辨率自适应，不再是写死的手机竖屏坐标。
/// 精灵取自原始传奇 H5 图集 y_homeUI-sheet（default.res.json 资源名 y_homeUI-sheet_json），经 Mir2AtlasLibrary 解析成 KSprite（与老 .Lib 解耦）。
/// 老 UI 框架（MirImageControl / MirButton）保持不动；新 UI 走这套，两条线并存。
/// </summary>
public sealed class MainDialogUI : KUIBase
{
    // 原版主界面精灵表（Egret 短名，对应 default.res.json 的 name；Mir2AtlasLibrary 自动归并到所属 sheet）。
    private const string SheetKey = "y_homeUI-sheet_json";

    private Mir2AtlasSheet _sheet;
    private bool _built;

    // 状态条（填充控件 + 满宽），用于 SetVitals 按百分比改宽度。
    private sealed class Bar { public KImage Fill; public float FullW; public float FullH; }
    private Bar _hpBar, _mpBar, _expBar;
    private KLabel _nameLabel, _levelLabel, _hpLabel, _mpLabel, _expLabel;

    // 示例人物状态（实际接入 GameScene 角色数据后即可替换）。
    private int _hp = 1820, _hpMax = 2400;
    private int _mp = 360, _mpMax = 600;
    private long _exp = 12450, _expMax = 30000;
    private int _level = 42;
    private string _heroName = "Player";

    public MainDialogUI(KCanvas canvas)
    {
        // 关键：所有 UI 必须挂在【场景自带】的 KCanvas 下才能被绘制，并随该场景 Dispose 一同销毁
        // （不再用 KUIRoot.Instance.GetCanvas(0) 全局 Canvas —— 它不随场景切换销毁）。
        Parent = canvas;
        // 根容器铺满画布，且 Pivot=0（左上原点），使子控件用 贴角锚定 时坐标公式干净、随分辨率自适应。
        Pivot = Vector2.Zero;
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
        AnchorOffset = KRectangleFOffset.Zero;
    }

    /// <summary>异步加载主界面图集并搭建 UI 控件树（幂等）。在 CMain.Init 里 fire-and-forget 调用，不阻塞登录。</summary>
    public async Task LoadAsync(GraphicsDevice device, CancellationToken ct = default)
    {
        if (_built) return;
        try
        {
            _sheet = await Mir2AtlasLibrary.GetSheetAsync(SheetKey, device, ct);
            Build();
        }
        catch (Exception ex)
        {
            PrintTool.LogError("[MainDialogUI] 加载/构建失败：" + ex);
        }
    }

    private bool HasFrame(string name) => name != null && _sheet != null && _sheet.Frames.ContainsKey(name);

    /// <summary>取一帧精灵（找不到返回 null）。</summary>
    private KSprite? Frame(string name)
        => HasFrame(name) ? (KSprite?)_sheet.Frames[name] : null;

    /// <summary>取某帧的真实像素尺寸（找不到用兜底值）。用于按实际精灵大小排布，不写死宽高。</summary>
    private Vector2 FSize(string name, float dw, float dh)
    {
        if (HasFrame(name))
        {
            KSprite sp = _sheet.Frames[name]; // KSpriteInfo 隐式转 KSprite 后才有 Rectangle
            var r = sp.Rectangle;
            return new Vector2(r.Width, r.Height);
        }
        return new Vector2(dw, dh);
    }

    /// <summary>放一张图：在父容器内以左上原点 (lx,ly) 定位；可指定尺寸（状态条拉伸用）。</summary>
    private KImage AddImage(KWidget parent, string frame, float lx, float ly, float? w = null, float? h = null, Color? tint = null)
    {
        var img = new KImage
        {
            Parent = parent,
            Pivot = Vector2.Zero,
            Anchor = Vector2.Zero,
            AnchorPosition = new Vector2(lx, ly),
        };
        var fr = Frame(frame);
        if (fr.HasValue) img.Sprite = fr.Value;
        if (w.HasValue && h.HasValue)
        {
            img.UseNativeSize = false;
            img.Size = new Vector2(w.Value, h.Value);
        }
        else
        {
            img.UseNativeSize = true;
        }
        if (tint.HasValue) img.Color = tint.Value;
        return img;
    }

    /// <summary>放一个文字标签（父容器内左上原点定位）。</summary>
    private KLabel AddLabel(KWidget parent, string text, float lx, float ly, float w, float h, Color color)
    {
        return new KLabel(text, color)
        {
            Parent = parent,
            Pivot = Vector2.Zero,
            Anchor = Vector2.Zero,
            AnchorPosition = new Vector2(lx, ly),
            Size = new Vector2(w, h),
        };
    }

    /// <summary>放一个按钮（用精灵帧做底图，点击仅打日志占位）。</summary>
    private KButton AddButton(KWidget parent, string frame, float lx, float ly, string hint)
    {
        var btn = new KButton
        {
            Parent = parent,
            Pivot = Vector2.Zero,
            Anchor = Vector2.Zero,
            AnchorPosition = new Vector2(lx, ly),
        };
        var fr = Frame(frame);
        if (fr.HasValue) btn.Sprite = fr.Value; else btn.UseNativeSize = false;
        btn.Label.Text = "";
        btn.PointerClickEvent += (s, e) => PrintTool.Log($"[MainDialogUI] 点击 {hint}");
        return btn;
    }

    /// <summary>贴角容器：anchorX/anchorY ∈ {0,0.5,1} 决定贴哪个角；offX/offY 为该角往（右/下）的像素边距；子控件用左上原点定位。</summary>
    private KWidget Container(KWidget parent, float anchorX, float anchorY, float offX, float offY, float w, float h)
    {
        return new KWidget
        {
            Parent = parent,
            Pivot = Vector2.Zero,
            Anchor = new Vector2(anchorX, anchorY),
            AnchorPosition = new Vector2(offX, offY),
            Size = new Vector2(w, h),
        };
    }

    /// <summary>
    /// 状态条：底图(y_h_msbg) + 一帧彩色填充（fillFrame，血/蓝/经验）按百分比拉伸宽度。
    /// 彩色帧缺失时用纯色 tint 兜底，保证条可见。返回 Bar 供 SetVitals 调整宽度。
    /// </summary>
    private Bar AddBar(KWidget parent, string fillFrame, float lx, float ly, float w, float h, Color fallback)
    {
        if (!HasFrame("y_h_msbg"))
            AddImage(parent, null, lx, ly, w, h, new Color(20, 20, 30)); // 底图缺失 → 深色背板
        else
            AddImage(parent, "y_h_msbg", lx, ly, w, h);

        var fill = AddImage(parent, fillFrame, lx, ly, w, h);
        if (!HasFrame(fillFrame)) fill.Color = fallback;
        fill.UseNativeSize = false;
        fill.Size = new Vector2(w, h);
        return new Bar { Fill = fill, FullW = w, FullH = h };
    }

    /// <summary>按百分比设置某条状态条的填充宽度（0~1）。</summary>
    private void SetBar(Bar bar, float pct)
    {
        if (bar == null || bar.Fill == null) return;
        bar.Fill.Size = new Vector2(Math.Max(1, bar.FullW * Math.Clamp(pct, 0, 1)), bar.FullH);
    }

    /// <summary>构建控件树（PC 经典布局：左上任务栏 / 右上小地图 / 正下方主条 / 主条上方技能栏）。</summary>
    private void Build()
    {
        if (_built) return;

        var panel = FSize("bg_zhukuang_1", 326, 109);     // 主条面板：头像 + 名字 + 血/蓝/经验（真实帧名 bg_zhukuang_1）
        var skSlot = FSize("y_h5h_img19", 72, 72);  // 技能格底图（72×72，JS 中作技能/快捷键底图）
        var map = FSize("y_h_map1", 142, 140);      // 小地图外框
        var barH = 18f; var hpW = 210f; var expH = 14f;

        // ===== 左上：任务追踪栏 =====
        float taskW = 300, taskH = 150;
        var taskC = Container(this, 0, 0, 8, 8, taskW, taskH);
        AddImage(taskC, null, 0, 0, taskW, taskH, new Color(16, 18, 30, 205));   // 半透明深色面板
        AddImage(taskC, null, 0, 0, taskW, 2, new Color(255, 210, 120, 230));     // 顶部分隔线
        AddLabel(taskC, "任务追踪", 12, 8, taskW - 24, 24, new Color(255, 220, 140));
        AddLabel(taskC, "· 主线：比奇城守卫 (0/1)", 12, 40, taskW - 24, 20, new Color(180, 255, 180));
        AddLabel(taskC, "· 击杀：半兽人 x5", 12, 66, taskW - 24, 20, new Color(210, 210, 210));
        AddLabel(taskC, "· 收集：金币 x1000", 12, 92, taskW - 24, 20, new Color(210, 210, 210));

        // ===== 右上：小地图 + 功能按钮 =====
        var fnSize = FSize("y_btn_email0", 44, 44);
        float fnStep = fnSize.X + 6;
        float fnRowW = 6 * fnStep - 6;
        float fnRowH = fnSize.Y;
        float topRW = Math.Max(map.X, fnRowW);
        float topRH = map.Y + 8 + fnRowH;
        var topC = Container(this, 1, 0, -(topRW + 8), 8, topRW, topRH);
        float mapX = (topRW - map.X) / 2;
        AddImage(topC, "y_h_map1", mapX, 0, map.X, map.Y);
        AddImage(topC, "y_h_map2", mapX, 0, map.X, map.Y);
        string[] fns = { "y_btn_email0", "y_btn_msg0", "y_btn_group0", "y_btn_pm0", "y_btn_jy0", "y_btn_fs0" };
        for (int i = 0; i < fns.Length; i++)
            AddButton(topC, fns[i], i * fnStep, (int)(map.Y + 8), fns[i].Replace("y_btn_", "").Replace("0", ""));

        // ===== 正下方居中：主条（mc_bgi1 角色面板 + 右侧菜单按钮）=====
        float menuBtnW = 56, menuGap = 6;
        string[] menus = { "y_h_btn0", "y_h_zd", "y_h_tz", "y_h_btn4", "y_h_btn5" }; // 背包/自动/组队/…（y_h_btn*/y_h_zd/y_h_tz 为真实帧名）
        float menusW = menus.Length * menuBtnW + (menus.Length - 1) * menuGap;
        float barW = panel.X + 12 + menusW;
        float barHgt = Math.Max(panel.Y, menuBtnW) + expH + 4; // 面板 + 底部经验条
        var barC = Container(this, 0.5f, 1, -barW / 2, -(barHgt + 8), barW, barHgt);
        // 角色面板（左）
        AddImage(barC, "bg_zhukuang_1", 0, 0);
        AddImage(barC, "y_h5h_img19", 16, 16, skSlot.X, skSlot.Y); // 头像（圆形技能格底图兼作头像框）
        _nameLabel = AddLabel(barC, _heroName, 96, 16, 220, 22, new Color(255, 230, 150));
        _levelLabel = AddLabel(barC, "Lv." + _level, 96, 42, 220, 20, new Color(180, 220, 255));
        _hpBar = AddBar(barC, "y_h_ms00", 96, 70, hpW, barH, new Color(200, 40, 40));   // 血（红）
        _mpBar = AddBar(barC, "y_h_ms10", 96, 92, hpW, barH, new Color(40, 80, 200));   // 蓝（蓝）
        _hpLabel = AddLabel(barC, "", 96, 70, hpW, barH, Color.White);
        _mpLabel = AddLabel(barC, "", 96, 92, hpW, barH, Color.White);
        // 经验条（主条底部横条）
        _expBar = AddBar(barC, "y_h_ms20", 0, panel.Y + 2, barW, expH, new Color(200, 180, 40));
        _expLabel = AddLabel(barC, "", 0, panel.Y + 2, barW, expH, Color.White);
        // 右侧菜单按钮
        float mx = panel.X + 12;
        for (int i = 0; i < menus.Length; i++)
            AddButton(barC, menus[i], mx + i * (menuBtnW + menuGap), (barHgt - menuBtnW) / 2, menus[i]);

        // ===== 主条上方居中：技能栏（F1~F8，y_h5h_img19 技能格）=====
        float skStep = skSlot.X + 8;
        float skRowW = 8 * skStep - 8;
        float skRowH = skSlot.Y;
        var skC = Container(this, 0.5f, 1, -skRowW / 2, -(barHgt + 8 + skRowH + 6), skRowW, skRowH);
        for (int i = 0; i < 8; i++)
        {
            AddImage(skC, "y_h5h_img19", i * skStep, 0, skSlot.X, skSlot.Y);
            AddLabel(skC, "F" + (i + 1), i * skStep + skSlot.X / 2 - 8, skSlot.Y - 16, 16, 14, new Color(220, 220, 220));
        }

        RefreshVitals();

        _built = true;
        int n = _sheet != null ? _sheet.Frames.Count : 0;
        PrintTool.Log($"[MainDialogUI] 主界面已构建(PC经典布局)：左上任务栏 / 右上小地图 / 正下方主条(bg_zhukuang_1+菜单) / 主条上方技能栏（SheetKey={SheetKey}, 帧数={n}）");
    }

    /// <summary>刷新状态条宽度与文字（示例数据；接入真实角色数据后调用即可）。</summary>
    public void RefreshVitals()
    {
        SetBar(_hpBar, (float)_hp / _hpMax);
        SetBar(_mpBar, (float)_mp / _mpMax);
        SetBar(_expBar, (float)_exp / _expMax);
        if (_hpLabel != null) _hpLabel.Text = $"HP {_hp}/{_hpMax}";
        if (_mpLabel != null) _mpLabel.Text = $"MP {_mp}/{_mpMax}";
        if (_expLabel != null) _expLabel.Text = $"EXP {_exp}/{_expMax}";
        if (_levelLabel != null) _levelLabel.Text = "Lv." + _level;
        if (_nameLabel != null) _nameLabel.Text = _heroName;
    }

    /// <summary>外部（GameScene 角色数据）更新状态。传 -1 表示该字段不变。</summary>
    public void SetVitals(int hp = -1, int hpMax = -1, int mp = -1, int mpMax = -1, long exp = -1, long expMax = -1, int level = -1, string name = null)
    {
        if (hp >= 0) _hp = hp;
        if (hpMax >= 0) _hpMax = hpMax;
        if (mp >= 0) _mp = mp;
        if (mpMax >= 0) _mpMax = mpMax;
        if (exp >= 0) _exp = exp;
        if (expMax >= 0) _expMax = expMax;
        if (level >= 0) _level = level;
        if (name != null) _heroName = name;
        RefreshVitals();
    }

    /// <summary>
    /// 每帧从真实角色数据刷新 HUD（数据源与老 MainDialog 完全一致：MapObject.User）。
    /// User 为 null（未选角/未登录）时直接跳过，不报错。
    /// </summary>
    public void SyncVitals()
    {
        var u = MapObject.User;
        if (u == null) return;
        SetVitals(
            hp: u.HP,
            hpMax: u.Stats[Stat.HP],
            mp: u.MP,
            mpMax: u.Stats[Stat.MP],
            exp: u.Experience,
            expMax: u.MaxExperience,
            level: u.Level,
            name: u.Name);
    }
}
#endif
