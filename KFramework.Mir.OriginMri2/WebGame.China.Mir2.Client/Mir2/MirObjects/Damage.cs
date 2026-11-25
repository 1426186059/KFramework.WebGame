using Client.MirControls;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public class Damage
    {
        public string Text;
        public Color Colour;
        public int Distance;
        public long StartTime;
        public long ExpireTime;
        public double Factor;
        public int Offset;

        public MirLabel DamageLabel;

        public Damage(string text, int duration, Color colour, int distance = 50)
        {
            StartTime = CMain.Time;
            ExpireTime = (long)(CMain.Time + duration);
            Text = text;
            Distance = distance;
            Factor = duration / this.Distance;
            Colour = colour;
        }

        public virtual void Draw(Point displayLocation)
        {
            long timeRemaining = ExpireTime - CMain.Time;

            if (DamageLabel == null)
            {
                DamageLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = CommonColor.Transparent,
                    ForeColour = Colour,
                    OutLine = true,
                    OutLineColour = CommonColor.Black,
                    Text = Text,
                    Font = FontConst.F8Bold
                };
            }

            // 与名字/头顶聊天一致：按标签实际宽度水平居中，不再用 Text.Length*3 估算。
            displayLocation.Offset(-DamageLabel.Size.Width / 2, (int)(((int)((double)timeRemaining / Factor)) - Distance) - 75 - Offset);

            DamageLabel.Location = displayLocation;
            DamageLabel.Draw();
        }
    }

}
