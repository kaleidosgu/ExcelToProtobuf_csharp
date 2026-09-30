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
        if (start > end)
        {
            throw new SteamApiException("起始日期不能晚于结束日期。", 400);
        }
        DateOnly todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (end > todayUtc)
        {
            throw new SteamApiException($"结束日期 {end:yyyy-MM-dd} 晚于当前 UTC 日期 " +
                $"{todayUtc:yyyy-MM-dd}。报表日期按 UTC 计算。", 400);
        }
        if (end.DayNumber - start.DayNumber > 365)
        {
            throw new SteamApiException("日期范围最多包含 366 个 UTC 日。", 400);
        }

        BaselineRecord? baseline = baselineId.HasValue
            ? await store.GetAsync(baselineId.Value, cancellationToken)
            : null;
        if (baselineId.HasValue && baseline == null)
        {
            throw new SteamApiException("未找到所选基线。", 404);
        }
        if (baseline != null)
        {
            List<string> missingBaselineValues = StatCatalog.All
                .Where(item => !baseline.Values.ContainsKey(item.ApiName))
                .Select(item => item.ApiName).ToList();
            if (missingBaselineValues.Count > 0)
            {
                throw new SteamApiException("所选基线缺少当前目录中的统计项，无法用于报表：" +
                    string.Join(", ", missingBaselineValues), 422);
            }
        }

        GlobalSnapshot snapshot = await gateway.GetGlobalAsync(start, end, cancellationToken);
        List<string> missing = StatCatalog.All.Where(item => !snapshot.Stats.ContainsKey(item.ApiName))
            .Select(item => item.ApiName).ToList();
        List<string> warnings = new();
        if (baseline?.InferredZeroStats.Count > 0)
        {
            warnings.Add("所选基线以下统计项采用推定 0，Steam 在创建时未返回明确全局值：" +
                string.Join(", ", baseline.InferredZeroStats) + "。基线后增加按当前累计值减 0 计算。");
        }
        if (missing.Count > 0)
        {
            warnings.Add("以下统计项本次未返回明确的全局值：" + string.Join(", ", missing) +
                "。这不等于统计项名称无效，也不能当作 0。请用“统计目录核对”检查 API Name，" +
                "并在 Steamworks 后台确认 Aggregated 已启用；若配置均正确，可能是聚合数据尚未生成或同步，" +
                "但当前响应无法确认具体原因。");
        }
        List<ReportRow> levels = new();
        for (int levelId = 1; levelId <= 20; levelId++)
        {
            string startsName = $"level_{levelId:00}_battle_starts";
            string reachedName = $"level_{levelId:00}_reached";
            long? starts = snapshot.Stats.TryGetValue(startsName, out GlobalStat? startsStat)
                ? startsStat.Total : null;
            long? reached = snapshot.Stats.TryGetValue(reachedName, out GlobalStat? reachedStat)
                ? reachedStat.Total : null;
            long? startsDelta = baseline == null || !starts.HasValue
                ? null : starts.Value - baseline.Values[startsName];
            long? reachedDelta = baseline == null || !reached.HasValue
                ? null : reached.Value - baseline.Values[reachedName];
            bool anomaly = startsDelta < 0 || reachedDelta < 0;
            if (anomaly)
            {
                warnings.Add($"关卡 {levelId:00} 当前累计值低于基线，请核查 Steam 数据。");
            }
            levels.Add(new ReportRow(levelId, starts, reached, startsDelta, reachedDelta, anomaly));
        }

        long? tutorial = snapshot.Stats.TryGetValue("tutorial_completed", out GlobalStat? tutorialStat)
            ? tutorialStat.Total : null;
        long? tutorialDelta = baseline == null || !tutorial.HasValue
            ? null : tutorial.Value - baseline.Values["tutorial_completed"];
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
            if (snapshot.Stats.TryGetValue(definition.ApiName, out GlobalStat? stat))
            {
                foreach (DailyValue value in stat.History)
                {
                    if (value.Date >= start && value.Date <= end)
                    {
                        byDate[value.Date] = value.Value;
                    }
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
            warnings.Add("部分统计项在所选日期内没有返回每日值；趋势图留空，区间合计只累计已返回的日期。" +
                "每日值缺失与累计全局值是否存在分别判断，空白不代表 0。");
        }

        return new ReportResult(options.AppId, snapshot.FetchedAt, start, end, baseline,
            levels, tutorial, tutorialDelta, daily, periodTotals, warnings);
    }
}
