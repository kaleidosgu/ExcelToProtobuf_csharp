/****************************************************************************
 * Description: Read-only Steam Partner Web API access and response parsing.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SteamReportSystem;

internal sealed class SteamGateway
{
    private readonly HttpClient client;
    private readonly SteamReportOptions options;

    public SteamGateway(HttpClient client, IOptions<SteamReportOptions> options)
    {
        this.client = client;
        this.options = options.Value;
        this.client.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<GlobalSnapshot> GetGlobalAsync(DateOnly? start, DateOnly? end,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> parameters = new()
        {
            new("appid", options.AppId.ToString(CultureInfo.InvariantCulture)),
            new("count", StatCatalog.All.Count.ToString(CultureInfo.InvariantCulture)),
            new("format", "json")
        };
        for (int index = 0; index < StatCatalog.All.Count; index++)
        {
            parameters.Add(new($"name[{index}]", StatCatalog.All[index].ApiName));
        }

        if (start.HasValue && end.HasValue)
        {
            parameters.Add(new("startdate", ToUnixSeconds(start.Value)));
            parameters.Add(new("enddate", ToUnixSeconds(end.Value)));
        }

        using JsonDocument document = await GetAsync("ISteamUserStats/GetGlobalStatsForGame/v1/",
            parameters, cancellationToken);
        JsonElement response = RequireObject(document.RootElement, "response");
        if (!response.TryGetProperty("globalstats", out JsonElement globalStats) ||
            globalStats.ValueKind != JsonValueKind.Object)
        {
            throw BuildGlobalStatsError(response);
        }
        Dictionary<string, GlobalStat> stats = new(StringComparer.Ordinal);

        foreach (StatDefinition definition in StatCatalog.All)
        {
            if (!globalStats.TryGetProperty(definition.ApiName, out JsonElement entry))
            {
                continue;
            }
            Console.WriteLine($"API Name: {definition.ApiName}, 数据: {entry.GetRawText()}");
            long total = ParseLong(RequireProperty(entry, "total"));
            List<DailyValue> history = new();
            if (entry.TryGetProperty("history", out JsonElement historyElement) &&
                historyElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement day in historyElement.EnumerateArray())
                {
                    long seconds = ParseLong(RequireProperty(day, "date"));
                    DateOnly date = DateOnly.FromDateTime(
                        DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);
                    long value = ParseLong(RequireProperty(day, "total"));
                    history.Add(new DailyValue(date, value));
                }
            }

            stats.Add(definition.ApiName, new GlobalStat(total, history));
        }

        return new GlobalSnapshot(DateTimeOffset.UtcNow, stats);
    }

    public async Task<PlayerResult> GetPlayerAsync(string steamId64, CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> parameters = new()
        {
            new("appid", options.AppId.ToString(CultureInfo.InvariantCulture)),
            new("steamid", steamId64),
            new("format", "json")
        };
        using JsonDocument document = await GetAsync("ISteamUserStats/GetUserStatsForGame/v2/",
            parameters, cancellationToken);
        JsonElement player = RequireObject(document.RootElement, "playerstats");
        if (player.TryGetProperty("success", out JsonElement success) &&
            success.ValueKind == JsonValueKind.False)
        {
            throw new SteamApiException("Steam 未返回该玩家的统计，请核查 SteamID 和玩家数据。", 404);
        }
        Dictionary<string, long> values = new(StringComparer.Ordinal);
        if (!player.TryGetProperty("stats", out JsonElement stats) || stats.ValueKind != JsonValueKind.Array)
        {
            throw new SteamApiException("Steam 未返回该玩家的统计，请核查 SteamID 和玩家数据。", 404);
        }
        foreach (JsonElement stat in stats.EnumerateArray())
        {
            string? name = RequireProperty(stat, "name").GetString();
            if (name != null && StatCatalog.All.Any(item => item.ApiName == name))
            {
                values[name] = ParseLong(RequireProperty(stat, "value"));
            }
        }

        List<PlayerStat> result = StatCatalog.All
            .Select(item => new PlayerStat(item.ApiName,
                values.TryGetValue(item.ApiName, out long value) ? value : null)).ToList();
        List<string> missing = result.Where(item => !item.Value.HasValue)
            .Select(item => item.ApiName).ToList();
        return new PlayerResult(steamId64, DateTimeOffset.UtcNow, result, missing);
    }

    public async Task<CatalogCheck> CheckCatalogAsync(CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> parameters = new()
        {
            new("appid", options.AppId.ToString(CultureInfo.InvariantCulture)),
            new("format", "json")
        };
        using JsonDocument document = await GetAsync("ISteamUserStats/GetSchemaForGame/v2/",
            parameters, cancellationToken);
        JsonElement game = RequireObject(document.RootElement, "game");
        string rawText = game.GetRawText();
        if (!game.TryGetProperty("availableGameStats", out JsonElement available) ||
            available.ValueKind != JsonValueKind.Object ||
            !available.TryGetProperty("stats", out JsonElement stats))
        {
            throw new SteamApiException("Steam 未返回已发布的统计目录（game.availableGameStats.stats 缺失）。" +
                "请核对 AppID 是否为配置这些统计项的游戏，并在 Steamworks 发布统计配置；" +
                "如果后台已有已发布统计项，请核对 Publisher Key 对该 AppID 的权限。", 422);
        }
        if (stats.ValueKind != JsonValueKind.Array)
        {
            throw new SteamApiException("Steam 返回的统计目录格式不正确。");
        }

        HashSet<string> published = new(StringComparer.Ordinal);
        foreach (JsonElement stat in stats.EnumerateArray())
        {
            if (stat.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
            {
                published.Add(name.GetString()!);
            }
        }

        List<string> found = StatCatalog.All.Where(item => published.Contains(item.ApiName))
            .Select(item => item.ApiName).ToList();
        List<string> missing = StatCatalog.All.Where(item => !published.Contains(item.ApiName))
            .Select(item => item.ApiName).ToList();
        return new CatalogCheck(found, missing,
            new[] { "此接口只核对 API Name 是否存在；Aggregated、Increment Only 等约束仍需与 Steamworks 后台人工核对。" });
    }

    private async Task<JsonDocument> GetAsync(string path, IEnumerable<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken)
    {
        if (options.AppId == 0)
        {
            throw new SteamApiException("请先在 appsettings.json 中配置真实 AppID。", 400);
        }

        string? key = Environment.GetEnvironmentVariable(options.PublisherKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SteamApiException($"请设置服务端环境变量 {options.PublisherKeyEnvironmentVariable}。", 400);
        }

        string query = string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        Uri uri = new(new Uri(options.PartnerApiBaseUrl), $"{path}?{query}");
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("x-webapi-key", key);

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string message = response.StatusCode switch
                {
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                        "Steam 拒绝访问。请检查 Publisher Key、AppID 和 IP 白名单。",
                    HttpStatusCode.TooManyRequests => "Steam 请求频率受限，请稍后重试。",
                    _ => $"Steam 请求失败，HTTP {(int)response.StatusCode}。"
                };
                throw new SteamApiException(message);
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SteamApiException("Steam 请求超时。");
        }
        catch (HttpRequestException)
        {
            throw new SteamApiException("无法连接 Steam Partner Web API。");
        }
        catch (JsonException)
        {
            throw new SteamApiException("Steam 返回了无法解析的数据。");
        }
    }

    private static string ToUnixSeconds(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            .ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static SteamApiException BuildGlobalStatsError(JsonElement response)
    {
        int? resultCode = null;
        string? strMsgError = null;
        if (response.TryGetProperty("result", out JsonElement result))
        {
            if (result.ValueKind == JsonValueKind.Number && result.TryGetInt32(out int numeric))
            {
                if(response.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.String)
                {
                    strMsgError = error.GetString();
                }
                resultCode = numeric;
            }
            else if (result.ValueKind == JsonValueKind.String &&
                int.TryParse(result.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int text))
            {
                resultCode = text;
            }
        }

        string detail = resultCode switch
        {
            2 => "Steam 返回通用失败。",
            8 => "Steam 判定请求参数无效，可能是 AppID 与 API Name 不匹配，或统计项尚未发布、未启用 Aggregated。",
            15 or 24 => "Steam 拒绝访问，请检查 Key 权限、发行商组及 AppID。",
            1 => "Steam 表示请求成功，但没有返回全局统计数据。",
            null => "Steam 没有提供可识别的结果码。",
            _ => "Steam 返回失败结果。"
        };
        string code = resultCode.HasValue ? $"（result={resultCode.Value}）" : string.Empty;
        return new SteamApiException($"未取得全局统计{code}：{detail} " +
            "请在 Steamworks 核对 AppID、41 项 API Name 及 Aggregated 设置，并发布统计配置；" +
            "发布后再运行“统计目录核对”。" + $"ErrorMsg:[{strMsgError}]", 422
            );
    }

    private static JsonElement RequireObject(JsonElement parent, string name)
    {
        JsonElement child = RequireProperty(parent, name);
        if (child.ValueKind != JsonValueKind.Object)
        {
            throw new SteamApiException($"Steam 响应缺少 {name} 对象。");
        }
        return child;
    }

    private static JsonElement RequireProperty(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out JsonElement child))
        {
            throw new SteamApiException($"Steam 响应缺少 {name} 字段。");
        }
        return child;
    }

    private static long ParseLong(JsonElement value)
    {
        string? text = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString(),
            _ => null
        };
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
        {
            throw new SteamApiException("Steam 响应中的统计值不是整数。");
        }
        return number;
    }
}
