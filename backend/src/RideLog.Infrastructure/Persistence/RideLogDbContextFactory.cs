using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RideLog.Infrastructure.Persistence;

/// <summary>
/// Used only by EF Core tooling (`dotnet ef`) so migrations can be generated without booting the API.
/// The connection string is a design-time placeholder — migrations need the provider, not a live database.
/// </summary>
public sealed class RideLogDbContextFactory : IDesignTimeDbContextFactory<RideLogDbContext>
{
    public RideLogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RideLogDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RideLog;Trusted_Connection=True;")
            .Options;
        return new RideLogDbContext(options);
    }
}
