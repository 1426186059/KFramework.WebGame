using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirControls
{
    public abstract class MirScene : MirControl
    {
        public static MirScene ActiveScene = new LoginScene();

        private static MouseButtons _buttons;
        private static long _lastClickTime;
        private static MirControl _clickedControl;

        protected MirScene()
        {
            DrawControlTexture = true;
            BackColour = Color.Black;
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
        }

        /// <summary>
        /// UI 类场景（登录/选人）是否在屏幕上水平居中显示（保持按高度缩放、两侧留黑边）；
        /// 游戏主场景为 false（铺满全屏、宽屏拓宽视野）。
        /// </summary>
        protected virtual bool CenterOnScreen => false;

        public override sealed Size Size
        {
            get { return base.Size; }
            set { base.Size = value; }
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

            // 烘焙：失效时把场景及所有子控件合成进 ControlTexture（复用下方 CreateTexture 逻辑）。
            if (!TextureValid)
                CreateTexture();

            if (ControlTexture == null || ControlTexture.Disposed) return;

            // ControlTexture 是包裹离屏 RenderTarget2D 的 SlimDX 纹理；
            // 取出其 RenderTarget2D（KFramework.MonoGame 类型）交给 PresentToScreen 拉伸铺满画布。
            var rt = ControlTexture.RenderTarget;
            if (rt == null) return;
            DXManager.PresentToScreen(rt);
        }

        // 烘焙整帧场景到离屏纹理：必须先切渲染目标到本场景纹理，子控件才会合成进它，
        // 而非直接画到画布；烘焙完恢复渲染目标。
        // 全屏自适应：离屏纹理按画布原生分辨率创建，烘焙时统一按屏幕高度缩放(参考 Unity
        // Scale-With-Screen-Size / Match=Height)，UI 比例正确、不拉伸变形；随后由
        // PresentToScreen 以 1:1 上屏（DXManager 已在 present 前清空 RenderTransform）。
        protected override void CreateTexture()
        {
            var vp = DXManager.GDevice.Viewport;
            int rtW = vp.Width, rtH = vp.Height;

            if (TextureSize.Width != rtW || TextureSize.Height != rtH)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new Texture(DXManager.Device, rtW, rtH, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                TextureSize = new Size(rtW, rtH);
            }
            Surface oldSurface = DXManager.CurrentSurface;
            Surface surface = ControlTexture.GetSurfaceLevel(0);
            DXManager.SetSurface(surface);

            DXManager.Device.Clear(ClearFlags.Target, BackColour, 0, 0);

            // 按屏幕高度统一缩放：s = 画布高 / 逻辑高(768)。宽屏下 UI 比例不变形。
            // CenterOnScreen 场景额外水平居中（先缩放后平移），两侧由 BackColour 补齐。
            float s = (float)rtH / Settings.ScreenHeight;
            float offsetX = 0f;
            if (CenterOnScreen)
                offsetX = (rtW - Settings.ScreenWidth * s) / 2f;
            DXManager.RenderOffsetX = offsetX;
            DXManager.RenderTransform = KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(s, s, offsetX, 0f);
            try
            {
                BeforeDrawControl();
                DrawChildControls();
                AfterDrawControl();
            }
            finally
            {
                DXManager.RenderTransform = null;
            }

            DXManager.Sprite.Flush();

            DXManager.SetSurface(oldSurface);
            TextureValid = true;
            surface.Dispose();
        }

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
            TextureValid = false;
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