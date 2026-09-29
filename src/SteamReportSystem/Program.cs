/****************************************************************************
 * Description: Local-only web host and management endpoints.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SteamReportSystem;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<SteamReportOptions>(builder.Configuration.GetSection("SteamReport"));
SteamReportOptions settings = builder.Configuration.GetSection("SteamReport").Get<SteamReportOptions>()
    ?? throw new InvalidOperationException("缺少 SteamReport 配置。");
string catalogPath = Path.IsPathRooted(settings.CatalogPath)
    ? settings.CatalogPath
    : Path.Combine(builder.Environment.ContentRootPath, settings.CatalogPath);
StatCatalog.Load(catalogPath);
Uri listenUri = new(settings.ListenUrl);
if (listenUri.Scheme != Uri.UriSchemeHttp || listenUri.Host != "127.0.0.1")
{
    throw new InvalidOperationException("个人电脑版本只能监听 http://127.0.0.1。 ");
}
if (!Uri.TryCreate(settings.PartnerApiBaseUrl, UriKind.Absolute, out Uri? partnerUri) ||
    partnerUri.Scheme != Uri.UriSchemeHttps || partnerUri.Host != "partner.steam-api.com")
{
    throw new InvalidOperationException("Steam Partner API 地址必须为 https://partner.steam-api.com/。");
}
if (StatCatalog.All.Select(item => item.ApiName).Distinct(StringComparer.Ordinal).Count() != StatCatalog.All.Count)
{
    throw new InvalidOperationException("统计目录中存在重复的 API Name。");
}

builder.WebHost.UseUrls(settings.ListenUrl);
string keyDirectory = Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
Directory.CreateDirectory(keyDirectory);
IDataProtectionBuilder protection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
if (OperatingSystem.IsWindows())
{
    protection.ProtectKeysWithDpapi();
}
builder.Services.AddRazorPages();
builder.Services.AddHttpClient<SteamGateway>();
builder.Services.AddSingleton<BaselineStore>();
builder.Services.AddTransient<ReportService>();
WebApplication app = builder.Build();
await app.Services.GetRequiredService<BaselineStore>().InitializeAsync(CancellationToken.None);

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (SteamApiException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
});
app.UseStaticFiles();
app.MapRazorPages();

app.MapGet("/api/status", (IOptions<SteamReportOptions> options) =>
    Results.Ok(new
    {
        options.Value.AppId,
        HasPublisherKey = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(options.Value.PublisherKeyEnvironmentVariable)),
        CatalogCount = StatCatalog.All.Count,
        LocalOnly = true
    }));
app.MapGet("/api/catalog", () => Results.Ok(StatCatalog.All));
app.MapGet("/api/catalog/check", async (SteamGateway gateway, CancellationToken cancellationToken) =>
    Results.Ok(await gateway.CheckCatalogAsync(cancellationToken)));
app.MapGet("/api/diagnostics/global-stat/{apiName}", async (string apiName, DateOnly? start, DateOnly? end,
    SteamGateway gateway, CancellationToken cancellationToken) =>
    Results.Ok(await gateway.ProbeGlobalStatAsync(apiName, start, end, cancellationToken)));
app.MapGet("/api/baselines", async (BaselineStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.ListAsync(cancellationToken)));
app.MapGet("/api/audit", async (BaselineStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.GetAuditAsync(cancellationToken)));
app.MapGet("/api/report", async (DateOnly? start, DateOnly? end, Guid? baselineId,
    ReportService reports, CancellationToken cancellationToken) =>
{
    (DateOnly from, DateOnly to) = ResolveDateRange(start, end);
    return Results.Ok(await reports.GetAsync(from, to, baselineId, cancellationToken));
});
app.MapGet("/api/report.csv", async (DateOnly? start, DateOnly? end, Guid? baselineId,
    ReportService reports, CancellationToken cancellationToken) =>
{
    (DateOnly from, DateOnly to) = ResolveDateRange(start, end);
    ReportResult report = await reports.GetAsync(from, to, baselineId, cancellationToken);
    byte[] bytes = Encoding.UTF8.GetPreamble()
        .Concat(Encoding.UTF8.GetBytes(BuildCsv(report))).ToArray();
    return Results.File(bytes, "text/csv; charset=utf-8", "steam-report.csv");
});
app.MapGet("/api/player/{steamId64}", async (string steamId64, SteamGateway gateway,
    CancellationToken cancellationToken) =>
{
    if (!ulong.TryParse(steamId64, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) || parsed == 0)
    {
        throw new SteamApiException("请输入有效的 SteamID64。", 400);
    }
    return Results.Ok(await gateway.GetPlayerAsync(steamId64, cancellationToken));
});
app.MapPost("/api/baselines", async (HttpContext context, CreateBaselineRequest request,
    SteamGateway gateway, BaselineStore store, CancellationToken cancellationToken) =>
{
    string? origin = context.Request.Headers.Origin;
    if (origin != $"{context.Request.Scheme}://{context.Request.Host}" ||
        context.Request.Headers["X-Requested-With"] != "SteamReportSystem")
    {
        throw new SteamApiException("请求来源未通过本机页面验证。", 403);
    }
    string reason = request.Reason?.Trim() ?? string.Empty;
    if (reason.Length < 3 || reason.Length > 500)
    {
        throw new SteamApiException("基线原因需填写 3 至 500 个字符。", 400);
    }

    try
    {
        GlobalSnapshot snapshot = await gateway.GetGlobalAsync(null, null, cancellationToken);
        BaselineRecord result = await store.CreateAsync(snapshot, reason, cancellationToken);
        return Results.Created($"/api/baselines/{result.Id}", result);
    }
    catch (SteamApiException exception)
    {
        await store.RecordFailureAsync(reason, exception.Message, cancellationToken);
        throw;
    }
});

app.Run();

static (DateOnly From, DateOnly To) ResolveDateRange(DateOnly? start, DateOnly? end)
{
    DateOnly to = end ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
    DateOnly from = start ?? to.AddDays(-29);
    return (from, to);
}

static string BuildCsv(ReportResult report)
{
    StringBuilder csv = new();
    csv.AppendLine("section,appid,fetched_at_utc,start_date_utc,end_date_utc,baseline_id,level_id,api_name,total,baseline_delta,period_total,date_utc,daily_value");
    string common = $"{report.AppId},{report.FetchedAt:O},{report.StartDate:yyyy-MM-dd},{report.EndDate:yyyy-MM-dd},{report.Baseline?.Id}";
    foreach (ReportRow level in report.Levels)
    {
        string id = level.LevelId.ToString("00", CultureInfo.InvariantCulture);
        AppendStat($"level_{id}_battle_starts", level.BattleStarts, level.BattleStartsAfterBaseline, id);
        AppendStat($"level_{id}_reached", level.Reached, level.ReachedAfterBaseline, id);
    }
    AppendStat("tutorial_completed", report.TutorialCompleted, report.TutorialCompletedAfterBaseline, string.Empty);

    foreach (DailyRow day in report.Daily)
    {
        foreach (StatDefinition definition in StatCatalog.All)
        {
            if (day.Values.TryGetValue(definition.ApiName, out long value))
            {
                csv.AppendLine($"daily,{common},{definition.LevelId:00},{definition.ApiName},,,,{day.Date:yyyy-MM-dd},{value}");
            }
        }
    }
    return csv.ToString();

    void AppendStat(string name, long? total, long? delta, string levelId)
    {
        report.PeriodTotals.TryGetValue(name, out long? period);
        csv.AppendLine($"summary,{common},{levelId},{name},{total},{delta},{period},,");
    }
}
