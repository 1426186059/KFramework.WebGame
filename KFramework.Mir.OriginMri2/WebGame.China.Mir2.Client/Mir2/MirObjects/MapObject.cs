using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public abstract class MapObject
    {
        public static Font ChatFont = FontConst.F10;
        public static List<MirLabel> LabelList = new List<MirLabel>();

        public static UserObject User;
        public static UserHeroObject Hero;
        public static HeroObject HeroObject;
        public static MapObject MouseObject, TargetObject, MagicObject;
        // 脚底选中环的"当前选中对象"：点击任意可交互实体（NPC/怪物/玩家）即设为它，
        // 单一变量天然互斥（新选中覆盖旧选中）；对象离场时由 Remove() 清理。
        public static MapObject SelectionTarget;

        private static uint mouseObjectID;
        public static uint MouseObjectID
        {
            get => mouseObjectID;
            set
            {
                if (mouseObjectID == value) return;
                mouseObjectID = value;
                MouseObject = MapControl.Objects.TryGetValue(value, out var obj) ? obj : null;
            }
        }

        private static uint lastTargetObjectId;
        private static uint targetObjectID;
        public static uint TargetObjectID
        {
            get => targetObjectID;
            set
            {
                if (targetObjectID == value) return;
                lastTargetObjectId = value;
                targetObjectID = value;
                TargetObject = MapControl.Objects.TryGetValue(value, out var obj) ? obj : null;
            }
        }

        /// <summary>
        /// 可攻击对象：此字段【一定是可攻击实体】——可攻击的怪物、其他玩家、卫士(守卫/弓卫)等敌对实体，
        /// 也包括召唤物/宠物等能被攻击的对象；绝不会是 NPC / 物品 / 自己 / 尸体。
        /// 普通攻击和所有攻击法术统一以它为目标；选中 NPC 时它必为 null（见 SelectTarget），
        /// 永不把 NPC 塞进来。与 MouseObject（鼠标悬停、移到 UI 即 null）不同，它一旦选中就持续保持。
        /// 后端就是 TargetObject/TargetObjectID。
        /// </summary>
        public static MapObject CanAttackTargetObject
        {
            get => TargetObject;
            set => TargetObjectID = value != null ? value.ObjectID : 0;
        }

        /// <summary>
        /// 是否攻击/追击当前选中的目标（b=bool 前缀，遵循项目命名习惯）。
        /// 点中可攻击对象时由 SelectTarget 置 true；点地面时置 false（仅停止自动攻击与追击，不清空 CanAttackTargetObject）。
        /// 与"是否选中目标"解耦：目标可以一直保持选中，但玩家点地面后不再自动打它/走向它。
        /// 【只管自动普攻/追击】施法（雷电术/灵魂火符等）不受此开关影响：UseMagic 只读 CanAttackTargetObject，
        /// 所以点地面后仍可手动施法攻击选中的目标。
        /// </summary>
        public static bool bAutoAttackSelectTarget;

        /// <summary>
        /// 统一"选中"入口（脚底环 SelectionTarget + 攻击目标 CanAttackTargetObject + 法术目标 MagicObjectID）。
        /// 【参数 target 只是"被点选的对象"，不要求可攻击】——NPC、物品也会传进来。
        /// 只有可攻击对象（怪物/玩家，排除 NPC/物品/死亡）才写入 CanAttackTargetObject；
        /// 传 NPC/物品/null/死亡对象时，脚底环可以留在它身上，但 CanAttackTargetObject 一律置 null，
        /// 从而保证 CanAttackTargetObject 永远只装可攻击对象。
        /// 所有点击选中处都应走这里。
        /// </summary>
        public static void SelectTarget(MapObject target)
        {
            if (target == null || target.Dead)
            {
                SelectionTarget = null;
                CanAttackTargetObject = null;
                MagicObjectID = 0;
                bAutoAttackSelectTarget = false;
                return;
            }

            SelectionTarget = target;

            if (target.Race == ObjectType.Item || target.Race == ObjectType.Merchant || target is NPCObject)
            {
                CanAttackTargetObject = null;
                MagicObjectID = 0;
                bAutoAttackSelectTarget = false;
                return;
            }

            CanAttackTargetObject = target;
            bAutoAttackSelectTarget = true; // 点中可攻击对象 → 允许自动攻击/追击它

            if (target is MonsterObject monster && monster.AI != 6)
                MagicObjectID = target.ObjectID;
            else
                MagicObjectID = 0;
        }

        private static uint magicObjectID;
        public static uint MagicObjectID
        {
            get => magicObjectID;
            set
            {
                if (magicObjectID == value) return;
                magicObjectID = value;
                MagicObject = MapControl.Objects.TryGetValue(value, out var obj) ? obj : null;
            }
        }

        /// <summary>
        /// 场景销毁（退出到选人界面）时清空所有跨场景残留的静态选中状态。
        /// 脚底选中环由 MapControl.DrawSelectionRing 每帧跟随 SelectionTarget/TargetObject 绘制，
        /// 这些是 static 字段；Dispose 只把 MapControl=null 不会清它们，导致重进游戏后旧环仍被画出来。
        /// 这里直接置字段与内部 ID，绕过各 ID setter 对 MapControl.Objects 的依赖（此时 MapControl 可能为 null）。
        /// </summary>
        public static void ClearSceneSelection()
        {
            SelectionTarget = null;
            MouseObject = null;
            TargetObject = null;
            MagicObject = null;
            mouseObjectID = 0;
            targetObjectID = 0;
            lastTargetObjectId = 0;
            magicObjectID = 0;
            bAutoAttackSelectTarget = false;
        }

        public abstract ObjectType Race { get; }
        public abstract bool Blocking { get; }

        public uint ObjectID;
        public string Name = string.Empty;
        public Point CurrentLocation, MapLocation;
        public MirDirection Direction;
        public bool Dead, Hidden, SitDown, Sneaking;
        public PoisonType Poison;
        public long DeadTime;
        public byte AI;
        public bool InTrapRock;
        public int JumpDistance;

        public bool Blend = true;

        public long BlindTime;
        public byte BlindCount;

        // 服务端 S.ObjectHealth 下发的精确血量（血条 "当前/总" 文本用）。
        public int HP, MaxHP;

        private byte percentHealth;
        public virtual byte PercentHealth
        {
            get { return percentHealth; }
            set
            {
                if (percentHealth == value) return;

                percentHealth = value;
            }
        }
        public long HealthTime;

        private byte percentMana;
        public virtual byte PercentMana
        {
            get { return percentMana; }
            set
            {
                if (percentMana == value) return;

                percentMana = value;
            }
        }

        public uint LastTargetObjectId => lastTargetObjectId;

        public List<QueuedAction> ActionFeed = new List<QueuedAction>();
        public QueuedAction NextAction
        {
            get { return ActionFeed.Count > 0 ? ActionFeed[0] : null; }
        }

        public List<Effect> Effects = new List<Effect>();
        public List<BuffType> Buffs = new List<BuffType>();

        public MLibrary BodyLibrary;
        public Color DrawColour = CommonColor.White, NameColour = CommonColor.White, LightColour = CommonColor.White;
        public MirLabel NameLabel, ChatLabel, GuildLabel;
        public long ChatTime;
        public int DrawFrame, DrawWingFrame;
        public Point DrawLocation, Movement, FinalDrawLocation, OffSetMove;
        public Rectangle DisplayRectangle;
        public int Light, DrawY;
        public long NextMotion, NextMotion2;
        public MirAction CurrentAction;
        public byte CurrentActionLevel;
        public bool SkipFrames;
        public FrameLoop FrameLoop = null;

        //Sound
        public int StruckWeapon;

        public MirLabel TempLabel;

        public static List<MirLabel> DamageLabelList = new List<MirLabel>();
        public List<Damage> Damages = new List<Damage>();

        protected Point GlobalDisplayLocationOffset
        {
            get { return new Point(0, 0); }
        }

        protected MapObject() { }

        protected MapObject(uint objectID)
        {
            ObjectID = objectID;

            if (MapControl.Objects.TryGetValue(ObjectID, out var existingObject))
                existingObject.Remove();

            MapControl.Objects[ObjectID] = this;
            MapControl.ObjectsList.Add(this);
            RestoreTargetStates();
        }

        public void Remove()
        {
            if (MouseObject == this) MouseObjectID = 0;
            if (TargetObject == this)
            {
                TargetObjectID = 0;
                lastTargetObjectId = ObjectID;
            }
            if (MagicObject == this) MagicObjectID = 0;
            if (SelectionTarget == this) SelectionTarget = null;

            MapControl.Objects.Remove(ObjectID);
            MapControl.ObjectsList.Remove(this);
            GameScene.Scene.MapControl.RemoveObject(this);

            if (ObjectID == Hero?.ObjectID)
                HeroObject = null;

            if (ObjectID != GameScene.NPCID) return;

            GameScene.NPCID = 0;
            GameScene.Scene.NPCDialog.Hide();
            GameScene.Scene.NPCTeleporterDialog.Hide();
        }

        public abstract void Process();
        public abstract void Draw();
        public abstract bool MouseOver(Point p);

        /// <summary>后台静默预取（懒队列）：每帧由 MapControl 节流调用，仅发起低优先级请求暖缓存，不阻塞；默认空实现。</summary>
        public virtual void PrefetchBackground() { }

        private void RestoreTargetStates()
        {
            if (MouseObjectID == ObjectID)
                MouseObject = this;

            if (TargetObjectID == ObjectID)
                TargetObject = this;

            if (MagicObjectID == ObjectID)
                MagicObject = this;

            if (!this.Dead &&
                TargetObject == null &&
                LastTargetObjectId == ObjectID)
            {
                switch (Race)
                {
                    case ObjectType.Player:
                    case ObjectType.Monster:
                    case ObjectType.Hero:
                        targetObjectID = ObjectID;
                        TargetObject = this;
                        break;
                }
            }
        }

        public void AddBuffEffect(BuffType type)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!(Effects[i] is BuffEffect)) continue;
                if (((BuffEffect)(Effects[i])).BuffType == type) return;
            }

            PlayerObject ob = null;

            if (Race == ObjectType.Player)
            {
                ob = (PlayerObject)this;
            }

            switch (type)
            {
                case BuffType.Fury:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 190, 7, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.ImmortalSkin:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 570, 5, 1400, this, true, type) { Repeat = true });
                    break;
                case BuffType.SwiftFeet:
                    if (ob != null) ob.Sprint = true;
                    break;
                case BuffType.MoonLight:
                case BuffType.DarkBody:
                    if (ob != null) ob.Sneaking = true;
                    break;
                case BuffType.VampireShot:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 2110, 6, 1400, this, true, type) { Repeat = false });
                    break;
                case BuffType.PoisonShot:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 2310, 7, 1400, this, true, type) { Repeat = false });
                    break;
                case BuffType.EnergyShield:
                    BuffEffect effect;

                    Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1880, 9, 900, this, true, type) { Repeat = false });
                    SoundManager.PlaySound(20000 + (ushort)Spell.EnergyShield * 10 + 0);

                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, this, true, type) { Repeat = true });
                    };
                    break;
                case BuffType.MagicBooster:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 90, 6, 1200, this, true, type) { Repeat = true });
                    break;
                case BuffType.PetEnhancer:
                    Effects.Add(new BuffEffect(Libraries.Magic3, 230, 6, 1200, this, true, type) { Repeat = true });
                    break;
                case BuffType.GameMaster:
                    Effects.Add(new BuffEffect(Libraries.CHumEffect[5], 0, 1, 1200, this, true, type) { Repeat = true });
                    break;
                case BuffType.GeneralMeowMeowShield:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.GeneralMeowMeow], 529, 7, 700, this, true, type) { Repeat = true, Light = 1 });
                    MirSounds.SoundManager.PlaySound(8322);
                    break;
                case BuffType.PowerBeadBuff:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.PowerUpBead], 64, 6, 600, this, true, type) { Blend = true, Repeat = true });
                    break;
                case BuffType.HornedArcherBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 468, 6, 600, this, true, type) { Repeat = false });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 474, 3, 1000, this, true, type) { Blend = true, Repeat = true });
                    };
                    break;
                case BuffType.ColdArcherBuff:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 477, 7, 700, this, true, type) { Repeat = false });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedArcher], 484, 3, 1000, this, true, type) { Blend = true, Repeat = true });
                    };
                    break;
                case BuffType.HornedWarriorShield:
                    Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedWarrior], 912, 18, 1800, this, true, type) { Repeat = true });
                    break;
                case BuffType.HornedCommanderShield:
                    Effects.Add(effect = new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1173, 1, 100, this, true, type) { Repeat = false, Light = 1 });
                    effect.Complete += (o, e) =>
                    {
                        Effects.Add(new BuffEffect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1174, 16, 1600, this, true, type) { Repeat = true, Light = 1 });
                    };
                    break;
            }
        }
        public void RemoveBuffEffect(BuffType type)
        {
            PlayerObject ob = null;

            if (Race == ObjectType.Player)
            {
                ob = (PlayerObject)this;
            }

            for (int i = 0; i < Effects.Count; i++)
            {
                if (!(Effects[i] is BuffEffect)) continue;
                if (((BuffEffect)(Effects[i])).BuffType != type) continue;
                Effects[i].Repeat = false;
            }

            switch (type)
            {
                case BuffType.SwiftFeet:
                    if (ob != null) ob.Sprint = false;
                    break;
                case BuffType.MoonLight:
                case BuffType.DarkBody:
                    if (ob != null) ob.Sneaking = false;
                    break;
            }
        }

        public Color ApplyDrawColour()
        {
            Color drawColour = DrawColour;
            if (drawColour == CommonColor.Gray)
            {
                drawColour = CommonColor.White;
                DXManager.SetGrayscale(true);
            }
            return drawColour;
        }

        public virtual Missile CreateProjectile(int baseIndex, MLibrary library, bool blend, int count, int interval, int skip, int lightDistance = 6, bool direction16 = true, Color? lightColour = null, uint targetID = 0)
        {
            return null;
        }

        /// <summary>头顶聊天文字【底部】相对脚底的上移像素。层级：名字(36) → 血条(BloodYOffset) → 称号/会员 → 聊天。</summary>
        protected virtual int ChatYOffset => BloodYOffset + 50;

        public void Chat(string text)
        {
            if (ChatLabel != null && !ChatLabel.IsDisposed)
            {
                ChatLabel.Dispose();
                ChatLabel = null;
            }

            const int chatWidth = 200;
            List<string> chat = new List<string>();

            int index = 0;
            for (int i = 1; i < text.Length; i++)
                if (TextRenderer.MeasureText(text.Substring(index, i - index), ChatFont, new Size(int.MaxValue, int.MaxValue)).Width > chatWidth)
                {
                    chat.Add(text.Substring(index, i - index - 1));
                    index = i - 1;
                }
            chat.Add(text.Substring(index, text.Length - index));

            text = chat[0];
            for (int i = 1; i < chat.Count; i++)
                text += string.Format("\n{0}", chat[i]);

            ChatLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = CommonColor.FromArgb(150, 12, 12, 12),
                ForeColour = CommonColor.DeepSkyBlue,
                OutLine = true,
                OutLineColour = CommonColor.Black,
                UnderLine = true,
                DrawFormat = new TextStyle(TextAnchor.UpperCenter),
                Text = text,
            };
            ChatTime = CMain.Time + 5000;
        }

        public virtual void DrawChat()
        {
            if (ChatLabel == null || ChatLabel.IsDisposed) return;

            if (CMain.Time > ChatTime)
            {
                ChatLabel.Dispose();
                ChatLabel = null;
                return;
            }

            ChatLabel.ForeColour = Dead ? CommonColor.Gray : CommonColor.DeepSkyBlue;
            // 与名字一致：只用 DrawLocation（脚底逻辑坐标）+ 固定偏移，不依赖逐帧变化的 DisplayRectangle。
            ChatLabel.Location = new Point(
                DrawLocation.X - ChatLabel.Size.Width / 2,
                DrawLocation.Y - ChatYOffset - ChatLabel.Size.Height - (Dead ? 35 : 0));
            ChatLabel.Draw();
        }

        public virtual void CreateLabel()
        {
            NameLabel = null;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != Name || LabelList[i].ForeColour != NameColour) continue;
                NameLabel = LabelList[i];
                break;
            }


            if (NameLabel != null && !NameLabel.IsDisposed) return;

            NameLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = CommonColor.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Text = Name,
            };
            NameLabel.Disposing += (o, e) => LabelList.Remove(NameLabel);
            LabelList.Add(NameLabel);



        }
        /// <summary>名字【中心】相对脚底 DrawLocation 的上移像素。
        /// 原始传奇 EntityInfo：名字文本顶部 y = -(bloodY-48) = -30（相对脚底），血条 y = -bloodY=-78。
        /// 名字中心 ≈ 30 + 半字高(约7) = 37，取 36 忠实还原（之前 48 偏高）。改这一个常量即可整体升降名字。
        /// 名字只用实体逻辑坐标 + 固定偏移，不碰动画帧 offX/offY，故跑动不抖。各体型可重写。</summary>
        protected virtual int NameYOffset => 36;

        public virtual void DrawName()
        {
            CreateLabel();

            if (NameLabel == null) return;

            // 只用 DrawLocation（脚底逻辑坐标，随移动平滑变化），不依赖逐帧变化的动画矩形。
            NameLabel.Location = new Point(
                DrawLocation.X - NameLabel.Size.Width / 2,
                DrawLocation.Y - NameYOffset - NameLabel.Size.Height / 2 + (Dead ? 35 : 0));
            NameLabel.Draw();
        }

        // 调试可视化：在对象的 DrawLocation（模型参考点/脚底基准）画一个红色圆圈，
        // 便于核对“模型实际画出来的位置”是否偏离了这个基准点（用来查模型画歪/偏移）。
        public static bool DebugShowDrawLocation = false;
        public static void DrawLocationMarker(Point p)
        {
            if (!DebugShowDrawLocation) return;
            const int r = 7;
            const int seg = 28;
            var pts = new KFramework.MonoGame.Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                double a = i * 2.0 * Math.PI / seg;
                pts[i] = new KFramework.MonoGame.Vector2(p.X + (float)(r * Math.Cos(a)), p.Y + (float)(r * Math.Sin(a)));
            }
            DXManager.DrawLines(pts, 2f, CommonColor.Red);
        }

        // 调试可视化：把点击区域 DisplayRectangle 画成绿色矩形轮廓，便于核对命中框是否与模型贴合。
        public static void DrawRectangleMarker(Rectangle rect)
        {
            if (!DebugShowDrawLocation) return;
            if (rect.Width <= 0 || rect.Height <= 0) return;
            int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
            var pts = new KFramework.MonoGame.Vector2[]
            {
                new KFramework.MonoGame.Vector2(x, y),
                new KFramework.MonoGame.Vector2(x + w, y),
                new KFramework.MonoGame.Vector2(x + w, y + h),
                new KFramework.MonoGame.Vector2(x, y + h),
                new KFramework.MonoGame.Vector2(x, y),
            };
            DXManager.DrawLines(pts, 2f, CommonColor.Lime);
        }

        public virtual void DrawBlend()
        {
            DXManager.SetBlend(true, 0.3F); //0.8
            Draw();
            DXManager.SetBlend(false);
        }
        public void DrawDamages()
        {
            for (int i = Damages.Count - 1; i >= 0; i--)
            {
                Damage info = Damages[i];
                if (CMain.Time > info.ExpireTime)
                {
                    if (info.DamageLabel != null)
                    {
                        info.DamageLabel.Dispose();
                    }

                    Damages.RemoveAt(i);
                }
                else
                {
                    // 伤害飘字与名字同源：用脚底 DrawLocation 作基准，避免转向/换帧时飘字跳动。
                    info.Draw(DrawLocation);
                }
            }
        }
        public virtual bool ShouldDrawHealth()
        {
            return false;
        }
        // ===== 原始传奇头顶血条（EntityInfo.setBlood / red_blood + empty_blood）=====
        // JS 权威：血条底图 empty_blood、填充 red_blood，二者 x=-width/2 居中、y=-bloodY+1；
        // bloodY 默认 78（实体配置可覆盖）。填充按比例裁剪（NorMonsterBloodView: icon.width=220*pct）。
        // 图集来自 BloodFont（default.res.json subkeys：red_blood/yellow_blood/empty_blood）。
        private static int _originBloodRequested;
        private MirLabel _bloodLabel;

        private static void EnsureOriginBloodSheet()
        {
            if (System.Threading.Interlocked.Exchange(ref _originBloodRequested, 1) != 0) return;
            _ = Mir2AtlasLibrary.GetSheetAsync("BloodFont_json");
        }

        /// <summary>原始血条相对脚底 DrawLocation 的上移像素（JS EntityInfo._bloodY 默认 78）。</summary>
        protected virtual int BloodYOffset => 78;

        public void DrawHealth()
        {
            if (Dead) return;
            if (Race != ObjectType.Player && Race != ObjectType.Monster && Race != ObjectType.Hero) return;

            if (CMain.Time >= HealthTime)
            {
                if (!ShouldDrawHealth()) return;
            }

            EnsureOriginBloodSheet();

            if (!Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "empty_blood", out var bg) || bg?.Texture == null ||
                !Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "red_blood", out var fill) || fill?.Texture == null)
                return; // 原始素材未就绪，本帧不画（图集异步加载，下一帧自然出现）

            // 以底图自然宽度为准；填充图按同一宽度、相同左边缘对齐，避免两者宽度不一致导致错位。
            int barW = Math.Max(1, (int)bg.SourceRectangle.Width);
            int barH = Math.Max(1, (int)bg.SourceRectangle.Height);
            int barX = DrawLocation.X - barW / 2;                 // JS: x = -width>>1
            int barY = DrawLocation.Y - BloodYOffset + 1;         // JS: y = -_bloodY + 1

            DXManager.Draw(bg.Texture, bg.SourceRectangle,
                           new Rectangle(barX, barY, barW, barH), CommonColor.White, MirBlend.Normal.ToBlendState());

            float pct = PercentHealth / 100F;
            if (pct > 1f) pct = 1f;
            int fillW = (int)(barW * pct);
            if (fillW > 0)
            {
                // 填充从左侧裁剪，高度与底图对齐（原版 icon.width = 220 * pct）。
                Rectangle fillSrc = new Rectangle(fill.SourceRectangle.X, fill.SourceRectangle.Y,
                                                  Math.Min(fillW, fill.SourceRectangle.Width), fill.SourceRectangle.Height);
                DXManager.Draw(fill.Texture, fillSrc,
                               new Rectangle(barX, barY, fillW, barH), CommonColor.White, MirBlend.Normal.ToBlendState());
            }

            // 数值 Label 在血条上方再抬高 10px
            DrawBloodLabel(barX + barW / 2, barY + barH / 2 - 10);
        }

        /// <summary>血条数值文本 "当前血/总血"（原始 txt_value = truehp/maxHp）。玩家用真实 HP，怪按配置 MaxHP×百分比还原。</summary>
        public static string GetHealthText(MapObject ob)
        {
            if (ob == null) return string.Empty;
            if (ob is UserObject player && player.Stats != null)
            {
                int max = player.Stats[Stat.HP];
                return max > 0 ? $"{player.HP}/{max}" : $"{ob.PercentHealth}%";
            }
            if (ob is MonsterObject monster)
            {
                // 服务端 S.ObjectHealth 已带精确 HP/MaxHP；未收到（0）时回退用 MonsterInfo 配置估算。
                if (ob.MaxHP > 0) return $"{ob.HP}/{ob.MaxHP}";
                int max = 0;
                if (ExcelTableMgr.MonstersReady && ExcelTableMgr.TryGetMonster(monster.OriginModel, out var info) && info.Stats != null)
                    max = info.Stats[Stat.HP];
                int cur = max > 0 ? (int)Math.Round(max * ob.PercentHealth / 100F) : 0;
                return max > 0 ? $"{cur}/{max}" : $"{ob.PercentHealth}%";
            }
            // Hero 等其它对象：服务端也下发了精确 HP/MaxHP
            if (ob.MaxHP > 0) return $"{ob.HP}/{ob.MaxHP}";
            return $"{ob.PercentHealth}%";
        }

        private void DrawBloodLabel(int centerX, int centerY)
        {
            string text = GetHealthText(this);

            if (_bloodLabel == null)
            {
                _bloodLabel = new MirLabel
                {
                    Font = FontConst.F9,
                    AutoSize = true,
                    BackColour = CommonColor.Transparent,
                    ForeColour = CommonColor.White,
                    OutLine = true,
                    OutLineColour = CommonColor.Black
                };
                _bloodLabel.Disposing += (o, e) => _bloodLabel = null;
                LabelList.Add(_bloodLabel);
            }
            // 先给 Text，AutoSize 才会重算 Size，之后才能按真实宽高居中定位。
            _bloodLabel.Text = text;
            _bloodLabel.Location = new Point(centerX - _bloodLabel.Size.Width / 2, centerY - _bloodLabel.Size.Height / 2);
            _bloodLabel.Draw();
        }

        public void DrawPoison()
        {
            byte poisoncount = 0;
            if (Poison != PoisonType.None)
            {
                if (Poison.HasFlag(PoisonType.Green))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Green);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Red))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Red);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Bleeding))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.DarkRed);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Slow))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Purple);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Stun) || Poison.HasFlag(PoisonType.Dazed))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Yellow);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Blindness))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.MediumVioletRed);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Frozen))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Blue);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.Paralysis) || Poison.HasFlag(PoisonType.LRParalysis))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Gray);
                    poisoncount++;
                }
                if (Poison.HasFlag(PoisonType.DelayedExplosion))
                {
                    DXManager.Draw(DXManager.PoisonDotBackground, new Rectangle(0, 0, 6, 6), new Vector3((float)(DisplayRectangle.X + 7 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 21), 0.0F), CommonColor.Black);
                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(DisplayRectangle.X + 8 + (poisoncount * 5)), (float)(DisplayRectangle.Y - 20), 0.0F), CommonColor.Orange);
                    poisoncount++;
                }
            }
        }

        public abstract void DrawBehindEffects(bool effectsEnabled);

        public abstract void DrawEffects(bool effectsEnabled);

        protected void LoopFrame(int start, int frameCount, int frameInterval, int duration)
        {
            if (FrameLoop == null)
            {
                FrameLoop = new FrameLoop
                {
                    Start = start,
                    End = start + frameCount - 1,
                    Loops = (duration / (frameInterval * frameCount)) - 1 //Remove 1 count as we've already done a loop before this is checked
                };
            }
        }
    }

    public class FrameLoop
    {
        public MirAction Action { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public int Loops { get; set; }

        public int CurrentCount { get; set; }
    }

}
