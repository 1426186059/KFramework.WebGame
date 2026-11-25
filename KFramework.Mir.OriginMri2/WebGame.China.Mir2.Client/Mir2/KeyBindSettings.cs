using System.Threading.Tasks;
using WebGame.Mir2.MonoGame.Client;

namespace Client
{

    public enum KeybindOptions : int
    {
        Bar1Skill1 = 0,
        Bar1Skill2,
        Bar1Skill3,
        Bar1Skill4,
        Bar1Skill5,
        Bar1Skill6,
        Bar1Skill7,
        Bar1Skill8,
        Bar2Skill1,
        Bar2Skill2,
        Bar2Skill3,
        Bar2Skill4,
        Bar2Skill5,
        Bar2Skill6,
        Bar2Skill7,
        Bar2Skill8,
        Inventory,
        Inventory2,
        Equipment,
        Equipment2,
        Skills,
        Skills2,
        Creature,
        MountWindow,
        Mount,
        Fishing,
        Skillbar,
        Mentor,
        Relationship,
        Friends,
        Guilds,
        GameShop,
        Quests,
        Closeall,
        Options,
        Options2,
        Group,
        Belt,
        BeltFlip,
        Pickup,
        Belt1,
        Belt1Alt,
        Belt2,
        Belt2Alt,
        Belt3,
        Belt3Alt,
        Belt4,
        Belt4Alt,
        Belt5,
        Belt5Alt,
        Belt6,
        Belt6Alt,
        Belt7,
        Belt7Alt,
        Belt8,
        Belt8Alt,
        Logout,
        Exit,
        CreaturePickup,
        CreatureAutoPickup,
        Minimap,
        Bigmap,
        Trade,
        Rental,
        ChangeAttackmode,
        AttackmodePeace,
        AttackmodeGroup,
        AttackmodeGuild,
        AttackmodeEnemyguild,
        AttackmodeRedbrown,
        AttackmodeAll,
        ChangePetmode,
        PetmodeBoth,
        PetmodeMoveonly,
        PetmodeAttackonly,
        PetmodeNone,
        Help,
        Keybind,
        Autorun,
        Cameramode,
        Screenshot,
        DropView,
        TargetDead,
        Ranking,
        AddGroupMember,
        HeroSkill1,
        HeroSkill2,
        HeroSkill3,
        HeroSkill4,
        HeroSkill5,
        HeroSkill6,
        HeroSkill7,
        HeroSkill8,
        HeroInventory,
        HeroEquipment,
        HeroSkills,
        TargetSpellLockOn,
        PetmodeFocusMasterTarget
    }

    public class KeyBind
    {
        public KeybindOptions function = KeybindOptions.Bar1Skill1;
        public string Group = "", Description = "";
        public Keys Key = 0;

        /// <summary>
        /// Require Options : 0 = Require unpressed key, 1 = Require pressed key, 2 = Don't care
        /// </summary>
        public byte RequireCtrl = 0;
        public byte RequireShift = 0;
        public byte RequireAlt = 0;
        public byte RequireTilde = 0;
    }


    public class KeyBindSettings
    {
        private static InIReader Reader = new InIReader(@".\KeyBinds.ini");
        public List<KeyBind> Keylist = new List<KeyBind>();
        public List<KeyBind> DefaultKeylist = new List<KeyBind>();

        public KeyBindSettings()
        {
            New(Keylist);
            New(DefaultKeylist);
        }

        // 浏览器端：InIReader 改为走 IndexedDB，加载为异步。
        public async Task LoadAsync()
        {
            await Reader.LoadAsync().ConfigureAwait(false);
            if (Reader.IsEmpty)
            {
                Reader.AutoPersist = false;
                Save(DefaultKeylist); // 仅写入内存（不逐条落库）
                Reader.AutoPersist = true;
                Reader.Save();        // 把默认键位持久化到浏览器 DB 一次
            }
            Reader.AutoPersist = false;
            Load();                  // 从已加载内容填充 Keylist
            MigrateSkillbarKeys();   // 旧存档把第一列绑成 F1..F8，迁移为 数字1..8 / F1..F8
            MigrateBeltKeys();       // 旧存档快捷栏是 数字1..5，迁移为 Q/W/E/R/T，并废弃冲突功能键
            Reader.AutoPersist = true;
        }

