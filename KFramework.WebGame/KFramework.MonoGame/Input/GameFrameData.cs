using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 每帧事件分发器：C# 每帧只跨界【一次】取回全部输入模块的事件，
    /// 再按模块编号把 payload <b>原样</b>转交对应的 Input_* 类解析。
    /// <para>
    /// 各输入模块依旧独立：监听、状态、以及 payload 格式都保持原样，
    /// 合并的只是这条"取数据"的通道（原先一帧 3 次跨界，现在 1 次）。
    /// </para>
    /// </summary>
    public static class GameFrameData
    {
        /// <summary>事件来源模块编号（与 input_common.ts 的 ModuleId 一致）。</summary>
        public static class ModuleId
        {
            public const int System = 0;
            public const int Keyboard = 1;
            public const int Mouse = 2;
            public const int Touch = 3;
            public const int Ime = 4;
        }

        /// <summary>系统事件种类（与 game_frame_take_js_data.ts 的 Sys* 一致）。</summary>
        public static class SystemEvent
        {
            public const int FocusLost = 0;
            public const int FocusGained = 1;
            public const int PageHidden = 2;
            public const int PageVisible = 3;
        }

        /// <summary>焦点变化（true = 获得焦点）。触发时各输入模块已先行清空/复位。</summary>
        public static event Action<bool> FocusChanged;

        // 与 TS 侧 MAX_BYTES 对齐：触摸每触点 16B、最多 64 条即 1KB，2048 留足余量。
        private const int MaxBytes = 2048;
        private static byte[]? _pending;
        private static bool _bound;

        /// <summary>
        /// 接收帧回调带来的本帧事件（由 <see cref="JSBind_GameUpdate.Frame"/> 调用，早于本帧任何 Update 步）。
        /// 数据在这里暂存，等首个 <see cref="Update"/> 消费 —— 这样每帧只有一次跨界。
        /// </summary>
        internal static void Receive(byte[]? events)
        {
            // 懒绑定：在帧回调入口就挂上窗口焦点 / 页面可见性监听，
            // 必须早于第一次取数据，否则首帧的系统事件会被漏掉。
            if (!_bound)
            {
                JSBind_GameFrameData.BindFrameEvents();
                _bound = true;
            }

            _pending = events;
        }

        /// <summary>每帧的每个 Update 步调用一次（由 Input.Update 驱动），早于各模块的边沿计算。</summary>
        public static void Update()
        {
            // 取走本帧事件并清空：固定步长下一帧可能跑多个 Update 步，
            // 只有首个步能拿到数据，后续步为空 —— 与原先"每步各 poll 一次"的行为一致。
            ReadOnlySpan<byte> events = _pending;
            _pending = null;

            ReadOnlySpan<byte> kb = default, mouse = default, touch = default;
            bool hasSystem = false;
            int systemKind = -1;

            // 本帧无数据（或已被首个 Update 步消费）时 count 为 0，循环不执行，
            // 但下面仍会无条件分发一次 —— 各模块的"每帧清理"依赖于此。
            int count = events.Length > 0 ? events[0] : 0;
            int off = 1;
            for (int i = 0; i < count; i++)
            {
                if (off + 3 > events.Length) break;
                int module = events[off];
                int len = events[off + 1] | (events[off + 2] << 8);
                if (off + 3 + len > events.Length) break;      // 长度越界即停，绝不解析半截事件

                ReadOnlySpan<byte> payload = events.Slice(off + 3, len);
                switch (module)
                {
                    case ModuleId.System:
                        hasSystem = true;
                        systemKind = payload.Length > 0 ? payload[0] : -1;
                        break;
                    case ModuleId.Keyboard: kb = payload; break;
                    case ModuleId.Mouse: mouse = payload; break;
                    case ModuleId.Touch: touch = payload; break;
                }
                off += 3 + len;
            }

            // 系统事件【先行】：失焦要让所有模块清空，必须早于它们解析本帧残留的事件，
            // 否则"窗外松手"那一帧仍会把按键电平写成按下，状态又卡住了。
            if (hasSystem) DispatchSystem(systemKind);

            // 无条件分发到每个模块：本帧没事件也要走一次 ——
            // 滚轮增量归零这类"每帧清理"就写在各模块的 Consume 开头。
            Input_KeyBoard.Consume(kb);
            Input_Mouse.Consume(mouse);
            Input_Touch.Consume(touch);
        }

        private static void DispatchSystem(int kind)
        {
            if (kind < 0) return;

            bool lost = kind is SystemEvent.FocusLost or SystemEvent.PageHidden;
            if (lost)
            {
                // 失焦：按键会在窗口外松手后收不到 keyup，指针会被系统手势接管而收不到 mouseup。
                // 与其每个模块各自去监听 window.blur，不如在这里统一清空一次 ——
                // 这正是"所有输入模块都需要失焦事件"的那个共同需求。
                Input_KeyBoard.Reset();
                Input_Mouse.Reset();
                Input_Touch.Reset();
            }

            FocusChanged?.Invoke(!lost);
        }
    }
}
