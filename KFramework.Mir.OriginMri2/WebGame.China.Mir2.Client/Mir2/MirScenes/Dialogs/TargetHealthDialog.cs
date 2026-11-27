using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 选中目标头顶信息栏。移植原始传奇 PlayerBloodViewSkin（187×95，图集 y_killr-sheet_json），
    /// 对应 CDN 图 b5c5bba6_12465.png（254×188 的 killR 图集）。每个图对应一个 MirImageControl：
    ///   名字行底 killR_xtbg1(44,2, scale9 16/4/100/10)、血条底 killR_xtbg(44,21)、
    ///   血条 killR_xt(44,22,131×14, FillAmount 裁剪)、头像 killR_wjtx2(4,0)、
    ///   状态图标位 killR_iconbg(buffGro 30,71,131×19)。
    /// 布局权威：default.thm.json 内嵌 PlayerBloodViewSkin.exml；帧表来自 CDN y_killr-sheet.json。
    /// </summary>
    public sealed class TargetHealthDialog : MirControl
    {
        private const int SkinW = 187;
        private const int SkinH = 95;

        private const string Sheet = "y_killr-sheet_json";

        private readonly MirImageControl _nameBg;
        private readonly MirImageControl _barBg;
        private readonly MirImageControl _bar;
        private readonly MirImageControl _head;
        private readonly MirImageControl[] _buffIcons = new MirImageControl[5];
        private readonly MirLabel _nameLabel;
        private readonly MirLabel _hpLabel;
        private MapObject _current;

        public TargetHealthDialog()
        {
            NotControl = true;
            Movable = false;
            Sort = false;
            Size = new Size(SkinW, SkinH);
            Anchor = EAnchorType.TopCenter;
            AnchorPos = new Point(0, 10);
            ApplyAnchor();

            // 头像 / 提示位 killR_wjtx2 (4,0)，帧实际 37×37
            _head = AddImage(Sheet + "#killR_wjtx2", 4, 0, 37, 37);

            // 名字行底 killR_xtbg1 (44,2)，帧实际 132×18，九宫格保留边框（scale9 16,4,100,10）
            _nameBg = AddImage(Sheet + "#killR_xtbg1", 44, 2, 132, 18);
            _nameBg.DrawMode = MirImageDrawMode.NineGrid;
            _nameBg.BorderLeft = 16;
            _nameBg.BorderTop = 4;
            _nameBg.BorderRight = 10;
            _nameBg.BorderBottom = 4;

            // 血条底 killR_xtbg (44,21)，帧实际 132×14
            _barBg = AddImage(Sheet + "#killR_xtbg", 44, 21, 132, 14);

            // 血条本体 killR_xt (44,22,130×12)，按 FillAmount 从左裁剪（不拉伸变形）
            _bar = AddImage(Sheet + "#killR_xt", 44, 22, 130, 12);
            _bar.FillOrigin = MirFillOrigin.Left;
            _bar.FillAmount = 0f;

            // 状态图标位 buffGro (30,71)，每格一个 killR_iconbg（帧实际 32×32，间距 32）
            for (int i = 0; i < _buffIcons.Length; i++)
            {
                _buffIcons[i] = AddImage(Sheet + "#killR_iconbg", 30 + i * 32, 60, 32, 32);
                _buffIcons[i].Visible = false;
            }

            _nameLabel = new MirLabel
            {
                Parent = this,
                AutoSize = false,
                BackColour = CommonColor.Transparent,
                ForeColour = CommonColor.FromArgb(255, 232, 176),
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Font = FontConst.F12,
                Size = new Size(139, 16),
                Location = new Point(40, 3),
                NotControl = true,
                Visible = false
            };

            _hpLabel = new MirLabel
            {
                Parent = this,
                AutoSize = false,
                BackColour = CommonColor.Transparent,
                ForeColour = CommonColor.White,
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Font = FontConst.F10,
                Size = new Size(128, 14),
                Location = new Point(44, 22),
                NotControl = true,
                Visible = false
            };
        }

        private MirImageControl AddImage(string atlasKey, int x, int y, int w, int h)
        {
            return new MirImageControl
            {
                AtlasNameKey = atlasKey,
                Parent = this,
                Location = new Point(x, y),
                Size = new Size(w, h),
                AutoSize = false,
                DrawControlTexture = false,
                NotControl = true,
            };
        }

        public void RefreshTarget()
        {
            MapObject target = MapObject.SelectionTarget;
            if (target == null)
            {
                Visible = false;
                _nameLabel.Visible = false;
                _hpLabel.Visible = false;
                _current = null;
                return;
            }

            bool show = !target.Dead && target != MapObject.User &&
                (target.Race == ObjectType.Monster ||
                 target.Race == ObjectType.Player ||
                 target.Race == ObjectType.Hero);

            Visible = show;
            _nameLabel.Visible = show;
            _hpLabel.Visible = show;
            _current = show ? target : null;
            if (!show)
            {
                return;
            }

            _nameLabel.Text = target.Name ?? string.Empty;
            _hpLabel.Text = MapObject.GetHealthText(target);

            float pct = System.Math.Max(0f, System.Math.Min(100f, _current.PercentHealth));
            _bar.FillAmount = pct / 100f;
        }

        protected internal override void DrawControl()
        {
            if (!Visible || !_nameLabel.Visible)
            {
                return;
            }

            if (_current != null)
            {
                _hpLabel.Text = MapObject.GetHealthText(_current);
                _bar.FillAmount = System.Math.Max(0f, System.Math.Min(100f, _current.PercentHealth)) / 100f;
            }

            base.DrawControl();
        }
    }
}
