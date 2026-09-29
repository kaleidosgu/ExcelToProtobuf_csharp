/****************************************************************************
 * Description: Global totals, baseline deltas, daily trends and period summaries.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using Microsoft.Extensions.Options;

namespace SteamReportSystem;

internal sealed class ReportService
{
    private readonly SteamGateway gateway;
    private readonly BaselineStore store;
    private readonly SteamReportOptions options;

    public ReportService(SteamGateway gateway, BaselineStore store, IOptions<SteamReportOptions> options)
    {
        this.gateway = gateway;
        this.store = store;
        this.options = options.Value;
    }

    public async Task<ReportResult> GetAsync(DateOnly start, DateOnly end, Guid? baselineId,
        CancellationToken cancellationToken)
    {
        if (start > end || end > DateOnly.FromDateTime(DateTime.UtcNow) ||
            end.DayNumber - start.DayNumber > 365)
        {
            throw new SteamApiException("日期范围须在今天之前，且不能超过 366 天。", 400);
        }

        BaselineRecord? baseline = baselineId.HasValue
            ? await store.GetAsync(baselineId.Value, cancellationToken)
            : null;
        if (baselineId.HasValue && baseline == null)
        {
            throw new SteamApiException("未找到所选基线。", 404);
        }

        GlobalSnapshot snapshot = await gateway.GetGlobalAsync(start, end, cancellationToken);
        List<string> missing = StatCatalog.All.Where(item => !snapshot.Stats.ContainsKey(item.ApiName))
            .Select(item => item.ApiName).ToList();
        if (missing.Count > 0)
        {
            throw new SteamApiException("Steam 未返回以下统计项：" + string.Join(", ", missing), 422);
        }

        List<string> warnings = new();
        List<ReportRow> levels = new();
        for (int levelId = 1; levelId <= 20; levelId++)
        {
            string startsName = $"level_{levelId:00}_battle_starts";
            string reachedName = $"level_{levelId:00}_reached";
            long starts = snapshot.Stats[startsName].Total;
            long reached = snapshot.Stats[reachedName].Total;
            long? startsDelta = baseline == null ? null : starts - baseline.Values[startsName];
            long? reachedDelta = baseline == null ? null : reached - baseline.Values[reachedName];
            bool anomaly = startsDelta < 0 || reachedDelta < 0;
            if (anomaly)
            {
                warnings.Add($"关卡 {levelId:00} 当前累计值低于基线，请核查 Steam 数据。");
            }
            levels.Add(new ReportRow(levelId, starts, reached, startsDelta, reachedDelta, anomaly));
        }

        long tutorial = snapshot.Stats["tutorial_completed"].Total;
        long? tutorialDelta = baseline == null ? null : tutorial - baseline.Values["tutorial_completed"];
        if (tutorialDelta < 0)
        {
            warnings.Add("教程完成当前累计值低于基线，请核查 Steam 数据。");
        }

        List<DailyRow> daily = new();
        Dictionary<string, long?> periodTotals = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<DateOnly, long>> historyByStat = new(StringComparer.Ordinal);
        foreach (StatDefinition definition in StatCatalog.All)
        {
            Dictionary<DateOnly, long> byDate = new();
            foreach (DailyValue value in snapshot.Stats[definition.ApiName].History)
            {
                if (value.Date >= start && value.Date <= end)
                {
                    byDate[value.Date] = value.Value;
                }
            }
            historyByStat.Add(definition.ApiName, byDate);
            periodTotals.Add(definition.ApiName, byDate.Count == 0 ? null : byDate.Values.Sum());
        }

        for (DateOnly date = start; date <= end; date = date.AddDays(1))
        {
            Dictionary<string, long> values = new(StringComparer.Ordinal);
            foreach (StatDefinition definition in StatCatalog.All)
            {
                if (historyByStat[definition.ApiName].TryGetValue(date, out long value))
                {
                    values.Add(definition.ApiName, value);
                }
            }
            daily.Add(new DailyRow(date, values));
        }

        if (historyByStat.Any(item => item.Value.Count < daily.Count))
        {
            warnings.Add("部分日期没有返回每日值；趋势图留空，区间合计只累计已返回的日期。请勿将空白视为 0。");
        }

        return new ReportResult(options.AppId, snapshot.FetchedAt, start, end, baseline,
            levels, tutorial, tutorialDelta, daily, periodTotals, warnings);
    }
}
