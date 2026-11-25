using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    // 原始传奇 buffGro 图标 35×35，本工程缩小 2 倍；每行 5 个。提到文件级供两个 Buff 窗口共用。
    public static class BuffGrid
    {
        public const int CellSize = 17;
        public const int Columns = 5;
    }

    public class BuffDialog : MirImageControl
    {
        public List<ClientBuff> Buffs = new List<ClientBuff>();

        protected MirButton _expandCollapseButton;
        protected MirLabel _buffCountLabel;
        protected List<MirImageControl> _buffList = new List<MirImageControl>();
        protected int _buffCount;
        public Func<bool> GetExpandedParameter;
        public Action<bool> SetExpandedParameter;
        private bool ExpandedBuffWindow
        {
            get { return GetExpandedParameter(); }
            set { SetExpandedParameter(value); }
        }

        public BuffDialog()
        {
            // 对齐原始传奇：玩家/怪物 buffGro 无底板，图标 35×35 每行 5 个（main10.25487.1.min.js:
            // BuffRender.x = i%5*35, y = floor(i/5)*35）。原 Prguse2#20 底板与展开按钮都是旧图库白块，去掉。
            Movable = false;
            Size = new Size(BuffGrid.Columns * BuffGrid.CellSize, BuffGrid.CellSize);
            Sort = true;

            Opacity = 1f;

            _expandCollapseButton = new MirButton
            {
                Size = new Size(16, 15),
                Parent = this,
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            _expandCollapseButton.Click += (o, e) =>
            {
                if (_buffCount == 1)
                {
                    ExpandedBuffWindow = true;
                }
                else
                {
                    ExpandedBuffWindow = !ExpandedBuffWindow;
                }

                UpdateWindow();
            };

            _buffCountLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold),
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
                image.Location = new Point((i % BuffGrid.Columns) * BuffGrid.CellSize, (i / BuffGrid.Columns) * BuffGrid.CellSize);
                image.Size = new Size(BuffGrid.CellSize, BuffGrid.CellSize);
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
            _expandCollapseButton.Visible = false;
            int columns = _buffCount > BuffGrid.Columns ? BuffGrid.Columns : _buffCount;
            int rows = (_buffCount + BuffGrid.Columns - 1) / BuffGrid.Columns;
            Size = new Size(columns * BuffGrid.CellSize, rows * BuffGrid.CellSize);
            ApplyAnchor();
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


    //UNFINISHED
    public class ClientPoisonBuff
    {
        public PoisonType Type;
        public string Caster;
        public int Value;
        public int TickSpeed;
        public long ExpireTime;
    }

    public sealed class PoisonBuffDialog : MirImageControl
    {
        public List<ClientPoisonBuff> Buffs = new List<ClientPoisonBuff>();

        private MirButton _expandCollapseButton;
        private MirLabel _buffCountLabel;
        private List<MirImageControl> _buffList = new List<MirImageControl>();
        private int _buffCount;

        public PoisonBuffDialog()
        {
            Movable = false;
            Size = new Size(BuffGrid.Columns * BuffGrid.CellSize, BuffGrid.CellSize);
            Sort = true;

            Opacity = 1f;

            _expandCollapseButton = new MirButton
            {
                Size = new Size(16, 15),
                Parent = this,
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            _expandCollapseButton.Click += (o, e) =>
            {
                if (_buffCount == 1)
                {
                    Settings.ExpandedBuffWindow = true;
                }
                else
                {
                    Settings.ExpandedBuffWindow = !Settings.ExpandedBuffWindow;
                }

                UpdateWindow();
            };

            _buffCountLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold),
                NotControl = true,
                Sort = true,
                Visible = false,
                ForeColour = CommonColor.Yellow,
                OutLineColour = CommonColor.Black,
            };
        }

        // 原始传奇 5config buff 表：中毒=501、麻痹=442、眩晕=451；其余取经典技能图标。
        private static string PoisonBuffIconId(PoisonType type)
        {
            switch (type)
            {
                case PoisonType.Green:
                case PoisonType.Bleeding:
                    return "501";       // 中毒
                case PoisonType.Red:
                case PoisonType.DelayedExplosion:
                    return "13031";     // 施毒术
                case PoisonType.Slow:
                    return "452";
                case PoisonType.Frozen:
                    return "12131";     // 冰咆哮
                case PoisonType.Stun:
                case PoisonType.Dazed:
                    return "451";       // 眩晕
                case PoisonType.Paralysis:
                case PoisonType.LRParalysis:
                    return "442";       // 麻痹
                default:
                    return "501";
            }
        }

        private static string PoisonBuffIconUrl(PoisonType type)
        {
            return Mir2OriginObjectLibrary.SkillIconPath(PoisonBuffIconId(type));
        }

        public void CreateBuff(ClientPoisonBuff buff)
        {
            var image = new MirImageControl
            {
                Parent = this,
                Visible = true,
                Sort = false,
                ImageUrl = PoisonBuffIconUrl(buff.Type),
            };

            _buffList.Insert(0, image);
            UpdateWindow();
        }

        public string BuffString(ClientPoisonBuff buff)
        {
            string text = RegexFunctions.SeperateCamelCase(buff.Type.ToLocalizedString()) + "\n";

            switch (buff.Type)
            {
                case PoisonType.Green:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? LanguageMgr.Get("Seconds"): LanguageMgr.Get("Second");

                        text += LanguageMgr.Get("ReceiveDamageEveryTick", buff.Value, tick, tickName);
                    }
                    break;
                case PoisonType.Red:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? LanguageMgr.Get("Seconds"): LanguageMgr.Get("Second");

                        text += LanguageMgr.Get("ReducesArmourRatePerTick", tick, tickName);
                    }
                    break;
                case PoisonType.Slow:
                    text += LanguageMgr.Get("ReducesMovementSpeed");
                    break;
                case PoisonType.Frozen:
                    text += LanguageMgr.Get("PreventsCastingMovingAttacking");
                    break;
                case PoisonType.Stun:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? LanguageMgr.Get("Seconds"): LanguageMgr.Get("Second");

                        text += LanguageMgr.Get("DamageReceivedIncrease", tick, tickName);
                    }
                    break;
                case PoisonType.Paralysis:
                    text += LanguageMgr.Get("PreventsMoveAndAttack");
                    break;
                case PoisonType.DelayedExplosion:
                    text += LanguageMgr.Get("TickingTimeBomb");
                    break;
                case PoisonType.Bleeding:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? LanguageMgr.Get("Seconds"): LanguageMgr.Get("Second");

                        text += LanguageMgr.Get("RecieveDamageEveryTime", buff.Value, tick, tickName);
                    }
                    break;
                case PoisonType.LRParalysis:
                    text += LanguageMgr.Get("PreventsMoveAttackCancelOnHit");
                    break;
                case PoisonType.Blindness:
                    text += LanguageMgr.Get("CausesTemporaryBlindness");
                    break;
                case PoisonType.Dazed:
                    text += LanguageMgr.Get("PreventsAttacking");
                    break;
            }

            text += LanguageMgr.Get("Expire", Functions.PrintTimeSpanFromSeconds(Math.Round((buff.ExpireTime - CMain.Time) / 1000D)));

            if (!string.IsNullOrEmpty(buff.Caster)) text += LanguageMgr.Get("CasterName", buff.Caster);

            return text;
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

                // 原始传奇 BuffRender：35×35，每行 5 个，常显。图标走 icon/skills CDN。
                image.Location = new Point((i % BuffGrid.Columns) * BuffGrid.CellSize, (i / BuffGrid.Columns) * BuffGrid.CellSize);
                image.Size = new Size(BuffGrid.CellSize, BuffGrid.CellSize);
                image.Hint = BuffString(buff);
                image.ImageUrl = PoisonBuffIconUrl(buff.Type);

                image.Visible = true;
                image.Opacity = 1f;

                if (!(Math.Round((buff.ExpireTime - CMain.Time) / 1000D) <= 5))
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
            _expandCollapseButton.Visible = false;
            int columns = _buffCount > BuffGrid.Columns ? BuffGrid.Columns : _buffCount;
            int rows = (_buffCount + BuffGrid.Columns - 1) / BuffGrid.Columns;
            Size = new Size(columns * BuffGrid.CellSize, rows * BuffGrid.CellSize);
            ApplyAnchor();
        }
    }

}

