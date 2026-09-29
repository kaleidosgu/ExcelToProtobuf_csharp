/****************************************************************************
 * Description: Published GunCross stat names and their report meaning.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System.Text.Json;

namespace SteamReportSystem;

internal static class StatCatalog
{
    public static IReadOnlyList<StatDefinition> All { get; private set; } = Create();

    public static void Load(string path)
    {
        string json = File.ReadAllText(path);
        List<StatDefinition>? definitions = JsonSerializer.Deserialize<List<StatDefinition>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (definitions == null || definitions.Count != 41 ||
            definitions.Any(item => string.IsNullOrWhiteSpace(item.ApiName) || !item.Aggregated) ||
            definitions.Select(item => item.ApiName).Distinct(StringComparer.Ordinal).Count() != definitions.Count)
        {
            throw new InvalidOperationException("统计目录必须包含 41 个唯一、可聚合的 API Name。");
        }

        HashSet<string> expected = Create().Select(item => item.ApiName).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(definitions.Select(item => item.ApiName)))
        {
            throw new InvalidOperationException("统计目录的 API Name 与当前报表支持的 41 项不一致。");
        }
        All = definitions;
    }

    private static IReadOnlyList<StatDefinition> Create()
    {
        List<StatDefinition> stats = new();
        for (int levelId = 1; levelId <= 20; levelId++)
        {
            string prefix = $"level_{levelId:00}";
            stats.Add(new StatDefinition($"{prefix}_battle_starts", $"关卡 {levelId:00} 战斗开始次数",
                "BattleStarts", levelId, "Count", true, true));
            stats.Add(new StatDefinition($"{prefix}_reached", $"关卡 {levelId:00} 到达人数",
                "Reached", levelId, "UniqueFlag", true, true));
        }

        stats.Add(new StatDefinition("tutorial_completed", "教程完成人数", "Tutorial",
            null, "UniqueFlag", true, true));
        return stats;
    }
}
