using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘 —— 对原始输入事件的封装。
    ///
    /// <para>自己 poll 自己的事件队列（<c>input_keyboard</c> 模块），维护按键电平与
    /// 按下/抬起边沿，并对外提供查询。JS 侧只负责把原始事件入队，不做任何语义处理。</para>
    ///
    /// <para>用事件而不是"电平差分"算边沿，因此同一帧内按下又抬起也能被正确识别。</para>
    /// </summary>
    public static class Input_KeyBoard
    {
        // 事件类型与字节布局统一在 Input_GameFrameData.EvType（镜像 TS 的 html_event_type），
        // 本模块不再自己解析字节流，故这里没有本地的事件编号与缓冲尺寸常量。
        private static readonly bool[] _LastKeyState = new bool[byte.MaxValue];
        private static readonly bool[] _NewKeyState = new bool[byte.MaxValue];

        /// <summary>任意键 刚按下</summary>
        public static event Action<Keys> KeyDown;
        /// <summary>任意键 刚抬起</summary>
        public static event Action<Keys> KeyUp;
        /// <summary>任意键 持续按住</summary>
        public static event Action<Keys> KeyPress;

        /// <summary>本装置是否处于激活状态；未激活时 <see cref="Update"/> / <see cref="LateUpdate"/> 直接跳过。由 <see cref="Activate"/> / <see cref="Unbind"/> 维护。</summary>
        public static bool Active { get; private set; }

        /// <summary>收到一次按键电平变化（由 <see cref="Input_GameFrameData"/> 按事件类型分发）。</summary>
        internal static void OnKey(byte keys, bool down)
        {
            if (!Active) return;
            _NewKeyState[keys] = down;
        }

        // 不再有 OnBlur：键盘失焦已并进 SysFocusLost，由 Input_GameFrameData 分发时调 ResetAll 统一清空
        // （本类的 Reset 已在 ResetAll 里）。JS 侧那条 KeyBlur 事件也已取消。

        /// <summary>
        /// 边沿计算：本帧电平与上帧电平的差分产生按下 / 抬起，电平为真的持续触发 KeyPress。
        /// 由 <see cref="Input_GameFrameData.Update"/> 在分发完本帧事件后调用一次。
        /// </summary>
        internal static void EndFrame()
        {
            if (!Active) return;

            //在LateUpdate里已经拷贝过了,这里不再拷贝
            //_NewKeyState.AsSpan().CopyTo(_LastKeyState);

            for(int i = 0; i < byte.MaxValue; i++)
            {
                if (_LastKeyState[i] != _NewKeyState[i])
                {
                    if (_NewKeyState[i])
                    {
                        KeyDown?.Invoke((Keys)i);
                    }
                    else
                    {
                        KeyUp?.Invoke((Keys)i);
                    }
                }

                if (_NewKeyState[i])
                {
                    KeyPress?.Invoke((Keys)i);
                }
            }
        }

        /// <summary>清空键盘状态（失焦时由 <see cref="Input_GameFrameData"/> 的 ResetAll 触发）。</summary>
        public static void Reset()
        {
            _NewKeyState.AsSpan().Clear();
            _LastKeyState.AsSpan().Clear();
        }

        /// <summary>固定步长下，一个渲染帧可能跑多个 Update 步；在每个步结束后清空按下/抬起边沿，
        /// 确保一次按键只被识别一次（否则边沿会在多个步里重复触发，导致"按一次"的逻辑随帧时序抖动）。
        /// 下一帧 <see cref="Update"/> 时边沿重新产生。</summary>
        public static void LateUpdate()
        {
            if (!Active) return;
            _NewKeyState.AsSpan().CopyTo(_LastKeyState);
        }

        /// <summary>解绑 JS 侧监听，并置 <see cref="Active"/> 为 false（关闭本装置采集）。</summary>
        public static void Deactivate()
        {
            Reset();
            JSBind_Input_Keyboard.UnbindKeyboard();
            Active = false;
        }

        /// <summary>激活装置：建立 / 恢复 JS 侧键盘监听（重新绑定到画布）。</summary>
        public static void Activate(bool bUseCanvasListener = true)
        {
            Reset();
            JSBind_Input_Keyboard.BindKeyboard(bUseCanvasListener ? GraphicsDevice.Canvas.Id : null);
            Active = true;
        }

        public static bool GetKey(Keys key)
        {
            return _NewKeyState[(byte)key];
        }

        public static bool GetKeyDown(Keys key)
        {
            return _NewKeyState[(byte)key] && _NewKeyState[(byte)key] != _LastKeyState[(byte)key];
        }

        public static bool GetKeyUp(Keys key)
        {
            return !_NewKeyState[(byte)key] && _NewKeyState[(byte)key] != _LastKeyState[(byte)key];
        }

        public static bool AnyKey
        {
            get
            {
                for (int i = 0; i < byte.MaxValue; i++)
                    if (GetKey((Keys)i)) return true;
                return false;
            }
        }

        public static bool AnyKeyDown
        {
            get
            {
                for (int i = 0; i < byte.MaxValue; i++)
                    if (GetKeyDown((Keys)i)) return true;
                return false;
            }
        }

        public static KPressState GetKeyState(Keys key)
        {
            if (GetKeyDown(key)) return KPressState.Down;
            if (GetKey(key)) return KPressState.Held;
            if (GetKeyUp(key)) return KPressState.Up;
            return KPressState.None;
        }

        public static bool Shift => GetKey(Keys.ShiftLeft) || GetKey(Keys.ShiftRight);
        public static bool Ctrl => GetKey(Keys.ControlLeft) || GetKey(Keys.ControlRight);
        public static bool Alt => GetKey(Keys.AltLeft) || GetKey(Keys.AltRight);

        /// <summary>WASD / 方向键组成的二维轴，Y 向下为正。</summary>
        public static Vector2 GetAxis()
        {
            float x = 0f, y = 0f;
            if (GetKey(Keys.KeyA) || GetKey(Keys.ArrowLeft)) x -= 1f;
            if (GetKey(Keys.KeyD) || GetKey(Keys.ArrowRight)) x += 1f;
            if (GetKey(Keys.KeyW) || GetKey(Keys.ArrowUp)) y -= 1f;
            if (GetKey(Keys.KeyS) || GetKey(Keys.ArrowDown)) y += 1f;
            return new Vector2(x, y);
        }
    }
}
