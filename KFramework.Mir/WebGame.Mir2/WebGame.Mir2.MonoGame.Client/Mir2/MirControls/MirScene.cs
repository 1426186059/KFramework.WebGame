using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;
using WebGame.Mir2.MonoGame.Client.Mir2._2026New;
using S = ServerPackets;

namespace Client.MirControls
{
    public abstract class MirScene : MirControl
    {
        public static MirScene ActiveScene = new LoginScene();

        private static MouseButtons _buttons;
        private static long _lastClickTime;
        private static MirControl _clickedControl;

        // 两层结构：世界层(地图/NPC/怪物/玩家) 与 UI 层(对话框/HUD)。
        // 场景的直接子控件经 AddControl/InsertControl 路由到对应容器；渲染时分别烘焙、
        // 各自用自己的相机变换，先上世界层再上 UI 层叠加（UI 层透明背景，不遮挡世界）。
        protected readonly LayerControl WorldLayer = new LayerControl { BackColour = Color.Black, DrawControlTexture = false };
        protected readonly LayerControl UILayer = new LayerControl { BackColour = Color.Transparent, DrawControlTexture = false };

        protected MirScene()
        {
            DrawControlTexture = true;
            BackColour = Color.Black;
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            // 世界层：均匀 aspect-fill 铺满（无偏移、不变形）；跟随玩家/居中由 KCamera 负责，此处先居中。
            WorldLayer.LayerTransform = (w, h) =>
            {
                float zoom = Math.Max((float)w / Settings.ScreenWidth, (float)h / Settings.ScreenHeight);
                float offX = (w - Settings.ScreenWidth * zoom) / 2f;
                float offY = (h - Settings.ScreenHeight * zoom) / 2f;
                return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(zoom, zoom, offX, offY);
            };
            // UI 层：按高度统一缩放并 pinned（逻辑 = KSetting.UIReferenceHeight），即"UI 映射到相机空间"后的屏幕固定坐标。
            // UI 参考分辨率集中配置在 KSetting，不再依赖 Settings.ScreenWidth/Height（那只是地图逻辑尺寸）。
            UILayer.LayerTransform = (w, h) =>
            {
                float s = (float)h / KSetting.UIReferenceHeight;
                return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(s, s, 0, 0f);
            };

            WorldLayer.Parent = this;
            UILayer.Parent = this;
        }

        public override sealed Size Size
        {
            get { return base.Size; }
            set { base.Size = value; }
        }

        // 统一路由：UI 控件进 UILayer，地图(MapControl)进 WorldLayer，层容器自身直接挂到场景。
        protected override void AddControl(MirControl control)
        {
            if (control == WorldLayer || control == UILayer)
                base.AddControl(control);
            else if (control is MapControl)
                WorldLayer.Add(control);
            else
                UILayer.Add(control);
        }

        public override void InsertControl(int index, MirControl control)
        {
            if (control == WorldLayer || control == UILayer)
                base.InsertControl(index, control);
            else if (control is MapControl)
                WorldLayer.Insert(index, control);
            else
                UILayer.Insert(index, control);
        }

        public override void Draw()
        {
            if (IsDisposed || !Visible)
                return;

            OnBeforeShown();

            DrawControl();

            if (CMain.DebugBaseLabel != null && !CMain.DebugBaseLabel.IsDisposed)
                CMain.DebugBaseLabel.Draw();

            if (CMain.HintBaseLabel != null && !CMain.HintBaseLabel.IsDisposed)
                CMain.HintBaseLabel.Draw();

            OnShown();
        }

        /// <summary>
        /// 浏览器端全屏呈现：覆盖基类 1:1 贴图，改为先把整帧场景（含子控件）烘焙进离屏
        /// 纹理(ControlTexture)，再把它拉伸铺满画布(Viewport)，使固定逻辑分辨率的 UI 等比
        /// 铺满窗口，消除"只显示在左上角 + 四周洋红底色"问题。
        /// </summary>
        protected internal override void DrawControl()
        {
            if (!DrawControlTexture) return;

            // 分层烘焙：世界层先，UI 层后（UI 透明叠加在世界之上）。
            WorldLayer.Bake();
            UILayer.Bake();

            // 两层渲染目标均为视口分辨率、1:1 上屏；先世界后 UI 叠加。
            var worldRT = WorldLayer.RenderTargetTexture;
            if (worldRT != null) DXManager.PresentToScreen(worldRT);

            var uiRT = UILayer.RenderTargetTexture;
            if (uiRT != null) DXManager.PresentToScreen(uiRT);
        }

        // 烘焙职责已下放到 WorldLayer / UILayer（见下方 LayerControl.CreateTexture），
        // 由 DrawControl 分别烘焙后叠加上屏，故场景根不再单独烘焙。

