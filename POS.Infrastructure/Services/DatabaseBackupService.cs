using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

/// <summary>
/// Backup/restore de la base SQLite (P4.3).
/// Export: <c>VACUUM INTO</c> → copia consistente y compacta sin bloquear la DB en uso.
/// Restore: valida el archivo ANTES de tocar nada (header SQLite + tablas esperadas);
/// si es inválido la base actual queda intacta. Antes de reemplazar, copia la DB actual
/// a un archivo <c>.bak-timestamp</c> para recuperación manual.
/// </summary>
public class DatabaseBackupService : IDatabaseBackupService
{
    private static readonly string[] RequiredTables =
    [
        "Products", "Categories", "Sales", "SaleItems", "Payments",
        "Users", "CashSessions", "Settings",
    ];

    private readonly string _connectionString;

    public DatabaseBackupService(PosDbContext db)
    {
        // Copiamos el connection string en el constructor: nunca operamos sobre la
        // conexión viva del contexto (dejarla abierta bloquearía el archivo al
        // reemplazarlo en restore).
        _connectionString = db.Database.GetDbConnection().ConnectionString;
    }

    public async Task<Result> ExportAsync(string destinationPath, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Conexión dedicada (nunca la del DbContext): se cierra al salir, dejando
            // libre el archivo destino para quien lo lea después.
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{EscapeSqlLiteral(destinationPath)}'";
            await cmd.ExecuteNonQueryAsync(ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("BACKUP_FAILED", $"No se pudo exportar la base de datos: {ex.Message}");
        }
    }

    public async Task<Result> RestoreAsync(string sourcePath, CancellationToken ct = default)
    {
        // 1. Existencia + tamaño mínimo (una DB vacía recién creada ya pesa > 8KB).
        if (!File.Exists(sourcePath))
            return Result.Failure("RESTORE_NOT_FOUND", "El archivo seleccionado no existe.");
        if (new FileInfo(sourcePath).Length < 512)
            return Result.Failure("RESTORE_INVALID", "El archivo no es una base de datos válida (demasiado pequeño).");

        // 2. Header mágico SQLite (los primeros 16 bytes).
        try
        {
            var header = new byte[16];
            await using (var fs = File.OpenRead(sourcePath))
                await fs.ReadAsync(header, 0, header.Length, ct);

            if (Encoding.ASCII.GetString(header) != "SQLite format 3\u0000")
                return Result.Failure("RESTORE_INVALID", "El archivo no es una base de datos SQLite.");
        }
        catch (Exception ex)
        {
            return Result.Failure("RESTORE_INVALID", $"No se pudo leer el archivo: {ex.Message}");
        }

        // 3. Apertura read-only + tablas esperadas (evita restaurar una DB de otra app).
        try
        {
            await using var conn = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly;Pooling=False");
            await conn.OpenAsync(ct);

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    tables.Add(reader.GetString(0));
            }

            var missing = RequiredTables.Where(t => !tables.Contains(t)).ToList();
            if (missing.Count > 0)
                return Result.Failure("RESTORE_INVALID",
                    $"El archivo no es una base Uenta válida (faltan tablas: {string.Join(", ", missing)}).");
        }
        catch (Exception ex)
        {
            return Result.Failure("RESTORE_INVALID", $"El archivo no se puede leer como base de datos: {ex.Message}");
        }

        // 4. Backup de la DB actual + reemplazo.
        try
        {
            var dbPath = DatabasePath();
            var backupPath = $"{dbPath}.bak-{DateTime.Now:yyyyMMddHHmmss}";

            // Liberar cualquier handle que el pool/contexto tenga sobre el archivo.
            SqliteConnection.ClearAllPools();
            await using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync(ct);
                conn.Close();
            }

            File.Copy(dbPath, backupPath, overwrite: true);
            File.Copy(sourcePath, dbPath, overwrite: true);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("RESTORE_FAILED", $"No se pudo restaurar la base de datos: {ex.Message}");
        }
    }

    private string DatabasePath()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        return builder.DataSource;
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
}