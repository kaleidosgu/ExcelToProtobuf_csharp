# GunCross 独立统计管理工具设计

> 文档日期：2026-09-29。目标产物是独立于 Unity 游戏包的 C# 管理应用。本文件只描述方案，不表示已经开发或部署工具。

## 1. 目标和结论

工具供内部运营或开发人员查询 GunCross 的 Steam 统计，并发起受控的“重置”。它不引用 Unity、Steamworks.NET、游戏程序集或游戏安装目录。推荐做成一个 **ASP.NET Core 管理服务 + 内置 Web 管理页**，整体作为一个独立 .NET 应用部署在受控服务器上。浏览器只连接管理服务；Publisher Web API Key 仅保存在服务端。

“重置”必须分清两种含义：

1. **报表归零（本工具可实现）**：保存一次 Steam 全局值作为基线，后续显示 `当前全局值 - 基线值`。Steam 原始值和玩家数据不会改变。当前 41 项都可使用；建议作为日常运营的主要重置方式。
2. **修改 Steam 原始数据（当前配置受限）**：Steam Web API 有设置指定 SteamID 统计值的接口，却没有全体玩家清零接口。当前 41 项均为 `Increment Only`，把已有值写成 0 会与配置冲突；因此工具的“Steam 原始值重置”按钮对这些项目必须禁用，不得伪装成已重置。当前用户的 `ISteamUserStats::ResetAllStats` 是 Steam 客户端 API，不是 Publisher Web API，也不能远程指定任意玩家。[官方说明](https://partner.steamgames.com/doc/features/achievements)

若将来确实要让所有玩家的 Steam 原始值重新起算，应由**游戏侧**实现版本统计项与 `ResetAllStats` 逻辑，并在玩家运行新版本时逐个生效；工具只能管理版本发布记录和监测进度，无法立即远程清空所有玩家。[官方方案](https://partner.steamgames.com/doc/features/achievements)

## 2. 现有项目数据口径

现有 [统计项设计文档](D:/GameProjects/dark-side/document/StatAnalysis/SteamStats首批统计项设计.md) 定义 41 个 `INT` 项，均为 `Client` 写入、`Aggregated`、`Increment Only`：

| 统计项 | 数量 | 单人值 | 全局值 |
| --- | ---: | --- | --- |
| `level_{01..20}_battle_starts` | 20 | 某玩家开始该关战斗的累计次数 | 所有玩家开始该关的累计次数 |
| `level_{01..20}_reached` | 20 | 是否至少进入过一次该关，0/1 | 至少进入过该关的累计玩家人数 |
| `tutorial_completed` | 1 | 是否完成教程，0/1 | 完成教程的累计玩家人数 |

关卡 ID 是项目 `LevelInfo` 的 ID，其中 14 是教程关；不能把 ID 视为玩家必然经历的顺序。当前 `StatReportInfo` 的关卡开始事件同时写 `battle_starts` 和 `reached`；教程完成事件写 `tutorial_completed`。成功、失败事件接口已预留，但现有 41 项中没有对应统计规则。`Param2` 可在游戏侧由 `LevelInfo.Difficult` 推出，本工具的统计维度仍以已发布的 API Name 为准。API Name 是 Steamworks 的 API 名称，不是显示名称。

本工具维护**独立的统计目录文件**，从已发布配置核对或导入 API Name；不在运行时读取游戏的 `.dat`、Excel 或生成的 C# 类。这样游戏包与管理工具可以分别发布。Steamworks 后台配置必须先发布，接口才可能返回该项。[统计配置说明](https://partner.steamgames.com/doc/features/achievements)

## 3. 部署与安全边界

```text
内部浏览器（管理员）
        │ HTTPS + 管理员认证
        ▼
GunCross.StatsAdmin（ASP.NET Core，受控服务器）
   ├─ 查询/报表服务 ──────────► Steam Partner Web API
   ├─ 基线与审计存储（SQLite 起步）
   ├─ 统计项目录（JSON / 数据库）
   └─ 密钥提供器（环境变量或服务器密钥库）
```

- 管理页、浏览器脚本、导出的 CSV 和日志中均不出现 Publisher Key。Steam 官方要求 Publisher Key 用于来自安全发行商服务器的请求，不得分发给客户端；Partner Host 为 `https://partner.steam-api.com`。[密钥认证](https://partner.steamgames.com/doc/webapi_overview/auth)、[Web API 概览](https://partner.steamgames.com/doc/webapi_overview)
- 服务器出网采用 HTTPS；可选在 Steamworks 为 Key 配置源 IP 白名单。生产环境按游戏 AppID 建专用发行商组，给 Key 最小必要的 General API 权限。AppID 必须属于该 Key 对应的发行商组。[密钥权限与创建](https://partner.steamgames.com/doc/webapi_overview/auth)
- 管理员 Steamworks 账号只用于在 Partner 后台创建/轮换 Key；运行时无需保存用户名、密码或登录 Cookie。应用配置里记录的是内部环境名称、AppID 和 Key 引用，不保存“开发者账号信息”。
- 本地单机版若直接存 Publisher Key，将变成携带高权限密钥的客户端部署；本方案不采用。测试环境也应使用受控主机或本机仅开发用途的后端，并限制 Key 对应的 AppID 与 IP。

## 4. 可配置项

`appsettings.json` 只放非机密配置；环境变量或密钥库放真实 Key。示例：

```json
{
  "StatsAdmin": {
    "Profiles": [
      {
        "Name": "guncross-production",
        "AppId": 123456,
        "PartnerApiBaseUrl": "https://partner.steam-api.com/",
        "PublisherKeySecretName": "GUNCROSS_STEAM_PUBLISHER_KEY",
        "CatalogPath": "config/guncross-stats.json"
      }
    ],
    "QueryTimeoutSeconds": 15,
    "GlobalCacheSeconds": 300,
    "DatabasePath": "data/stats-admin.db"
  }
}
```

`123456` 只是占位符，部署时填写真实 AppID；`GUNCROSS_STEAM_PUBLISHER_KEY` 是服务端环境变量名，不是 Key 值。启动时校验 AppID 非零、目录 API Name 唯一、密钥可读取；提供“测试连接”只做只读查询，不把密钥写入错误信息。多游戏、多环境通过多个 Profile 配置；每个 Profile 关联自己的 AppID、密钥引用、目录和基线。

统计目录记录 `ApiName`、`DisplayName`、`Category`、`LevelId?`、`ValueKind`（Count / UniqueFlag）、`Aggregated`、`IncrementOnly`、`MinValue`、`MaxValue`、`Enabled`。首版目录从上述 41 项生成并人工核对 Steamworks 配置；查询时只把目录中的 API Name 发给 Steam。`GetSchemaForGame/v2` 可辅助核对存在性，但不能把目录中的业务解释交给 Steam Schema 自动推断。[Web API 参考](https://partner.steamgames.com/doc/webapi/ISteamUserStats)

## 5. Steam 接口映射

| 功能 | Steam 接口 | 输入 | 本工具处理 |
| --- | --- | --- | --- |
| 全局累计值 | `GET ISteamUserStats/GetGlobalStatsForGame/v1/` | Publisher Key、AppID、`count`、`name[0..n-1]` | 查询 41 个 API Name；解析每项全局值；可选 `startdate`、`enddate` 获取日变化数据 |
| 单个玩家统计 | `GET ISteamUserStats/GetUserStatsForGame/v2/` | Key、AppID、SteamID64 | 显示该玩家已有统计项；不把缺失项一律当 0 |
| 游戏统计目录核对 | `GET ISteamUserStats/GetSchemaForGame/v2/` | Key、AppID | 辅助检查配置是否已发布、名称是否存在 |
| 设置某玩家统计 | `POST ISteamUserStats/SetUserStatsForGame/v1/` | Publisher Key、AppID、SteamID64、`count`、`name[i]`、`value[i]` | 仅在该项类型及 Steamworks 约束允许时开放；当前 41 项禁止向下设值 |

接口 URL 与参数以 [Steamworks Web API 参考](https://partner.steamgames.com/doc/webapi/ISteamUserStats) 为准。Web API 使用 `application/x-www-form-urlencoded`；POST 参数放请求体。Key 优先通过 `x-webapi-key` 请求头传递，避免出现在 URL、代理日志和浏览器历史中；实现前针对选定方法做联调验证。[请求格式](https://partner.steamgames.com/doc/webapi_overview)、[Key 传递方式](https://partner.steamgames.com/doc/webapi_overview/auth)

`GetGlobalStatsForGame` 只适用于在 Steamworks 勾选了 `Aggregated` 的项；它不能列出所有 SteamID。`GetUserStatsForGame` 必须先有指定 SteamID64；如需批量核对，应从已有合法玩家 ID 清单导入，逐个查询，并限制速率。当前业务数据是全局累计量，不能由相邻关卡 ID 的差值直接算“流失”或“失败”。[全局统计说明](https://partner.steamgames.com/doc/features/achievements)

## 6. 管理服务模块与 C# 接口草案

建议目标框架为团队当前支持的 .NET LTS，使用 `HttpClientFactory`、`System.Text.Json`、ASP.NET Core 身份认证和 SQLite。以下是内部业务接口草案，具体 DTO 按 Steam 实际响应联调后确定，不承诺 Steam 原始 JSON 的字段形状：

```csharp
public interface ISteamStatsGateway
{
    Task<GlobalStatsSnapshot> GetGlobalAsync(
        string profile, IReadOnlyList<string> apiNames,
        DateTimeOffset? from, DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<PlayerStatsSnapshot> GetPlayerAsync(
        string profile, ulong steamId64,
        CancellationToken cancellationToken);
}

public interface IStatsReportService
{
    Task<LevelStatsReport> GetLevelReportAsync(
        string profile, Guid? baselineId,
        CancellationToken cancellationToken);
}

public interface IStatsResetService
{
    Task<BaselineRecord> CreateBaselineAsync(
        string profile, string reason, string operatorId,
        CancellationToken cancellationToken);

    Task<ResetCapability> CheckSteamWriteCapabilityAsync(
        string profile, ulong steamId64, IReadOnlyList<string> apiNames,
        CancellationToken cancellationToken);
}
```

推荐项目结构：

```text
GunCross.StatsAdmin/
  Program.cs                  # DI、认证、授权、管理页/API
  Configuration/              # Profile 与密钥引用
  Steam/                      # HttpClient 网关、请求/响应 DTO、错误映射
  Catalog/                    # 41 项目录及校验
  Reports/                    # 关卡报表、CSV 导出
  Resets/                     # 基线、能力判断、审批/执行记录
  Storage/                    # SQLite 表和迁移
  wwwroot/                    # 内置管理页资源
  Tests/                      # HTTP 假服务与报表逻辑测试
```

管理页建议提供：Profile 切换、连接状态、全局统计表（关卡 ID / 开始次数 / 到达人数 / 教程完成数）、日期过滤、基线切换、指定 SteamID64 查询、CSV 导出、基线创建与审计记录。`GetSchemaForGame` 核对页面显示缺失或未发布的 API Name，避免把接口错误显示成 0。对 41 项中的 `reached` 与 `tutorial_completed`，界面标注为**累计唯一玩家人数**；报表基线后的差值表示基线以后首次达到该状态的新增人数，并非该期间所有活跃玩家数。

## 7. 重置流程

### 7.1 推荐：报表基线归零

1. 操作员选择 Profile、检查 41 项实时全局快照，填写原因。
2. 服务端一次性抓取所有需归零的项，确保返回项完整；任一项缺失或失败时不创建基线。
3. 在 SQLite 事务中写入 `BaselineId`、AppID、时间、操作员、原因、各 API Name 的基线值和请求追踪号；保留旧基线以支持历史复核。
4. 新报表同时显示 **Steam 原始累计值** 与 **基线后变化值**，导出文件也标明基线时间和 ID。若当前值低于基线值，标记数据异常，不静默截断为 0。

此流程立即生效，但只改变本工具的展示口径。全局值仍持续增长，游戏中的单人统计与 Steam 社区数据不变。已完成教程的玩家在基线后不会再次计入“新增教程完成人数”，因为 0/1 项不会重复增加。

### 7.2 指定玩家 Steam 原始值设置

该能力是**有条件的维护功能**，不是通用清零。执行前先读取指定玩家的当前值、读取本地已核对的 Steamworks 约束，逐项判断目标值是否在 `[MinValue, MaxValue]` 且符合 `IncrementOnly` 等限制；写入后再次读取并审计实际结果。Steam 接口的 `value[i]` 文档为 `uint32`，首版只支持非负 `INT` 项；其他类型需另行验证。[设置接口](https://partner.steamgames.com/doc/webapi/ISteamUserStats)

当前 41 项全部为仅限增量，**已有正值的项目不支持降到 0**。服务端应返回 `UnsupportedBySteamStatConfiguration`，管理页禁用执行按钮。即使将来某些项目允许下降，也必须要求已知 SteamID64、维护员权限、操作理由、读取前值、二次确认、写入后验证和完整审计；不提供“对所有玩家循环写 0”。

### 7.3 真正清除玩家统计的替代路径

- **单个当前登录玩家**：在具备游戏 App 上下文的客户端，通过 `ISteamUserStats::ResetAllStats(false)` 清统计，`true` 还会清成就。Steam 官方定位主要为开发测试用途；该调用影响当前用户，不能由本工具的 Publisher Web API 远程指定其他玩家。[客户端 API](https://partner.steamgames.com/doc/api/ISteamUserStats#ResetAllStats)
- **逐玩家版本迁移**：游戏侧新增 `Version` 项，加载统计后与内置版本比较；不一致时由游戏调用 `ResetAllStats`、设新版本，再上传。只在玩家运行新版本时发生；工具可追踪版本与报表，但不能单独完成此操作。[Steam 官方建议](https://partner.steamgames.com/doc/features/achievements)
- **全体即时清零**：Steam 官方明确没有直接全局清空所有用户统计和成就的方法。对此需求应在产品层定义新统计版本或采用报表基线，不能在工具中设计一个实际上做不到的“Steam 全局重置”。[Steam 官方说明](https://partner.steamgames.com/doc/features/achievements)

## 8. 数据表与错误处理

最小存储：`profiles`（不存明文 Key）、`stat_catalog`、`baseline_sets`、`baseline_values`、`operation_audit`。审计至少记录时间、操作者、Profile/AppID、动作、目标 SteamID（如有）、API Name 列表、前后值摘要、原因、结果、请求追踪号；密钥和完整 HTTP 报文不入库。数据库定期备份，基线及审计记录不允许普通操作员删除。

请求失败分为：认证/权限错误（Key、发行商组、IP 白名单）、AppID 错误、API Name 不存在或未发布、SteamID 无效/该玩家无统计、Steam 服务暂不可用、超时/限流、响应格式异常。只有确认返回数值时才显示 0；其余情况显示明确状态。只读查询可有限重试，写请求默认不自动重试，避免重复操作或不明状态。每次响应保留时间戳和数据来源；可设置 5 分钟缓存，但创建基线与写前检查必须强制实时读取。

## 9. 实施顺序与验收

1. 建立独立 .NET 项目、Profile 配置、密钥提供器、管理员认证和 HTTP 网关；用测试 AppID/Key 验证三类只读接口。
2. 导入 41 项目录，完成全局表、指定玩家查询、CSV 导出和异常展示；逐项核对与 Steamworks 后台的 API Name、类型、`Aggregated`、`Increment Only`。
3. 实现基线快照、报表归零和审计；用模拟响应验证完整性、基线差值、0/1 新增人数解释及失败不落库。
4. 将 Steam 原始值设置能力单独封装并默认关闭；只有出现可下降的统计项且在测试 AppID 验证成功后再按项开放。当前 41 项的“清零”验收结果应是明确拒绝，而非返回成功。

验收示例：41 项全部返回时可展示原始值；创建基线后新报表从 0 开始累积且原始值不变；指定 SteamID 可查询，未知 ID 有明确提示；Key 权限错误不泄露密钥；尝试重置 `level_01_reached=1` 到 0 时界面禁用、服务端也拒绝；未提供 SteamID 时不出现“列出该 App 全部玩家”的误导功能。

## 10. 参考资料

- [Steamworks Web API：ISteamUserStats](https://partner.steamgames.com/doc/webapi/ISteamUserStats)
- [Steamworks：Stats and Achievements（Aggregated、限制、重置）](https://partner.steamgames.com/doc/features/achievements)
- [Steamworks：Authentication using Web API Keys](https://partner.steamgames.com/doc/webapi_overview/auth)
- [Steamworks：Web API Overview](https://partner.steamgames.com/doc/webapi_overview)
- [Steamworks 客户端 API：ISteamUserStats::ResetAllStats](https://partner.steamgames.com/doc/api/ISteamUserStats#ResetAllStats)
