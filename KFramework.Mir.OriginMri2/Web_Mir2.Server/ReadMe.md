# Web_Mir2.Server

> ## ⚠️ 第一准则：能抛异常就抛异常，千万别兜底
>
> 服务端遇到任何数据 / 配置异常（找不到 NPC、任务、物品、地图等），
> **一律直接抛异常**（`throw new System.InvalidOperationException(...)`），让问题在日志里立刻暴露出来。
>
> **禁止**写 `if (xxx == null) return;` 这类静默跳过，**禁止**用“旧字段 / 备用键兜底”掩盖问题——
> 静默兜底会让 bug 表现为“功能没反应、数据不推进”却没有任何报错，极难排查。
>
> 完整说明与代码书写规范见文末「代码书写规范与异常处理准则」。

## 历史脉络

本工程的服务端代码最初从 `D:\OpenSource\Crystal\Server`（经典 Mir2 桌面版服务端 + WinForms/SlimDX 客户端）移植而来，沿用其引擎骨架（MirEnvir/MirObjects/MirDatabase、地图/NPC/怪物/物品/任务等对象模型）。

现阶段的数据源已切换到**《原始传奇》**（Egret 页游，构建 fugu_20201203）——即本工程要复刻的目标游戏。物品、怪物、任务、NPC、技能等配置一律以原始传奇配置表为准，通过构建期转换工具产出运行时表。

术语务必分清，不要混淆：

- **原始传奇** = Egret 页游 fugu_20201203，本工程要复刻的目标，其配置表（`0config`~`5config`）与 `main*.min.js` 是行为标准。
- **原版传奇（Crystal）** = `D:\OpenSource\Crystal`，经典 Mir2 桌面版，仅是本工程的历史代码来源，**不再是行为/数据标准**，不要拿它的数据或逻辑去覆盖原始传奇的权威表。

本工程使用的配置分两类，务必分清：

1. **我们自己的运行时表**（`Web_Mir2.Server.Version/ExcelConfig/` 下 `ItemInfo.json`、`MonsterInfo.json`、`NPCInfo.json`、`QuestInfo.json`、`MagicInfo.json` 等）——由转换工具从原始传奇表构建而来，服务器直接读取参与逻辑。
2. **原始传奇表**（同目录下 `0config*.json` ~ `5config*.json` 六个分片）——见下节。

## 原始传奇表是权威参考表，必须认真维护

`0config`~`5config` 六个分片就是**原始传奇**的配置表（从原始传奇客户端原样提取），是本项目一切数值、字段语义、行为逻辑的**唯一标准**。不是「工具转换/排查用表」，也不是「运行时不直接参与逻辑」的附属数据。我们对原始传奇行为的任何复刻、对齐、纠错，最终都要回到这些表来取证。

因此维护时遵守以下强约束：

- **字段 1:1 映射，不擅自增删**。源表有哪些字段，DTO 就有哪些字段；某个字段即使当前样本全为 `null`、客户端似乎不读，也是权威数据的一部分，不得随意删除。要动字段必须先回到源分片和原始传奇 JS 取证。
- **字段语义以原始传奇 JS 实测为准**。`Tools/原始传奇-资源提取/原始传奇-资源参考/` 下的 `main*.min.js` 是最终行为标准；跨表数据、转换工具冲突时，一律以 JS 逐字段访问实测结果为准（例如 `OriginItemTable` 的 col12/18/22 曾与别的推断冲突，以 JS 为准）。
- **注释一行写清**：`// 列 N：类型，条数。语义（关键证据）。`，写来源事实，不写「备用」「未用」「本地不参与逻辑」这类降级表述。
- **单表 DTO 字段类型明确，禁止用 `JsonElement` 当字段类型**。源数据稀疏时用 `int?`/`string?` 等可空具体类型承载。只有根类 `OriginConfigRoot.ExtraTables` 对「尚未建 DTO 的表」做兜底时才使用 `JsonElement`，那是不丢数据的加载机制，不是单表字段的偷懒写法。
- **不做兼容字段/兼容分支**。发现旧结构残留（如并行数组、旧版字段别名）直接删除，只保留权威的一种表示，不留双轨回退。
- **多义字段用中性名**。同一列在不同子类型下含义不同的（如 `OriginItemTable` 的 col21/22/26），字段名保持中性并在注释中逐案说明，不要硬塞成某个单一语义的名字。

任何对上述表的改动，都要重新核对源分片与 JS，并确认 AOT 源生成仍能正确反序列化。

## 配置表目录与结构

