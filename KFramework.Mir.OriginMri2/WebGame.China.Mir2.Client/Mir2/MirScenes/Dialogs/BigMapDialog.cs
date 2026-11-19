using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class BigMapDialog : MirImageControl
    {
        private const string MapSheet = "y_mappanel-sheet";
        private const int SkinW = 818, SkinH = 536;
        private const int MapAreaX = 156, MapAreaY = 53, MapAreaW = 604, MapAreaH = 442;

        MirLabel CoordinateLabel, TitleLabel;
        public MirButton CloseButton, MyLocationButton;
        private readonly MirButton[] _actionButtons = new MirButton[4];

        public int TargetMapIndex;

        public BigMapViewPort ViewPort;

        private Point mouseLocation = Point.Zero;
        public Point MouseLocation
        {
            get { return mouseLocation; }
            set
            {
                if (mouseLocation == value) return;

                mouseLocation = value;
                MakeCoordinateLabel();
            }
        }

        private BigMapRecord currentRecord;
        public BigMapRecord CurrentRecord
        {
            get { return currentRecord; }
            set
            {
                if (currentRecord == value) return;
                SetButtonsVisibility(false);
                currentRecord = value;
                ViewPort.UserRadarDot.Visible = currentRecord != null && currentRecord.Index == GameScene.Scene.MapControl.Index;
                SetButtonsVisibility(true);
            }
        }

        public BigMapDialog()
        {
            AutoSize = false;
            Size = new Size(SkinW, SkinH);
            Location = Center;
            NotControl = false;
            DrawImage = false;
            Sort = true;

            // 背景：原始传奇 common_bg.png（gameui4/jpg，走 manifest 哈希布局解析）
            var bg = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/common_bg.png",
                Location = new Point(0, 0),
                NotControl = true,
                Size = new Size(790, 536),
            };

            // 标题 "地图"（mapSkin _Group1 14,13 / txt_title x=20 style233）
            TitleLabel = new MirLabel
            {
                ForeColour = Color.FromArgb(255, 240, 200, 120),
                Parent = this,
                AutoSize = false,
                Size = new Size(208, 37),
                NotControl = true,
                Location = new Point(34, 13),
                Font = new Font(Settings.FontName, 14F, KFramework.MonoGame.FontStyle.Bold),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Text = "地图",
                OutLine = true
            };

            var nameBar = new MirImageControl
            {
                Parent = this,
                AtlasNameKey = MapSheet,
                AtlasFrame = "y_map_bg1",
                Location = new Point(MapAreaX + MapAreaW / 2 - 60, MapAreaY - 3),
                Size = new Size(120, 30),
                NotControl = true,
            };

            AddLegend(30 + 35, 53 + 10, "y_map_icon0", "死亡掉装");
            AddLegend(30 + 35, 53 + 80, "y_map_icon1", "击杀红名");

            string[] btnLabels = { "行会召集", "随机", "回城", "队伍召集" };
            int[] btnY = { 354, 265, 309, 398 };
            for (int i = 0; i < _actionButtons.Length; i++)
            {
                int k = i;
                var b = new MirButton
                {
                    Parent = this,
                    Location = new Point(30 + 3, 53 + btnY[i]),
                    Size = new Size(115, 36),
                    AtlasNameKey = "y_common-sheet",
                    AtlasFrame = "com_z_btn_1",
                    HoverAtlasFrame = "com_z_btn_1_1",
                    PressedAtlasFrame = "com_z_btn_1_1",
                    Sound = SoundList.ButtonA,
                };
                b.Label.Text = btnLabels[i];
                b.Label.ForeColour = Color.FromArgb(255, 245, 220, 160);
                b.CenterText = true;
                b.Click += (o, e) => OnActionButton(k);
                _actionButtons[i] = b;
            }

            // 关闭按钮（mapSkin btn_close 768,2，50×50，com_btn_close0/1）
            CloseButton = new MirButton
            {
                Parent = this,
                Location = new Point(780, 10),
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                HoverAtlasFrame = "com_btn_close1",
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 地图视口（保留经典地图渲染/标记/寻路），放到 mapSkin 的地图区
            ViewPort = new BigMapViewPort()
            {
                Parent = this,
                Location = new Point(MapAreaX, MapAreaY),
            };

            // 坐标显示（页游 grp_endP 风格：y_map_bg0 底 + 文本）
            CoordinateLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                OutLine = true,
                Location = new Point(MapAreaX + 8, MapAreaY + MapAreaH - 22),
                Parent = this,
                NotControl = true,
            };
        }

        private void AddLegend(int x, int y, string frame, string text)
        {
            var icon = new MirImageControl
            {
                Parent = this,
                AtlasNameKey = MapSheet,
                AtlasFrame = frame,
                Location = new Point(x, y),
                Size = new Size(28, 28),
                NotControl = true,
            };
            var label = new MirLabel
            {
                Parent = this,
                Text = text,
                AutoSize = true,
                ForeColour = Color.Red,
                Location = new Point(x - 10, y + 30),
                NotControl = true,
            };
        }

        // 原始传奇 4 按钮：本服务端无"行会召集/随机/队伍召集"道具传送，仅"回城"接现有回城逻辑；
        // 其余保留外观，点击给提示，不做无效传送。
        private void OnActionButton(int index)
        {
            switch (index)
            {
                case 2: // 回城
                    if (MapObject.User != null)
                        Network.Enqueue(new C.TownRevive());
                    break;
                default:
                    GameScene.Scene.ChatDialog.ReceiveChat("该功能暂未开放", ChatType.System);
                    break;
            }
        }

        private void MakeCoordinateLabel()
        {
            CoordinateLabel.Text = $"[ {MouseLocation.X}, {MouseLocation.Y} ]";
        }

        public void ShowCoordinateLabel()
        {
            CoordinateLabel.Visible = true;
        }

        public void HideCoordinateLabel()
        {
            CoordinateLabel.Visible = false;
        }

        public void Toggle()
        {
            if (Visible)
                Hide();
            else
                Show();
        }

        public override void Show()
        {
            var map = GameScene.Scene.MapControl;
            // PC 地图 BigMap 多为 0，回退到瓦片 img（小地图已验证可用）也能开大地图窗口
            if (map.BigMap <= 0 && map.MapImg <= 0) return;

            // 打开时置于最前，避免被屏幕右上角常驻的对话框（如 BuffsDialog）挡住关闭按钮的点击
            BringToFront();
            base.Show();
            TargetMyLocation();
        }

        private void TargetMyLocation()
        {
            var map = GameScene.Scene.MapControl;
            if (map == null) return;
            SetTargetMap(map.Index);
        }

        public void SetTargetMap(int MapIndex)
        {
            // 禁止通过大地图跳转到其它地图：只允许查看当前所在地图。
            if (MapIndex != GameScene.Scene.MapControl.Index) return;

            TargetMapIndex = MapIndex;
            CurrentRecord = null;
            if (!GameScene.MapInfoList.ContainsKey(MapIndex))
                Network.Enqueue(new C.RequestMapInfo() { MapIndex = MapIndex });
            else
                CurrentRecord = GameScene.MapInfoList[MapIndex];
            Redraw();
        }

        private void SetButtonsVisibility(bool visible)
        {
            if (currentRecord == null) return;
            SetMovementButtonsVisibility(visible);
        }

        private void SetMovementButtonsVisibility(bool visible)
        {
            foreach (var button in currentRecord.MovementButtons.Values)
                button.Visible = visible;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CoordinateLabel = null;
                CloseButton = null;
                MyLocationButton = null;
                TitleLabel = null;
            }

            base.Dispose(disposing);
        }

    }

    public sealed class BigMapViewPort : MirControl
    {
        BigMapDialog ParentDialog;
        float ScaleX;
        float ScaleY;
        public MirImageControl UserRadarDot;

        int BigMap_MouseCoordsProcessing_OffsetX, BigMap_MouseCoordsProcessing_OffsetY;

        public MirImageControl[] Players;
        public static Dictionary<string, Point> PlayerLocations = new Dictionary<string, Point>();

        // 大地图远程整图（替代 Libraries.MiniMap 本地库）：按地图 BigMap id 从 CDN 取，缓存到纹理。
        private KFramework.MonoGame.Texture2D _bigMapTexture;
        private int _bigMapLoadedIdx = -1;

        // 大地图整图：原始传奇中与小地图共用 assets/resource/minimap/{img}.jpg（CDN 经核查无 bigmap/ 目录），
        // 故直接取 minimap 整图；img 为 MapImg（当前地图）或 BigMap（其它地图）。
        private async void LoadBigMapTexture(int img)
        {
            try
            {
                var tex = await Mir2OriginResource.GetMinimapTextureAsync(img);
                if (_bigMapLoadedIdx == img) _bigMapTexture = tex;
            }
            catch
            {
                // 该地图无 minimap 远程资源时保持 null
            }
        }

        public BigMapViewPort()
        {
            NotControl = false;
            // Sort=false：防止点地图时基类 OnMouseDown 调 TrySort() 把这个大面积视口置顶，
            // 从而盖住关闭按钮导致“点过地图后关不掉”。置顶逻辑仍对对话框本身生效（保持大地图在最前）。
            Sort = false;
            Size = new Size(604, 442);

            // 原始传奇地图点：自己 y_map_p_1 / 队友 y_map_p_0，均出自 PC Web 的 y_homeUI-sheet（9×9）
            UserRadarDot = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet#y_map_p_1",
                Parent = this,
                Visible = false,
                NotControl = true
            };

            Players = new MirImageControl[Globals.MaxGroup];
            for (int i = 0; i < Players.Length; i++)
            {
                Players[i] = new MirImageControl
                {
                    AtlasNameKey = "y_homeUI-sheet#y_map_p_0",
                    Parent = this,
                    NotControl = false,
                    Visible = false,
                };
            }

            BeforeDraw += (o, e) => OnBeforeDraw();
            MouseMove += UpdateBigMapCoordinates;
            ParentChanged += (o, e) => SetParent();
            MouseDown += OnMouseClick;
        }

        private void SetParent()
        {
            ParentDialog = (BigMapDialog)Parent;
        }

        private void UpdateBigMapCoordinates(object sender, MouseEventArgs e)
        {
            int MouseCoordsOnBigMap_MapValue_X = (int)((e.Location.X - BigMap_MouseCoordsProcessing_OffsetX) / ScaleX);
            int MouseCoordsOnBigMap_MapValue_Y = (int)((e.Location.Y - BigMap_MouseCoordsProcessing_OffsetY) / ScaleY);

            ParentDialog.MouseLocation = new Point(MouseCoordsOnBigMap_MapValue_X, MouseCoordsOnBigMap_MapValue_Y);
        }


        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (ScaleX <= 0 || ScaleY <= 0) return;

            int X = (int)((e.Location.X - BigMap_MouseCoordsProcessing_OffsetX) / ScaleX);
            int Y = (int)((e.Location.Y - BigMap_MouseCoordsProcessing_OffsetY) / ScaleY);

            var map = GameScene.Scene.MapControl;

            // 越界或目标格不可走：直接提示并 return，避免对不可达点做全图 A* 寻路导致卡顿
            // （FindPath 的 MaxNodes 默认 0 仅限制“找到后”的路径长度，对不可达点仍会搜索整张地图）。
            if (X < 0 || Y < 0 || X >= map.Width || Y >= map.Height || !map.EmptyCell(new Point(X, Y)))
            {
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.CouldNotFindPath), ChatType.System);
                return;
            }

            var path = map.PathFinder.FindPath(MapObject.User.CurrentLocation, new Point(X, Y));

            if (path == null || path.Count == 0)
            {
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.CouldNotFindPath), ChatType.System);
            }
            else
            {
                map.CurrentPath = path;
                map.AutoPath = true;
            }
        }


        private void OnBeforeDraw()
        {
            if (!Parent.Visible) return;

            if (ParentDialog == null) ParentDialog = Parent as BigMapDialog;
            if (ParentDialog == null) return;

            MouseMove -= UpdateBigMapCoordinates;

            BigMapRecord currentRecord;
            if (!GameScene.MapInfoList.TryGetValue(ParentDialog.TargetMapIndex, out currentRecord))
            {
                return;
            }

            ParentDialog.CurrentRecord = currentRecord;
            int index = currentRecord.MapInfo.BigMap;

            if (index <= 0 && ParentDialog.TargetMapIndex == GameScene.Scene.MapControl.Index)
            {
                index = GameScene.Scene.MapControl.MapImg;
            }

            if (index <= 0)
            {
                return;
            }

            if (index != _bigMapLoadedIdx)
            {
                _bigMapLoadedIdx = index;
                _bigMapTexture = null;
                if (index > 0) LoadBigMapTexture(index);
            }

            MouseMove += UpdateBigMapCoordinates;
            Size = _bigMapTexture != null ? new Size(_bigMapTexture.Width, _bigMapTexture.Height) : Libraries.MiniMap.GetSize(index);
            Rectangle viewRect = new Rectangle(0, 0, Math.Min(604, Size.Width), Math.Min(442, Size.Height));

            viewRect.X = 156 + (604 - viewRect.Width) / 2;
            viewRect.Y = 53 + (442 - viewRect.Height) / 2;

            Location = viewRect.Location;
            Size = viewRect.Size.ToSize();

            BigMap_MouseCoordsProcessing_OffsetX = DisplayLocation.X;
            BigMap_MouseCoordsProcessing_OffsetY = DisplayLocation.Y;

            ScaleX = Size.Width / (float)currentRecord.MapInfo.Width;
            ScaleY = Size.Height / (float)currentRecord.MapInfo.Height;

            viewRect.Location = new Point(
                (int)(ScaleX * MapObject.User.CurrentLocation.X) - viewRect.Width / 2,
                (int)(ScaleY * MapObject.User.CurrentLocation.Y) - viewRect.Height / 2);

            if (viewRect.Right >= Size.Width)
                viewRect.X = Size.Width - viewRect.Width;
            if (viewRect.Bottom >= Size.Height)
                viewRect.Y = Size.Height - viewRect.Height;

            if (viewRect.X < 0) viewRect.X = 0;
            if (viewRect.Y < 0) viewRect.Y = 0;

            if (_bigMapTexture != null)
            {
                DXManager.Draw(_bigMapTexture,
                    new Rectangle(0, 0, _bigMapTexture.Width, _bigMapTexture.Height),
                    new Rectangle(DisplayLocation.X, DisplayLocation.Y, Size.Width, Size.Height),
                    Color.White);
            }

            int startPointX = (int)(viewRect.X / ScaleX);
            int startPointY = (int)(viewRect.Y / ScaleY);

            var map = GameScene.Scene.MapControl;
            if (ParentDialog.TargetMapIndex == map.Index)
            {
                float x;
                float y;
                foreach (var ob in  MapControl.Objects.Values)
                {
                    if (ob.Race == ObjectType.Item || ob.Dead || ob.Race == ObjectType.Spell || ob.ObjectID == MapObject.User.ObjectID) continue;
                    x = ((ob.CurrentLocation.X - startPointX) * ScaleX) + DisplayLocation.X;
                    y = ((ob.CurrentLocation.Y - startPointY) * ScaleY) + DisplayLocation.Y;

                    Color colour;

                    if (GroupDialog.GroupList.Contains(ob.Name) || ob.Name.EndsWith(string.Format("({0})", MapObject.User.Name)))
                        colour = Color.FromArgb(0, 0, 255);
                    else
                        if (ob is PlayerObject)
                        colour = Color.FromArgb(255, 255, 255);
                    else if (ob is NPCObject || ob.AI == 6)
                        colour = Color.FromArgb(0, 255, 50);
                    else
                        colour = Color.FromArgb(255, 0, 0);

                    DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(x - 0.5), (float)(y - 0.5), 0.0F), colour);
                }

                x = MapObject.User.CurrentLocation.X * ScaleX;
                y = MapObject.User.CurrentLocation.Y * ScaleY;
                var s = UserRadarDot.Size;
                UserRadarDot.Location = new Point((int)x - s.Width / 2, (int)y - s.Height / 2);

                if (GroupDialog.GroupList.Count > 0)
                {
                    for (int i = 0; i < GameScene.Scene.GroupDialog.GroupMembers.Length; i++)
                    {
                        string groupMembersName = GameScene.Scene.GroupDialog.GroupMembers[i].Text;
                        Players[i].Visible = false;

                        foreach (var groupMembersMap in GroupDialog.GroupMembersMap.Where(x => x.Key == groupMembersName && x.Value == map.Title))
                        {
                            foreach (var groupMemberLocation in PlayerLocations.Where(x => x.Key == groupMembersMap.Key))
                            {

                                float alteredX = ((groupMemberLocation.Value.X - startPointX) * ScaleX);
                                float alteredY = ((groupMemberLocation.Value.Y - startPointY) * ScaleY);

                                if (groupMembersName != MapObject.User.Name)
                                    Players[i].Visible = true;

                                Players[i].Hint = groupMemberLocation.Key;
                                Players[i].Location = new Point((int)(alteredX - 0.5F) - 4, (int)(alteredY - 0.5F) - 4);
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < Players.Length; i++)
                    {
                        Players[i].Visible = false;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    Players[i].Visible = false;
                }
            }

            foreach (var record in ParentDialog.CurrentRecord.MovementButtons)
            {
                var movementInfo = record.Key;
                var label = record.Value;

                float x = movementInfo.Location.X * ScaleX;
                float y = movementInfo.Location.Y * ScaleY;

                var s = label.Size;

                label.Location = new Point((int)x - s.Width / 2, (int)y - s.Height / 2);
            }

            // 大地图点击后的寻路路线（白色点点点）：读取 MapControl.CurrentPath，
            // 按屏幕最小间距取样绘制，避免过密连成线；随角色走动实时缩短。
            if (GameScene.Scene.MapControl.AutoPath && GameScene.Scene.MapControl.CurrentPath != null)
            {
                var mc = GameScene.Scene.MapControl;
                float minDist = 12F; // 相邻点的最小屏幕间距（像素）
                float lastX = float.MinValue, lastY = float.MinValue;
                for (int i = 0; i < mc.CurrentPath.Count; i++)
                {
                    Node node = mc.CurrentPath[i];
                    float rx = ((node.Location.X - startPointX) * ScaleX) + DisplayLocation.X;
                    float ry = ((node.Location.Y - startPointY) * ScaleY) + DisplayLocation.Y;
                    float dx = rx - lastX, dy = ry - lastY;
                    if (i == 0 || dx * dx + dy * dy >= minDist * minDist)
                    {
                        DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3(rx - 2F, ry - 2F, 0.0F), Color.White);
                        lastX = rx; lastY = ry;
                    }
                }
            }
        }
    }

    public class BigMapRecord
    {
        public int Index;
        public ClientMapInfo MapInfo;
        public Dictionary<ClientMovementInfo, MirControl> MovementButtons = new Dictionary<ClientMovementInfo, MirControl>();

        public BigMapRecord() { }
    }

}
