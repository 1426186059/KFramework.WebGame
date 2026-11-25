using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;

namespace Client.MirScenes.Dialogs
{

    /// <summary>
    /// 右上小地图 —— 直接移植原始传奇 HomeSmallMapCtrl / SmaillMapSkin。
    /// 布局权威：default.thm.json 内嵌 SmaillMapSkin.gjs；逻辑：main10.25487.1.min.js 的 HomeSmallMapCtrl。
    /// 皮肤 120×144：标题栏 y_h_map0(120×25) + 地图窗 y_h_map2(120×119) + 视口 scroller(140×140*0.85)
    /// + mapImg(604×442) + 覆盖 y_h_map1 + 底条 y_h_msbg + 跨服标 y_map_kua
    /// + 点位 y_map_p_0(怪)/_1(玩家)/_3(NPC) + txt_name/txt_info。
    /// 本工程图集为高清版 y_homeUI-sheet（帧约 142px 宽），按帧原生尺寸等比放大。
    /// ★Anchor=TopRight 固定在右上角。
    /// </summary>
    public sealed class MiniMapDialog : MirImageControl
    {
        private const string HomeSheet = "y_homeUI-sheet";

        // 高清帧原生尺寸：标题栏 142×25、窗体 142×140、底条 141×29
        private const int FrameW = 142;
        private const int TitleH = 25;
        private const int WinH = 140;
        private const int PanelH = TitleH + WinH;   // 165
        // 地图可视窗（相对本控件左上角），对应 gjs scroller(140×140*0.85≈119，x=1,y=25)
        private const int ViewX = 3, ViewY = TitleH + 3, ViewW = 136, ViewH = 132;

        private readonly MirImageControl _title, _win, _cover, _bottomBar;

        // 保留字段名以兼容可能的外部引用
        public MirImageControl LightSetting, NewMail;
        public MirButton ToggleButton, BigMapButton, MailButton;
        public MirLabel LocationLabel, MapNameLabel;
        public MirLabel AModeLabel, PModeLabel;

        private float _fade = 1F;
        private bool _bigMode = true;

        // 小地图纹理：CDN minimap/{img}.jpg，经 ImageUrl 通道加载后收进 _miniMapTexture
        private KFramework.MonoGame.Texture2D? _miniMapTexture;
        private int _miniMapLoadedImg = -1;

        public List<MirLabel> QuestIcons = new List<MirLabel>();

        public MiniMapDialog()
        {
            AutoSize = false;
            Size = new Size(FrameW, PanelH);
            PixelDetect = false;

            // ★锚点固定在右上角（与 SkillBarDialog 同款）
            Anchor = EAnchorType.TopRight;
            AnchorPos = new Point(0, 0);
            ApplyAnchor();

            // 原始传奇外框（图集子控件，由引擎绘制）
            _title = Frame("y_h_map0", 0, 0, FrameW, TitleH);
            _win = Frame("y_h_map2", 0, TitleH, FrameW, WinH);
            _cover = Frame("y_h_map1", 0, TitleH, FrameW, WinH);
            _bottomBar = Frame("y_h_msbg", 0, PanelH - 29, 141, 29);

            BeforeDraw += MiniMap_BeforeDraw;

            // 小地图纹理加载完成：收进 _miniMapTexture 并清掉 Texture，避免整图盖住边框
            RemoteImageLoaded += (s, e) => 
            {
                _win.Visible = false;
                _miniMapTexture = Texture; 
                Texture = null;
            };

            MapNameLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Size = new Size(FrameW - 8, 20),
                Location = new Point(4, 2),
                NotControl = true,
                ForeColour = CommonColor.White,
                OutLineColour = CommonColor.Black,
                Font = FontConst.F10,
            };

            LocationLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Size = new Size(FrameW - 16, 16),
                Location = new Point(8, PanelH - 24),
                NotControl = true,
                ForeColour = CommonColor.White,
                OutLineColour = CommonColor.Black,
                Font = FontConst.F10,
                RichText = true,
            };

