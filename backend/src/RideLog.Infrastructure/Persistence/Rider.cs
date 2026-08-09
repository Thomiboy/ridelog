using Microsoft.AspNetCore.Identity;
using RideLog.Application.Auth;

namespace RideLog.Infrastructure.Persistence;

/// <summary>
/// A rider's login. Carries where they stand with the owner, because that is a fact about the rider
/// rather than a relationship to something else — a one-to-one side table for it would be the shape
/// this project already turned down for a ride's metric series.
///
/// Not a role: a role says what a rider may *do* (docs/adr/0006 and #159 sharpened that), while this
/// says whether they are in at all. Folding the second into the first would leave "role" meaning two
/// different things.
/// </summary>
public sealed class Rider : IdentityUser
{
    public Approval Approval { get; set; } = Approval.Pending;
}
