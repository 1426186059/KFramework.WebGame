using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace MapInfoEditor
{
    internal enum PlaceMode { None, SafeZone, Monster, Npc }

    internal class MonsterItem
    {
        public int Index;
        public string Name;
        public override string ToString() => $"{Index} - {Name}";
    }

    public partial class MainForm : Form
    {
        private readonly MapInfoStore _store = new MapInfoStore();
        private MapEntry _current;
        private OriginalMapData _currentMapData;
        private int _cell = 4;
        private bool _loadingGrid;
        private bool _dirty;
        private PlaceMode _placeMode = PlaceMode.None;

        // 控件
        private ToolStrip _ts;
        private TextBox _txtFilter;
        private ListView _lvMaps;
        private TabControl _tabs;
        private PropertyGrid _pg;
        private DataGridView _dgvSZ, _dgvMon, _dgvNpc;
        private Button _btnAddSZ, _btnDelSZ, _btnAddMon, _btnDelMon, _btnAddNpc, _btnDelNpc;
        private Panel _pnlPreview;
        private PictureBox _pic;
        private Label _lblLegend, _lblStatus;
        private ToolStripComboBox _cmbPlace, _cmbMonster;

        public MainForm()
        {
            InitializeComponent();
            try
            {
                _store.Load();
                PopulateMonsterCombo();
                LoadMapList("");
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "MapInfo 编辑器";
            this.Width = 1300; this.Height = 840;
            this.StartPosition = FormStartPosition.CenterScreen;

            // ---- 顶部工具栏 ----
            _ts = new ToolStrip();
            var btnSave = new ToolStripButton("保存") { Text = "💾 保存" };
            btnSave.Click += (s, e) => { _store.Save(); _dirty = false; UpdateTitle(); MessageBox.Show("已保存到 MapInfo_Fix.json", "提示"); };
            var btnZoomIn = new ToolStripButton("放大");
            btnZoomIn.Click += (s, e) => { _cell = Math.Min(24, _cell + 1); RenderPreview(); };
            var btnZoomOut = new ToolStripButton("缩小");
            btnZoomOut.Click += (s, e) => { _cell = Math.Max(1, _cell - 1); RenderPreview(); };
            _cmbPlace = new ToolStripComboBox() { Width = 110, ToolTipText = "在右侧预览图上点击放置" };
            _cmbPlace.Items.AddRange(new object[] { "无", "安全区", "怪物", "NPC" });
            _cmbPlace.SelectedIndex = 0;
            _cmbPlace.SelectedIndexChanged += (s, e) =>
            {
                _placeMode = _cmbPlace.SelectedIndex switch { 1 => PlaceMode.SafeZone, 2 => PlaceMode.Monster, 3 => PlaceMode.Npc, _ => PlaceMode.None };
            };
            _cmbMonster = new ToolStripComboBox() { Width = 200, ToolTipText = "放置怪物时使用的怪物" };
            _ts.Items.AddRange(new ToolStripItem[] { btnSave, btnZoomOut, btnZoomIn, new ToolStripLabel("放置模式:"), _cmbPlace, new ToolStripLabel("怪物:"), _cmbMonster });
            this.Controls.Add(_ts);

            // ---- 左侧地图列表 ----
            var pnlLeft = new Panel() { Dock = DockStyle.Left, Width = 250 };
            _txtFilter = new TextBox() { Dock = DockStyle.Top, PlaceholderText = "过滤 (id/名称)..." };
            _txtFilter.TextChanged += (s, e) => LoadMapList(_txtFilter.Text);
            _lvMaps = new ListView() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
            _lvMaps.Columns.Add("id", 50); _lvMaps.Columns.Add("名称", 150); _lvMaps.Columns.Add("FileName", 60);
            _lvMaps.SelectedIndexChanged += (s, e) => { if (_lvMaps.SelectedItems.Count > 0) SelectMap((MapEntry)_lvMaps.SelectedItems[0].Tag); };
            pnlLeft.Controls.Add(_lvMaps); pnlLeft.Controls.Add(_txtFilter);
            this.Controls.Add(pnlLeft);

            // ---- 右侧主分割：中(编辑) | 右(预览) ----
            var split = new SplitContainer() { Dock = DockStyle.Fill, SplitterDistance = 430 };
            split.Panel1.Controls.Add(BuildEditorPanel());
            split.Panel2.Controls.Add(BuildPreviewPanel());
            this.Controls.Add(split);

            this.FormClosing += (s, e) =>
            {
                if (_dirty && MessageBox.Show("有未保存的修改，确定退出？", "确认", MessageBoxButtons.YesNo) == DialogResult.No)
                    e.Cancel = true;
            };
        }

        private Panel BuildEditorPanel()
        {
            var p = new Panel() { Dock = DockStyle.Fill };
            _tabs = new TabControl() { Dock = DockStyle.Fill };

            // 常规
            var tabGen = new TabPage("常规");
            _pg = new PropertyGrid() { Dock = DockStyle.Fill };
            _pg.PropertyValueChanged += (s, e) => { _dirty = true; UpdateTitle(); };
            tabGen.Controls.Add(_pg);

            // 安全区
            var tabSZ = new TabPage("安全区");
            _dgvSZ = NewGrid();
            _dgvSZ.CellValueChanged += Dgv_CellValueChanged;
            _btnAddSZ = new Button() { Text = "添加", Dock = DockStyle.Bottom };
            _btnAddSZ.Click += (s, e) => AddRowAtCenter(PlaceMode.SafeZone);
            _btnDelSZ = new Button() { Text = "删除选中", Dock = DockStyle.Bottom };
            _btnDelSZ.Click += (s, e) => DeleteSelected(_dgvSZ, "SafeZones");
            tabSZ.Controls.Add(_dgvSZ); tabSZ.Controls.Add(_btnDelSZ); tabSZ.Controls.Add(_btnAddSZ);

            // 怪物
            var tabMon = new TabPage("怪物刷新");
            _dgvMon = NewGrid();
            _dgvMon.CellValueChanged += Dgv_CellValueChanged;
            _btnAddMon = new Button() { Text = "添加", Dock = DockStyle.Bottom };
            _btnAddMon.Click += (s, e) => AddRowAtCenter(PlaceMode.Monster);
            _btnDelMon = new Button() { Text = "删除选中", Dock = DockStyle.Bottom };
            _btnDelMon.Click += (s, e) => DeleteSelected(_dgvMon, "Respawns");
            tabMon.Controls.Add(_dgvMon); tabMon.Controls.Add(_btnDelMon); tabMon.Controls.Add(_btnAddMon);

            // NPC
            var tabNpc = new TabPage("NPC");
            _dgvNpc = NewGrid();
            _dgvNpc.CellValueChanged += Dgv_CellValueChanged;
            _btnAddNpc = new Button() { Text = "添加", Dock = DockStyle.Bottom };
            _btnAddNpc.Click += (s, e) => AddRowAtCenter(PlaceMode.Npc);
            _btnDelNpc = new Button() { Text = "删除选中", Dock = DockStyle.Bottom };
            _btnDelNpc.Click += (s, e) => DeleteSelected(_dgvNpc, "NPCs");
            tabNpc.Controls.Add(_dgvNpc); tabNpc.Controls.Add(_btnDelNpc); tabNpc.Controls.Add(_btnAddNpc);

            _tabs.TabPages.AddRange(new TabPage[] { tabGen, tabSZ, tabMon, tabNpc });
            p.Controls.Add(_tabs);
            return p;
        }

        private Panel BuildPreviewPanel()
        {
            var p = new Panel() { Dock = DockStyle.Fill, BackColor = Color.Black };
            _pnlPreview = new Panel() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Black };
            _pic = new PictureBox() { SizeMode = PictureBoxSizeMode.AutoSize };
            _pic.MouseClick += Pic_MouseClick;
            _pnlPreview.Controls.Add(_pic);

            _lblLegend = new Label() { Dock = DockStyle.Top, Height = 40, ForeColor = Color.White, BackColor = Color.FromArgb(30, 30, 30),
                Text = "图例：浅色=可走  深色=不可走(墙)  绿框=安全区(亮绿=出生点)  红点=怪物  蓝圈=Boss  绿点=NPC\n提示：选择上方“放置模式”后，在图上点击即可放置对应元素" };
            _lblStatus = new Label() { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.White, BackColor = Color.FromArgb(30, 30, 30), Text = "就绪" };

            p.Controls.Add(_lblStatus); p.Controls.Add(_lblLegend); p.Controls.Add(_pnlPreview);
            return p;
        }

        private DataGridView NewGrid()
        {
            return new DataGridView()
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                EditMode = DataGridViewEditMode.EditOnEnter, RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White
            };
        }

        // ---------- 数据加载 ----------
        private void PopulateMonsterCombo()
        {
            foreach (var kv in _store.Monsters.OrderBy(x => x.Key))
                _cmbMonster.Items.Add(new MonsterItem { Index = kv.Key, Name = kv.Value.Name });
            if (_cmbMonster.Items.Count > 0) _cmbMonster.SelectedIndex = 0;
        }

        private void LoadMapList(string filter)
        {
            _lvMaps.Items.Clear();
            var f = filter?.Trim().ToLower() ?? "";
            foreach (var e in _store.Maps.OrderBy(x => x.Index))
            {
                if (f != "" && e.Index.ToString().Contains(f) == false && e.Title.ToLower().Contains(f) == false && e.FileName.ToLower().Contains(f) == false)
                    continue;
                var it = new ListViewItem(new[] { e.Index.ToString(), e.Title, e.FileName }) { Tag = e };
                _lvMaps.Items.Add(it);
            }
        }

        private void SelectMap(MapEntry e)
        {
            _current = e;
            _currentMapData = _store.DecodeMap(e.Index);
            _pg.SelectedObject = new MapGeneralVM(e);
            RefreshTabs();
            RenderPreview();
        }

        // ---------- 网格构建 ----------
        private void RefreshTabs()
        {
            if (_current == null) return;
            _loadingGrid = true;
            BuildSZGrid(); BuildMonGrid(); BuildNpcGrid();
            _loadingGrid = false;
        }

        private void BuildSZGrid()
        {
            _dgvSZ.Columns.Clear(); _dgvSZ.Rows.Clear();
            AddCol(_dgvSZ, "X", "X", false); AddCol(_dgvSZ, "Y", "Y", false);
            AddCol(_dgvSZ, "Size", "半径", false); AddCol(_dgvSZ, "StartPoint", "出生点", false, true);
            foreach (var sz in _store.GetSafeZones(_current))
            {
                int i = _dgvSZ.Rows.Add(sz.X, sz.Y, sz.Size, sz.Start);
                _dgvSZ.Rows[i].Tag = sz.Node;
            }
        }

        private void BuildMonGrid()
        {
            _dgvMon.Columns.Clear(); _dgvMon.Rows.Clear();
            AddCol(_dgvMon, "MonsterIndex", "怪物ID", false);
            AddCol(_dgvMon, "怪物名", "怪物名", true);
            AddCol(_dgvMon, "X", "X", false); AddCol(_dgvMon, "Y", "Y", false);
            AddCol(_dgvMon, "Count", "数量", false); AddCol(_dgvMon, "Spread", "扩散", false);
            AddCol(_dgvMon, "Delay", "延迟", false); AddCol(_dgvMon, "Direction", "方向", false);
            foreach (var rp in _store.GetRespawns(_current))
            {
                string name = _store.Monsters.TryGetValue(rp.MonsterIndex, out var mm) ? mm.Name : "(未知)";
                bool boss = _store.IsBoss(rp.MonsterIndex, name);
                int i = _dgvMon.Rows.Add(rp.MonsterIndex, name + (boss ? " [Boss]" : ""), rp.X, rp.Y, rp.Count, rp.Spread, rp.Delay, rp.Direction);
                _dgvMon.Rows[i].Tag = rp.Node;
            }
        }

        private void BuildNpcGrid()
        {
            _dgvNpc.Columns.Clear(); _dgvNpc.Rows.Clear();
            AddCol(_dgvNpc, "Index", "Index", true);
            AddCol(_dgvNpc, "FileName", "脚本", false); AddCol(_dgvNpc, "Name", "名称", false);
            AddCol(_dgvNpc, "X", "X", false); AddCol(_dgvNpc, "Y", "Y", false);
            AddCol(_dgvNpc, "Rate", "频率", false); AddCol(_dgvNpc, "Image", "图像", false);
            foreach (var np in _store.GetNpcs(_current))
            {
                int i = _dgvNpc.Rows.Add(np.Index, np.FileName, np.Name, np.X, np.Y, np.Rate, np.Image);
                _dgvNpc.Rows[i].Tag = np.Node;
            }
        }

        private void AddCol(DataGridView dgv, string name, string header, bool readOnly, bool checkbox = false)
        {
            DataGridViewColumn col = checkbox ? new DataGridViewCheckBoxColumn() : new DataGridViewTextBoxColumn();
            col.Name = name; col.HeaderText = header; col.ReadOnly = readOnly;
            dgv.Columns.Add(col);
        }

        // ---------- 编辑 ----------
        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loadingGrid) return;
            var dgv = (DataGridView)sender;
            var row = dgv.Rows[e.RowIndex];
            if (row.Tag is not JsonObject node) return;
            var col = dgv.Columns[e.ColumnIndex].Name;
            var val = row.Cells[e.ColumnIndex].Value;
            if (col == "怪物名") return;
            if (col == "StartPoint" || dgv.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
                node[col] = JsonValue.Create(Convert.ToBoolean(val));
            else if (col == "FileName" || col == "Name")
                node[col] = JsonValue.Create(val?.ToString() ?? "");
            else
                node[col] = JsonValue.Create(Convert.ToInt32(val));
            _dirty = true; UpdateTitle(); RenderPreview();
        }

        private void DeleteSelected(DataGridView dgv, string field)
        {
            if (_current == null || dgv.SelectedRows.Count == 0) return;
            var node = (JsonObject)dgv.SelectedRows[0].Tag;
            var arr = _store.GetValues(_current, field);
            _store.RemoveFromArray(arr, node);
            _dirty = true; UpdateTitle();
            RefreshTabs(); RenderPreview();
        }

        private void AddRowAtCenter(PlaceMode mode)
        {
            if (_current == null || _currentMapData == null) return;
            int c = _currentMapData.Cols / 2, r = _currentMapData.Rows / 2;
            PlaceAt(c, r, mode);
        }

        private void Pic_MouseClick(object sender, MouseEventArgs e)
        {
            if (_current == null || _currentMapData == null || _placeMode == PlaceMode.None) return;
            int c = e.X / _cell, r = e.Y / _cell;
            if (c < 0 || r < 0 || c >= _currentMapData.Cols || r >= _currentMapData.Rows) return;
            PlaceAt(c, r, _placeMode);
        }

        private void PlaceAt(int c, int r, PlaceMode mode)
        {
            if (mode == PlaceMode.SafeZone) _store.AddSafeZone(_current, c, r, 10, false);
            else if (mode == PlaceMode.Monster)
            {
                int mi = (_cmbMonster.SelectedItem as MonsterItem)?.Index ?? 0;
                _store.AddRespawn(_current, mi, c, r, 1, 0, 0, 0);
            }
            else if (mode == PlaceMode.Npc) _store.AddNpc(_current, "NPC", "新NPC", c, r, 100, 0);
            _dirty = true; UpdateTitle();
            RefreshTabs(); RenderPreview();
        }

        // ---------- 预览渲染 ----------
        private void RenderPreview()
        {
            if (_current == null)
            {
                _lblStatus.Text = "未选择地图";
                return;
            }
            if (_currentMapData == null)
            {
                if (_pic.Image != null) { _pic.Image.Dispose(); _pic.Image = null; }
                _pic.Size = new Size(0, 0);
                _lblStatus.Text = $"无法解码地图数据块 (id={_current.Index})，可能 dat 中无此图";
                return;
            }
            var d = _currentMapData;
            int cell = _cell;
            int w = Math.Max(1, d.Cols * cell), h = Math.Max(1, d.Rows * cell);
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                for (int rr = 0; rr < d.Rows; rr++)
                {
                    for (int cc = 0; cc < d.Cols; cc++)
                    {
                        byte nib = d.CellFlags[rr * d.Cols + cc];
                        bool walk = (nib & 8) == 0;
                        Color col = walk ? Color.FromArgb(235, 232, 222) : Color.FromArgb(64, 64, 64);
                        if ((nib & 2) != 0) col = walk ? Color.FromArgb(200, 232, 200) : Color.FromArgb(40, 80, 40);
                        using (var br = new SolidBrush(col))
                            g.FillRectangle(br, cc * cell, rr * cell, cell, cell);
                    }
                }
                foreach (var sz in _store.GetSafeZones(_current))
                {
                    int x0 = (sz.X - sz.Size) * cell, y0 = (sz.Y - sz.Size) * cell;
                    int sw = (2 * sz.Size + 1) * cell;
                    using (var pen = new Pen(sz.Start ? Color.Lime : Color.Green, sz.Start ? 3 : 2))
                        g.DrawRectangle(pen, x0, y0, sw, sw);
                }
                foreach (var rp in _store.GetRespawns(_current))
                {
                    string mname = _store.Monsters.TryGetValue(rp.MonsterIndex, out var mm) ? mm.Name : "";
                    bool boss = _store.IsBoss(rp.MonsterIndex, mname);
                    int cx = rp.X * cell + cell / 2, cy = rp.Y * cell + cell / 2;
                    int rad = Math.Max(3, (int)(cell * 0.45));
                    if (boss)
                        using (var pen = new Pen(Color.Blue, 2)) g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                    else
                        using (var br = new SolidBrush(Color.Red)) g.FillEllipse(br, cx - rad, cy - rad, rad * 2, rad * 2);
                }
                foreach (var np in _store.GetNpcs(_current))
                {
                    int cx = np.X * cell + cell / 2, cy = np.Y * cell + cell / 2;
                    int rad = Math.Max(3, (int)(cell * 0.45));
                    using (var br = new SolidBrush(Color.Green)) g.FillEllipse(br, cx - rad, cy - rad, rad * 2, rad * 2);
                }
            }
            if (_pic.Image != null) _pic.Image.Dispose();
            _pic.Image = bmp;
            _pic.Size = bmp.Size;
            _lblStatus.Text = $"{_current.Title} (id={_current.Index})  网格 {d.Cols}×{d.Rows}  可走 {d.Walkable}  " + (d.Warn ?? "");
        }

        private void UpdateTitle()
        {
            this.Text = "MapInfo 编辑器" + (_dirty ? " *" : "") + $"  ({_store.Maps.Count} 张地图)";
        }
    }
}
