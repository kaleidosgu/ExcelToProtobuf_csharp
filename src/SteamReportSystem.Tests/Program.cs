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
BaselineRecord baseline = await store.CreateAsync(snapshot, "自动测试基线", CancellationToken.None);
ReportService reports = new(gateway, store, Options.Create(options));
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
    public long Total { get; set; } = 10;
    public bool GlobalResultOnly { get; set; }
    public bool OmitAvailableGameStats { get; set; }

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
                values.Add(definition.ApiName, new { total = Total, history });
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
