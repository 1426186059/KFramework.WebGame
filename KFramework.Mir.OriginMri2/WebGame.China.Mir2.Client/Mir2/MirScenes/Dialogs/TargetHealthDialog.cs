using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;

namespace Client.MirScenes.Dialogs
{
    public sealed class TargetHealthDialog : MirControl
    {
        private const int SkinW = 320;                 // 原始 400 缩放 0.8
        private const int BarW = 176;                  // 220 * 0.8
        private const int BarH = 11;                   // 14 * 0.8
        private const int BarX = (SkinW - BarW) / 2;   // 原始 x=90（相对400）= (400-220)/2
        private const int BarY = (int)(68 * 0.8);      // 54
        private const int NameY = (int)(44 * 0.8);     // 35

        private readonly MirLabel _nameLabel;
        private readonly MirLabel _hpLabel;
        private MapObject _current; // 当前显示的目标（点击选中即有，不必等攻击回包）

        public TargetHealthDialog()
        {
            NotControl = true;
            Movable = false;
            Sort = false;
            Size = new Size(SkinW, (int)(245 * 0.8));

            _nameLabel = new MirLabel
            {
                Parent = this,
                AutoSize = false,
                BackColour = CommonColor.Transparent,
                ForeColour = CommonColor.FromArgb(255, 232, 176),
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Font = FontConst.F12,
                Size = new Size(260, 20),
                Location = new Point((SkinW - 260) / 2, NameY),
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
                Size = new Size(130, 16),
                Location = new Point((SkinW - 130) / 2, BarY - 2),
                NotControl = true,
                Visible = false
            };
        }

        public void RefreshTarget()
        {
            MapObject target = MapObject.SelectionTarget;
            if (target == null)
            {
                return;
            }

            bool show = target != null && !target.Dead && target != MapObject.User &&
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
            }

            Rectangle src1x1 = new Rectangle(0, 0, 1, 1);
            DXManager.Draw(DXManager.WhitePixel, src1x1,
                           new Rectangle(Location.X, Location.Y + BarY - 4, SkinW, BarH + 8),
                           CommonColor.FromArgb(140, 0, 0, 0));

            float pct = _current == null ? 0f : System.Math.Max(0f, System.Math.Min(1f, _current.PercentHealth / 100F));
            int fillW = (int)(BarW * pct);
            if (fillW > 0)
            {
                DXManager.Draw(
                    DXManager.WhitePixel, 
                    src1x1,
                    new Rectangle(Location.X + BarX, Location.Y + BarY, fillW, BarH),
                    CommonColor.FromArgb(255, 220, 40, 40));
            }

            base.DrawControl();
        }

    }
}
