using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using System.Text.RegularExpressions;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{

    public sealed class ChatDialog : MirImageControl
    {
        public List<ChatHistory> FullHistory = new List<ChatHistory>();
        public List<ChatHistory> History = new List<ChatHistory>();
        public List<MirLabel> ChatLines = new List<MirLabel>();
        private MirImageControl ChatHistoryPanel;

        public List<ChatItem> LinkedItems = new List<ChatItem>();
        public List<MirLabel> LinkedItemButtons = new List<MirLabel>();

        public MirButton SendChatBtn;
        public MirTextBox ChatTextBox;
        public MirButton ChatInputBtn;
        public Font ChatFont = FontConst.F10;

        public int StartIndex, LineCount = 7, WindowSize;
        // 私聊目标角色ID:由好友/伴侣等入口设置;非私聊为 0。取代原先把名字塞进消息文本的做法。
        public uint LastPrivateChatTargetId = 0;

        // 下拉/快捷键选定的“发送频道”;与 CurrentViewChannel(视图过滤)解耦,避免打字时误改视图。
        public ChatChannelFilter CurrentSendChannel = ChatChannelFilter.All;

        // 下拉频道选择后,聊天面板只显示该频道(综合=全部);发送频道由 ToggleChatFilter 一并设置。
        public ChatChannelFilter CurrentViewChannel = ChatChannelFilter.All;

        // 频道下拉相关(从 ChatControlBar 合并)
        private MirDropDownBox2 mChatChannel;
        private MirImageControl mChatChannel_BtnJianTou;

        private readonly List<string> ChatChannelList = new List<string>()
        {
            "综合", "世界", "私聊", "行会", "队伍", "附近"
        };

        // 与 ChatChannelList 一一对应:下拉选中第 i 项时,映射到对应的频道过滤类型。
        private static readonly ChatChannelFilter[] ChannelFilterMap = new ChatChannelFilter[]
        {
            ChatChannelFilter.All,         // 综合
            ChatChannelFilter.World,       // 世界
            ChatChannelFilter.PrivateChat, // 私聊
            ChatChannelFilter.Guild,       // 行会
            ChatChannelFilter.Team,        // 队伍
            ChatChannelFilter.Nearby,      // 附近(本作无独立过滤,归并到无特定频道)
        };

        public ChatDialog()
        {
            //Library = Libraries.Prguse;
            //Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, Settings.ScreenHeight - 97);
            PixelDetect = true;

            //AtlasNameKey = "y_homeUI-sheet";
            //AtlasFrame = "y_h_msbg";
            //AtlasFrame = "bg_liaotianzy_06";

            AutoSize = false;
            Size = new Size(600, 120);
            Anchor = EAnchorType.BottomCenter;
            AnchorPos = new Point(0, -6);
            ApplyAnchor();

            KeyDown += ChatPanel_KeyDown;
            MouseWheel += ChatPanel_MouseWheel;

            var mChatTextBoxBg = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_msbg",
                AutoSize = false,
                Size = new Size(470, 20),
                Parent = this,
            };
            mChatTextBoxBg.Location = mChatTextBoxBg.Bottom.Add(new Point(0, -5));

            var mChatTipLabel = new MirLabel
            {
                Parent = mChatTextBoxBg,
                Text = "请输入文字",
                Size = new Size(465, 20),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Location = new Point(5, 0),
                Opacity = 0.5f,
            };

            ChatTextBox = new MirTextBox
            {
                BackColour = CommonColor.DarkGray,
                ForeColour = CommonColor.Black,
                Parent = mChatTextBoxBg,
                Size = new Size(470, 20),
                Location = new Point(0, 0),
                MaxLength = Globals.MaxChatLength,
                Visible = false,
                Font = ChatFont,
            };
            ChatTextBox.TextBox.KeyPress += ChatTextBox_KeyPress;
            ChatTextBox.TextBox.KeyDown += ChatTextBox_KeyDown;
            ChatTextBox.TextBox.KeyUp += ChatTextBox_KeyUp;

            // 空按钮:覆盖在聊天输入框背景之上,点击聚焦并开始输入(类似按 Enter 打开聊天)
            ChatInputBtn = new MirButton
            {
                Parent = mChatTextBoxBg,
                Size = mChatTextBoxBg.Size,
                Location = new Point(0, 0),
                Sound = SoundList.ButtonA,
                Text = "",
                CenterText = true,
            };

            ChatInputBtn.Click += (o, e) =>
            {
                StartChatInput();
            };

            // 聊天文字容器:把聊天接收文字(ChatLines / 物品链接)归到其下,使其绘制在输入框与下拉列表之下,
            // 下拉展开时不再被聊天文字挡住。仅绘制、不拦截鼠标(点击/悬停穿透到下层控件)。
            ChatHistoryPanel = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = Size,
                NotControl = true,
            };

            // 频道下拉 + 发送按钮(从 ChatControlBar 合并进来)
            mChatChannel = new MirDropDownBox2()
            {
                BackColour = CommonColor.FromArgb(255, 15, 15, 15),
                ForeColour = CommonColor.White,
                Parent = this,
                Size = new Size(60, 30),
                Location = new Point(0, 90),
                Visible = true,
                Enabled = true,
                DropMode = 1,
            };
            mChatChannel.ValueChanged += (o, e) =>
            {
                // 写回选中项,按钮 Label 才显示当前频道
                mChatChannel.SelectedIndex = mChatChannel._WantedIndex;
                int idx = mChatChannel.SelectedIndex;
                if (idx >= 0 && idx < ChannelFilterMap.Length)
                    ToggleChatFilter(ChannelFilterMap[idx]);
            };
            mChatChannel.DropDownStateChanged += (s, e) =>
            {
                if (mChatChannel_BtnJianTou != null)
                {
                    // 展开时箭头朝上,收起时朝下(翻转是之前给 MirImageControl 加的 FlipY)
                    mChatChannel_BtnJianTou.FlipY = e.IsOpen;
                }
            };

            mChatChannel.Items = ChatChannelList;
            mChatChannel.MinimumOption = 0;
            mChatChannel.SelectedIndex = 0; // 默认显示第一个频道(综合)
            mChatChannel.DropDownButton.Library = null;
            mChatChannel.DropDownButton.Index = 0;
            mChatChannel.DropDownButton.AtlasNameKey = "y_homeUI-sheet";
            mChatChannel.DropDownButton.AtlasFrame = "btn_liaotian_11";
            mChatChannel.DropDownButton.PressedAtlasFrame = "btn_liaotian_12";
            mChatChannel.DropDownButton.Size = new Size(60, 19);
            mChatChannel.ButtonTextOffsetX = -10;
            mChatChannel_BtnJianTou = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_liaotian_13",
                Parent = mChatChannel.DropDownButton,
                Size = new Size(12, 9),
                NotControl = true,
            };
            mChatChannel_BtnJianTou.Location = mChatChannel_BtnJianTou.Right.Subtract(new Point(5, 0));

            SendChatBtn = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_liaotian_11",
                PressedAtlasFrame = "btn_liaotian_12",
                Location = new Point(540, 95),
                Parent = this,
                Sound = SoundList.ButtonA,
                Text = "发送",
                CenterText = true,
            };

            SendChatBtn.Click += (o, e) =>
            {
                SendChatMessage();
            };

            ToggleChatFilter(ChatChannelFilter.All);



        }

        public void SetChatText(string newText)
        {
            string newMsg = ChatTextBox.Text += newText;

            if (newMsg.Length > Globals.MaxChatLength) return;

            ChatTextBox.Text = newMsg;
            ChatTextBox.SetFocus();
            ChatTextBox.Visible = true;
            ChatTextBox.TextBox.SelectionLength = 0;
            ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
        }

        private void ChatTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    //KFramework.MonoGame.PrintTool.Log($"[Chat] ChatTextBox_KeyPress Enter Visible={ChatTextBox.Visible} Active={CMain.Instance?.ActiveControl?.GetType().Name}");
                    e.Handled = true;
                    SendChatMessage();
                    break;
                case Keys.Escape:
                    e.Handled = true;
                    ChatTextBox.Visible = false;
                    ChatTextBox.Text = string.Empty;
                    LinkedItems.Clear();
                    break;
            }
        }

        public void SendChatMessage()
        {
            string msg = ChatTextBox.Text;

            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.ToUpper() == "@LEVELEFFECT")
                    Settings.LevelEffect = !Settings.LevelEffect;

                if (msg.ToUpper() == "@TARGETDEAD")
                    Settings.TargetDead = !Settings.TargetDead;

                ChatType type = ViewChannelToType(CurrentSendChannel);
                uint targetId = 0;

                if (type == ChatType.PrivateChat)
                {
                    targetId = LastPrivateChatTargetId;
                }
                else
                {
                    // 非私聊:剥掉可能残留的旧版路由前缀(!/!!/!~),避免服务端重复解析;@ 命令前缀保留。
                    msg = StripLegacyPrefix(msg);
                    LastPrivateChatTargetId = 0;
                }

                Network.Enqueue(new C.Chat
                {
                    Message = msg,
                    LinkedItems = new List<ChatItem>(LinkedItems),
                    Type = type,
                    PrivateChatTargetId = targetId,
                });
            }

            // 无论是否发送,都收起输入框(空内容时等同于按 Enter 关闭输入),与 Enter 键行为一致
            ChatTextBox.Visible = false;
            ChatTextBox.Text = string.Empty;
            LinkedItems.Clear();
        }

        // 聚焦聊天输入框并开始输入(供"开始聊天"按钮与 Enter 打开聊天共用)
        public void StartChatInput()
        {
            ChatTextBox.SetFocus();
            // 频道已由下拉/快捷键显式选定(CurrentSendChannel),无需再往输入框塞前缀文本。
            ChatTextBox.Visible = true;
            ChatTextBox.TextBox.SelectionLength = 0;
            ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
        }

        public void ReceiveChat(string text, ChatType type)
        {
            Color foreColour, backColour;

            switch (type)
            {
                case ChatType.World:
                    backColour = CommonColor.Yellow;
                    foreColour = CommonColor.Black;
                    break;
                case ChatType.System:
                    backColour = CommonColor.Red;
                    foreColour = CommonColor.White;
                    break;
                case ChatType.Team:
                    backColour = CommonColor.White;
                    foreColour = CommonColor.Brown;
                    break;
                case ChatType.PrivateChat:
                    foreColour = CommonColor.CornflowerBlue;
                    backColour = CommonColor.White;
                    break;
                case ChatType.Guild:
                    backColour = CommonColor.White;
                    foreColour = CommonColor.Green;
                    break;
                default:
                    backColour = CommonColor.White;
                    foreColour = CommonColor.Black;
                    break;
            }

            List<string> chat = new List<string>();

            int chatWidth = Settings.Resolution != 800 ? 614 : 390;
            int index = 0;

            for (int i = 1; i < text.Length; i++)
            {
                if (i - index < 0) continue;

                if (TextRenderer.MeasureText(text.Substring(index, i - index), ChatFont, new Size(int.MaxValue, int.MaxValue)).Width > chatWidth)
                {
                    int offset = i - index;
                    int newIndex = i - 1;

                    var itemLinkMatches = RegexFunctions.ChatItemLinks.Matches(text.Substring(index)).Cast<Match>();

                    if (itemLinkMatches.Any())
                    {
                        var match = itemLinkMatches.SingleOrDefault(x => (x.Index < (i - index)) && (x.Index + x.Length > offset - 1));

                        if (match != null)
                        {
                            offset = match.Index;
                            newIndex = match.Index;
                        }
                    }

                    chat.Add(text.Substring(index, offset - 1));
                    index = newIndex;
                }
            }

            chat.Add(text.Substring(index, text.Length - index));

            // 在每条消息首行前加频道前缀,如【世界】【私聊】,便于区分来源
            string channelPrefix = GetChannelPrefix(type);
            if (chat.Count > 0 && !string.IsNullOrEmpty(channelPrefix))
                chat[0] = channelPrefix + chat[0];

            if (StartIndex == History.Count - LineCount)
                StartIndex += chat.Count;

            for (int i = 0; i < chat.Count; i++)
                FullHistory.Add(new ChatHistory { Text = chat[i], BackColour = backColour, ForeColour = foreColour, Type = type });

            Update();
        }

        // 频道 → 显示前缀
        private static string GetChannelPrefix(ChatType type)
        {
            switch (type)
            {
                case ChatType.World: return "【世界】";
                case ChatType.PrivateChat: return "【私聊】";
                case ChatType.Guild: return "【行会】";
                case ChatType.Team: return "【队伍】";
                case ChatType.Nearby: return "【附近】";
                case ChatType.System: return "【系统】";
                default: return "";
            }
        }

        // 视图频道 → 对应的消息类型(All 不会走到这里)
        private static ChatType ViewChannelToType(ChatChannelFilter filter)
        {
            switch (filter)
            {
                case ChatChannelFilter.World: return ChatType.World;
                case ChatChannelFilter.PrivateChat: return ChatType.PrivateChat;
                case ChatChannelFilter.Guild: return ChatType.Guild;
                case ChatChannelFilter.Team: return ChatType.Team;
                case ChatChannelFilter.Nearby: return ChatType.Nearby;
                case ChatChannelFilter.System: return ChatType.System;
                default: return ChatType.Nearby;
            }
        }

        // 剥掉非私聊消息里可能残留的旧版路由前缀(!/!!/!~);注意只判断 '!' 开头,不误伤 '@' 命令前缀。
        private static string StripLegacyPrefix(string msg)
        {
            if (msg.StartsWith("!~")) return msg.Substring(2).TrimStart();
            if (msg.StartsWith("!!")) return msg.Substring(2).TrimStart();
            if (msg.StartsWith("!")) return msg.Substring(1).TrimStart();
            return msg;
        }

        public override void Update()
        {
            base.Update();
            History = new List<ChatHistory>();

            for (int i = 0; i < FullHistory.Count; i++)
            {
                switch (FullHistory[i].Type)
                {
                    case ChatType.Nearby:
                        if (Settings.FilterNormalChat) continue;
                        break;
                    case ChatType.PrivateChat:
                        if (Settings.FilterWhisperChat) continue;
                        break;
                    case ChatType.World:
                        if (Settings.FilterShoutChat) continue;
                        break;
                    case ChatType.System:
                        if (Settings.FilterSystemChat) continue;
                        break;
                    case ChatType.Team:
                        if (Settings.FilterGroupChat) continue;
                        break;
                    case ChatType.Guild:
                        if (Settings.FilterGuildChat) continue;
                        break;
                }

                // 下拉频道视图过滤:选定具体频道时只显示该频道(综合=全部)
                if (CurrentViewChannel != ChatChannelFilter.All && FullHistory[i].Type != ViewChannelToType(CurrentViewChannel))
                    continue;

                History.Add(FullHistory[i]);
            }

            for (int i = 0; i < ChatLines.Count; i++)
                ChatLines[i].Dispose();

            for (int i = 0; i < LinkedItemButtons.Count; i++)
                LinkedItemButtons[i].Dispose();

            ChatLines.Clear();
            LinkedItemButtons.Clear();

            if (StartIndex >= History.Count) StartIndex = History.Count - 1;
            if (StartIndex < 0) StartIndex = 0;

            int y = 1;

            for (int i = StartIndex; i < History.Count; i++)
            {
                MirLabel temp = new MirLabel
                {
                    AutoSize = true,
                    BackColour = History[i].BackColour,
                    ForeColour = History[i].ForeColour,
                    Location = new Point(1, y),
                    OutLine = false,
                    Parent = ChatHistoryPanel,
                    Text = History[i].Text,
                    Font = ChatFont,
                };
                temp.MouseWheel += ChatPanel_MouseWheel;
                ChatLines.Add(temp);

                temp.Click += (o, e) =>
                {
                    if (!(o is MirLabel l)) return;

                    string[] parts = l.Text.Split(':', ' ');
                    if (parts.Length == 0) return;

                    string name = Regex.Replace(parts[0], "[^A-Za-z0-9]", "");

                    ChatTextBox.SetFocus();
                    ChatTextBox.Text = string.Format("/{0} ", name);
                    ChatTextBox.Visible = true;
                    ChatTextBox.TextBox.SelectionLength = 0;
                    ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
                };

                string currentLine = History[i].Text;

                int oldLength = currentLine.Length;

                Capture capture = null;

                foreach (Match match in RegexFunctions.ChatItemLinks.Matches(currentLine).Cast<Match>().OrderBy(o => o.Index).ToList())
                {
                    try
                    {
                        int offSet = oldLength - currentLine.Length;

                        capture = match.Groups[1].Captures[0];
                        string[] values = capture.Value.Split('/');
                        currentLine = currentLine.Remove(capture.Index - 1 - offSet, capture.Length + 2).Insert(capture.Index - 1 - offSet, values[0]);
                        string text = currentLine.Substring(0, capture.Index - 1 - offSet) + " ";
                        Size size = TextRenderer.MeasureText(text, temp.Font, new Size(temp.Size.Width, int.MaxValue));

                        ChatLink(values[0], ulong.Parse(values[1]), temp.Location.Add(new Point(size.Width - 10, 0)));
                    }
                    catch(Exception ex)
                    {
                        //Temporary debug to catch unknown error
                        CMain.SaveError(ex.ToString());
                        CMain.SaveError(currentLine);
                        CMain.SaveError(capture.Value);
                        throw;
                    }
                }

                temp.Text = currentLine;

                y += 16;
                if (i - StartIndex == LineCount - 1) break;
            }

        }

        private void ChatLink(string name, ulong uniqueID, Point p)
        {
            UserItem item = GameScene.ChatItemList.FirstOrDefault(x => x.UniqueID == uniqueID);

            if (item != null)
            {
                MirLabel temp = new MirLabel
                {
                    AutoSize = true,
                    Visible = true,
                    Parent = ChatHistoryPanel,
                    Location = p,
                    Text = name,
                    ForeColour = CommonColor.Blue,
                    Sound = SoundList.ButtonC,
                    Font = ChatFont,
                    OutLine = false,
                };

                temp.MouseEnter += (o, e) => temp.ForeColour = CommonColor.Red;
                temp.MouseLeave += (o, e) =>
                {
                    GameScene.Scene.DisposeItemLabel();
                    temp.ForeColour = CommonColor.Blue;
                };
                temp.MouseDown += (o, e) => temp.ForeColour = CommonColor.Blue;
                temp.MouseUp += (o, e) => temp.ForeColour = CommonColor.Red;

                temp.Click += (o, e) =>
                {
                    GameScene.Scene.CreateItemLabel(item);
                };

                LinkedItemButtons.Add(temp);
            }
        }


        private void ChatPanel_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.ArrowUp:
                    if (StartIndex == 0) return;
                    StartIndex--;
                    break;
                case Keys.Home:
                    if (StartIndex == 0) return;
                    StartIndex = 0;
                    break;
                case Keys.ArrowDown:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex++;
                    break;
                case Keys.End:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex = History.Count - 1;
                    break;
                case Keys.PageUp:
                    if (StartIndex == 0) return;
                    StartIndex -= LineCount;
                    break;
                case Keys.PageDown:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex += LineCount;
                    break;
                default:
                    return;
            }
            Update();
            e.Handled = true;
        }
        private void ChatPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (StartIndex == 0 && count >= 0) return;
            if (StartIndex == History.Count - 1 && count <= 0) return;

            StartIndex -= count;
            Update();
        }
        private void ChatTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            CMain.Shift = e.Shift;
            CMain.Alt = e.Alt;
            CMain.Ctrl = e.Control;

            switch (e.KeyCode)
            {
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                case Keys.Tab:
                    CMain.CMain_KeyUp(sender, e);
                    break;

            }
        }
        private void ChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            CMain.Shift = e.Shift;
            CMain.Alt = e.Alt;
            CMain.Ctrl = e.Control;

            switch (e.KeyCode)
            {
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                case Keys.Tab:
                    CMain.CMain_KeyDown(sender, e);
                    break;

            }
        }

        public void ToggleChatFilter(ChatChannelFilter filter)
        {
            // 下拉选定频道:既作为聊天面板视图过滤(只看该频道),也作为发送频道(显式下发给服务端,取代文本前缀)。
            CurrentViewChannel = filter;
            CurrentSendChannel = filter;

            // 重新渲染聊天面板以应用视图过滤
            Update();
        }

        public class ChatHistory
        {
            public string Text;
            public Color ForeColour, BackColour;
            public ChatType Type;
        }
    }
}
