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

        /// <summary>非零 = NPCInfo.Image 是原始传奇 model 号，走 Mir2OriginObjectLibrary 的独立图集。</summary>
        private int _originModel;

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
            Colour = info.Colour;

            // 换成原始传奇 NPC 表后 Image 就是页游 model 号（如 医师=1078）；旧表只有 0~185，
            // 用 1000 作阈值区分，两套表都能安全运行，CDN 资源就绪后自动生效。
            _originModel = info.Image >= 1000 ? (int)info.Image : 0;
            if (_originModel > 0)
                Mir2OriginObjectLibrary.Request(Mir2ObjectKind.NPC, _originModel,
                                                Mir2OriginObjectLibrary.ActionStand,
                                                Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, 0));

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
                if (CMain.Random.Next(2) == 0 && Frames.Count > 1)
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
                EffectFrameInterval = Frame.EffectInterval;
            }

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            GameScene.Scene.MapControl.TextureValid = false;

        }
        public override void Draw()
        {
            // 换表后 Image 是原始传奇 model 号（1000+），与 .Lib 图集无关，LoadLibrary 常常返回 null。
            // 因此 CDN 分支必须先于 BodyLibrary 判空执行，否则 NPC 会整个不画。
            // 2config.modelInfo 里 NPC 待机动作 totaldir=1，方向一律折叠到 NONE(=4)。
            bool drawnByCdn = false;
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, (int)Direction);
                Mir2OriginObjectLibrary.Request(Mir2ObjectKind.NPC, _originModel, action, dir);
                drawnByCdn = Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.NPC, _originModel, action, dir,
                                                                   FrameIndex, DrawLocation.X, DrawLocation.Y, DrawColour);
            }

            if (!drawnByCdn)
            {
                if (BodyLibrary == null) return;
                BodyLibrary.DrawTinted(DrawFrame, DrawLocation, DrawColour, Colour, true);
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
            if (MapControl.MapLocation == CurrentLocation) return true;

            // CDN 渲染的 NPC（_originModel>0）没有 BodyLibrary 像素数据，VisiblePixel 恒失败。
            // 改用 CDN 精灵的实际绘制矩形命中（与 DrawAnchored 同公式，精确对齐显示像素）；
            // 资源未就绪时退化为格子包围盒兜底，保证至少能点中（避免永久点不到）。
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.NPCTotalDir, (int)Direction);
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.NPC, _originModel, action, dir, FrameIndex,
                                                          DrawLocation.X, DrawLocation.Y, out KFramework.MonoGame.Rectangle krect)
                    && p.X >= krect.X && p.X < krect.X + krect.Width
                    && p.Y >= krect.Y && p.Y < krect.Y + krect.Height)
                    return true;

                int cw = MapControl.CellWidth, ch = MapControl.CellHeight;
                return p.X >= DrawLocation.X - cw && p.X < DrawLocation.X + cw * 2
                    && p.Y >= DrawLocation.Y - ch * 3 && p.Y < DrawLocation.Y + ch;
            }

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
