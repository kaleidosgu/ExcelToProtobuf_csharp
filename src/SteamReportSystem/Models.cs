/****************************************************************************
 * Description: Configuration and report models for SteamReportSystem.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
namespace SteamReportSystem;

internal sealed class SteamReportOptions
{
    public uint AppId { get; set; }
    public string PublisherKeyEnvironmentVariable { get; set; } = "STEAM_REPORT_PUBLISHER_KEY";
    public string PartnerApiBaseUrl { get; set; } = "https://partner.steam-api.com/";
    public string CatalogPath { get; set; } = "config/guncross-stats.json";
    public string DatabasePath { get; set; } = "data/steam-report.db";
    public string ListenUrl { get; set; } = "http://127.0.0.1:5079";
}

internal sealed record StatDefinition(string ApiName, string DisplayName, string Category, int? LevelId,
    string ValueKind, bool Aggregated, bool IncrementOnly);

internal sealed record DailyValue(DateOnly Date, long Value);

internal sealed record GlobalStat(long Total, IReadOnlyList<DailyValue> History);

internal sealed record GlobalSnapshot(DateTimeOffset FetchedAt, IReadOnlyDictionary<string, GlobalStat> Stats,
    IReadOnlyList<string> EmptyStats);

internal sealed record BaselineRecord(Guid Id, uint AppId, DateTimeOffset CreatedAt, string Reason,
    IReadOnlyDictionary<string, long> Values, IReadOnlyList<string> InferredZeroStats);

internal sealed record BaselineListItem(Guid Id, uint AppId, DateTimeOffset CreatedAt, string Reason,
    bool IsAvailable, string? UnavailableReason, IReadOnlyList<string> InferredZeroStats);

internal sealed record BaselineAuditRecord(string OccurredAt, string Action, string BaselineId,
    string Reason, string OperatorId, string Result, bool IsAvailable, string? UnavailableReason);

internal sealed record ReportRow(int LevelId, long? BattleStarts, long? Reached,
    long? BattleStartsAfterBaseline, long? ReachedAfterBaseline, bool HasAnomaly);

internal sealed record DailyRow(DateOnly Date, IReadOnlyDictionary<string, long> Values);

internal sealed record ReportResult(uint AppId, DateTimeOffset FetchedAt, DateOnly StartDate, DateOnly EndDate,
    BaselineRecord? Baseline, IReadOnlyList<ReportRow> Levels, long? TutorialCompleted,
    long? TutorialCompletedAfterBaseline, IReadOnlyList<DailyRow> Daily,
    IReadOnlyDictionary<string, long?> PeriodTotals, IReadOnlyList<string> Warnings);

internal sealed record PlayerStat(string ApiName, long? Value);

internal sealed record PlayerResult(string SteamId64, DateTimeOffset FetchedAt,
    IReadOnlyList<PlayerStat> Stats, IReadOnlyList<string> Missing);

internal sealed record CatalogCheck(IReadOnlyList<string> Found, IReadOnlyList<string> Missing,
    IReadOnlyList<string> Warnings);

internal sealed record GlobalStatProbe(uint AppId, string ApiName, string? ResultCode,
    bool HasGlobalStats, bool EntryPresent, string? EntryKind, bool HasTotal, long? Total,
    IReadOnlyList<string> EntryFields);

internal sealed record CreateBaselineRequest(string Reason);

internal sealed class SteamApiException : Exception
{
    public int StatusCode { get; }

    public SteamApiException(string message, int statusCode = 502) : base(message)
    {
        StatusCode = statusCode;
    }
}
