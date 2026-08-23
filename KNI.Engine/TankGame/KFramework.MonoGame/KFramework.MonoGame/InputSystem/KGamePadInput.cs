using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 手柄输入设备，支持四个玩家
    /// </summary>
    public class KGamePadInput : IKInputDevice
    {
        public const int MaxPlayers = 4;

        public string Name => "GamePad";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => true;

        private readonly GamePadState[] _prev = new GamePadState[MaxPlayers];
        private readonly GamePadState[] _curr = new GamePadState[MaxPlayers];

        /// <summary>死区阈值，摇杆小于此值视为 0</summary>
        public float DeadZone { get; set; } = 0.2f;

        /// <summary>按键按下（参数：玩家、按键）</summary>
        public event Action<PlayerIndex, Buttons> ButtonDown;

        /// <summary>按键抬起（参数：玩家、按键）</summary>
        public event Action<PlayerIndex, Buttons> ButtonUp;

        /// <summary>手柄连接状态变化（参数：玩家、是否已连接）</summary>
        public event Action<PlayerIndex, bool> ConnectionChanged;

        private static readonly Buttons[] AllButtons =
        {
            Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
            Buttons.Start, Buttons.Back, Buttons.BigButton,
            Buttons.LeftShoulder, Buttons.RightShoulder,
            Buttons.LeftStick, Buttons.RightStick,
            Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
            Buttons.LeftTrigger, Buttons.RightTrigger,
        };

        public void Init()
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                _curr[i] = GamePad.GetState((PlayerIndex)i);
                _prev[i] = _curr[i];
            }
        }

        public void Update(GameTime gameTime)
        {
            for (int i = 0; i < MaxPlayers; i++)
            {
                _prev[i] = _curr[i];
                _curr[i] = GamePad.GetState((PlayerIndex)i);

                var player = (PlayerIndex)i;

                if (_curr[i].IsConnected != _prev[i].IsConnected)
                    ConnectionChanged?.Invoke(player, _curr[i].IsConnected);

                if (!_curr[i].IsConnected) continue;

                for (int b = 0; b < AllButtons.Length; b++)
                {
                    var btn = AllButtons[b];
                    bool now = _curr[i].IsButtonDown(btn);
                    bool before = _prev[i].IsButtonDown(btn);
                    if (now && !before) ButtonDown?.Invoke(player, btn);
                    else if (!now && before) ButtonUp?.Invoke(player, btn);
                }
            }
        }

        public void Reset()
        {
            Init();
        }

        /// <summary>手柄是否已连接</summary>
        public bool IsConnected(PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].IsConnected;

        /// <summary>按键是否按住</summary>
        public bool GetButton(Buttons button, PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].IsButtonDown(button);

        /// <summary>按键是否本帧刚按下</summary>
        public bool GetButtonDown(Buttons button, PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].IsButtonDown(button) && _prev[(int)player].IsButtonUp(button);

        /// <summary>按键是否本帧刚抬起</summary>
        public bool GetButtonUp(Buttons button, PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].IsButtonUp(button) && _prev[(int)player].IsButtonDown(button);

        public KPressState GetButtonState(Buttons button, PlayerIndex player = PlayerIndex.One)
        {
            bool now = _curr[(int)player].IsButtonDown(button);
            bool before = _prev[(int)player].IsButtonDown(button);
            if (now && !before) return KPressState.Down;
            if (now) return KPressState.Held;
            if (before) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>左摇杆，已应用死区。Y 已翻转为屏幕方向（向下为正）</summary>
        public Vector2 GetLeftStick(PlayerIndex player = PlayerIndex.One)
        {
            var v = _curr[(int)player].ThumbSticks.Left;
            return ApplyDeadZone(new Vector2(v.X, -v.Y));
        }

        /// <summary>右摇杆，已应用死区。Y 已翻转为屏幕方向（向下为正）</summary>
        public Vector2 GetRightStick(PlayerIndex player = PlayerIndex.One)
        {
            var v = _curr[(int)player].ThumbSticks.Right;
            return ApplyDeadZone(new Vector2(v.X, -v.Y));
        }

        /// <summary>左扳机，0~1</summary>
        public float GetLeftTrigger(PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].Triggers.Left;

        /// <summary>右扳机，0~1</summary>
        public float GetRightTrigger(PlayerIndex player = PlayerIndex.One)
            => _curr[(int)player].Triggers.Right;

        /// <summary>方向键组成的二维轴，Y 向下为正</summary>
        public Vector2 GetDPadAxis(PlayerIndex player = PlayerIndex.One)
        {
            var dpad = _curr[(int)player].DPad;
            float x = 0f, y = 0f;
            if (dpad.Left == ButtonState.Pressed) x -= 1f;
            if (dpad.Right == ButtonState.Pressed) x += 1f;
            if (dpad.Up == ButtonState.Pressed) y -= 1f;
            if (dpad.Down == ButtonState.Pressed) y += 1f;
            return new Vector2(x, y);
        }

        /// <summary>震动，0~1</summary>
        public void SetVibration(float left, float right, PlayerIndex player = PlayerIndex.One)
        {
            GamePad.SetVibration(player, MathHelper.Clamp(left, 0f, 1f), MathHelper.Clamp(right, 0f, 1f));
        }

        /// <summary>停止所有手柄震动</summary>
        public void StopAllVibration()
        {
            for (int i = 0; i < MaxPlayers; i++)
                GamePad.SetVibration((PlayerIndex)i, 0f, 0f);
        }

        private Vector2 ApplyDeadZone(Vector2 v)
        {
            if (v.Length() < DeadZone) return Vector2.Zero;
            return v;
        }
    }
}
