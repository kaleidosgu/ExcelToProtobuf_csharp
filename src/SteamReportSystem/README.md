# SteamReportSystem

GunCross 的本机 Steam 统计报表工具。Web 服务只监听 `127.0.0.1`，浏览器访问本机页面；Publisher Key 不发给浏览器，也不写入配置文件、CSV 或 SQLite。

## 运行

1. 使用 .NET 9 SDK（当前 Visual Studio 2022 17.14 已支持）。
2. 在 `appsettings.json` 中填入真实的 `SteamReport:AppId`。统计目录当前固定为设计文档中的 41 个 API Name；首次使用前请与 Steamworks 已发布配置逐项核对。
3. 在启动工具的同一用户环境设置 `STEAM_REPORT_PUBLISHER_KEY`。PowerShell 当前会话示例：`$env:STEAM_REPORT_PUBLISHER_KEY = Read-Host "Publisher Key" -MaskInput`。不要把真实 Key 写入仓库或命令历史。
4. 在此目录运行 `dotnet run`，打开 `http://127.0.0.1:5079`。

项目已加入 `src/HiProtobuf.sln`，但没有引用原有的 .NET Framework、Unity 或游戏程序集。仅构建本项目可运行 `dotnet build src/SteamReportSystem/SteamReportSystem.csproj`。

离线验证项目也已加入解决方案。可运行 `dotnet run --project src/SteamReportSystem.Tests/SteamReportSystem.Tests.csproj`。它使用模拟 Steam 响应，不需要真实 Key。

## 报表口径

- 全局累计值来自 `GetGlobalStatsForGame`；每日值通过日期参数获取。默认展示最近 30 个完整 UTC 日。页面提供自选日期范围、趋势图、逐日表、关卡汇总及 CSV。
- `reached` 与 `tutorial_completed` 是每个统计项的首次完成累计人数。每日值代表当天该项的新增人数，不代表活跃玩家数。不同关卡人数不能相加为全游戏去重人数。
- Steam 未返回某日数据时，图表与表格显示空白；区间合计仅累计返回的每日值，并提示数据不完整。首次用真实 AppID 联调时应核实 Steam 的日期边界和响应字段。
- 基线是一次完整的 41 项实时全局快照。基线后值为当前累计值减去快照值；Steam 原始数据不变。历史基线与操作记录存于 `data/steam-report.db`。请自行备份该文件。
- 本工具不提供 Steam 原始统计写入或全体玩家清零。当前 41 项均为 `Increment Only`，不能把已有正值写回 0。

## 接口和安全

页面只在本机开放。创建基线的接口要求同源 `Origin` 和自定义请求头，以阻止其他网页向本机服务提交操作。使用其他设备访问或多人共享需要另外设计身份认证和 HTTPS。

Publisher Key 通过 `x-webapi-key` 请求头发送到 `https://partner.steam-api.com/`。如果 Steam 对某个方法不接受此传递方式，应在测试环境验证后调整，不能把 Key 放到浏览器或 URL 中。

## `availableGameStats` 缺失或 `result=8`

这两个响应不能仅凭本地代码判断唯一原因。先确认 `appsettings.json` 的 AppID 属于配置统计项的游戏，
再到该 App 的 Steamworks“统计与成就”页面核对 41 项 API Name 与 `config/guncross-stats.json` 完全一致、
各项已启用 Aggregated，并已发布统计配置。本地 JSON 的 Aggregated 字段只是工具预期，不会修改 Steamworks。
确认后重启工具，运行“统计目录核对”；该功能只能核对 API Name，不能从 Schema 响应验证 Aggregated。
如果目录仍缺失，请检查 Publisher Key 对该 AppID 的权限。不要发送或截图暴露 Key。
