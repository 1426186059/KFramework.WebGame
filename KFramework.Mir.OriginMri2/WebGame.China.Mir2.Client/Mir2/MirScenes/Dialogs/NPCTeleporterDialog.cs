using System;
using System.Collections.Generic;
using Client.MirControls;
using Client.MirSounds;
using MirEngine;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 传送员目的地数据（由服务端下发；当前服务端尚无 deliver 系统，先定义客户端模型）。
    /// </summary>
    public sealed class DeliverDestination
    {
        public int Id;            // deliverId（服务端用以定位目的地图/坐标）
        public string Name;       // 显示名，如 "比奇城"
        public string Category;   // 左侧分类，如 "比奇省"（空串归入"全部"）
        public int Cost;          // 消耗（元宝/金币），0 表示免费
        public string CostText;   // 自定义消耗文本（优先于 Cost 展示）
        public string Description;// 悬浮/副标题说明（可选）
    }

    /// <summary>
    /// 传送员专属对话框（仿原始传奇页游 NpcDeliver 面板）。
    /// 图集：gameui4 公共 UI 图集 y_common-sheet（背景 y_cs_bg.jpg 在同目录 gameui4/jpg 下）。
    /// 结构：标题 + 关闭 + 左侧分类列 + 右侧目的地列 + 上下滚动 + 消耗提示。
    /// </summary>
    public sealed class NPCTeleporterDialog : MirImageControl
    {
        public event Action<DeliverDestination> OnTeleport;

        private const string Sheet = "y_common-sheet";
        private const string BgUrl = "resource/assets/gameui4/jpg/y_cs_bg.jpg";

        private const int PanelW = 480;
        private const int PanelH = 440;
        private const int CatX = 16, CatY = 56, CatW = 150, CatH = 30, CatStep = 36;
        private const int DestX = 176, DestY = 56, DestW = 288, DestH = 30, DestStep = 36;
        private const int MaxDestRows = 9;

        private MirImageControl _titleIcon;
        private MirLabel _titleLabel;
        private MirButton _closeButton;
        private MirLabel _costLabel;

        private readonly List<MirButton> _categoryButtons = new List<MirButton>();
        private string _currentCategory = "";

        private List<DeliverDestination> _allItems = new List<DeliverDestination>();
        private List<DeliverDestination> _visibleItems = new List<DeliverDestination>();
        private readonly List<MirButton> _destButtons = new List<MirButton>();
        private int _destStart;

        private MirButton _upButton, _downButton;

        public NPCTeleporterDialog()
        {
            ImageUrl = BgUrl;                 // 传送员面板底图（y_cs_bg）
            Size = new Size(PanelW, PanelH);
            Movable = true;
            Sort = true;

            _titleIcon = new MirImageControl
            {
                AtlasNameKey = Sheet + "#btn_npcfh_03",
                Parent = this,
                Location = new Point(16, 12),
                Size = new Size(15, 16),
            };

            _titleLabel = new MirLabel
            {
                Text = "传送员",
                Parent = this,
                Location = new Point(38, 10),
                AutoSize = true,
                ForeColour = Color.BurlyWood,
                Font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold),
            };

            _closeButton = new MirButton
            {
                AtlasNameKey = Sheet + "#com_btn_close0",
                Parent = this,
                Location = new Point(PanelW - 34, 8),
                Size = new Size(26, 42),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();

            // 右侧目的地列：预建 MaxDestRows 个按钮，按需显隐/改文本。
            for (int i = 0; i < MaxDestRows; i++)
            {
                int row = i;
                var btn = new MirButton
                {
                    AtlasNameKey = Sheet + "#com_z_btn_1",
                    HoverAtlasFrame = "com_z_btn_1_1",
                    Parent = this,
                    Location = new Point(DestX, DestY + i * DestStep),
                    Size = new Size(DestW, DestH),
                    CenterText = true,
                    Sound = SoundList.ButtonA,
                    Visible = false,
                };
                btn.Click += (o, e) =>
                {
                    if (row + _destStart < _visibleItems.Count)
                        SelectDestination(_visibleItems[row + _destStart]);
                };
                _destButtons.Add(btn);
            }

            _upButton = new MirButton
            {
                Parent = this,
                Location = new Point(PanelW - 30, DestY + 2),
                Size = new Size(20, 18),
                CenterText = true,
                Text = "▲",
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            _upButton.Click += (o, e) =>
            {
                if (_destStart > 0) { _destStart--; RefreshDestList(); }
            };

            _downButton = new MirButton
            {
                Parent = this,
                Location = new Point(PanelW - 30, DestY + MaxDestRows * DestStep - 20),
                Size = new Size(20, 18),
                CenterText = true,
                Text = "▼",
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            _downButton.Click += (o, e) =>
            {
                if (_destStart + MaxDestRows < _visibleItems.Count) { _destStart++; RefreshDestList(); }
            };

            _costLabel = new MirLabel
            {
                Text = "",
                Parent = this,
                Location = new Point(DestX, PanelH - 30),
                AutoSize = true,
                ForeColour = Color.White,
                Font = new Font(Settings.FontName, 9F),
            };
        }

        /// <summary>填充并显示：title=窗口标题，items=该 NPC 的全部传送目的地。</summary>
        public void Open(string title, List<DeliverDestination> items)
        {
            _titleLabel.Text = string.IsNullOrEmpty(title) ? "传送员" : title;
            _allItems = items ?? new List<DeliverDestination>();

            BuildCategories();

            Location = new Point((Settings.ScreenWidth - Size.Width) / 2,
                                 (Settings.ScreenHeight - Size.Height) / 2);
            Visible = true;
            BringToFront();
        }

        private void BuildCategories()
        {
            foreach (var b in _categoryButtons)
                b.Dispose();
            _categoryButtons.Clear();

            var cats = new List<string>();
            bool anyCat = false;
            foreach (var it in _allItems)
            {
                var c = it.Category ?? "";
                if (!string.IsNullOrEmpty(c)) anyCat = true;
                if (!cats.Contains(c)) cats.Add(c);
            }
            if (!anyCat)
            {
                cats.Clear();
                cats.Add("");
            }

            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var btn = new MirButton
                {
                    AtlasNameKey = Sheet + "#com_z_btn_1",
                    HoverAtlasFrame = "com_z_btn_1_1",
                    Parent = this,
                    Location = new Point(CatX, CatY + i * CatStep),
                    Size = new Size(CatW, CatH),
                    CenterText = true,
                    Text = string.IsNullOrEmpty(cat) ? "全部" : cat,
                    Sound = SoundList.ButtonA,
                };
                int idx = i;
                btn.Click += (o, e) => SelectCategory(cats[idx]);
                _categoryButtons.Add(btn);
            }

            _currentCategory = cats[0];
            SelectCategory(_currentCategory);
        }

        private void SelectCategory(string cat)
        {
            _currentCategory = cat;
            _visibleItems = new List<DeliverDestination>();
            foreach (var it in _allItems)
            {
                if (string.IsNullOrEmpty(cat) || (it.Category ?? "") == cat)
                    _visibleItems.Add(it);
            }
            _destStart = 0;
            RefreshDestList();
        }

        private void RefreshDestList()
        {
            for (int i = 0; i < _destButtons.Count; i++)
            {
                var btn = _destButtons[i];
                int dataIdx = i + _destStart;
                if (dataIdx < _visibleItems.Count)
                {
                    var it = _visibleItems[dataIdx];
                    btn.Text = string.IsNullOrEmpty(it.CostText)
                        ? (it.Cost > 0 ? $"{it.Name}  {it.Cost}" : it.Name)
                        : $"{it.Name}  {it.CostText}";
                    btn.Visible = true;
                }
                else
                {
                    btn.Visible = false;
                }
            }

            bool canScroll = _visibleItems.Count > MaxDestRows;
            _upButton.Visible = canScroll && _destStart > 0;
            _downButton.Visible = canScroll && _destStart + MaxDestRows < _visibleItems.Count;
        }

        private void SelectDestination(DeliverDestination item)
        {
            _costLabel.Text = string.IsNullOrEmpty(item.CostText)
                ? (item.Cost > 0 ? $"传送消耗：{item.Cost}" : "免费传送")
                : $"传送消耗：{item.CostText}";

            OnTeleport?.Invoke(item);
        }
    }
}
