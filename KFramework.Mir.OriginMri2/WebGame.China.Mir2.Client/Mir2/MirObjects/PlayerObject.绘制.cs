using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirControls;
using S = ServerPackets;
using C = ClientPackets;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public partial class PlayerObject : MapObject
    {
        private void UpdateWingEffect()
        {
            if (WingEffect > 0 && CMain.Time >= NextMotion2)
            {
                GameScene.Scene.MapControl.TextureValid = false;

                if (SkipFrames) UpdateFrame2();

                if (UpdateFrame2() >= Frame.EffectCount)
                    EffectFrameIndex = Frame.EffectCount - 1;
                else
                    NextMotion2 += EffectFrameInterval;
            }
        }



        public override Missile CreateProjectile(int baseIndex, MLibrary library, bool blend, int count, int interval, int skip, int lightDistance = 6, bool direction16 = true, Color? lightColour = null, uint targetID = 0)
        {
            if (targetID == 0)
            {
                targetID = TargetID;
            }

            MapObject ob = MapControl.GetObject(targetID);

            var targetPoint = TargetPoint;

            if (ob != null) targetPoint = ob.CurrentLocation;

            int duration = Functions.MaxDistance(CurrentLocation, targetPoint) * 50;

            Missile missile = new Missile(library, baseIndex, duration / interval, duration, this, targetPoint)
            {
                Target = ob,
                Interval = interval,
                FrameCount = count,
                Blend = blend,
                Skip = skip,
                Light = lightDistance,
                LightColour = lightColour == null ? CommonColor.White : (Color)lightColour
            };

            Effects.Add(missile);

            return missile;
        }

        //Rebuild
        /// <summary>
        /// 脚步声 —— 原始传奇做法：角色动作音由 ActionSound 绑定在动画帧上，
        /// 玩家只有跑步(action=3)在帧1播 RunSound(soundId=1)，走路不发声；
        /// 音效走 CDN audio/{id}.mp3（PlayOriginSound）。
        /// 旧实现是经典 Mir2 按 .map 瓦片材质(草地/石头/木板/洞穴)判断脚步声的近千行死代码，
        /// CDN 地图下相关字段全为 0、完全失效，已删除。
        /// </summary>

        public void PlayStepSound()
        {
            if (CurrentAction != MirAction.Running) return;   // 原版仅跑步有脚步声
            if (FrameIndex != 1 && FrameIndex != 4) return;  // 绑定在跑步动画的落脚帧
            if (RidingMount) return;                          // 坐骑另有音效
            SoundManager.PlayOriginSound(1);                  // ActionSound.RunSound
        }

        // 角色战斗音效统一改用原始传奇（ActionSound 权威定义，见主 JS）：
        //   RunSound=1 / AttackSound=2 / FemaleDieSound=3 / MaleDieSound=4；命中通用音=6（skillEffects）。
        // 经典 Mir2 那套按武器材质(剑/斧/棍)、性别、坐骑类型细分音色的 wav 属于被抛弃的旧资源体系，
        // 原始传奇没有这些细分，全部走 CDN audio/{id}.mp3（SoundManager.PlayOriginSound）。

        public void PlayStruckSound() => SoundManager.PlayOriginSound(6);   // 命中/受击通用音

        public void PlayFlinchSound() => SoundManager.PlayOriginSound(6);   // 受身退缩，同命中音

        public void PlayAttackSound() => SoundManager.PlayOriginSound(2);   // AttackSound

        public void PlayDieSound() => SoundManager.PlayOriginSound(Gender == 0 ? 4 : 3); // 男4/女3

        public void PlayMountSound() => SoundManager.PlayOriginSound(1);    // 坐骑移动，用跑步音


        public override void Draw()
        {
            DrawBehindEffects(Settings.Effect);

            float oldOpacity = DXManager.Opacity;
            if (Hidden && !DXManager.Blending) DXManager.SetOpacity(0.5F);

            // 原始传奇 CDN 独立图集：分层复合（法阵→足迹→坐骑→翅膀→身体→武器→旋光→称号→徽记），每层一个 origin model/{n}。
            // 身体/武器来自 cm.item[col17]（装扮/幻武可覆盖）；装扮类来自 cm.title[id].model。
            // 纯线上：origin 未就绪/缺失时不再回落 .Lib，直接跳过模型绘制（仅保留通用特效层）。
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.DefaultTotalDir, (int)Direction);
                // 原始传奇 5 方向精灵：逻辑方向 5/6/7（左下/左/左上）复用右侧真帧 3/2/1，绘制时需水平翻转。
                bool mirror = (int)Direction >= 5;
                KFramework.MonoGame.SpriteEffects fx = mirror ? KFramework.MonoGame.SpriteEffects.FlipHorizontally : KFramework.MonoGame.SpriteEffects.None;
                // 局部：绘制一个 origin 层（请求+锚定绘制）。每层用自己的 standx/standy（来自 2config.modelInfo），
                // 各部件按自身枢轴对齐角色点、自然合成正确人形；不强制共享单一枢轴
                // （之前强制成盔甲注册点会把武器/护肩等彼此错开，模型“散架/画歪”）。
                // 头发/头部饰品应使用身体的枢轴（见 bodySx/bodySy），其余部件保持自身枢轴。
                void DrawLayer(int m, int? sx = null, int? sy = null)
                {
                    if (m <= 0) return;
                    EResLoadPriority pr = (this == User) ? EResLoadPriority.User : EResLoadPriority.Visible;
                    Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Player, m, action, dir, pr);
                    Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.Player, m, action, dir, FrameIndex, DrawLocation.X, DrawLocation.Y, DrawColour, fx, standX: sx, standY: sy);
                }
                // 地图玩家部件对齐原始传奇 UIModelUtil.setFirstPlayerModel/showPlayerModel，只显示：
                //   Body(衣服) + Weapon(武器) + Head(斗笠) + Miansha(面纱)。
                // 原版本就没有护肩/玉佩/勋章/披风/疗伤石；护盾(Shield)原版虽有但本项目不需要，一并去掉。
                // 法阵/足迹/坐骑/翅膀/旋光/称号/徽记等特效层也不绘制。
                // 身体层主模型（盔甲/时装，由 _originModel 给出）若在 CDN 确认缺失，回落裸身 9001/9002。
                int bodyModel = _originModel;
                if (Mir2OriginObjectLibrary.IsMissing(Mir2ObjectKind.Player, bodyModel, action, dir))
                {
                    int naked = Gender == MirGender.Female ? 9002 : 9001;
                    if (!_loggedBodyFallback.ContainsKey(bodyModel))
                    {
                        _loggedBodyFallback[bodyModel] = 1;
                        CMain.SaveError($"[玩家] 身体模型 {bodyModel} 在 CDN 缺失，回落裸身 {naked}（仍走 origin，非旧 .Lib）。");
                    }
                    bodyModel = naked;
                }
                // 枢轴：身体优先用自身 stand，缺则回落裸身 9001/9002；斗笠/武器等部件无自身 stand，
                // 跟随身体角色锚点（原版 getModelConfig 对玩家部件回落到 ≈536/530，非画布中心）。
                int baseBody = Gender == MirGender.Female ? 9002 : 9001;
                int charSx, charSy;
                if (!Mir2OriginObjectLibrary.TryGetStand(bodyModel, action, out charSx, out charSy)
                    && !Mir2OriginObjectLibrary.TryGetStand(baseBody, action, out charSx, out charSy))
                {
                    Mir2OriginObjectLibrary.TryGetStand(9001, action, out charSx, out charSy);
                }
                DrawLayer(bodyModel, charSx, charSy);              // Body 衣服（身体，含装扮覆盖）——命中框也据此计算
                // 原始传奇 UIModelUtil.setFirstPlayerModel：无斗笠/头盔时 Head 层回落到头发
                // （男 9498 / 女 9499）。女裸身 9002 少了头发就会被误看成男裸模。
                int headModel = PlayerDouLiModel();
                if (headModel <= 0) headModel = PlayerHairModel();
                DrawLayer(headModel, charSx, charSy);              // Head 斗笠/头盔，缺省为头发
                DrawLayer(PlayerMianShaModel(), charSx, charSy);   // Miansha 面纱（面部）
                DrawLayer(PlayerWeaponModel(), charSx, charSy);    // Weapon 武器（前，含幻武覆盖）

                // 鼠标命中框/血条定位用：与“身体实际绘制”用完全相同的参数（同 action / 同帧 / 同镜像 / 身体自己的枢轴）。
                // 各层已恢复各自 standx/standy，身体层用的就是它自己的注册点，命中框据此计算即贴合身体。
                // 名字定位已改到 MapObject.DrawName 里相对脚底基准 DrawLocation 固定定位，不依赖此处。
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.Player, bodyModel, action, dir, FrameIndex,
                        DrawLocation.X, DrawLocation.Y, out var bodyRect, 1f, mirror: (int)Direction >= 5, standX: charSx, standY: charSy))
                {
                    DisplayRectangle = new Rectangle(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
                    DrawRectangleMarker(DisplayRectangle);
                }
                // 调试：在脚底基准 DrawLocation 画红圈，核对模型是否偏移。
                DrawLocationMarker(DrawLocation);
            }


            

            // 纯线上：origin 未就绪/缺失时不回落 .Lib，直接跳过模型绘制（通用特效层仍由 DrawBehindEffects/DrawEffects 负责）。
            DXManager.SetOpacity(oldOpacity);
        }


        public override void DrawBehindEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }
        }


        public override void DrawEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }

            if (!effectsEnabled) return;

            switch (CurrentAction)
            {
                case MirAction.Attack1:
                    switch (Spell)
                    {
                        case Spell.攻杀剑术: // 攻杀剑术
                            Libraries.Magic.DrawBlend(1820 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.双斩: // 双斩
                            Libraries.Magic2.DrawBlend(1960 + ((int)Direction * 10) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.刺杀剑术: // 刺杀剑术
                            Libraries.Magic.DrawBlend(2190 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.半月弯刀: // 半月弯刀
                            Libraries.Magic.DrawBlend(2560 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.双烈火剑: // 双烈火剑
                            Libraries.Magic2.DrawBlend(220 + ((int)Direction * 20) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.双半月: // 半月弯刀（双）
                            Libraries.Magic2.DrawBlend(40 + ((int)Direction * 10) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.烈火剑法: // 烈火剑法
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                    }
                    break;
                case MirAction.Attack4:

                    switch (Spell)
                    {
                        case Spell.双斩: // 双斩
                            Libraries.Magic2.DrawBlend(2050 + ((int)Direction * 10) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.双烈火剑: // 双烈火剑
                            Libraries.Magic2.DrawBlend(226 + ((int)Direction * 20) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                        case Spell.烈火剑法: // 烈火剑法
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, CommonColor.White, true, 0.7F);
                            break;
                    }
                    break;
            }


        }


        public void DrawCurrentEffects()
        {
            if (CurrentEffect == SpellEffect.MagicShieldUp && !MagicShield)
            {
                MagicShield = true;
                Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (CurrentEffect == SpellEffect.ElementalBarrierUp && !ElementalBarrier)
            {
                ElementalBarrier = true;
                Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (ElementEffect > 0 && !HasElements)
            {
                HasElements = true;
                if (ElementEffect == 4)
                    Effects.Add(new ElementsEffect(Libraries.Magic3, 1660, 10, 10 * 100, this, true, 4, ElementOrbMax));
                else
                {
                    if (ElementEffect >= 1)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1630, 10, 10 * 100, this, true, 1, ElementOrbMax));
                    if (ElementEffect >= 2)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1640, 10, 10 * 100, this, true, 2, ElementOrbMax));
                    if (ElementEffect == 3)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1650, 10, 10 * 100, this, true, 3, ElementOrbMax));
                }
                ElementEffect = 0;
            }
        }


        public override void DrawBlend()
        {
            DXManager.SetBlend(true, 0.3F);
            Draw();
            DXManager.SetBlend(false);
        }



        private bool IsVitalEffect(Effect effect)
        {
            if ((effect.Library == Libraries.Magic) && (effect.BaseIndex == 3890))
                return true;
            if ((effect.Library == Libraries.Magic3) && (effect.BaseIndex == 1890))
                return true;
            return false;
        }


        public override bool MouseOver(Point p)
        {
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.DefaultTotalDir, (int)Direction);
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.Player, _originModel, action, dir, FrameIndex,
                                                          DrawLocation.X, DrawLocation.Y, out KFramework.MonoGame.Rectangle krect, 1f,
                                                          mirror: (int)Direction >= 5))
                    return krect.Contains(p.X, p.Y);
            }
            return MapControl.MapLocation == CurrentLocation || BodyLibrary != null && BodyLibrary.VisiblePixel(DrawFrame + ArmourOffSet, p.Subtract(FinalDrawLocation), false);
        }


        private void CreateNameLabel()
        {
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


        private void CreateGuildLabel()
        {
            if (string.IsNullOrEmpty(GuildName))
                return;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != GuildName || LabelList[i].ForeColour != NameColour) continue;
                GuildLabel = LabelList[i];
                break;
            }

            if (GuildLabel != null && !GuildLabel.IsDisposed) return;

            GuildLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = CommonColor.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = CommonColor.Black,
                Text = GuildName,
            };
            GuildLabel.Disposing += (o, e) => LabelList.Remove(GuildLabel);
            LabelList.Add(GuildLabel);
        }


        public override void CreateLabel()
        {
            NameLabel = null;
            GuildLabel = null;

            CreateNameLabel();
            CreateGuildLabel();
        }


        public override void DrawName()
        {
            CreateLabel();

            // 名字中心 = 脚底 - NameYOffset（身体中段），行会名在名字上方 16px（对应原始传奇 bloodY-32 vs -48）。
            int nameCenterY = DrawLocation.Y - NameYOffset + (Dead ? 35 : 0);

            if (GuildLabel != null && !string.IsNullOrEmpty(GuildName))
            {
                GuildLabel.Text = GuildName;
                GuildLabel.Location = new Point(DrawLocation.X - GuildLabel.Size.Width / 2, nameCenterY - 16 - GuildLabel.Size.Height / 2);
                GuildLabel.Draw();
            }

            if (NameLabel != null)
            {
                NameLabel.Text = Name;
                NameLabel.Location = new Point(DrawLocation.X - NameLabel.Size.Width / 2, nameCenterY - NameLabel.Size.Height / 2);
                NameLabel.Draw();
            }
        }


    }
}