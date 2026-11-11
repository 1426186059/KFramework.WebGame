using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using MirEngine;
using C = ClientPackets;
using WebGame.Mir2.MonoGame.Client;
using WebGame.China.Mir2.Client.Mir2.MirScenes.GameScene;

namespace Client.MirScenes.Dialogs
{
    public sealed class BigMapDialog : MirImageControl
    {
        MirLabel CoordinateLabel, TitleLabel;
        public MirButton CloseButton, MyLocationButton;

        public int TargetMapIndex;

        public BigMapViewPort ViewPort;

        private Point mouseLocation = Point.Empty;
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
                TitleLabel.Text = currentRecord != null ? currentRecord.MapInfo.Title : string.Empty;
                ViewPort.UserRadarDot.Visible = currentRecord != null && currentRecord.Index == GameScene.Scene.MapControl.Index;
                SetButtonsVisibility(true);
            }
        }

        public BigMapDialog()
        {
            Index = 820;
            Library = Libraries.Title;
            Sort = true;
            // 浏览器端资源异步加载，MirImageControl 在 AutoSize 下 GetTrueSize 可能返回 0，
            // 导致对话框 Size 为 0、整个窗口收不到鼠标（关闭按钮与地图点击全部失效）。
            // 显式关闭 AutoSize 并给出确定尺寸：优先用真实贴图尺寸，否则用经典大地图框尺寸兜底。
            AutoSize = false;
            Size = (Library != null && Index >= 0) ? Library.GetTrueSize(Index) : Size.Empty;
            if (Size.IsEmpty) Size = new Size(596, 460);
            Location = Center;
            NotControl = false;

            MyLocationButton = new MirButton
            {
                Index = 824,
                HoverIndex = 825,
                PressedIndex = 826,
                Location = new Point(400, Size.Height - 33),
                Library = Libraries.Title,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            MyLocationButton.Click += (o, e) => TargetMyLocation();


            ViewPort = new BigMapViewPort()
            {
                Parent = this
            };

            CoordinateLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.White,
                Location = new Point(519, 435),
                Parent = this,
                NotControl = true,
            };

            TitleLabel = new MirLabel
            {
                ForeColour = Color.White,
                Parent = this,
                AutoSize = false,
                Size = new Size(699, 20),
                NotControl = true,
                Location = new Point(19, 6),
                Font = new Font(Settings.FontName, 9F, KFramework.MonoGame.FontStyle.Bold),
                DrawFormat = TextFormatFlags.HorizontalCenter
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(Size.Width - 25, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();
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
                var tex = await Mir2OriginResource.GetMinimapTextureAsync(img, DXManager.GDevice);
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
            Size = new Size(568, 380);

            UserRadarDot = new MirImageControl
            {
                Library = Libraries.Prguse2,
                Index = 1350,
                Parent = this,
                Visible = false,
                NotControl = true
            };

            Players = new MirImageControl[Globals.MaxGroup];
            for (int i = 0; i < Players.Length; i++)
            {
                Players[i] = new MirImageControl
                {
                    Index = 1350,
                    Library = Libraries.Prguse2,
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

            // ParentDialog 由 ParentChanged 事件赋值，事件可能晚于首次绘制触发，此处从非空的 Parent 兜底解析，避免 NRE。
            if (ParentDialog == null) ParentDialog = Parent as BigMapDialog;
            if (ParentDialog == null) return;

            MouseMove -= UpdateBigMapCoordinates;

            BigMapRecord currentRecord;
            if (!GameScene.MapInfoList.TryGetValue(ParentDialog.TargetMapIndex, out currentRecord))
                return;

            ParentDialog.CurrentRecord = currentRecord;
            int index = currentRecord.MapInfo.BigMap;
            // 原始传奇大地图与小地图共用 assets/resource/minimap/{img}.jpg 同一张图（CDN 无 bigmap/ 目录）；
            // PC 地图 BigMap 多为 0，查看当前地图时回退到瓦片 img（即正在显示的小地图同一张图）。
            if (index <= 0 && ParentDialog.TargetMapIndex == GameScene.Scene.MapControl.Index)
                index = GameScene.Scene.MapControl.MapImg;

            if (index <= 0)
                return;

            // 大地图远程整图：按 BigMap id 从 CDN 取（替代本地 Libraries.MiniMap），缓存避免重复下载。
            if (index != _bigMapLoadedIdx)
            {
                _bigMapLoadedIdx = index;
                _bigMapTexture = null;
                if (index > 0) LoadBigMapTexture(index);
            }

            MouseMove += UpdateBigMapCoordinates;

            Size = _bigMapTexture != null ? new Size(_bigMapTexture.Width, _bigMapTexture.Height) : Libraries.MiniMap.GetSize(index);
            Rectangle viewRect = new Rectangle(0, 0, Math.Min(568, Size.Width), Math.Min(380, Size.Height));

            viewRect.X = 14 + (568 - viewRect.Width) / 2;
            viewRect.Y = 52 + (380 - viewRect.Height) / 2;

            Location = viewRect.Location;
            Size = viewRect.Size;

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
                DXManager.Draw(_bigMapTexture,
                    new KFramework.MonoGame.Rectangle(0, 0, _bigMapTexture.Width, _bigMapTexture.Height),
                    new KFramework.MonoGame.Rectangle(DisplayLocation.X, DisplayLocation.Y, Size.Width, Size.Height),
                    Color.White);
            else
                Libraries.MiniMap.Draw(index, DisplayLocation, Size, Color.FromArgb(255, 255, 255));

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
                                Players[i].Location = new Point((int)(alteredX - 0.5F) - 3, (int)(alteredY - 0.5F) - 3);
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
