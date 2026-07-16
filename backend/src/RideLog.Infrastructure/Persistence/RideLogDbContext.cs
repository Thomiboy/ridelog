using Microsoft.EntityFrameworkCore;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Persistence;

public class RideLogDbContext(DbContextOptions<RideLogDbContext> options) : DbContext(options)
{
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RawFile> RawFiles => Set<RawFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(RideLogDbContext).Assembly);
}
