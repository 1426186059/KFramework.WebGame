using System.Text.Json.Nodes;

namespace MapInfoEditor
{
    /// <summary>把 MapInfo 的常用/规则字段暴露给 PropertyGrid 编辑，改动直接写回 JsonNode。</summary>
    public class MapGeneralVM
    {
        private readonly MapEntry _e;
        public MapGeneralVM(MapEntry e) { _e = e; }
        private JsonObject N => _e.Node;

        public int Index => _e.Index;
        public string FileName { get => N["FileName"]?.GetValue<string>() ?? ""; set => N["FileName"] = JsonValue.Create(value); }
        public string Title { get => N["Title"]?.GetValue<string>() ?? ""; set => N["Title"] = JsonValue.Create(value); }
        public int MiniMap { get => N["MiniMap"]?.GetValue<int>() ?? 0; set => N["MiniMap"] = JsonValue.Create(value); }
        public int BigMap { get => N["BigMap"]?.GetValue<int>() ?? 0; set => N["BigMap"] = JsonValue.Create(value); }
        public int Music { get => N["Music"]?.GetValue<int>() ?? 0; set => N["Music"] = JsonValue.Create(value); }
        public int Light { get => N["Light"]?.GetValue<int>() ?? 0; set => N["Light"] = JsonValue.Create(value); }
        public string NoReconnectMap { get => N["NoReconnectMap"]?.GetValue<string>() ?? ""; set => N["NoReconnectMap"] = JsonValue.Create(value); }

        private bool GetB(string k) => N[k]?.GetValue<bool>() ?? false;
        private void SetB(string k, bool v) => N[k] = JsonValue.Create(v);

        public bool NoTeleport { get => GetB("NoTeleport"); set => SetB("NoTeleport", value); }
        public bool NoReconnect { get => GetB("NoReconnect"); set => SetB("NoReconnect", value); }
        public bool NoRecall { get => GetB("NoRecall"); set => SetB("NoRecall", value); }
        public bool NoRandom { get => GetB("NoRandom"); set => SetB("NoRandom", value); }
        public bool NoEscape { get => GetB("NoEscape"); set => SetB("NoEscape", value); }
        public bool NoDrug { get => GetB("NoDrug"); set => SetB("NoDrug", value); }
        public bool NoFight { get => GetB("NoFight"); set => SetB("NoFight", value); }
        public bool NoDropMonster { get => GetB("NoDropMonster"); set => SetB("NoDropMonster", value); }
        public bool NoNames { get => GetB("NoNames"); set => SetB("NoNames", value); }
        public bool NoMount { get => GetB("NoMount"); set => SetB("NoMount", value); }
        public bool NoGroup { get => GetB("NoGroup"); set => SetB("NoGroup", value); }
        public bool RequiredGroup { get => GetB("RequiredGroup"); set => SetB("RequiredGroup", value); }
        public bool Fight { get => GetB("Fight"); set => SetB("Fight", value); }
        public bool Fire { get => GetB("Fire"); set => SetB("Fire", value); }
        public bool NoTownTeleport { get => GetB("NoTownTeleport"); set => SetB("NoTownTeleport", value); }
        public bool GT { get => GetB("GT"); set => SetB("GT", value); }
    }
}
