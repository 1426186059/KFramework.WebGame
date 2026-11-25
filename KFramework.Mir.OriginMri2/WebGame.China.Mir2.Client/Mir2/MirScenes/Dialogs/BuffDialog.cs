using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    public class BuffDialog : MirImageControl
    {
        // 原始传奇 buffGro 图标 35×35，本工程缩小 2 倍；每行 5 个。两个 Buff 窗口共用。
        public const int CellSize = 17;
        public const int Columns = 5;
        public const int BufferXOffset = 5;

        public List<ClientBuff> Buffs = new List<ClientBuff>();

        protected MirLabel _buffCountLabel;
        protected List<MirImageControl> _buffList = new List<MirImageControl>();
        protected int _buffCount;

        public BuffDialog()
        {
            // 对齐原始传奇：玩家/怪物 buffGro 无底板，图标 35×35 每行 5 个（main10.25487.1.min.js:
            // BuffRender.x = i%5*35, y = floor(i/5)*35）。原 Prguse2#20 底板与展开按钮都是旧图库白块，去掉。
            Movable = false;
            Size = new Size(BuffDialog.Columns * BuffDialog.CellSize, BuffDialog.CellSize);
            Sort = true;
            Anchor = EAnchorType.BottomRight;
            AnchorPos = new Point(-30, 0);
            ApplyAnchor();

            Opacity = 1f;

            _buffCountLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Font = FontConst.F10Bold,
                NotControl = true,
                Sort = true,
                Visible = false,
                ForeColour = CommonColor.Yellow,
                OutLineColour = CommonColor.Black,
            };
        }

        // BuffType 的数值即原始传奇 5config buff id，直接查表取 icon（icon/skills/{icon}.png）。
        private static string BuffIconUrl(BuffType type)
        {
            return Mir2OriginObjectLibrary.SkillIconPath(OriginBuffTable.GetIcon((int)type));
        }

        public void CreateBuff(ClientBuff buff)
        {
            var image = new MirImageControl
            {
                Parent = this,
                Visible = true,
                Sort = false,
                ImageUrl = BuffIconUrl(buff.Type),
            };

            _buffList.Insert(0, image);
            UpdateWindow();
        }

        public void RemoveBuff(int buffId)
        {
            _buffList[buffId].Dispose();
            _buffList.RemoveAt(buffId);

            UpdateWindow();
        }

        public void Process()
        {
            if (!Visible) return;

            if (_buffList.Count != _buffCount)
            {
                UpdateWindow();
            }

            for (var i = 0; i < _buffList.Count; i++)
            {
                var image = _buffList[i];
                var buff = Buffs[i];

                // 原始传奇 BuffRender：35×35，每行 5 个，常显。图标走 icon/skills CDN，不再依赖旧 .Lib。
                image.Location = new Point(
                    (i % BuffDialog.Columns) * BuffDialog.CellSize, 
                    -(i / BuffDialog.Columns) * BuffDialog.CellSize);

                image.Size = new Size(BuffDialog.CellSize, BuffDialog.CellSize);
                image.AutoSize = false;
                image.Hint = BuffString(buff);
                image.ImageUrl = BuffIconUrl(buff.Type);
                image.Visible = true;
                image.Opacity = 1f;

                if (buff.Paused || buff.Infinite || !(Math.Round((buff.ExpireTime - CMain.Time) / 1000D) <= 5))
                {
                    continue;
                }

                var time = (buff.ExpireTime - CMain.Time) / 100D;

                if (Math.Round(time) % 10 < 5)
                {
                    image.Visible = false;
                }
            }

        }

        private void UpdateWindow()
        {
            _buffCount = _buffList.Count;

            // 原始传奇 buffGro 网格布局（本工程图标缩小 2 倍），无底板、无收起态、无数量标签。
            _buffCountLabel.Visible = false;
            int columns = _buffCount > BuffDialog.Columns ? BuffDialog.Columns : _buffCount;
            int rows = (_buffCount + BuffDialog.Columns - 1) / BuffDialog.Columns;
            Size = new Size(columns * BuffDialog.CellSize, rows * BuffDialog.CellSize);
        }

        public string BuffString(ClientBuff buff)
        {
            string text = RegexFunctions.SeperateCamelCase(buff.Type.ToLocalizedString()) + "\n";
            bool overridestats = false;

            switch (buff.Type)
            {
                case BuffType.GameMaster:
                    GMOptions options = (GMOptions)buff.Values[0];

                    if (options.HasFlag(GMOptions.GameMaster)) text += LanguageMgr.Get("Invisible");
                    if (options.HasFlag(GMOptions.Superman)) text += LanguageMgr.Get("Superman");
                    if (options.HasFlag(GMOptions.Observer)) text += LanguageMgr.Get("Observer");
                    break;
                case BuffType.MentalState:
                    switch (buff.Values[0])
                    {
                        case 0:
                            text += LanguageMgr.Get("AgressiveFullDamageCantShootOverWalls");
                            break;
                        case 1:
                            text += LanguageMgr.Get("TrickShotMinimalDamage");
                            break;
                        case 2:
                            text += LanguageMgr.Get("GroupModeMediumDamageDontStealAgro");
                            break;
                    }
                    break;
                case BuffType.Hiding:
                case BuffType.ClearRing:
                    text += LanguageMgr.Get("InvisibleToManyMonsters");
                    break;
                case BuffType.MoonLight:
                    text += LanguageMgr.Get("InvisibleToPlayersAndMonstersAtDistance");
                    break;
                case BuffType.EnergyShield:
                    overridestats = true;
                    text += LanguageMgr.Get("ChanceGainHpWhenAttacked", buff.Stats[Stat.EnergyShieldPercent], buff.Stats[Stat.EnergyShieldHPGain]);
                    break;
                case BuffType.DarkBody:
                    text += LanguageMgr.Get("InvisibleToManyMonstersAbleToMove");
                    break;
                case BuffType.VampireShot:
                    text += LanguageMgr.Get("GivesVampiricAbility");
                    break;
                case BuffType.PoisonShot:
                    text += LanguageMgr.Get("GivesPoisonAbility");
                    break;
                case BuffType.Concentration:
                    text += LanguageMgr.Get("IncreaseElementExtractionChance");
                    break;
                case BuffType.MagicBooster:
                    overridestats = true;
                    text += LanguageMgr.Get("IncreaseMcAndConsumption", buff.Stats[Stat.MinMC], buff.Stats[Stat.MaxMC], buff.Stats[Stat.ManaPenaltyPercent]);
                    break;
                case BuffType.Transform:
                    text += LanguageMgr.Get("DisguisesYourAppearance");
                    break;
                case BuffType.Mentee:
                    text += LanguageMgr.Get("LearnSkillPointsTwiceAsQuick");
                    break;
                case BuffType.Guild:
                    text += GameScene.Scene.GuildDialog.ActiveStats;
                    break;
                case BuffType.Blindness:
                    text += LanguageMgr.Get("ReducesVisibility");
                    break;
                case BuffType.Newbie:
                    text += LanguageMgr.Get("GuildMemberBoost");
                    break;
            }

            if (!overridestats)
            {
                foreach (var val in buff.Stats.Values)
                {
                    var c = val.Value < 0 ? LanguageMgr.Get("Decreases"): LanguageMgr.Get("Increases");
                    var strKey = val.Key.ToLocalizedString();

                    var sign = "";

                    if (val.Key.ToString().Contains("Percent"))
                        sign = "%";
                    else if (val.Key.ToString().Contains("Multiplier"))
                        sign = "x";

                    var txt = LanguageMgr.Get("BuffEffect", c, strKey, val.Value, sign);

                    text += txt;
                }
            }

            if (buff.Paused)
            {
                text += LanguageMgr.Get("ExpirePaused");
            }
            else if (buff.Infinite)
            {
                text += LanguageMgr.Get("ExpireNever");
            }
            else
            {
                text += LanguageMgr.Get("Expire", Functions.PrintTimeSpanFromSeconds(Math.Round((buff.ExpireTime - CMain.Time) / 1000D)));
            }

            if (!string.IsNullOrEmpty(buff.Caster)) text += LanguageMgr.Get("CasterName", buff.Caster);

            return text;
        }

        private string CombinedBuffText()
        {
            string text = LanguageMgr.Get("ActiveBuffs");
            var stats = new Stats();

            for (var i = 0; i < _buffList.Count; i++)
            {
                var buff = Buffs[i];

                stats.Add(buff.Stats);
            }

            foreach (var val in stats.Values)
            {
                var c = val.Value < 0 ? LanguageMgr.Get("Decreases"): LanguageMgr.Get("Increases");
                var strKey = val.Key.ToLocalizedString();

                var sign = "";

                if (val.Key.ToString().Contains("Percent"))
                    sign = "%";
                else if (val.Key.ToString().Contains("Multiplier"))
                    sign = "x";

                var txt = LanguageMgr.Get("BuffEffect", c, strKey, val.Value, sign);;

                text += txt;
            }

            return text;
        }

        private int BuffImage(BuffType type)
        {
            switch (type)
            {
                //Skills
                case BuffType.Fury:
                    return 76;
                case BuffType.Rage:
                    return 49;
                case BuffType.ImmortalSkin:
                    return 80;
                case BuffType.CounterAttack:
                    return 7;

                case BuffType.MagicBooster:
                    return 73;
                case BuffType.MagicShield:
                    return 30;

                case BuffType.Hiding:
                case BuffType.ClearRing:
                    return 17;
                case BuffType.Haste:
                    return 60;
                case BuffType.SoulShield:
                    return 13;
                case BuffType.BlessedArmour:
                    return 14;
                case BuffType.ProtectionField:
                    return 50;
                case BuffType.UltimateEnhancer:
                    return 35;
                case BuffType.Curse:
                    return 45;
                case BuffType.EnergyShield:
                    return 57;

                case BuffType.SwiftFeet:
                    return 67;
                case BuffType.LightBody:
                    return 68;
                case BuffType.MoonLight:
                    return 65;
                case BuffType.DarkBody:
                    return 70;

                case BuffType.Concentration:
                    return 96;
                case BuffType.VampireShot:
                    return 100;
                case BuffType.PoisonShot:
                    return 102;
                case BuffType.MentalState:
                    return 199;

                //Monster
                case BuffType.RhinoPriestDebuff:
                    return 217;
                case BuffType.Blindness:
                    return 226;

                //Special
                case BuffType.GameMaster:
                    return 173;
                case BuffType.General:
                    return 182;
                case BuffType.Exp:
                    return 260;
                case BuffType.Drop:
                    return 162;
                case BuffType.Gold:
                    return 168;
                case BuffType.Knapsack:
                case BuffType.BagWeight:
                    return 235;
                case BuffType.Transform:
                    return 241;
                case BuffType.Mentor:
                case BuffType.Mentee:
                    return 248;
                case BuffType.Lover:
                    return 201;
                case BuffType.Guild:
                    return 203;
                case BuffType.Rested:
                    return 240;
                case BuffType.TemporalFlux:
                    return 261;
                case BuffType.Skill:
                    return 200;
                case BuffType.Newbie:
                    return 182;

                //Stats
                case BuffType.Impact:
                    return 249;
                case BuffType.Magic:
                    return 165;
                case BuffType.Taoist:
                    return 250;
                case BuffType.Storm:
                    return 170;
                case BuffType.HealthAid:
                    return 161;
                case BuffType.ManaAid:
                    return 169;
                case BuffType.Defence:
                    return 166;
                case BuffType.MagicDefence:
                    return 158;
                case BuffType.WonderDrug:
                    return 252;
                default:
                    return 0;
            }
        }
    }




}

