using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入总调度器 —— 统一管理各输入设备，每帧按顺序轮询
    /// </summary>
    public static class KInputMgr
    {
        private static readonly List<IKInputDevice> mDeviceList = new List<IKInputDevice>();

        /// <summary>键盘</summary>
        public static KKeyboardInput Keyboard { get; private set; }

        /// <summary>鼠标</summary>
        public static KMouseInput Mouse { get; private set; }

        /// <summary>手柄</summary>
        public static KGamePadInput GamePad { get; private set; }

        /// <summary>触摸</summary>
        public static KTouchInput Touch { get; private set; }

        /// <summary>指针事件分发器</summary>
        public static KPointerDispatcher Pointer { get; private set; }

        /// <summary>总开关，关闭后所有设备停止轮询</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>是否已初始化</summary>
        public static bool Inited { get; private set; }

        /// <summary>
        /// 初始化，在 KSceneMgr.Init 之前调用
        /// </summary>
        public static void Init()
        {
            if (Inited) return;

            mDeviceList.Clear();

            Keyboard = new KKeyboardInput();
            Mouse = new KMouseInput();
            GamePad = new KGamePadInput();
            Touch = new KTouchInput();

            // 分发器依赖鼠标与触摸，必须排在它们之后更新
            Pointer = new KPointerDispatcher(Mouse, Touch);

            AddDevice(Keyboard);
            AddDevice(Mouse);
            AddDevice(GamePad);
            AddDevice(Touch);
            AddDevice(Pointer);

            Inited = true;
        }

        /// <summary>
        /// 每帧调用一次，放在其它逻辑 Update 之前
        /// </summary>
        public static void Update(GameTime gameTime)
        {
            if (!Enabled || !Inited) return;

            for (int i = 0; i < mDeviceList.Count; i++)
            {
                IKInputDevice device = mDeviceList[i];
                if (!device.Enabled) continue;
                if (!device.IsAvailable) continue;
                device.Update(gameTime);
            }
        }

        /// <summary>重置所有设备状态，切场景或窗口失焦时调用</summary>
        public static void Reset()
        {
            for (int i = 0; i < mDeviceList.Count; i++)
                mDeviceList[i].Reset();
        }

        /// <summary>添加自定义设备（会立即 Init）</summary>
        public static void AddDevice(IKInputDevice device)
        {
            if (device == null) return;
            if (mDeviceList.Contains(device)) return;
            device.Init();
            mDeviceList.Add(device);
        }

        /// <summary>移除设备</summary>
        public static void RemoveDevice(IKInputDevice device)
        {
            if (device == null) return;
            mDeviceList.Remove(device);
        }

        /// <summary>按名字查找设备</summary>
        public static IKInputDevice GetDevice(string name)
        {
            for (int i = 0; i < mDeviceList.Count; i++)
                if (mDeviceList[i].Name == name) return mDeviceList[i];
            return null;
        }

        // ===== 常用快捷方法 =====

        /// <summary>
        /// 注册可点击对象。允许在 Init 之前调用（如 UI 在构造函数里注册），
        /// 此时会自动完成初始化，避免注册丢失
        /// </summary>
        public static void Register(IClickable clickable)
        {
            if (!Inited) Init();
            Pointer.Register(clickable);
        }

        /// <summary>注销可点击对象</summary>
        public static void Unregister(IClickable clickable) => Pointer?.Unregister(clickable);

        /// <summary>按键是否按住</summary>
        public static bool GetKey(Keys key) => Keyboard != null && Keyboard.GetKey(key);

        /// <summary>按键是否本帧刚按下</summary>
        public static bool GetKeyDown(Keys key) => Keyboard != null && Keyboard.GetKeyDown(key);

        /// <summary>按键是否本帧刚抬起</summary>
        public static bool GetKeyUp(Keys key) => Keyboard != null && Keyboard.GetKeyUp(key);

        /// <summary>鼠标键是否按住</summary>
        public static bool GetMouseButton(MouseButton button) => Mouse != null && Mouse.GetButton(button);

        /// <summary>鼠标键是否本帧刚按下</summary>
        public static bool GetMouseButtonDown(MouseButton button) => Mouse != null && Mouse.GetButtonDown(button);

        /// <summary>鼠标键是否本帧刚抬起</summary>
        public static bool GetMouseButtonUp(MouseButton button) => Mouse != null && Mouse.GetButtonUp(button);

        // ===== Unity 风格重载：0=左键 1=右键 2=中键 =====

        /// <summary>鼠标键是否按住，索引同 Unity：0 左 1 右 2 中</summary>
        public static bool GetMouseButton(int index) => GetMouseButton(ToButton(index));

        /// <summary>鼠标键是否本帧刚按下，索引同 Unity：0 左 1 右 2 中</summary>
        public static bool GetMouseButtonDown(int index) => GetMouseButtonDown(ToButton(index));

        /// <summary>鼠标键是否本帧刚抬起，索引同 Unity：0 左 1 右 2 中</summary>
        public static bool GetMouseButtonUp(int index) => GetMouseButtonUp(ToButton(index));

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

        /// <summary>是否有任意键按住（含鼠标），同 Unity Input.anyKey</summary>
        public static bool AnyKey
        {
            get
            {
                if (Keyboard != null && Keyboard.AnyKey) return true;
                if (Mouse != null && (Mouse.GetButton(MouseButton.Left)
                    || Mouse.GetButton(MouseButton.Right)
                    || Mouse.GetButton(MouseButton.Middle))) return true;
                return false;
            }
        }

        /// <summary>是否有任意键本帧刚按下，同 Unity Input.anyKeyDown</summary>
        public static bool AnyKeyDown
        {
            get
            {
                if (Keyboard != null && Keyboard.AnyKeyDown) return true;
                if (Mouse != null && (Mouse.GetButtonDown(MouseButton.Left)
                    || Mouse.GetButtonDown(MouseButton.Right)
                    || Mouse.GetButtonDown(MouseButton.Middle))) return true;
                return false;
            }
        }

        /// <summary>触摸点数量，同 Unity Input.touchCount</summary>
        public static int TouchCount => Touch != null ? Touch.TouchCount : 0;

        /// <summary>获取触摸点，同 Unity Input.GetTouch(i)</summary>
        public static KTouch GetTouch(int index) => Touch.GetTouch(index);

        /// <summary>鼠标屏幕坐标</summary>
        public static Vector2 MousePosition => Mouse != null ? Mouse.Position : Vector2.Zero;

        /// <summary>鼠标滚轮本帧增量</summary>
        public static int ScrollDelta => Mouse != null ? Mouse.ScrollDelta : 0;

        /// <summary>指针是否停在 UI 上</summary>
        public static bool IsPointerOverUI => Pointer != null && Pointer.IsPointerOverUI;

        /// <summary>
        /// 移动轴：键盘 WASD / 方向键 与 手柄左摇杆合并，Y 向下为正
        /// </summary>
        public static Vector2 GetMoveAxis()
        {
            Vector2 axis = Keyboard != null ? Keyboard.GetAxis() : Vector2.Zero;

            if (axis == Vector2.Zero && GamePad != null && GamePad.IsConnected())
            {
                axis = GamePad.GetLeftStick();
                if (axis == Vector2.Zero) axis = GamePad.GetDPadAxis();
            }

            return axis;
        }

        /// <summary>
        /// 退出键：Esc 或 手柄 Back
        /// </summary>
        public static bool GetQuitPressed()
        {
            if (Keyboard != null && Keyboard.GetKeyDown(Keys.Escape)) return true;
            if (GamePad != null && GamePad.GetButtonDown(Buttons.Back)) return true;
            return false;
        }

    }
}
