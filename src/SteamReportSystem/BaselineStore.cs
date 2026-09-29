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
        CancellationToken cancellationToken)
    {
        if (snapshot.Stats.Count != StatCatalog.All.Count ||
            StatCatalog.All.Any(item => !snapshot.Stats.ContainsKey(item.ApiName)))
        {
            throw new SteamApiException("Steam 未返回全部 41 项，基线未创建。", 422);
        }

        BaselineRecord baseline = new(Guid.NewGuid(), appId, snapshot.FetchedAt, reason,
            snapshot.Stats.ToDictionary(item => item.Key, item => item.Value.Total, StringComparer.Ordinal));
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

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO operation_audit (id, occurred_at, app_id, action, baseline_id, reason, operator_id, result)
                VALUES ($id, $occurredAt, $appId, 'CreateBaseline', $baselineId, $reason, $operatorId, 'Success')
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$occurredAt", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$appId", appId);
            command.Parameters.AddWithValue("$baselineId", baseline.Id.ToString());
            command.Parameters.AddWithValue("$reason", baseline.Reason);
            command.Parameters.AddWithValue("$operatorId", Environment.UserName);
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

    public async Task<IReadOnlyList<BaselineRecord>> ListAsync(CancellationToken cancellationToken)
    {
        List<BaselineRecord> baselines = new();
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
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
                reader.GetString(3), new Dictionary<string, long>()));
        }
        return baselines;
    }

    public async Task<BaselineRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        BaselineRecord? baseline = null;
        Dictionary<string, long> values = new(StringComparer.Ordinal);
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
                    reader.GetString(3), values);
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
        return baseline;
    }

    public async Task<IReadOnlyList<object>> GetAuditAsync(CancellationToken cancellationToken)
    {
        List<object> audit = new();
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
            audit.Add(new
            {
                OccurredAt = reader.GetString(0),
                Action = reader.GetString(1),
                BaselineId = reader.GetString(2),
                Reason = reader.GetString(3),
                OperatorId = reader.GetString(4),
                Result = reader.GetString(5)
            });
        }
        return audit;
    }
}