| 位置 | 说明 |
| --- | --- |
| `Web_Mir2.Server.Version/ExcelConfig/0config*.json` ~ `5config*.json` | 原始传奇权威分片 JSON，服务端的加载输入 |
| `Server/ExcelConfig_OriginMir2/OriginConfigRoot.cs` | 六个分片根类的公共基类；未声明表经 `[JsonExtensionData]` 收进 `ExtraTables`，不丢数据 |
| `Server/ExcelConfig_OriginMir2/OriginMir2Config0.cs` ~ `5.cs` | 各分片根类，已建 DTO 的表走强类型属性 |
| `Server/ExcelConfig_OriginMir2/ConfigTable/*.cs` | 21 个单表 DTO（`OriginItemTable`/`OriginTaskTable`/`OriginMonsterTable`/`OriginBoxTable` 等） |
| `Server/ExcelConfig/*.cs` | 我们自己运行时表的 DTO（`ItemInfoTable`/`QuestInfoTable` 等） |
| `Server/MirEnvir/Envir.配置表.cs` | 配置表统一加载入口与派生索引 |
| `Library/Json/JsonTool.cs` | AOT 源生成上下文 `AppJsonContext`，注册 6 个分片根类 |

**给某张表建 DTO 的方式**：在 `ConfigTable/` 新建单表类，再到对应分片根类加一个 `[JsonPropertyName]` 强类型属性；该表会自动从 `ExtraTables` 转为强类型，其余表继续兜底。

## 加载链路

`LoadExcelConfig` → `LoadExcelConfigFromJson`：

1. `LoadMyExcelConfig` —— 读取我们自己的 `ExcelConfig/*.json`。
2. `LoadOriginMir2Configs` —— 两阶段：
   - 阶段一：`0`~`5config` 六个分片 DTO 全量加载进 `OriginConfigs`（强类型表走属性，其余表走 `ExtraTables`）；
   - 阶段二：全部加载完后统一派生运行时索引（条件 `OriginConditions`、奖励包 `OriginBoxes`、任务 `OriginTasks`、目标 `OriginGoals`、商店 `OriginStores`、文字样式 `OriginTextStyles`、类型前缀 `OriginTaskTypeNames`、NPC 摆放 `MapNpcPlacements`）。
3. `PostLoadExcelConfig` —— 我们的运行时表收尾绑定。

配置表从本地 JSON 加载，不依赖外部资源服务器；分片文件缺失或解析失败只记日志，不影响其它分片启动。

## 构建期转换工具

`Tools/` 下各转换器负责把原始传奇权威表映射成我们的运行时表，是权威数据进入运行时的唯一正路：

| 工具 | 源表 → 产物 |
| --- | --- |
| `Mir2ItemConverter` | `1config.item` → `ItemInfo.json` |
| `Mir2MonsterConverter` | `4config.monster` → `MonsterInfo.json` |
| `Mir2QuestConverter` | `4config._task` + `2config._task_tx` → `QuestInfo.json` + `Envir/Quests/*.txt` |
| `Mir2SkillConverter` | `5config.skill` 等 → `MagicInfo.json` / `SkillBookInfo.json` |
| `Mir2NPCConverter` / `Mir2NPCScriptGen` | `3config.npc` → `NPCInfo.json` / NPC 脚本对话 |

原始传奇权威资源与 JS 参考存档：`Tools/原始传奇-资源提取/原始传奇-资源参考/`（`main*.min.js`、`0`~`5config` 分片等）。改任何映射前先在此取证。

## 代码书写规范与异常处理准则

### 服务端禁止静默兜底

服务端遇到数据 / 配置异常（找不到 NPC、任务、物品、地图等），**直接抛异常**
（`throw new System.InvalidOperationException(...)`），让问题在日志里暴露出来。

不要写 `if (xxx == null) return;` 这类静默降级，也不要用“旧字段 / 备用键兜底”掩盖问题——
静默兜底会让 bug 表现为“功能没反应、数据不推进”却没有任何报错，极难排查。

典型例子：`Server/MirObjects/PlayerObject.任务.cs` 的 `FinishQuest()` 只按策划表 `tnpc`
（交付 NPC 的 `Info.Index`）匹配当前地图实例，找不到 / 不可见就抛异常；绝不用全局
`FinishNpcObjectID` 兜底——该字段被同一 NPC 的多个摆放实例反复覆盖，会导致领奖无反应、
任务不推进、客户端 NPC 任务面板不关闭，而且没有任何日志。

### 代码块必须加大括号，禁用 #region

- 所有 `if` / `for` / `foreach` / `while` 等代码块**必须使用大括号**，即使只有一行语句；
- **禁止使用 `#region` / `#endregion`** 等折叠宏，不方便审查者阅读代码。
