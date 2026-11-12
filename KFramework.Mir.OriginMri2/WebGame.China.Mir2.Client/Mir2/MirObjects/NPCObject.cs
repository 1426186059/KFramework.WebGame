using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirObjects
{
    public class NPCObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Merchant; }
        }
        public override bool Blocking
        {
            get { return true; }
        }

        public FrameSet Frames;
        public Frame Frame;

        public long QuestTime;
        public int BaseIndex, FrameIndex, FrameInterval,
            EffectFrameIndex, EffectFrameInterval, QuestIndex;

        public ushort Image;
        public Color Colour = Color.White;
        /// <summary>原始页游 NPC 显示缩放（来自 ObjectNPC.Scale，即 NPCInfo.OriginSettings.ScalePercent/100）。</summary>
        public float Scale = 1f;

        /// <summary>非零 = NPCInfo.Image 是原始传奇 model 号，走 Mir2OriginObjectLibrary 的独立图集。</summary>
        private int _originModel;

        /// <summary>经典兜底诊断去重：每个 origin model 只打一次日志，定位“名字在、精灵没有”的根因。</summary>
        private static readonly System.Collections.Generic.Dictionary<int, int> _npcFallbackLogged = new();
        /// <summary>1827/1910 绘制诊断去重：CDN 资源确认存在，记录实际绘制屏幕矩形与脚点，确认是否被推到屏幕外。</summary>
        private static readonly System.Collections.Generic.Dictionary<int, int> _npcDrawDiag = new();

        /// <summary>后台懒预取：把自己当前动作的各方向暖好（低优先级，空闲才下）。</summary>
        public override void PrefetchBackground()
        {
            if (_originModel > 0)
                Mir2OriginObjectLibrary.PreloadAction(Mir2ObjectKind.NPC, _originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), Mir2OriginObjectLibrary.PriorityBackground);
        }

        public QuestIcon QuestIcon = QuestIcon.None;

        private bool _canChangeDir = true;

        public bool CanChangeDir
        {
            get { return _canChangeDir; }
            set
            {
                _canChangeDir = value;
                if (value == false) Direction = 0;
            }
        }

        public List<ClientQuestInfo> Quests;


        public NPCObject(uint objectID) : base(objectID)
        {
        }

        public void Load(S.ObjectNPC info)
        {
            Name = string.IsNullOrEmpty(info.NameUI) ? info.Name : info.NameUI;
            NameColour = info.NameColour;
            CurrentLocation = info.Location;
            Direction = info.Direction;
            Movement = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            Quests = GameScene.QuestInfoList.Where(c => c.NPCIndex == ObjectID).ToList();

            Image = info.Image;
            Scale = info.Scale > 0 ? info.Scale : 1f; // 数据未配置缩放（ScalePercent=0）时回落默认 1；否则按 0 缩放会渲染成 1px 不可见
            Colour = info.Colour;

            // 换成原始传奇 NPC 表后 Image 就是页游 model 号（如 医师=1078）；旧表只有 0~185，
            // 用 1000 作阈值区分，两套表都能安全运行，CDN 资源就绪后自动生效。
            _originModel = info.Image >= 1000 ? (int)info.Image : 0;
            if (_originModel > 0)
                Mir2OriginObjectLibrary.Request(Mir2ObjectKind.NPC, _originModel,
                                                Mir2OriginObjectLibrary.ActionStand,
                                                Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, 0), Mir2OriginObjectLibrary.PriorityVisible);

            LoadLibrary();

            // BodyLibrary 可能为 null：Image 越界，或浏览器端资源库尚未异步加载完成。
            // 直接 BodyLibrary.Frames 会 NullReferenceException（?? 只能兜住 Frames 为 null），
            // 这里与 MonsterObject 保持一致，先判空再回退默认帧集。
            Frames = (BodyLibrary != null ? BodyLibrary.Frames : null) ?? FrameSet.DefaultNPC;

            Light = 10;
            BaseIndex = 0;

            SetAction(true);
        }

        public void LoadLibrary()
        {
            if (Image < Libraries.NPCs.Length)
                BodyLibrary = Libraries.NPCs[Image];
            else if (Image >= 1000 && Image < 1100)
                BodyLibrary = Libraries.Flags[Image - 1000];
        }

        public override void Process()
        {
            bool update = CMain.Time >= NextMotion || GameScene.CanMove;

            ProcessFrames();

            if (update)
            {
                UpdateBestQuestIcon();
            }

            if (Frame == null)
            {
                DrawFrame = 0;
                DrawWingFrame = 0;
            }
            else
            {
                DrawFrame = Frame.Start + (Frame.OffSet * (byte)Direction) + FrameIndex;
                DrawWingFrame = Frame.EffectStart + (Frame.EffectOffSet * (byte)Direction) + EffectFrameIndex;
            }

            DrawY = CurrentLocation.Y;

            DrawLocation = new Point((Movement.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (Movement.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(User.OffSetMove);
            DrawLocation.Offset(GlobalDisplayLocationOffset);

            if (BodyLibrary != null)
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));

            if (BodyLibrary != null && update)
            {
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));
                DisplayRectangle = new Rectangle(DrawLocation, BodyLibrary.GetTrueSize(DrawFrame));
            }

            for (int i = 0; i < Effects.Count; i++)
                Effects[i].Process();

            Color colour = DrawColour;

            switch (Poison)
            {
                case PoisonType.None:
                    DrawColour = Color.White;
                    break;
                case PoisonType.Green:
                    DrawColour = Color.Green;
                    break;
                case PoisonType.Red:
                    DrawColour = Color.Red;
                    break;
                case PoisonType.Bleeding:
                    DrawColour = Color.DarkRed;
                    break;
                case PoisonType.Slow:
                    DrawColour = Color.Purple;
                    break;
                case PoisonType.Stun:
                case PoisonType.Dazed:
                    DrawColour = Color.Yellow;
                    break;
                case PoisonType.Blindness:
                    DrawColour = Color.MediumVioletRed;
                    break;
                case PoisonType.Frozen:
                    DrawColour = Color.Blue;
                    break;
                case PoisonType.Paralysis:
                case PoisonType.LRParalysis:
                    //case PoisonType.FlamingMutantWeb:
                    DrawColour = Color.Gray;
                    break;
            }


            if (colour != DrawColour) GameScene.Scene.MapControl.TextureValid = false;


            if (CMain.Time > QuestTime)
            {
                QuestTime = CMain.Time + 500;
                if (++QuestIndex > 1) QuestIndex = 0;
            }
        }
        public virtual void ProcessFrames()
        {
            if (Frame == null) return;

            switch (CurrentAction)
            {
                case MirAction.Standing:
                case MirAction.Harvest:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    if(EffectFrameInterval > 0)
                        if (CMain.Time >= NextMotion2)
                        {
                            GameScene.Scene.MapControl.TextureValid = false;

                            if (SkipFrames) UpdateFrame2();

                            if (UpdateFrame2() >= Frame.EffectCount)
                                EffectFrameIndex = Frame.EffectCount - 1;
                            else
                                NextMotion2 += EffectFrameInterval;
                        }
                    break;

            }

        }
        public int UpdateFrame()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--FrameIndex);

            return ++FrameIndex;
        }

        public int UpdateFrame2()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--EffectFrameIndex);

            return ++EffectFrameIndex;
        }

        public virtual void SetAction(bool randomStartFrame = false)
        {
            if (ActionFeed.Count == 0)
            {
                // 原始页游 NPC（走 CDN 独立图集）：只播待机，避免随机切到 Harvest→Attack 图集缺失/未预取导致整段消失。
                if (_originModel > 0)
                    CurrentAction = MirAction.Standing;
                else if (CMain.Random.Next(2) == 0 && Frames.Count > 1)
                    CurrentAction = MirAction.Harvest;
                else
                    CurrentAction = MirAction.Standing;

                Frames.TryGetValue(CurrentAction, out Frame);

                if (randomStartFrame)
                {
                    var frameIndex = new Random().Next(Frame.Count);

                    FrameIndex = frameIndex;
                    EffectFrameIndex = Math.Min(Frame.EffectCount, frameIndex);
                }
                else
                {
                    FrameIndex = 0;
                    EffectFrameIndex = 0;
                }

                if (MapLocation != CurrentLocation)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = CurrentLocation;
                    GameScene.Scene.MapControl.AddObject(this);
                }

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该 NPC 模型该动作的 intval（逐模型逐动作每帧毫秒）覆盖帧间隔；
                // 非 origin NPC/缺失动作时回落 Frame.Interval。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;
                EffectFrameInterval = Frame.EffectInterval;
            }
            else
            {
                QueuedAction action = ActionFeed[0];
                ActionFeed.RemoveAt(0);

                CurrentAction = action.Action;
                CurrentLocation = action.Location;

                //if(CanChangeDir)
                //    Direction = action.Direction;

                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该 NPC 模型该动作的 intval（逐模型逐动作每帧毫秒）覆盖帧间隔；
                // 非 origin NPC/缺失动作时回落 Frame.Interval。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;
                EffectFrameInterval = Frame.EffectInterval;
            }

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            GameScene.Scene.MapControl.TextureValid = false;

        }
        public override void Draw()
        {
            // 换表后 Image 多为原始传奇 model 号（1000+），走 CDN 独立图集；但部分 NPC 的 Image 仍是经典 .Lib 索引
            // （如 1827）被误当 origin 号，导致 origin 精灵缺失而不显示。故 origin 缺失时回落 .Lib 经典图集，保证 NPC 始终可见。
            // 2config.modelInfo 里 NPC 待机动作 totaldir=1，方向一律折叠到 NONE(=4)。
            bool drawnByCdn = false;
            int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
            int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, (int)Direction);
            if (_originModel > 0)
            {
                Mir2OriginObjectLibrary.Request(Mir2ObjectKind.NPC, _originModel, action, dir, Mir2OriginObjectLibrary.PriorityVisible);
                drawnByCdn = Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.NPC, _originModel, action, dir,
                                                                   FrameIndex, DrawLocation.X, DrawLocation.Y, DrawColour, scale: Scale);
                // 回退：当前动作帧未就绪时改画已就绪的待机帧，保证 NPC 始终可见（仍走 origin，非 .Lib）。
                if (!drawnByCdn)
                    drawnByCdn = Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.NPC, _originModel,
                                                                      Mir2OriginObjectLibrary.ActionStand, dir, 0,
                                                                      DrawLocation.X, DrawLocation.Y, DrawColour, scale: Scale);

                // 鼠标命中框/血条定位用：必须与“模型实际绘制”用完全相同的参数（同 action / 同帧），否则命中框偏到模型旁。
                // 名字定位已改到 MapObject.DrawName 里相对脚底基准 DrawLocation 固定定位，不依赖此处。
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.NPC, _originModel, action, dir, FrameIndex,
                        DrawLocation.X, DrawLocation.Y, out var nrect, Scale))
                {
                    DisplayRectangle = new Rectangle(nrect.X, nrect.Y, nrect.Width, nrect.Height);
                    DrawRectangleMarker(DisplayRectangle);
                    // 调试：1827/1910 资源已确认在 CDN 上存在（含版本目录与图集），此处记录实际绘制屏幕矩形与脚点，确认是否被推到屏幕外。
                    if ((_originModel == 1827 || _originModel == 1910) && !_npcDrawDiag.ContainsKey(_originModel))
                    {
                        _npcDrawDiag[_originModel] = 1;
                        CMain.SaveError($"[NPC1827诊断] model={_originModel} drawnByCdn={drawnByCdn} nrect=({nrect.X},{nrect.Y},{nrect.Width},{nrect.Height}) DrawLoc=({DrawLocation.X},{DrawLocation.Y}) Scale={Scale}");
                    }
                }
                // 调试：在脚底基准 DrawLocation 画红圈，核对模型是否偏移。
                DrawLocationMarker(DrawLocation);
            }

            // origin 没画出来时回落 .Lib 经典图集，保证 NPC 始终可见。
            // 不再用 IsPending 卡门：origin 请求若挂起（既不 404 也不完成）会永久挡住兜底而整只不显示；
            // 直接“origin 没画就落经典”，origin 真正就绪的那一帧 drawnByCdn 会先置 true 跳过此处（仅加载期可能短暂闪一下）。
            if (!drawnByCdn && BodyLibrary != null)
            {
                if (!_npcFallbackLogged.ContainsKey(_originModel))
                {
                    _npcFallbackLogged[_originModel] = 1;
                    bool missing = Mir2OriginObjectLibrary.IsMissing(Mir2ObjectKind.NPC, _originModel, action, dir)
                                || Mir2OriginObjectLibrary.IsMissing(Mir2ObjectKind.NPC, _originModel, Mir2OriginObjectLibrary.ActionStand, dir);
                    bool pending = Mir2OriginObjectLibrary.IsPending(Mir2ObjectKind.NPC, _originModel, action, dir)
                                || Mir2OriginObjectLibrary.IsPending(Mir2ObjectKind.NPC, _originModel, Mir2OriginObjectLibrary.ActionStand, dir);
                    CMain.SaveError($"[NPC兜底] 名={Name} model={_originModel} Image={Image} NPCsLen={Libraries.NPCs.Length} 落Flags={Image >= Libraries.NPCs.Length} missing={missing} pending={pending} DrawFrame={DrawFrame}");
                }
                BodyLibrary.Draw(DrawFrame, DrawLocation, DrawColour, false);
                drawnByCdn = true;
            }

            if (QuestIcon == QuestIcon.None) return;
            if (BodyLibrary == null) return;

            var offSet = BodyLibrary.GetOffSet(BaseIndex);
            var size = BodyLibrary.GetSize(BaseIndex);

            int imageIndex = 981 + ((int)QuestIcon * 2) + QuestIndex;

            Libraries.Prguse.Draw(imageIndex, DrawLocation.Add(offSet).Add(size.Width / 2 - 28, -40), Color.White, false);
        }

        public override bool MouseOver(Point p)
        {
            // CDN 渲染的 NPC（_originModel>0）：图没（未就绪/缺失）直接不可点击；
            // 有图时用精灵实际绘制矩形精确命中（与 DrawAnchored 同公式，对齐显示像素）。
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, (int)Direction);
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.NPC, _originModel, action, dir, FrameIndex,
                                                          DrawLocation.X, DrawLocation.Y, out KFramework.MonoGame.Rectangle krect, Scale)
                    && p.X >= krect.X && p.X < krect.X + krect.Width
                    && p.Y >= krect.Y && p.Y < krect.Y + krect.Height)
                    return true;
                // origin 未就绪/缺失（回落经典 .Lib）时，用经典图集做命中检测，保证可见即可点击。
                if (BodyLibrary != null)
                    return BodyLibrary.VisiblePixel(DrawFrame, p.Subtract(FinalDrawLocation), false);
                return false;
            }

            if (MapControl.MapLocation == CurrentLocation) return true;
            return BodyLibrary != null && BodyLibrary.VisiblePixel(DrawFrame, p.Subtract(FinalDrawLocation), false);
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
        }

        public override void DrawEffects(bool effectsEnabled)
        {
            if (!effectsEnabled) return;

            if (BodyLibrary == null) return;

            if (DrawWingFrame > 0)
                BodyLibrary.DrawBlend(DrawWingFrame, DrawLocation, Color.White, true);
        }

        public override void DrawName()
        {
            if (!Name.Contains("_"))
            {
                base.DrawName();
                return;
            }

            string[] splitName = Name.Split('_');

            for (int s = 0; s < splitName.Length; s++)
            {
                CreateNPCLabel(splitName[s], s);
                TempLabel.Location = new Point(DisplayRectangle.X + (48 - TempLabel.Size.Width) / 2, DisplayRectangle.Y - (32 - TempLabel.Size.Height / 2) + (Dead ? 35 : 8) - (((splitName.Length - 1) * 10) / 2) + (s * 12));
                TempLabel.Draw();
            }
        }

        public void CreateNPCLabel(string word, int wordOrder)
        {
            TempLabel = null;
            var setForeColour = (wordOrder == 0 ? NameColour : Color.White);
            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != word || LabelList[i].ForeColour != setForeColour) continue;
                TempLabel = LabelList[i];
                break;
            }

            if (TempLabel != null && !TempLabel.IsDisposed) return;

            TempLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = setForeColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = word,
            };

            TempLabel.Disposing += (o, e) => LabelList.Remove(TempLabel);
            LabelList.Add(TempLabel);
        }


        //Quests

        public void UpdateBestQuestIcon()
        {
            ClientQuestProgress quests = GetAvailableQuests(true).FirstOrDefault();
            QuestIcon bestIcon = QuestIcon.None;

            if (quests != null)
            {
                bestIcon = quests.Icon;
            }

            QuestIcon = bestIcon;
        }

        public List<ClientQuestProgress> GetAvailableQuests(bool returnFirst = false)
        {
            List<ClientQuestProgress> quests = new List<ClientQuestProgress>();

            foreach (ClientQuestProgress q in User.CurrentQuests.Where(q => !User.CompletedQuests.Contains(q.QuestInfo.Index)))
            {
                if (q.QuestInfo.FinishNPCIndex == ObjectID)
                {
                    quests.Add(q);
                }
                else if (q.QuestInfo.NPCIndex == ObjectID && q.QuestInfo.SameFinishNPC)
                {
                    quests.Add(q);

                    if (returnFirst) return quests;
                }
            }

            foreach (ClientQuestProgress quest in (
                from q in Quests
                where !quests.Exists(p => p.QuestInfo.Index == q.Index)
                where CanAccept(q)
                where !User.CompletedQuests.Contains(q.Index)
                select q).Select(
                q => User.CurrentQuests.Exists(p => p.QuestInfo.Index == q.Index) ?
                    new ClientQuestProgress { QuestInfo = q, Taken = true, Completed = false } :
                    new ClientQuestProgress { QuestInfo = q }))
            {
                quests.Add(quest);

                if (returnFirst) return quests;
            }

            return quests;
        }

        public bool CanAccept(ClientQuestInfo quest)
        {
            if (quest.MinLevelNeeded > User.Level || quest.MaxLevelNeeded < User.Level)
                return false;

            if (!quest.ClassNeeded.HasFlag(RequiredClass.None))
            {
                switch (User.Class)
                {
                    case MirClass.Warrior:
                        if (!quest.ClassNeeded.HasFlag(RequiredClass.Warrior))
                            return false;
                        break;
                    case MirClass.Wizard:
                        if (!quest.ClassNeeded.HasFlag(RequiredClass.Wizard))
                            return false;
                        break;
                    case MirClass.Taoist:
                        if (!quest.ClassNeeded.HasFlag(RequiredClass.Taoist))
                            return false;
                        break;
                    case MirClass.Assassin:
                        if (!quest.ClassNeeded.HasFlag(RequiredClass.Assassin))
                            return false;
                        break;
                    case MirClass.Archer:
                        if (!quest.ClassNeeded.HasFlag(RequiredClass.Archer))
                            return false;
                        break;
                }
            }

            //check against active quest list
            return quest.QuestNeeded <= 0 || User.CompletedQuests.Contains(quest.QuestNeeded);
        }

    }
}
