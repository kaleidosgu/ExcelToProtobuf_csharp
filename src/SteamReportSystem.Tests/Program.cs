/****************************************************************************
 * Description: Offline integration checks for Steam response and baseline logic.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SteamReportSystem;

DateOnly end = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
DateOnly start = end.AddDays(-1);
string databasePath = Path.Combine(AppContext.BaseDirectory, $"test-{Guid.NewGuid()}.db");
SteamReportOptions options = new()
{
    AppId = 123,
    DatabasePath = databasePath,
    PublisherKeyEnvironmentVariable = "STEAM_REPORT_TEST_KEY"
};
Environment.SetEnvironmentVariable(options.PublisherKeyEnvironmentVariable, "local-test-key");
MockHandler handler = new(start, end);
SteamGateway gateway = new(new HttpClient(handler), Options.Create(options));
BaselineStore store = new(Options.Create(options), new TestEnvironment());
await store.InitializeAsync(CancellationToken.None);

GlobalSnapshot snapshot = await gateway.GetGlobalAsync(start, end, CancellationToken.None);
Assert(snapshot.Stats.Count == 41, "应解析全部 41 项统计。");
Assert(snapshot.Stats["level_01_battle_starts"].History.Count == 2, "应解析两日历史。");
GlobalStatProbe probe = await gateway.ProbeGlobalStatAsync("level_14_battle_starts", null, null,
    CancellationToken.None);
Assert(probe.AppId == 123 && probe.Total == 17 && probe.EntryPresent,
    "单项诊断应使用相同 AppID 并读取明确的全局值。");
GlobalStatProbe datedProbe = await gateway.ProbeGlobalStatAsync("level_14_battle_starts", start, end,
    CancellationToken.None);
Assert(datedProbe.Total == 18, "单项诊断应能对比带日期范围的响应。");
handler.ProbeEmpty = true;
GlobalStatProbe emptyProbe = await gateway.ProbeGlobalStatAsync("level_14_battle_starts", null, null,
    CancellationToken.None);
Assert(emptyProbe.EntryPresent && !emptyProbe.HasTotal && emptyProbe.EntryFields.Count == 0,
    "单项诊断应把空对象报告为空对象，而非零。");
handler.ProbeEmpty = false;
handler.ProbeResultOnly = true;
GlobalStatProbe failedProbe = await gateway.ProbeGlobalStatAsync("level_14_battle_starts", null, null,
    CancellationToken.None);
Assert(failedProbe.ResultCode == "8" && !failedProbe.HasGlobalStats,
    "单项诊断应显示 Steam 的请求级结果码。");
handler.ProbeResultOnly = false;
BaselineRecord baseline = await store.CreateAsync(snapshot, "自动测试基线", CancellationToken.None);
BaselineListItem availableBaseline = (await store.ListAsync(CancellationToken.None)).Single();
Assert(availableBaseline.IsAvailable && availableBaseline.UnavailableReason == null,
    "完整基线应在列表中标为可用。");
Assert((await store.GetAuditAsync(CancellationToken.None)).Single().IsAvailable,
    "成功创建的完整基线应在操作记录中标为可用。");
ReportService reports = new(gateway, store, Options.Create(options));
DateOnly futureUtcDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
bool explainedUtcLimit = false;
try
{
    await reports.GetAsync(futureUtcDate, futureUtcDate, null, CancellationToken.None);
}
catch (SteamApiException exception)
{
    explainedUtcLimit = exception.Message.Contains("当前 UTC 日期", StringComparison.Ordinal);
}
Assert(explainedUtcLimit, "未来 UTC 日期应说明当前 UTC 日期。");
ReportResult report = await reports.GetAsync(start, end, baseline.Id, CancellationToken.None);
Assert(report.Levels.Count == 20, "关卡汇总应有 20 行。");
Assert(report.Levels[0].BattleStartsAfterBaseline == 0, "刚创建的基线差值应为零。");
Assert(report.PeriodTotals["level_01_battle_starts"] == 3, "区间合计应为两日值之和。");
Assert(report.Daily.Count == 2, "趋势图应有两日。");

handler.OmitFirstDay = true;
ReportResult sparse = await reports.GetAsync(start, end, baseline.Id, CancellationToken.None);
Assert(!sparse.Daily[0].Values.ContainsKey("level_01_battle_starts"), "缺失日期不能伪装成零。");
Assert(sparse.PeriodTotals["level_01_battle_starts"] == 2, "区间只累加返回的日期。");
Assert(sparse.Warnings.Count > 0, "缺失日期应有明确提示。");
handler.OmitFirstDay = false;

handler.EmptyFirstStat = true;
GlobalSnapshot emptyStatSnapshot = await gateway.GetGlobalAsync(start, end, CancellationToken.None);
Assert(!emptyStatSnapshot.Stats.ContainsKey("level_01_battle_starts"), "空对象不能被解释为零。");
Assert(emptyStatSnapshot.EmptyStats.Contains("level_01_battle_starts"),
    "应区分返回空对象与完全未返回的统计项。");
ReportResult partial = await reports.GetAsync(start, end, baseline.Id, CancellationToken.None);
Assert(partial.Levels[0].BattleStarts == null && partial.Levels[0].BattleStartsAfterBaseline == null,
    "缺少全局值的报表单元格应留空。");
Assert(partial.Warnings.Any(item => item.Contains("level_01_battle_starts", StringComparison.Ordinal)),
    "报表应列出没有全局值的统计项。");
bool emptyStatBaselineRefused = false;
try
{
    await store.CreateAsync(emptyStatSnapshot, "空对象快照", CancellationToken.None);
}
catch (SteamApiException exception)
{
    emptyStatBaselineRefused = exception.Message.Contains("level_01_battle_starts", StringComparison.Ordinal);
}
Assert(emptyStatBaselineRefused, "未经目录核对的空对象不能用于推定 0 基线。");
handler.EmptyFirstStat = false;

handler.Total = 5;
ReportResult decreased = await reports.GetAsync(start, end, baseline.Id, CancellationToken.None);
Assert(decreased.Levels[0].BattleStartsAfterBaseline == -5 && decreased.Levels[0].HasAnomaly,
    "低于基线的值应保留负数并标记异常。");
handler.Total = 10;

handler.OmitLastStat = true;
GlobalSnapshot incomplete = await gateway.GetGlobalAsync(null, null, CancellationToken.None);
bool refused = false;
try
{
    await store.CreateAsync(incomplete, "不完整快照", CancellationToken.None);
}
catch (SteamApiException)
{
    refused = true;
}
Assert(refused, "缺失统计项时必须拒绝建立基线。");
Assert((await store.ListAsync(CancellationToken.None)).Count == 1, "拒绝操作后不应留下半条基线。");

CatalogCheck catalog = await gateway.CheckCatalogAsync(CancellationToken.None);
Assert(catalog.Found.Count == 41 && catalog.Missing.Count == 0, "应核对目录名称。");
BaselineRecord inferredBaseline = await store.CreateAsync(emptyStatSnapshot,
    "推定零基线", CancellationToken.None, catalog);
Assert(inferredBaseline.Values["level_01_battle_starts"] == 0 &&
    inferredBaseline.InferredZeroStats.SequenceEqual(new[] { "level_01_battle_starts" }),
    "目录核对通过后，返回空对象的计数项应以推定 0 保存并标记来源。");
BaselineListItem inferredListItem = (await store.ListAsync(CancellationToken.None))
    .Single(item => item.Id == inferredBaseline.Id);
Assert(inferredListItem.IsAvailable && inferredListItem.InferredZeroStats.Contains("level_01_battle_starts"),
    "含推定 0 的完整基线应可用，并在列表中说明来源。");
BaselineAuditRecord inferredAudit = (await store.GetAuditAsync(CancellationToken.None))
    .Single(item => item.BaselineId == inferredBaseline.Id.ToString());
Assert(inferredAudit.IsAvailable && inferredAudit.Result.Contains("level_01_battle_starts", StringComparison.Ordinal),
    "操作记录应保留推定 0 的统计项名称。");
BaselineRecord savedInferredBaseline = (await store.GetAsync(inferredBaseline.Id, CancellationToken.None))!;
Assert(savedInferredBaseline.InferredZeroStats.Contains("level_01_battle_starts"),
    "推定 0 的来源应持久保存。");
handler.OmitLastStat = false;
ReportResult inferredReport = await reports.GetAsync(start, end, inferredBaseline.Id, CancellationToken.None);
Assert(inferredReport.Levels[0].BattleStartsAfterBaseline == 10 &&
    inferredReport.Warnings.Any(item => item.Contains("推定 0", StringComparison.Ordinal)),
    "报表应按推定 0 计算差值，并说明基线来源。");
bool absentStillRejected = false;
try
{
    await store.CreateAsync(incomplete, "完全未返回", CancellationToken.None, catalog);
}
catch (SteamApiException exception)
{
    absentStillRejected = exception.Message.Contains("tutorial_completed", StringComparison.Ordinal);
}
Assert(absentStillRejected, "目录核对通过也不能将完全未返回的统计项推定为 0。");
handler.OmitAvailableGameStats = true;
bool explainedMissingCatalog = false;
try
{
    await gateway.CheckCatalogAsync(CancellationToken.None);
}
catch (SteamApiException exception)
{
    explainedMissingCatalog = exception.Message.Contains("game.availableGameStats.stats 缺失", StringComparison.Ordinal)
        && exception.Message.Contains("Steamworks", StringComparison.Ordinal);
}
Assert(explainedMissingCatalog, "缺少已发布目录时应提示检查 Steamworks 配置。");
handler.OmitAvailableGameStats = false;
PlayerResult player = await gateway.GetPlayerAsync("76561198000000000", CancellationToken.None);
Assert(player.Stats.Count == 41 && player.Missing.Count == 40, "玩家缺失值不能当作零。");
Assert(player.Stats[0].Value == 0, "Steam 明确返回的零应保留为零。");

handler.GlobalResultOnly = true;
bool showedResultCode = false;
try
{
    await gateway.GetGlobalAsync(start, end, CancellationToken.None);
}
catch (SteamApiException exception)
{
    showedResultCode = exception.Message.Contains("result=8", StringComparison.Ordinal);
}
Assert(showedResultCode, "缺少 globalstats 时应显示 Steam 的结果码。");

await store.RecordFailureAsync("失败的创建尝试", "统计项缺值", CancellationToken.None);
BaselineAuditRecord failedAudit = (await store.GetAuditAsync(CancellationToken.None)).First();
Assert(!failedAudit.IsAvailable && failedAudit.UnavailableReason == "基线未创建",
    "创建失败的操作记录应标为不可用。");

await using (SqliteConnection connection = new($"Data Source={databasePath}"))
{
    await connection.OpenAsync();
    await using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "DELETE FROM baseline_values WHERE baseline_id = $id AND api_name = $name";
    command.Parameters.AddWithValue("$id", baseline.Id.ToString());
    command.Parameters.AddWithValue("$name", "level_01_battle_starts");
    await command.ExecuteNonQueryAsync();
}
BaselineListItem unavailableBaseline = (await store.ListAsync(CancellationToken.None))
    .Single(item => item.Id == baseline.Id);
Assert(!unavailableBaseline.IsAvailable &&
    unavailableBaseline.UnavailableReason!.Contains("level_01_battle_starts", StringComparison.Ordinal),
    "缺少当前目录统计项的基线应在列表中标为不可用，并说明原因。");
BaselineAuditRecord previousSuccess = (await store.GetAuditAsync(CancellationToken.None))
    .Single(item => item.BaselineId == baseline.Id.ToString());
Assert(!previousSuccess.IsAvailable && previousSuccess.UnavailableReason!
    .Contains("level_01_battle_starts", StringComparison.Ordinal),
    "已保存基线后来缺项时，操作记录应显示当前不可用。");
bool rejectedUnavailableBaseline = false;
try
{
    await reports.GetAsync(start, end, baseline.Id, CancellationToken.None);
}
catch (SteamApiException exception)
{
    rejectedUnavailableBaseline = exception.StatusCode == 422 &&
        exception.Message.Contains("level_01_battle_starts", StringComparison.Ordinal);
}
Assert(rejectedUnavailableBaseline, "报表不能使用不完整的基线。");

SqliteConnection.ClearAllPools();
File.Delete(databasePath);
Console.WriteLine("SteamReportSystem offline checks passed.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

internal sealed class MockHandler : HttpMessageHandler
{
    private readonly DateOnly start;
    private readonly DateOnly end;
    public bool OmitLastStat { get; set; }
    public bool OmitFirstDay { get; set; }
    public bool EmptyFirstStat { get; set; }
    public long Total { get; set; } = 10;
    public bool GlobalResultOnly { get; set; }
    public bool OmitAvailableGameStats { get; set; }
    public bool ProbeEmpty { get; set; }
    public bool ProbeResultOnly { get; set; }

    public MockHandler(DateOnly start, DateOnly end)
    {
        this.start = start;
        this.end = end;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("x-webapi-key", out IEnumerable<string>? keys) ||
            keys.Single() != "local-test-key")
        {
            throw new Exception("Publisher Key 应通过请求头发送。");
        }

        object body;
        string path = request.RequestUri!.AbsolutePath;
        if (path.Contains("GetGlobalStatsForGame", StringComparison.Ordinal))
        {
            if (request.RequestUri.Query.Contains("count=1", StringComparison.Ordinal))
            {
                if (!request.RequestUri.Query.Contains("name%5B0%5D=level_14_battle_starts",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("单项诊断必须只请求目标 API Name。");
                }
                bool hasDates = request.RequestUri.Query.Contains("startdate=", StringComparison.Ordinal) &&
                    request.RequestUri.Query.Contains("enddate=", StringComparison.Ordinal);
                if (ProbeResultOnly)
                {
                    body = new { response = new { result = 8 } };
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                    });
                }
                object stat = ProbeEmpty ? new { } : new { total = hasDates ? 18 : 17 };
                body = new { response = new { result = 1, globalstats = new Dictionary<string, object>
                {
                    ["level_14_battle_starts"] = stat
                } } };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                });
            }
            if (GlobalResultOnly)
            {
                body = new { response = new { result = 8 } };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                });
            }
            Dictionary<string, object> values = new();
            foreach (StatDefinition definition in StatCatalog.All.Take(OmitLastStat ? 40 : 41))
            {
                List<object> history = new();
                if (!OmitFirstDay)
                {
                    history.Add(new { date = ToUnix(start), total = 1 });
                }
                history.Add(new { date = ToUnix(end), total = 2 });
                values.Add(definition.ApiName,
                    EmptyFirstStat && definition.ApiName == "level_01_battle_starts"
                        ? new { } : new { total = Total, history });
            }
            body = new { response = new { globalstats = values } };
        }
        else if (path.Contains("GetSchemaForGame", StringComparison.Ordinal))
        {
            body = OmitAvailableGameStats
                ? new { game = (object)new { gameName = "Test Game" } }
                : new { game = (object)new { availableGameStats = new
                {
                    stats = StatCatalog.All.Select(item => new { name = item.ApiName }).ToArray()
                } } };
        }
        else
        {
            body = new { playerstats = new { stats = new[]
            {
                new { name = "level_01_battle_starts", value = 0 }
            } } };
        }

        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }

    private static long ToUnix(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
}

internal sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "SteamReportSystem.Tests";
    public string EnvironmentName { get; set; } = "Test";
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
