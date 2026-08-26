using System.Collections;
using System.Reflection;
using RideLog.Application.Analysis;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// <see cref="MonthlyTrainingSummary"/> is the only value that leaves this app, so "no rider's
/// identity goes to a third party" is enforced where it cannot be forgotten: in the type. This walks
/// what the type can actually hold rather than what today's builder happens to put in it — a caller
/// cannot leak a field that has nowhere to live (#187, docs/adr/0008).
///
/// The same move as <c>IOwnerMailSender.NotifyOwnerAsync</c> taking no recipient (#168): a promise
/// somebody has to keep becomes a fact the compiler keeps.
/// </summary>
public class SummaryCarriesNoIdentityTests
{
    /// <summary>Fragments that would name a person, an account, or a place on the map.</summary>
    private static readonly string[] Forbidden =
        ["Id", "Email", "Rider", "User", "Polyline", "Route", "Latitude", "Longitude"];

    private static IEnumerable<(Type Owner, PropertyInfo Property)> Reachable()
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([typeof(MonthlyTrainingSummary)]);

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return (type, property);

                foreach (var next in Unwrap(property.PropertyType).Where(Ours))
                {
                    queue.Enqueue(next);
                }
            }
        }
    }

    /// <summary>A property's type, and the element type when it is a collection or a nullable.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            foreach (var argument in type.GetGenericArguments())
            {
                yield return argument;
            }
        }
    }

    private static bool Ours(Type type) => type.Namespace?.StartsWith("RideLog", StringComparison.Ordinal) == true;

    [Fact]
    public void Nothing_reachable_from_the_summary_can_name_a_rider_or_a_place()
    {
        var offenders = Reachable()
            .Where(entry => Forbidden.Any(fragment =>
                entry.Property.Name.Contains(fragment, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Owner.Name}.{entry.Property.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// A <c>Guid</c> here would be a handle on a row in this rider's log — useless to a reader of the
    /// analysis and a durable reference for anyone else.
    /// </summary>
    [Fact]
    public void No_identifier_type_is_reachable_from_the_summary()
    {
        var offenders = Reachable()
            .Where(entry => (Nullable.GetUnderlyingType(entry.Property.PropertyType)
                ?? entry.Property.PropertyType) == typeof(Guid))
            .Select(entry => $"{entry.Owner.Name}.{entry.Property.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The per-point series never travels. It is the largest thing a ride holds, the model drowns in
    /// it, and elevation-and-speed point by point is the route in all but name. What it is worth —
    /// the zone split, the distance per temperature band — travels already, worked out here.
    /// </summary>
    [Fact]
    public void The_per_point_series_is_not_reachable_from_the_summary()
    {
        var offenders = Reachable()
            .Where(entry => Unwrap(entry.Property.PropertyType)
                .Any(type => type == typeof(Domain.Rides.MetricSample)))
            .Select(entry => $"{entry.Owner.Name}.{entry.Property.Name}")
            .ToList();

        Assert.Empty(offenders);
    }
}
