using POS.Application.Common;

namespace POS.Application.Abstractions;

/// <summary>
/// Exporta/restaura la base de datos SQLite (P4.3). La ruta actual vive en
/// Infrastructure (connection string); aquí solo el contrato para la UI.
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>Exporta la base actual a <paramref name="destinationPath"/> (copia consistente).</summary>
    Task<Result> ExportAsync(string destinationPath, CancellationToken ct = default);

    /// <summary>
    /// Restaura la base desde <paramref name="sourcePath"/>. Valida el archivo ANTES de
    /// tocar la base actual: si es inválido devuelve error y la base actual queda intacta.
    /// </summary>
    Task<Result> RestoreAsync(string sourcePath, CancellationToken ct = default);
}