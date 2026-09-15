using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 输入总调度（上层便捷入口）。
    ///
    /// <para>键盘 / 鼠标 / 触摸的采集与状态都在基石层：
    /// <see cref="Input_KeyBoard"/> / <see cref="Input_Mouse"/> / <see cref="Input_Touch"/>，
    /// 三者各自 poll 自己的事件队列（一帧 3 次跨界，输入是低频操作，完全可接受）。</para>
    ///
    /// <para>因此本类不再维护"设备列表 + 轮询"，只负责驱动指针分发（UI 点击），
    /// 并把查询转发到基石封装 —— 保持原有调用方式不变。</para>
    /// </summary>
    public static class KInputMgr
    {
        /// <summary>指针事件分发器</summary>
        public static KPointerDispatcher Pointer { get; private set; }

        /// <summary>总开关</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>是否已初始化</summary>
        public static bool Inited { get; private set; }

        public static void Init()
        {
            if (Inited) return;

            Pointer = new KPointerDispatcher();
            Pointer.Init();

            Inited = true;
        }

        /// <summary>
        /// 每帧调用一次，放在其它逻辑 Update 之前。
        /// 注意：键盘 / 鼠标 / 触摸的事件由 <see cref="Input.Poll"/> 各模块自行取回（Game 每帧调用），
        /// 这里只驱动指针分发。
        /// </summary>
        public static void Update(GameTime gameTime)
        {
            if (!Enabled || !Inited) return;

            Pointer?.Update(gameTime);
        }

        public static void Reset()
        {
            Input.Reset();
            Pointer?.Reset();
        }

        /// <summary>注册可点击对象。</summary>
        public static void Register(IClickable clickable)
        {
            if (!Inited) Init();
            Pointer.Register(clickable);
        }

        /// <summary>注销可点击对象</summary>
        public static void Unregister(IClickable clickable) => Pointer?.Unregister(clickable);

        // ===== 键盘 =====

        public static bool GetKey(Keys key) => Input_KeyBoard.GetKey(key);
        public static bool GetKeyDown(Keys key) => Input_KeyBoard.GetKeyDown(key);
        public static bool GetKeyUp(Keys key) => Input_KeyBoard.GetKeyUp(key);

        // ===== 鼠标 =====

        public static bool GetMouseButton(MouseButton button) => Input_Mouse.GetButton(button);
        public static bool GetMouseButtonDown(MouseButton button) => Input_Mouse.GetButtonDown(button);
        public static bool GetMouseButtonUp(MouseButton button) => Input_Mouse.GetButtonUp(button);

        // Unity 风格重载：0=左键 1=右键 2=中键
        public static bool GetMouseButton(int index) => GetMouseButton(ToButton(index));
        public static bool GetMouseButtonDown(int index) => GetMouseButtonDown(ToButton(index));
        public static bool GetMouseButtonUp(int index) => GetMouseButtonUp(ToButton(index));

        // ===== 触摸 =====

        public static int TouchCount => Input_Touch.TouchCount;
        public static KTouch GetTouch(int index) => Input_Touch.GetTouch(index);

        // ===== 聚合查询 =====

        public static bool AnyKey => Input_KeyBoard.AnyKey;
        public static bool AnyKeyDown => Input_KeyBoard.AnyKeyDown;
        public static Vector2 MousePosition => Input_Mouse.Position;
        public static int ScrollDelta => Input_Mouse.ScrollDelta;
        public static bool IsPointerOverUI => Pointer != null && Pointer.IsPointerOverUI;

        public static Vector2 GetMoveAxis() => Input_KeyBoard.GetAxis();

        /// <summary>退出键：Esc</summary>
        public static bool GetQuitPressed() => Input_KeyBoard.GetKeyDown(Keys.Escape);

        private static MouseButton ToButton(int index)
        {
            return index switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Right,
                2 => MouseButton.Middle,
                3 => MouseButton.XButton1,
                4 => MouseButton.XButton2,
                _ => MouseButton.Left,
            };
        }
    }
}