        public override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (MouseControl != null && MouseControl != this)
                MouseControl.OnMouseDown(e);
            else
                base.OnMouseDown(e);
        }
        public override void OnMouseUp(MouseEventArgs e)
        {
            if (!Enabled)
                return;
            if (MouseControl != null && MouseControl != this)
                MouseControl.OnMouseUp(e);
            else
                base.OnMouseUp(e);
        }
        public override void OnMouseMove(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (MouseControl != null && MouseControl != this && MouseControl.Moving)
                MouseControl.OnMouseMove(e);
            else
                base.OnMouseMove(e);
        }
        public override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (MouseControl != null && MouseControl != this)
                MouseControl.OnMouseWheel(e);
            else
                base.OnMouseWheel(e);
        }

        public override void OnMouseClick(MouseEventArgs e)
        {
            if (!Enabled)
                return;
            if (_buttons == e.Button)
            {
                if (_lastClickTime + SystemInformation.DoubleClickTime >= CMain.Time)
                {
                    OnMouseDoubleClick(e);
                    return;
                }
            }
            else
                _lastClickTime = 0;

            // 浏览器端为手动派发：OnMouseUp 中的 Deactivate() 已把 ActiveControl 置空，
            // 故此处改用 MouseControl（鼠标按下时所在控件，松开前不会被清空）来派发 Click，
            // 否则按钮 Click 永远收不到（表现为所有按钮点击无反应）。
            if (MouseControl != null && MouseControl != this && MouseControl.IsMouseOver(CMain.MPoint))
                MouseControl.OnMouseClick(e);
            else
                base.OnMouseClick(e);

            _clickedControl = MouseControl;

            _lastClickTime = CMain.Time;
            _buttons = e.Button;
        }

        public override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (!Enabled)
                return;
            _lastClickTime = 0;
            _buttons = MouseButtons.None;

            if (MouseControl != null && MouseControl != this && MouseControl.IsMouseOver(CMain.MPoint))
            {
                if (MouseControl == _clickedControl)
                    MouseControl.OnMouseDoubleClick(e);
                else
                    MouseControl.OnMouseClick(e);
            }
            else
            {
                if (MouseControl == _clickedControl)
                    base.OnMouseDoubleClick(e);
                else
                    base.OnMouseClick(e);
            }
        }

        public override void Redraw()
        {
            WorldLayer.Invalidate();
            UILayer.Invalidate();
        }
        
        public virtual void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.Disconnect: // Disconnected
                    Disconnect((S.Disconnect) p);
                    Network.Disconnect();
                    break;
                case (short)ServerPacketIds.NewItemInfo:
                    NewItemInfo((S.NewItemInfo) p);
                    break;
                case (short)ServerPacketIds.NewMonsterInfo:
                    NewMonsterInfo((S.NewMonsterInfo)p);
                    break;
                case (short)ServerPacketIds.NewNPCInfo:
                    NewNPCInfo((S.NewNPCInfo)p);
                    break;
                case (short)ServerPacketIds.NewChatItem:
                    NewChatItem((S.NewChatItem)p);
                    break;
                case (short)ServerPacketIds.NewQuestInfo:
                    NewQuestInfo((S.NewQuestInfo)p);
                    break;
                case (short)ServerPacketIds.NewRecipeInfo:
                    NewRecipeInfo((S.NewRecipeInfo)p);
                    break;
                case (short)ServerPacketIds.NewHeroInfo:
                    NewHeroInfo((S.NewHeroInfo)p);
                    break;
            }
        }

        private void NewItemInfo(S.NewItemInfo info)
        {
            GameScene.ItemInfoList.Add(info.Info);
            GameScene.OnItemInfoReceived(info.Info.Index);
        }

        private void NewMonsterInfo(S.NewMonsterInfo info)
        {
            GameScene.MonsterInfoList.RemoveAll(x => x.Index == info.Info.Index);
            GameScene.MonsterInfoList.Add(info.Info);
            GameScene.OnMonsterInfoReceived(info.Info.Index);
        }

        private void NewNPCInfo(S.NewNPCInfo info)
        {
            GameScene.NPCInfoList.RemoveAll(x => x.Index == info.Info.Index);
            GameScene.NPCInfoList.Add(info.Info);
            GameScene.OnNPCInfoReceived(info.Info.Index);
        }

        private void NewHeroInfo(S.NewHeroInfo info)
        {
            AddHeroInformation(info.Info, info.StorageIndex);
        }

        public void AddHeroInformation(ClientHeroInformation info, int storageIndex = -1)
        {
            if (info == null) return;
            GameScene.HeroInfoList.RemoveAll(x => x.Index == info.Index);
            GameScene.HeroInfoList.Add(info);

            if (storageIndex < 0) return;
            GameScene.HeroStorage[storageIndex] = info;
        }

        private void NewChatItem(S.NewChatItem p)
        {
            if (GameScene.ChatItemList.Any(x => x.UniqueID == p.Item.UniqueID)) return;

            GameScene.Bind(p.Item);
            GameScene.ChatItemList.Add(p.Item);
        }

        private void NewQuestInfo(S.NewQuestInfo info)
        {
            GameScene.QuestInfoList.Add(info.Info);
        }

        private void NewRecipeInfo(S.NewRecipeInfo info)
        {
            GameScene.RecipeInfoList.Add(info.Info);

            GameScene.Bind(info.Info.Item);

            for (int j = 0; j < info.Info.Tools.Count; j++)
                GameScene.Bind(info.Info.Tools[j]);

            for (int j = 0; j < info.Info.Ingredients.Count; j++)
                GameScene.Bind(info.Info.Ingredients[j]);
        }

        private static void Disconnect(S.Disconnect p)
        {
            switch (p.Reason)
            {
                case 0:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ShuttingDown), true);
                    break;
                case 1:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisconnectedAnotherUserLogged), true);
                    break;
                case 2:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisconnectedPacketError), true);
                    break;
                case 3:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisconnectedServerCrashed), true);
                    break;
                case 4:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisconnectedKickedByAdmin), true);
                    break;
                case 5:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisconnectedMaxConnectionsReached), true);
                    break;
            }

            GameScene.LogTime = 0;
        }

        public abstract void Process();

        #region Disposable

        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);

            if (!disposing) return;

            if (ActiveScene == this) ActiveScene = null;

            _buttons = 0;
            _lastClickTime = 0;
            _clickedControl = null;
        }

        #endregion
    }
}