        /// <summary>
        /// 旧版默认把第一列(Bar1Skill1..8)绑成 F1..F8、第二列(Bar2Skill1..8)绑成 Ctrl+F1..F8，
        /// 与现在的第一列=数字1..8、第二列=F1..F8 冲突。存档里仍是旧值时重置这两组并落库。
        /// 只在明确识别到旧默认时迁移，不覆盖玩家的其它自定义绑定。
        /// </summary>
        private void MigrateSkillbarKeys()
        {
            KeyBind b1 = Keylist.SingleOrDefault(x => x.function == KeybindOptions.Bar1Skill1);
            KeyBind b2 = Keylist.SingleOrDefault(x => x.function == KeybindOptions.Bar2Skill1);
            bool oldLayout = b1 != null && b2 != null && b1.Key == Keys.F1 && b2.Key == Keys.F1 && b2.RequireCtrl == 1;
            if (!oldLayout) return;

            KeyBind[] defaults = DefaultKeylist.Where(x =>
                x.function >= KeybindOptions.Bar1Skill1 && x.function <= KeybindOptions.Bar2Skill8).ToArray();
            foreach (KeyBind def in defaults)
            {
                KeyBind cur = Keylist.SingleOrDefault(x => x.function == def.function);
                if (cur == null) continue;
                cur.Key = def.Key;
                cur.RequireAlt = def.RequireAlt;
                cur.RequireShift = def.RequireShift;
                cur.RequireCtrl = def.RequireCtrl;
                cur.RequireTilde = def.RequireTilde;
            }
            Save(Keylist);   // 持久化迁移结果
        }

        /// <summary>
        /// 快捷栏历史绑定是数字 1..5（Belt1..Belt5 = Digit1..5），现改为 Q/W/E/R/T；
        /// 同时 Q/E/R/T 原本被 任务/宠物/技能栏/交易 占用，这几个功能键直接废弃(None)。
        /// 只在明确识别到旧布局时迁移，不覆盖玩家其它自定义绑定。
        /// </summary>
        private void MigrateBeltKeys()
        {
            KeyBind[] beltSlots =
            {
                Keylist.SingleOrDefault(x => x.function == KeybindOptions.Belt1),
                Keylist.SingleOrDefault(x => x.function == KeybindOptions.Belt2),
                Keylist.SingleOrDefault(x => x.function == KeybindOptions.Belt3),
                Keylist.SingleOrDefault(x => x.function == KeybindOptions.Belt4),
                Keylist.SingleOrDefault(x => x.function == KeybindOptions.Belt5),
            };
            // 只有 1 号格仍是旧的数字键才算旧存档（1 号原本=1，任意自定义过就不动）。
            if (beltSlots[0] == null || beltSlots[0].Key != Keys.Digit1) return;

            Keys[] newKeys = { Keys.KeyQ, Keys.KeyW, Keys.KeyE, Keys.KeyR, Keys.KeyT };
            for (int i = 0; i < beltSlots.Length; i++)
            {
                if (beltSlots[i] != null) beltSlots[i].Key = newKeys[i];
            }

            // 废弃占用 Q/E/R/T 的功能键。
            KeybindOptions[] abandoned =
            {
                KeybindOptions.Quests, KeybindOptions.Creature, KeybindOptions.Skillbar, KeybindOptions.Trade, KeybindOptions.Exit
            };
            foreach (KeybindOptions function in abandoned)
            {
                KeyBind key = Keylist.SingleOrDefault(x => x.function == function);
                if (key != null) key.Key = Keys.None;
            }

            Save(Keylist);   // 持久化迁移结果
        }