            // 点击整块小地图 → 打开大地图（对应 gjs img_0 clickHandler → uim.showOrHide(547)）
            Click += (o, e) => GameScene.Scene.BigMapDialog.Toggle();
        }

        private MirImageControl Frame(string name, int x, int y, int w, int h)
        {
            return new MirImageControl
            {
                AtlasNameKey = HomeSheet + "#" + name,
                AutoSize = false,
                Size = new Size(w, h),
                Location = new Point(x, y),
                Parent = this,
                NotControl = true,
            };
        }

        private void MiniMap_BeforeDraw(object sender, EventArgs e)
        {
            foreach (var icon in QuestIcons)
            {
                icon.Dispose();
            }
            QuestIcons.Clear();

            MapControl map = GameScene.Scene.MapControl;
            if (map == null)
            {
                return;
            }

            // 地图图像 id 变化时发起 CDN 小地图纹理加载（minimap/{img}.jpg）。
            // 之前这段在移植时被误删，导致 LoadMiniMapTexture 从未被调用、_miniMapTexture 永远为 null。
            if (map.MapImg != _miniMapLoadedImg)
            {
                _miniMapLoadedImg = map.MapImg;
                _miniMapTexture = null;
                if (map.MapImg > 0)
                    LoadMiniMapTexture(map.MapImg);
            }
            if (_miniMapTexture == null)
            {
                return;
            }

            // 恢复原版换算：scale = 纹理像素 / 地图格子数；裁剪框尺寸 = 视口显示尺寸，1:1 绘制，
            // 绘制尺寸永远等于视口，不会超出。之前误用 map.PixelWidth(格子×48) 算 srcW，
            // 导致裁剪框被放大数倍，小地图远超视口范围。
            int texW = _miniMapTexture.Width, texH = _miniMapTexture.Height;
            float scaleX = texW / (float)Math.Max(1, map.Width);
            float scaleY = texH / (float)Math.Max(1, map.Height);

            Rectangle viewRect = new Rectangle(0, 0, ViewW, ViewH);
            viewRect.Location = new Point(
                (int)(scaleX * MapObject.User.CurrentLocation.X) - ViewW / 2,
                (int)(scaleY * MapObject.User.CurrentLocation.Y) - ViewH / 2);
            if (viewRect.Right >= texW) viewRect.X = texW - ViewW;
            if (viewRect.Bottom >= texH) viewRect.Y = texH - ViewH;
            if (viewRect.X < 0) viewRect.X = 0;
            if (viewRect.Y < 0) viewRect.Y = 0;

            Point drawLocation = Location;
            drawLocation.Offset(ViewX, ViewY);
            DXManager.Draw(_miniMapTexture, viewRect, new Vector3(drawLocation.X, drawLocation.Y, 0), CommonColor.FromArgb((int)(255 * _fade), 255, 255, 255));

            float startPointX = viewRect.X / scaleX;
            float startPointY = viewRect.Y / scaleY;

            // 对象点位：怪红 / 玩家白 / NPC 绿 / 队友蓝（对应 y_map_p_0/1/3）
            foreach (var ob in MapControl.Objects.Values)
            {
                if (ob == MapObject.User || ob.Race == ObjectType.Item || ob.Dead || ob.Race == ObjectType.Spell || ob.Sneaking) continue;
                float x = ((ob.CurrentLocation.X - startPointX) * scaleX) + drawLocation.X;
                float y = ((ob.CurrentLocation.Y - startPointY) * scaleY) + drawLocation.Y;
                if (x < drawLocation.X || x > drawLocation.X + ViewW || y < drawLocation.Y || y > drawLocation.Y + ViewH) continue;

                Color colour;
                if ((GroupDialog.GroupList.Contains(ob.Name) && MapObject.User != ob) || ob.Name.EndsWith(string.Format("({0})", MapObject.User.Name)))
                    colour = CommonColor.FromArgb(0, 0, 255);
                else if (ob is PlayerObject)
                    colour = CommonColor.FromArgb(255, 255, 255);
                else if (ob is NPCObject || ob.AI == 6)
                    colour = CommonColor.FromArgb(0, 255, 50);
                else
                    colour = CommonColor.FromArgb(255, 0, 0);

                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(x - 0.5), (float)(y - 0.5), 0.0F), colour);

                if (ob is NPCObject npc && npc.GetAvailableQuests(true).Any())
                {
                    string text = "";
                    Color color = CommonColor.Empty;
                    switch (npc.QuestIcon)
                    {
                        case QuestIcon.ExclamationBlue: color = CommonColor.DodgerBlue; text = "!"; break;
                        case QuestIcon.ExclamationYellow: color = CommonColor.Yellow; text = "!"; break;
                        case QuestIcon.ExclamationGreen: color = CommonColor.Green; text = "!"; break;
                        case QuestIcon.QuestionBlue: color = CommonColor.DodgerBlue; text = "?"; break;
                        case QuestIcon.QuestionWhite: color = CommonColor.White; text = "?"; break;
                        case QuestIcon.QuestionYellow: color = CommonColor.Yellow; text = "?"; break;
                        case QuestIcon.QuestionGreen: color = CommonColor.Green; text = "?"; break;
                    }
                    QuestIcons.Add(new MirLabel
                    {
                        AutoSize = true,
                        Parent = this,
                        Font = FontConst.F9Bold,
                        DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                        Text = text,
                        ForeColour = color,
                        Location = new Point((int)(x - Location.X) - 6, (int)(y - Location.Y) - 10),
                        NotControl = true,
                        Visible = true,
                        Modal = true
                    });
                }
            }

            // 自己（黄点，最后画，对应 y_map_p_2）；玩家恒在视口中心
            float mx = drawLocation.X + (MapObject.User.CurrentLocation.X - startPointX) * scaleX;
            float my = drawLocation.Y + (MapObject.User.CurrentLocation.Y - startPointY) * scaleY;
            DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 5, 5), new Vector3((float)(mx - 1), (float)(my - 1), 0), CommonColor.Yellow);
        }

        private async void LoadMiniMapTexture(int img)
        {
            _win.Visible = true;
            ImageUrl = Mir2OriginResource.MinimapPath(img);
        }

        public void Toggle()
        {
            // 原始传奇小地图无折叠态；快捷键切换地图内容显示/隐藏（外框始终保留）
            _fade = _fade == 0F ? 1F : 0F;
            Redraw();
        }

        private void SetSmallMode() { }
        private void SetBigMode() { }

        public void Process()
        {
            MapControl map = GameScene.Scene.MapControl;
            if (map == null) return;

            MapNameLabel.Text = map.Title;
            LocationLabel.Text = $"{SafeStatus(map)}  {MapObject.User.CurrentLocation.X}, {MapObject.User.CurrentLocation.Y}";
        }

        /// <summary>
        /// 原始传奇 HomeSmallMapCtrl.updateMyPosTxt 的判定：
        ///   safe = (mapCfg.safe == 1) || map.isSafe(gridX, gridY)
        /// 整图安全来自 0config.map.safe；逐格安全来自进图时 MapReader 已解码进 M2CellInfo 的 Flags bit1
        /// （见 MapCode.cs CellInfo.IsSafe）——复用现成数据，零额外异步加载。
        /// 显示 "安全区域"/"危险区域" 并配绿/红（对应原版 Logic.getCol(50)/(49)）。
        /// </summary>
        private string SafeStatus(MapControl map)
        {
            bool safe = false;

            // 条件1：整图安全（0config.map.safe == 1）
            if (ExcelTableMgr.Maps.TryGetValue(map.Index, out var cfg) && cfg.safe == 1)
                safe = true;

            // 条件2：当前格子安全（进图时已从 map.dat bit1 解码到 M2CellInfo[x,y].Safe）
            if (!safe && map.M2CellInfo != null)
            {
                Point p = MapObject.User.CurrentLocation;
                if (p.X >= 0 && p.Y >= 0 && p.X < map.Width && p.Y < map.Height)
                    safe = map.M2CellInfo[p.X, p.Y].IsSafe;
            }

            // 整行前景保持白色：坐标用它；状态词用富文本标签单独上色。
            LocationLabel.ForeColour = CommonColor.White;
            return safe ? "<color=lime>安全区域</color>" : "<color=red>危险区域</color>";
        }
    }
}
