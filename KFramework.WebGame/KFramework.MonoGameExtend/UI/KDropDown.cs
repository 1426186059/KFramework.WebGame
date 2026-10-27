using System;
using System.Collections.Generic;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 下拉选择框（参考 Unity Dropdown）：主体是一个 KButton（自带标签 + 四态），点击展开一组选项（KButton），
    /// 选中某项后更新主标签文本并触发 OnValueChanged(selectedIndex)；点击展开区域之外自动收起。
    /// <para>用法：var dd = new KDropDown(); dd.Items.Add("A"); dd.Items.Add("B"); dd.OnValueChanged += (s,i)=>{ }; 挂在 KCanvas 下。</para>
    /// </summary>
    public class KDropDown : KButton
    {
        private readonly List<KButton> _optionButtons = new List<KButton>();
        private readonly List<string> _items = new List<string>();
        private int _selectedIndex = -1;
        private bool _expanded;

        public float OptionHeight { get; set; } = 28f;
        public Color OptionNormalColor { get; set; } = new Color(20, 20, 20, 230);
        public Color OptionHoverColor { get; set; } = new Color(140, 70, 0, 230);

        public List<string> Items => _items;
        public int SelectedIndex => _selectedIndex;

        public event EventHandler<int> OnValueChanged;

        public KDropDown()
        {
            // 主按钮点击 → 切换展开/收起
            PointerClickEvent += (s, e) => ToggleDropDown();
        }

        public void AddItem(string text) { _items.Add(text); }
        public void ClearItems() { _items.Clear(); CloseDropDown(); _selectedIndex = -1; if (Label != null) Label.Text = ""; }

        private void ToggleDropDown()
        {
            if (_expanded) CloseDropDown();
            else OpenDropDown();
        }

        private void OpenDropDown()
        {
            EnsureOptions(_items.Count);
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                var btn = _optionButtons[i];
                bool show = i < _items.Count;
                btn.activeSelf = show;
                if (show)
                {
                    btn.LocalPosition = new Vector3(0, Size.Y + i * OptionHeight, 0);
                    btn.Size = new Vector2(Size.X, OptionHeight);
                    if (btn.Label != null) btn.Label.Text = _items[i];
                }
            }
            _expanded = true;
        }

        private void CloseDropDown()
        {
            _expanded = false;
            foreach (var b in _optionButtons) b.activeSelf = false;
        }

        private void EnsureOptions(int count)
        {
            while (_optionButtons.Count < count)
            {
                int idx = _optionButtons.Count;
                var btn = new KButton();
                btn.MinMaxAnchor = RectangleF.MinMax(0, 0, 0, 0);
                btn.NormalColor = OptionNormalColor;
                btn.HighlightedColor = OptionHoverColor;
                btn.PressedColor = OptionHoverColor;
                btn.DisabledColor = OptionNormalColor;
                btn.Priority = 10; // 高于主按钮，确保点击选项时优先命中选项
                btn.Parent = this;
                btn.activeSelf = false;
                btn.PointerClickEvent += (s, e) => SelectOption(idx);
                // 触发一次状态刷新，让上面设置的四态颜色立即生效（KButton 的 _targetColor 在构造时已按默认色缓存）
                btn.Interactable = false;
                btn.Interactable = true;
                _optionButtons.Add(btn);
            }
        }

        private void SelectOption(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _selectedIndex = index;
            if (Label != null) Label.Text = _items[index];
            OnValueChanged?.Invoke(this, index);
            CloseDropDown();
        }

        public override void Update()
        {
            base.Update();
            if (_expanded)
            {
                RectangleF box = GetExpandedBounds();
                Vector2 mp = KInputMgr.MousePosition;
                if (!(mp.X >= box.Left && mp.X <= box.Right && mp.Y >= box.Top && mp.Y <= box.Bottom))
                    CloseDropDown();
            }
        }

        private RectangleF GetExpandedBounds()
        {
            RectangleF main = GetWorldBound();
            float optH = _items.Count * OptionHeight;
            return new RectangleF(main.Left, main.Top, main.Width, main.Height + optH);
        }
    }
}
