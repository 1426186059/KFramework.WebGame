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
            // 场景 RT 底色用【透明洋红】(alpha=0)：本端口 blit 走 alpha 合成（非原版洋红 color key），
            // 洋红区即透明，上屏时露出下层地图——与 Crystal「洋红=透明键」等价。GameScene 拆两层后，
            // UI 区域外透出地图；登陆/选人场景会在各自构造里覆盖为黑底。
            BackColour = Color.FromArgb(0, 255, 0, 255);
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
        }

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

            if (CMain.DebugDialog != null && !CMain.DebugDialog.IsDisposed)
                CMain.DebugDialog.Draw();

            if (CMain.HintBaseLabel != null && !CMain.HintBaseLabel.IsDisposed)
                CMain.HintBaseLabel.Draw();

            OnShown();
        }

        /// <summary>
        /// 场景(UI) RT：只装 UI，不装地图。底色用【透明洋红】(alpha=0)，上屏时洋红区即透明，
        /// 露出下方已先画好的地图 RT（见 GameScene.DrawControl：先 MapControl.DrawControl 再 base.DrawControl）——与
        /// 原版 Crystal 的「地图 RT + UI RT 两层、洋红=透明键」拓扑一致。UI 只在变动时重烤
        /// （Redraw 置 TextureValid=false），不随地图动画每帧重烤。登陆/选人等场景在各自构造里把底色覆盖为黑。
        /// </summary>
        protected override void CreateTexture()
        {
            if (Size != TextureSize)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new Texture(DXManager.Device, Size.Width, Size.Height, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                TextureSize = Size;
            }
            Surface oldSurface = DXManager.CurrentSurface;
            Surface surface = ControlTexture.GetSurfaceLevel(0);
            DXManager.SetSurface(surface);

            DXManager.Device.Clear(ClearFlags.Target, BackColour, 0, 0);

            BeforeDrawControl();
            DrawChildControls();
            AfterDrawControl();

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
            {
                MouseControl.OnMouseMove(e);
            }
            else
                base.OnMouseMove(e);
        }
        public override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            // 沿父链冒泡：从鼠标所在控件向上，直到某个控件消费（Handled）或到达本场景。
            // 这样滚轮悬停在滚动视图的子控件（行/输入框）上时，事件能冒泡到 MirScrollView 并滚动内容。
            // 冒泡到"本场景(this)"这一层时不再调虚方法自身（否则会重读静态 MouseControl 形成无限递归），
            // 改为循环结束后调一次 base（仅触发场景自己的 MouseWheel 事件）。
            var c = MouseControl;
            while (c != null && !ReferenceEquals(c, this))
            {
                c.OnMouseWheel(e);
                if (e.Handled)
                    return;
                c = c.Parent;
            }
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
            if (this is LoginScene)
                KFramework.MonoGame.PrintTool.LogError($"[Mir][Click] MC={MouseControl?.GetType().Name} over={MouseControl?.IsMouseOver(CMain.MPoint)} MP=({CMain.MPoint.X},{CMain.MPoint.Y}) loc={MouseControl?.DisplayLocation}");

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

        /// <summary>
        /// 画布(窗口)尺寸变化后按锚点重算 UI 位置：遍历 UI 层整棵子树调用 ApplyAnchor()。
        ///
        /// 从 UI 层往下递归整棵子树，而非只处理顶层：子控件的 DisplayLocation 虽随父累加
        /// （见 DisplayLocation），但"居中/贴边"是【相对屏幕】算出来的，父动了不代表子仍是居中。
        ///
        /// 控件自己决定动不动：默认 Anchor 为 None（绝对定位的控件本就无需变动），
        /// 只有位置依赖 Settings.ScreenWidth/Height 的控件才设 Anchor，通常一两行即可 ——
        /// 基准位置复用 MirControl 已有的九宫格定位属性（见 EAnchorType / #region Positions）。
        ///
        /// 对应传统桌面端的做法：其窗口不可自由拉伸（FormBorderStyle.FixedDialog），
        /// 分辨率只在场景切换时改，且改完立即 `ActiveScene = new GameScene(); Dispose();`
        /// 重建场景，由构造代码按新尺寸重新布局。浏览器端不能重建 GameScene（会掉线），
        /// 故改为就地重算位置，效果等价。
        /// </summary>
        public virtual void ApplyAnchors()
        {
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            // 整棵控件树（地图 + UI）按锚点重算位置；未设锚点的控件不产生任何变化。
            ApplyAnchorTree(this);

            // 位置已变，场景离屏纹理需重新烘焙。
            Redraw();
        }

        // 世界层不参与重算：MapControl 每帧用 FullScreenSize 调 UpdateViewPort，
        // 自行同步 Size 与可视范围（见 GameScene.MapControl.UpdateViewPort）。
        private static void ApplyAnchorTree(MirControl control)
        {
            if (control == null) return;

            control.ApplyAnchor();

            if (control.Controls == null) return;
            for (int i = 0; i < control.Controls.Count; i++)
                ApplyAnchorTree(control.Controls[i]);
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


        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);

            if (!disposing) return;

            if (ActiveScene == this) ActiveScene = null;

            _buttons = 0;
            _lastClickTime = 0;
            _clickedControl = null;
        }

    }
}