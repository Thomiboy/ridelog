using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Persistence.Configurations;

internal sealed class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> builder)
    {
        builder.HasKey(ride => ride.Id);

        // 450 matches the key length of ASP.NET Core Identity users.
        builder.Property(ride => ride.UserId).IsRequired().HasMaxLength(450);
        builder.Property(ride => ride.Sport).IsRequired().HasMaxLength(64);
        builder.Property(ride => ride.Source).HasConversion<string>().HasMaxLength(16);

        // The metric series is stored as a JSON document in a single column (not a child table):
        // it's read as a whole and only ever replaced, matching the raw-files-in-the-DB approach.
        var seriesConverter = new ValueConverter<IReadOnlyList<MetricSample>?, string?>(
            series => series == null ? null : JsonSerializer.Serialize(series, (JsonSerializerOptions?)null),
            json => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<MetricSample>>(json, (JsonSerializerOptions?)null));
        var seriesComparer = new ValueComparer<IReadOnlyList<MetricSample>?>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            series => series == null ? 0 : series.Aggregate(0, (hash, sample) => HashCode.Combine(hash, sample.GetHashCode())),
            series => series == null ? null : series.ToList());
        builder.Property(ride => ride.MetricSeries).HasConversion(seriesConverter, seriesComparer);

        // Weather rides along in the same shape and for the same reason: read whole, replaced whole.
        var weatherConverter = new ValueConverter<IReadOnlyList<WeatherReading>?, string?>(
            weather => weather == null ? null : JsonSerializer.Serialize(weather, (JsonSerializerOptions?)null),
            json => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<WeatherReading>>(json, (JsonSerializerOptions?)null));
        var weatherComparer = new ValueComparer<IReadOnlyList<WeatherReading>?>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            weather => weather == null ? 0 : weather.Aggregate(0, (hash, reading) => HashCode.Combine(hash, reading.GetHashCode())),
            weather => weather == null ? null : weather.ToList());
        builder.Property(ride => ride.Weather).HasConversion(weatherConverter, weatherComparer);
        builder.Property(ride => ride.WeatherOutcome).HasConversion<string>().HasMaxLength(16);

        // Uniqueness half of the duplicate guard: the same source event can never be stored
        // twice; cross-source duplicates are caught by the Ride.Overlaps matching contract.
        builder.HasIndex(ride => new { ride.UserId, ride.StartTime }).IsUnique();

        builder.HasMany(ride => ride.RawFiles)
            .WithOne()
            .HasForeignKey(file => file.RideId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(ride => ride.RawFiles).AutoInclude(false);
    }
}
