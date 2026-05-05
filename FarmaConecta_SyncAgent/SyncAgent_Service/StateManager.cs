using Dapper;
using Microsoft.Data.Sqlite;
using SyncAgent_Core;

namespace SyncAgent_Service;

public class ProdutoHash
{
    public string IdInterno { get; set; } = string.Empty;
    public string HashAssinatura { get; set; } = string.Empty;
}

public class StateManager
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public StateManager()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FarmaConecta");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }

        _dbPath = Path.Combine(appData, "estado_sync.sqlite");
        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTableQuery = @"
            CREATE TABLE IF NOT EXISTS ProdutoHash (
                id_interno VARCHAR PRIMARY KEY,
                hash_assinatura VARCHAR(32) NOT NULL
            );";

        await connection.ExecuteAsync(createTableQuery);
    }

    public async Task<string?> GetHashAsync(string idInterno)
    {
        using var connection = new SqliteConnection(_connectionString);
        var query = "SELECT hash_assinatura FROM ProdutoHash WHERE id_interno = @IdInterno;";
        return await connection.QuerySingleOrDefaultAsync<string>(query, new { IdInterno = idInterno });
    }

    public async Task UpdateHashesAsync(IEnumerable<ProdutoHash> hashes)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var query = @"
                INSERT INTO ProdutoHash (id_interno, hash_assinatura)
                VALUES (@IdInterno, @HashAssinatura)
                ON CONFLICT(id_interno) DO UPDATE SET hash_assinatura = excluded.hash_assinatura;";

            await connection.ExecuteAsync(query, hashes, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
