namespace RideLog.Domain.Rides;

/// <summary>
/// An original upload/sync payload kept verbatim for re-processing.
/// Stored in the database for now (Azure SQL free offer); Blob Storage is on the backlog.
/// </summary>
public class RawFile
{
    public required Guid Id { get; init; }

    public required string UserId { get; init; }

    public Guid RideId { get; init; }

    public required RawFileFormat Format { get; init; }

    public string? FileName { get; init; }

    public required byte[] Content { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}
