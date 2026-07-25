namespace RideLog.Application.Rides;

/// <summary>A place on the route where the rider paused for more than about a minute.</summary>
public sealed record RestStop(double Latitude, double Longitude);
