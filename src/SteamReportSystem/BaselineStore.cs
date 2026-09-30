/****************************************************************************
 * Description: SQLite persistence for report baselines and operation audit.
 *
 * Document: https://github.com/hiramtan/HiProtobuf
 * Author: hiramtan@live.com
 ****************************************************************************/
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SteamReportSystem;

internal sealed class BaselineStore
{
    private readonly string connectionString;
    private readonly uint appId;

    public BaselineStore(IOptions<SteamReportOptions> options, IWebHostEnvironment environment)
    {
        SteamReportOptions value = options.Value;
        string path = Path.IsPathRooted(value.DatabasePath)
            ? value.DatabasePath
            : Path.Combine(environment.ContentRootPath, value.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        appId = value.AppId;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS baseline_sets (
                id TEXT PRIMARY KEY,
                app_id INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                reason TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS baseline_values (
                baseline_id TEXT NOT NULL,
                api_name TEXT NOT NULL,
                value INTEGER NOT NULL,
                PRIMARY KEY (baseline_id, api_name),
                FOREIGN KEY (baseline_id) REFERENCES baseline_sets(id)
            );
            CREATE TABLE IF NOT EXISTS baseline_inferred_zero (
                baseline_id TEXT NOT NULL,
                api_name TEXT NOT NULL,
                PRIMARY KEY (baseline_id, api_name),
                FOREIGN KEY (baseline_id) REFERENCES baseline_sets(id)
            );
            CREATE TABLE IF NOT EXISTS operation_audit (
                id TEXT PRIMARY KEY,
                occurred_at TEXT NOT NULL,
                app_id INTEGER NOT NULL,
                action TEXT NOT NULL,
                baseline_id TEXT NOT NULL,
                reason TEXT NOT NULL,
                operator_id TEXT NOT NULL DEFAULT 'local',
                result TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText = "PRAGMA table_info(operation_audit)";
        bool hasOperator = false;
        await using (SqliteDataReader reader = await migration.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                hasOperator |= reader.GetString(1) == "operator_id";
            }
        }
        if (!hasOperator)
        {
            await using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE operation_audit ADD COLUMN operator_id TEXT NOT NULL DEFAULT 'local'";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<BaselineRecord> CreateAsync(GlobalSnapshot snapshot, string reason,
        CancellationToken cancellationToken, CatalogCheck? catalog = null)
    {
        List<string> absent = StatCatalog.All.Where(item => !snapshot.Stats.ContainsKey(item.ApiName) &&
                !snapshot.EmptyStats.Contains(item.ApiName))
            .Select(item => item.ApiName).ToList();
        if (absent.Count > 0)
        {
            throw new SteamApiException("Steam 响应未包含以下统计项，基线未创建：" +
                string.Join(", ", absent) + "。", 422);
        }
        if (snapshot.EmptyStats.Count > 0 &&
            (catalog == null || catalog.Missing.Count > 0 ||
                StatCatalog.All.Any(item => !catalog.Found.Contains(item.ApiName))))
        {
            throw new SteamApiException("有统计项返回空对象，须先确认全部 41 项存在于已发布目录，" +
                "才可将空对象按推定 0 建立基线：" + string.Join(", ", snapshot.EmptyStats), 422);
        }
        List<string> inferredZeroStats = snapshot.EmptyStats.Distinct(StringComparer.Ordinal).ToList();
        Dictionary<string, long> values = snapshot.Stats.ToDictionary(item => item.Key,
            item => item.Value.Total, StringComparer.Ordinal);
        foreach (string apiName in inferredZeroStats)
        {
            StatDefinition? definition = StatCatalog.All.FirstOrDefault(item => item.ApiName == apiName);
            if (definition == null || !definition.Aggregated || !definition.IncrementOnly ||
                definition.ValueKind is not ("Count" or "UniqueFlag") || !values.TryAdd(apiName, 0))
            {
                throw new SteamApiException($"统计项 {apiName} 不符合推定 0 的条件，基线未创建。", 422);
            }
        }

        BaselineRecord baseline = new(Guid.NewGuid(), appId, snapshot.FetchedAt, reason,
            values, inferredZeroStats);
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO baseline_sets (id, app_id, created_at, reason)
                VALUES ($id, $appId, $createdAt, $reason)
                """;
            command.Parameters.AddWithValue("$id", baseline.Id.ToString());
            command.Parameters.AddWithValue("$appId", appId);
            command.Parameters.AddWithValue("$createdAt", baseline.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$reason", baseline.Reason);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (KeyValuePair<string, long> item in baseline.Values)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO baseline_values (baseline_id, api_name, value)
                VALUES ($baselineId, $apiName, $value)
                """;
            command.Parameters.AddWithValue("$baselineId", baseline.Id.ToString());
            command.Parameters.AddWithValue("$apiName", item.Key);
            command.Parameters.AddWithValue("$value", item.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (string apiName in inferredZeroStats)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO baseline_inferred_zero (baseline_id, api_name)
                VALUES ($baselineId, $apiName)
                """;
            command.Parameters.AddWithValue("$baselineId", baseline.Id.ToString());
            command.Parameters.AddWithValue("$apiName", apiName);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO operation_audit (id, occurred_at, app_id, action, baseline_id, reason, operator_id, result)
                VALUES ($id, $occurredAt, $appId, 'CreateBaseline', $baselineId, $reason, $operatorId, $result)
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$occurredAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$appId", appId);
            command.Parameters.AddWithValue("$baselineId", baseline.Id.ToString());
            command.Parameters.AddWithValue("$reason", baseline.Reason);
            command.Parameters.AddWithValue("$operatorId", Environment.UserName);
            command.Parameters.AddWithValue("$result", inferredZeroStats.Count == 0 ? "Success" :
                "Success；以下统计项的基线值为推定 0：" + string.Join(", ", inferredZeroStats));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return baseline;
    }

    public async Task RecordFailureAsync(string reason, string result, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO operation_audit (id, occurred_at, app_id, action, baseline_id, reason, operator_id, result)
            VALUES ($id, $occurredAt, $appId, 'CreateBaseline', '', $reason, $operatorId, $result)
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$occurredAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$appId", appId);
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$operatorId", Environment.UserName);
        command.Parameters.AddWithValue("$result", result);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaselineListItem>> ListAsync(CancellationToken cancellationToken)
    {
        List<BaselineRecord> baselines = new();
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, app_id, created_at, reason
                FROM baseline_sets WHERE app_id = $appId ORDER BY created_at DESC
                """;
            command.Parameters.AddWithValue("$appId", appId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                baselines.Add(new BaselineRecord(Guid.Parse(reader.GetString(0)),
                    (uint)reader.GetInt64(1), DateTimeOffset.Parse(reader.GetString(2)),
                    reader.GetString(3), new Dictionary<string, long>(), new List<string>()));
            }
        }

        Dictionary<Guid, HashSet<string>> savedNames = baselines.ToDictionary(item => item.Id,
            _ => new HashSet<string>(StringComparer.Ordinal));
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT v.baseline_id, v.api_name
                FROM baseline_values v
                JOIN baseline_sets b ON b.id = v.baseline_id
                WHERE b.app_id = $appId
                """;
            command.Parameters.AddWithValue("$appId", appId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                savedNames[Guid.Parse(reader.GetString(0))].Add(reader.GetString(1));
            }
        }

        Dictionary<Guid, List<string>> inferredNames = baselines.ToDictionary(item => item.Id,
            _ => new List<string>());
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT z.baseline_id, z.api_name
                FROM baseline_inferred_zero z
                JOIN baseline_sets b ON b.id = z.baseline_id
                WHERE b.app_id = $appId
                ORDER BY z.api_name
                """;
            command.Parameters.AddWithValue("$appId", appId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                inferredNames[Guid.Parse(reader.GetString(0))].Add(reader.GetString(1));
            }
        }

        List<BaselineListItem> result = new();
        foreach (BaselineRecord baseline in baselines)
        {
            List<string> missing = StatCatalog.All.Where(item => !savedNames[baseline.Id].Contains(item.ApiName))
                .Select(item => item.ApiName).ToList();
            result.Add(new BaselineListItem(baseline.Id, baseline.AppId, baseline.CreatedAt, baseline.Reason,
                missing.Count == 0, missing.Count == 0 ? null :
                    $"缺少当前目录中的 {missing.Count} 项基线值：" + string.Join(", ", missing),
                inferredNames[baseline.Id]));
        }
        return result;
    }

    public async Task<BaselineRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        BaselineRecord? baseline = null;
        Dictionary<string, long> values = new(StringComparer.Ordinal);
        List<string> inferredZeroStats = new();
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, app_id, created_at, reason FROM baseline_sets
                WHERE id = $id AND app_id = $appId
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$appId", appId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                baseline = new BaselineRecord(Guid.Parse(reader.GetString(0)),
                    (uint)reader.GetInt64(1), DateTimeOffset.Parse(reader.GetString(2)),
                    reader.GetString(3), values, inferredZeroStats);
            }
        }

        if (baseline == null)
        {
            return null;
        }

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT api_name, value FROM baseline_values WHERE baseline_id = $id
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(reader.GetString(0), reader.GetInt64(1));
            }
        }
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT api_name FROM baseline_inferred_zero WHERE baseline_id = $id ORDER BY api_name
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                inferredZeroStats.Add(reader.GetString(0));
            }
        }
        return baseline;
    }

    public async Task<IReadOnlyList<BaselineAuditRecord>> GetAuditAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, BaselineListItem> baselines = (await ListAsync(cancellationToken))
            .ToDictionary(item => item.Id);
        List<BaselineAuditRecord> audit = new();
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT occurred_at, action, baseline_id, reason, operator_id, result
            FROM operation_audit WHERE app_id = $appId ORDER BY occurred_at DESC LIMIT 100
            """;
        command.Parameters.AddWithValue("$appId", appId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string baselineId = reader.GetString(2);
            BaselineListItem? baseline = Guid.TryParse(baselineId, out Guid parsed) &&
                baselines.TryGetValue(parsed, out BaselineListItem? found) ? found : null;
            string? unavailableReason = baseline?.UnavailableReason ??
                (string.IsNullOrEmpty(baselineId) ? "基线未创建" : "已保存基线不存在或 AppID 不匹配");
            audit.Add(new BaselineAuditRecord(reader.GetString(0), reader.GetString(1),
                baselineId, reader.GetString(3), reader.GetString(4), reader.GetString(5),
                baseline?.IsAvailable == true, baseline?.IsAvailable == true ? null : unavailableReason));
        }
        return audit;
    }
}
