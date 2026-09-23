using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
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

        // 两层结构：世界层(地图/NPC/怪物/玩家) 与 UI 层(对话框/HUD)。
        // 场景的直接子控件经 AddControl/InsertControl 路由到对应容器；渲染时分别烘焙、
        // 各自用自己的相机变换，先上世界层再上 UI 层叠加（UI 层透明背景，不遮挡世界）。
        protected readonly WorldLayerControl WorldLayer = new WorldLayerControl { BackColour = Color.Black, DrawControlTexture = false };
        protected readonly UILayerControl UILayer = new UILayerControl { BackColour = Color.Transparent, DrawControlTexture = false };

        protected MirScene()
        {
            DrawControlTexture = true;
            BackColour = Color.Black;
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            // 世界层 = 相机投影（世界坐标 → 屏幕）。
            // MapControl 按【全屏/窗口原生分辨率】烘焙世界（地板 1:1 铺满、无黑边、无放大），
            // 世界层只做 1:1 透传（不缩放、不 aspect-fill）——世界坐标本就不该被放大。
            // 窗口更大只是“看到更多世界”（以 48x32 世界像素为单位的视口更大），而非放大世界。
            // 命中：MapControl 内鼠标即世界像素（与屏幕 1:1），WorldLayer 恒为单位变换。
            // 注意：KCamera.ScreenToWorldPos 的 s=h/768 只服务于【UI 层逻辑坐标】，与世界层无关。
            // 两层各自的 世界→屏幕 变换见 WorldLayerControl / UILayerControl（覆写 GetLayerTransform）。

            WorldLayer.Parent = this;
            UILayer.Parent = this;
        }

        public override sealed Size Size
        {
            get { return base.Size; }
            set { base.Size = value; }
        }

        // 统一路由：UI 控件进 UILayer，地图(MapControl)进 WorldLayer，层容器自身直接挂到场景。
        // 必须用 Insert(末尾) 而不是 Add：MirControl.AddControl 只把控件塞进层容器的 Controls，
        // 并不改 control._parent，于是 Parent 仍指向场景根；而置顶/排序逻辑
        // （TrySort / OnVisibleChanged / BringToFront）写的都是
        // "Parent.Controls.Remove(this); Parent.Controls.Add(this)"，
        // Parent 与真实收录容器一旦不一致，控件就被加回场景根、掉出 UILayer：
        // 层烘焙不到它（窗口画不出、点不动），并触发 [Mir][UIHost] 归属断言。
        protected override void AddControl(MirControl control)
        {
            if (control == WorldLayer || control == UILayer)
                base.AddControl(control);
            else if (control is MapControl)
                WorldLayer.Insert(WorldLayer.Controls.Count, control);
            else
                UILayer.Insert(UILayer.Controls.Count, control);
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
            WorldLayer.Invalidate();
            UILayer.Invalidate();
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
        /// 对应原版 Crystal 的做法：原版窗口不可自由拉伸（FormBorderStyle.FixedDialog），
        /// 分辨率只在场景切换时改，且改完立即 `ActiveScene = new GameScene(); Dispose();`
        /// 重建场景，由构造代码按新尺寸重新布局。浏览器端不能重建 GameScene（会掉线），
        /// 故改为就地重算位置，效果等价。
        /// </summary>
        public virtual void ApplyAnchors()
        {
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            // UI 层整棵子树（含嵌套对话框，例如登录框嵌在 _background 内）。
            // 虽然子控件的 DisplayLocation 会随父累加，但"居中/贴边"这类布局是【相对屏幕】算出来的，
            // 父动了不代表子仍是居中，故需逐个调用；未设锚点的控件不会产生任何变化。
            ApplyAnchorTree(UILayer);

            // 直接挂在场景根上、未走 AddControl 路由的控件（如调试标签）。
            if (Controls != null)
                for (int i = 0; i < Controls.Count; i++)
                {
                    var c = Controls[i];
                    if (c == null || c == WorldLayer || c == UILayer) continue;
                    ApplyAnchorTree(c);
                }

            // 位置已变，两层离屏纹理需重新烘焙。
            WorldLayer.Invalidate();
            UILayer.Invalidate();
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