        public void Load()
        {
            foreach (KeyBind Inputkey in Keylist)
            {
                Inputkey.RequireAlt = Reader.ReadByte(Inputkey.function.ToString(), "RequireAlt", Inputkey.RequireAlt);
                Inputkey.RequireShift = Reader.ReadByte(Inputkey.function.ToString(), "RequireShift", Inputkey.RequireShift);
                Inputkey.RequireTilde = Reader.ReadByte(Inputkey.function.ToString(), "RequireTilde", Inputkey.RequireTilde);
                Inputkey.RequireCtrl = Reader.ReadByte(Inputkey.function.ToString(), "RequireCtrl", Inputkey.RequireCtrl);
                string Input = Reader.ReadString(Inputkey.function.ToString(), "RequireKey", Inputkey.Key.ToString());
                Enum.TryParse(Input, out Inputkey.Key);
                
            }
        }

        public void Save(List<KeyBind> keyList)
        {
            Reader.AutoPersist = false; // 批量写入期间不逐条落库
            Reader.Write("Guide", "01", "RequireAlt,RequireShift,RequireTilde,RequireCtrl");
            Reader.Write("Guide", "02", "have 3 options: 0/1/2");
            Reader.Write("Guide", "03", "0 < you cannot have this key pressed to use the function");
            Reader.Write("Guide", "04", "1 < you have to have this key pressed to use this function");
            Reader.Write("Guide", "05", "2 < it doesnt matter if you press this key to use this function");
            Reader.Write("Guide", "06", "by default just use 2, unless you have 2 functions on the same key");
            Reader.Write("Guide", "07", "example: change attack mode (ctrl+h) and help (h)");
            Reader.Write("Guide", "08", "if you set either of those to requireshift 2, then they wil both work at the same time or not work");
            Reader.Write("Guide", "09", "");
            Reader.Write("Guide", "10", "To get the value for RequireKey look at:");
            Reader.Write("Guide", "11", "https://msdn.microsoft.com/en-us/library/keys(v=vs.110).aspx");
        
            foreach (KeyBind Inputkey in keyList)
            {
                Reader.Write(Inputkey.function.ToString(), "RequireAlt", Inputkey.RequireAlt);
                Reader.Write(Inputkey.function.ToString(), "RequireShift", Inputkey.RequireShift);
                Reader.Write(Inputkey.function.ToString(), "RequireTilde", Inputkey.RequireTilde);
                Reader.Write(Inputkey.function.ToString(), "RequireCtrl", Inputkey.RequireCtrl);
                Reader.Write(Inputkey.function.ToString(), "RequireKey", Inputkey.Key.ToString());
            }
            Reader.AutoPersist = true;
            Reader.Save(); // 统一落库到浏览器 DB 一次
        }

