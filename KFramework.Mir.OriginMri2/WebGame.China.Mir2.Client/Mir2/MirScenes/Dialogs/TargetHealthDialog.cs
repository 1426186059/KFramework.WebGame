using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using KFramework.MonoGameExtend;
using System.Drawing;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 屏幕正上方的选中目标血条面板。
    /// 移植《原始传奇》NorMonsterBloodSkin（default.thm.json 的 EXML，400x245）：
    ///   _Image1(blood_bg0) 居中 y=65；icon(rolebg2) x=90 y=68 w=220 h=14；
    ///   txt_value x=134 y=69 w=132 居中；txt_name x=63 y=44 w=275 居中。
    /// 资源帧 blood_bg0/rolebg2 本地图集无可靠来源，按 EXML 几何用纯色矩形还原（比例缩放到 320 宽）。
    /// </summary>
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
                Font = new Font(Settings.FontName, 12F),
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
                Font = new Font(Settings.FontName, 10F),
                Size = new Size(130, 16),
                Location = new Point((SkinW - 130) / 2, BarY - 2),
                NotControl = true,
                Visible = false
            };
        }

        /// <summary>根据当前选中目标刷新显示。点击选中(SelectionTarget)即刻显示，不必等攻击回包设 TargetObject。</summary>
        public void RefreshTarget()
        {
            // SelectionTarget = 点击选中的对象（脚底环），SelectTarget() 在鼠标左键点击时立即设置；
            // TargetObject 是攻击目标，只有真正攻击/回包后才有——所以不能只用它，否则要"打一下"才显示。
            MapObject target = MapObject.SelectionTarget ?? MapObject.TargetObject;

            // 悬停(MouseObject)也可临时显示，覆盖"只是把鼠标移到怪身上"的操作习惯。
            if (target == null) target = MapObject.MouseObject;

            bool show = target != null && !target.Dead &&
                        (target.Race == ObjectType.Monster || target.Race == ObjectType.Player || target.Race == ObjectType.Hero) &&
                        target != MapObject.User;

            Visible = show;
            _nameLabel.Visible = show;
            _hpLabel.Visible = show;
            _current = show ? target : null;
            if (!show) return;

            _nameLabel.Text = target.Name ?? string.Empty;
            _hpLabel.Text = MapObject.GetHealthText(target);
        }

        protected internal override void DrawControl()
        {
            if (!Visible || !_nameLabel.Visible) return;

            // 底框（blood_bg0）：深色半透明底，居中
            Rectangle src1x1 = new Rectangle(0, 0, 1, 1);
            DXManager.Draw(DXManager.WhitePixel, src1x1,
                           new Rectangle(Location.X, Location.Y + BarY - 4, SkinW, BarH + 8),
                           CommonColor.FromArgb(140, 0, 0, 0));

            // 红条（rolebg2）：按 PercentHealth 从左缩放，原始 icon.width = 220 * pct
            float pct = _current == null ? 0f : System.Math.Max(0f, System.Math.Min(1f, _current.PercentHealth / 100F));
            int fillW = (int)(BarW * pct);
            if (fillW > 0)
            {
                DXManager.Draw(DXManager.WhitePixel, src1x1,
                               new Rectangle(Location.X + BarX, Location.Y + BarY, fillW, BarH),
                               CommonColor.FromArgb(255, 220, 40, 40));
            }

            base.DrawControl();
        }
    }
}
