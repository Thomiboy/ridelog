using Microsoft.EntityFrameworkCore;
using RideLog.Infrastructure.Persistence;

// Placed in the DI namespace so callers get AddRideLogPersistence without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRideLogPersistence(this IServiceCollection services, string connectionString)
        => services.AddDbContext<RideLogDbContext>(options => options.UseSqlServer(connectionString));
}
