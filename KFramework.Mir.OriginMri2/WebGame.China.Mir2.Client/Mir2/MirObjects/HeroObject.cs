using Client.MirControls;
using Client.MirScenes.Dialogs;
using S = ServerPackets;

namespace Client.MirObjects
{
    public class HeroObject : PlayerObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Hero; }
        }

        public string OwnerName;
        public MirLabel OwnerLabel;

        public override bool ShouldDrawHealth()
        {
            if (GroupDialog.GroupList.Contains(OwnerName) || OwnerName == User.Name)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public HeroObject(uint objectID) : base(objectID)
        {
        }

        public void Load(S.ObjectHero info)
        {
            Load((S.ObjectPlayer)info);
            OwnerName = info.OwnerName;

            if (info.ObjectID == Hero?.ObjectID)
                Hero.CurrentLocation = info.Location;
        }

        public override void CreateLabel()
        {
            base.CreateLabel();

            OwnerLabel = null;
            string ownerText = LanguageMgr.Get("OwnerHero", OwnerName);

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != ownerText || LabelList[i].ForeColour != NameColour) continue;
                OwnerLabel = LabelList[i];
                break;
            }

            if (OwnerLabel != null && !OwnerLabel.IsDisposed) return;

            OwnerLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = CommonColor.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Text = ownerText,
            };
            OwnerLabel.Disposing += (o, e) => LabelList.Remove(OwnerLabel);
            LabelList.Add(OwnerLabel);
        }

        public override void DrawName()
        {
            CreateLabel();

            if (NameLabel == null || OwnerLabel == null) return;

            // 名字中心 = 脚底 - NameYOffset（身体中段），主人名贴在名字下方。
            int nameCenterY = DrawLocation.Y - NameYOffset + (Dead ? 35 : 0);

            NameLabel.Location = new Point(DrawLocation.X - NameLabel.Size.Width / 2, nameCenterY - NameLabel.Size.Height / 2);
            NameLabel.Draw();

            OwnerLabel.Location = new Point(DrawLocation.X - OwnerLabel.Size.Width / 2, NameLabel.Location.Y + NameLabel.Size.Height - 1);
            OwnerLabel.Draw();
        }
    }
}
