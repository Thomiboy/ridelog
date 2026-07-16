using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Persistence;

public class RideLogDbContext(DbContextOptions<RideLogDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RawFile> RawFiles => Set<RawFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RideLogDbContext).Assembly);
    }
}
