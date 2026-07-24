using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RideLog.Domain.Rides;
using RideLog.Domain.Users;

namespace RideLog.Infrastructure.Persistence;

public class RideLogDbContext(DbContextOptions<RideLogDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RawFile> RawFiles => Set<RawFile>();
    public DbSet<PolarConnection> PolarConnections => Set<PolarConnection>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RideLogDbContext).Assembly);
    }
}