        public void New(List<KeyBind> list)
        {
            KeyBind InputKey;
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("InventoryOpenClose"), function = KeybindOptions.Inventory, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.F9 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("InventoryOpenCloseAlt"), function = KeybindOptions.Inventory2, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyI };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("EquipmentOpenClose"), function = KeybindOptions.Equipment, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.F10 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("EquipmentOpenCloseAlt"), function = KeybindOptions.Equipment2, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyC };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("SkillsOpenClose"), function = KeybindOptions.Skills, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.F11 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("SkillsOpenCloseAlt"), function = KeybindOptions.Skills2, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyS };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("HeroInventoryOpenClose"), function = KeybindOptions.HeroInventory, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyI };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("HeroEquipmentOpenClose"), function = KeybindOptions.HeroEquipment, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyC };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("HeroSkillsOpenClose"), function = KeybindOptions.HeroSkills, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyS };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("CreaturesOpenClose"), function = KeybindOptions.Creature, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("MountOpenClose"), function = KeybindOptions.MountWindow, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyJ };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("FishingOpenClose"), function = KeybindOptions.Fishing, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyN };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("SkillbarOpenClose"), function = KeybindOptions.Skillbar, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("MentorOpenClose"), function = KeybindOptions.Mentor, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("RelationshipOpenClose"), function = KeybindOptions.Relationship, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyL };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("FriendsOpenClose"), function = KeybindOptions.Friends, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyF };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("GuildOpenClose"), function = KeybindOptions.Guilds, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyG };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("GameshopOpenClose"), function = KeybindOptions.GameShop, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyY };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("QuestDiaryOpenClose"), function = KeybindOptions.Quests, RequireAlt = 0, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("OptionsOpenClose"), function = KeybindOptions.Options, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.F12 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("OptionsOpenCloseAlt"), function = KeybindOptions.Options2, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyO };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("GroupOpenClose"), function = KeybindOptions.Group, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyP };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("BeltOpenClose"), function = KeybindOptions.Belt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyZ };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("MinimapOpenClose"), function = KeybindOptions.Minimap, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyV };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("BigmapOpenClose"), function = KeybindOptions.Bigmap, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyB };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("RankingOpenClose"), function = KeybindOptions.Ranking, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyK };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("HelpOpenClose"), function = KeybindOptions.Help, RequireAlt = 2, RequireShift = 0, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyH };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("KeybindsOpenClose"), function = KeybindOptions.Keybind, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyU };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Dialogs", Description = LanguageMgr.Get("CloseAllWindows"), function = KeybindOptions.Closeall, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Escape };
            list.Add(InputKey);

            // 第一列（标注 1..8）= 数字键 1..8；第二列（标注 F1..F8）= F1..F8。
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 1", function = KeybindOptions.Bar1Skill1, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit1 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 2", function = KeybindOptions.Bar1Skill2, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit2 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 3", function = KeybindOptions.Bar1Skill3, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit3 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 4", function = KeybindOptions.Bar1Skill4, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit4 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 5", function = KeybindOptions.Bar1Skill5, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit5 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 6", function = KeybindOptions.Bar1Skill6, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit6 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 7", function = KeybindOptions.Bar1Skill7, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit7 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarSlot")+" 8", function = KeybindOptions.Bar1Skill8, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 0, Key = Keys.Digit8 };
            list.Add(InputKey);

            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 1", function = KeybindOptions.Bar2Skill1, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F1 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 2", function = KeybindOptions.Bar2Skill2, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F2 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 3", function = KeybindOptions.Bar2Skill3, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F3 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 4", function = KeybindOptions.Bar2Skill4, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F4 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 5", function = KeybindOptions.Bar2Skill5, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F5 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 6", function = KeybindOptions.Bar2Skill6, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F6 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 7", function = KeybindOptions.Bar2Skill7, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F7 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("SkillbarAltSlot")+" 8", function = KeybindOptions.Bar2Skill8, RequireAlt = 2, RequireShift = 0, RequireTilde = 0, RequireCtrl = 2, Key = Keys.F8 };
            list.Add(InputKey);

            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 1", function = KeybindOptions.HeroSkill1, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F1 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 2", function = KeybindOptions.HeroSkill2, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F2 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 3", function = KeybindOptions.HeroSkill3, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F3 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 4", function = KeybindOptions.HeroSkill4, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F4 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 5", function = KeybindOptions.HeroSkill5, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F5 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 6", function = KeybindOptions.HeroSkill6, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F6 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 7", function = KeybindOptions.HeroSkill7, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F7 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Skillbar", Description = LanguageMgr.Get("HeroSkillbarSlot")+" 8", function = KeybindOptions.HeroSkill8, RequireAlt = 2, RequireShift = 1, RequireTilde = 0, RequireCtrl = 0, Key = Keys.F8 };
            list.Add(InputKey);

            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("RotateBelt"), function = KeybindOptions.BeltFlip, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyZ };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",1), function = KeybindOptions.Belt1, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyQ };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",1), function = KeybindOptions.Belt1Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad1 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",2), function = KeybindOptions.Belt2, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyW };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",2), function = KeybindOptions.Belt2Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad2 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",3), function = KeybindOptions.Belt3, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyE };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",3), function = KeybindOptions.Belt3Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad3 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",4), function = KeybindOptions.Belt4, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyR };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",4), function = KeybindOptions.Belt4Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad4 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",5), function = KeybindOptions.Belt5, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyT };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",5), function = KeybindOptions.Belt5Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad5 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",6), function = KeybindOptions.Belt6, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Digit6 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",6), function = KeybindOptions.Belt6Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad6 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",7), function = KeybindOptions.Belt7, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Digit7 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",7), function = KeybindOptions.Belt7Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad7 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlot",8), function = KeybindOptions.Belt8, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Digit8 };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Belt", Description = LanguageMgr.Get("BeltSlotAlt",8), function = KeybindOptions.Belt8Alt, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Numpad8 };
            list.Add(InputKey);

            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("Logout"), function = KeybindOptions.Logout, RequireAlt = 1, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyX };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("Exit"), function = KeybindOptions.Exit, RequireAlt = 1, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("MountDismount"), function = KeybindOptions.Mount, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyM };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("PickupFloorItem"), function = KeybindOptions.Pickup, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Tab };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("CreatureItemPickup"), function = KeybindOptions.CreaturePickup, RequireAlt = 0, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyX };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("CreatureAutoPickup"), function = KeybindOptions.CreatureAutoPickup, RequireAlt = 1, RequireShift = 2, RequireTilde = 2, RequireCtrl = 0, Key = Keys.KeyA };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("RequestTrade"), function = KeybindOptions.Trade, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "General", Description = LanguageMgr.Get("RecruitGroupMember"), function = KeybindOptions.AddGroupMember, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyG };
            list.Add(InputKey);

            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("ToggleAttackMode"), function = KeybindOptions.ChangeAttackmode, RequireAlt = 2, RequireShift = 0, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyH };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModePeace"), function = KeybindOptions.AttackmodePeace, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModeGroup"), function = KeybindOptions.AttackmodeGroup, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModeGuild"), function = KeybindOptions.AttackmodeGuild, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModeEnemyGuild"), function = KeybindOptions.AttackmodeEnemyguild, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModeRedBrown"), function = KeybindOptions.AttackmodeRedbrown, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetAttackModeAll"), function = KeybindOptions.AttackmodeAll, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("TogglePetMode"), function = KeybindOptions.ChangePetmode, RequireAlt = 0, RequireShift = 2, RequireTilde = 2, RequireCtrl = 1, Key = Keys.KeyA };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetPetModeBoth"), function = KeybindOptions.PetmodeBoth, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetPetModeMoveOnly"), function = KeybindOptions.PetmodeMoveonly, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetPetModeAttackOnly"), function = KeybindOptions.PetmodeAttackonly, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetPetModeNone"), function = KeybindOptions.PetmodeNone, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("SetPetModeFocusMasterTarget"), function = KeybindOptions.PetmodeFocusMasterTarget, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("ToggleAutorun"), function = KeybindOptions.Autorun, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.KeyD };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("ToggleCameraMode"), function = KeybindOptions.Cameramode, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Insert };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("TakeScreenshot"), function = KeybindOptions.Screenshot, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.PrintScreen };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Toggle", Description = LanguageMgr.Get("ToggleDropview"), function = KeybindOptions.DropView, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.Tab };
            list.Add(InputKey);
            InputKey = new KeyBind { Group = "Combat", Description = LanguageMgr.Get("HoldEnableTargetSpellLockOn"), function = KeybindOptions.TargetSpellLockOn, RequireAlt = 2, RequireShift = 2, RequireTilde = 2, RequireCtrl = 2, Key = Keys.None };
            list.Add(InputKey);
        }

        public string GetKey(KeybindOptions Option, bool defaultKey = false)
        {
            List<KeyBind> lst;

            if (defaultKey) lst = CMain.InputKeys.DefaultKeylist;
            else lst = CMain.InputKeys.Keylist;

            string output = "";
            for (int i = 0; i < lst.Count; i++)
            {
                if (lst[i].function == Option)
                {
                    if (lst[i].Key == Keys.None) return output;
                    if (lst[i].RequireAlt == 1)
                        output = "Alt";
                    if (lst[i].RequireCtrl == 1)
                        output = output != "" ? output + " + Ctrl" : "Ctrl";
                    if (lst[i].RequireShift == 1)
                        output = output != "" ? output + " + Shift" : "Shift";
                    if (lst[i].RequireTilde == 1)
                        output = output != "" ? output + " + ~" : "~";

                    output = output != "" ? output + " + " + lst[i].Key.ToString() : lst[i].Key.ToString();
                    return output;
                }
            }
            return "";
        }
    }

    
}
