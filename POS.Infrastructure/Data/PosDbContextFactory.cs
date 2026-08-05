using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace POS.Infrastructure.Data;

/// <summary>
/// Usada por `dotnet ef migrations add` en tiempo de diseño.
/// La conexión real la provee el host (Desktop/Api) vía AddInfrastructure.
/// </summary>
public class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite("Data Source=pos.db")
            .Options;

        return new PosDbContext(options);
    }
}
