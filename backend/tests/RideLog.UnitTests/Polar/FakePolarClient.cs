using RideLog.Application.Polar;

namespace RideLog.UnitTests.Polar;

/// <summary>In-memory <see cref="IPolarClient"/> for exercising the sync orchestration without HTTP.</summary>
internal sealed class FakePolarClient : IPolarClient
{
    public PolarTransaction? Transaction { get; set; }
    public Dictionary<string, PolarExercise> Exercises { get; } = [];
    public Dictionary<string, byte[]?> Gpx { get; } = [];
    public Dictionary<string, byte[]?> Tcx { get; } = [];
    public List<string> Committed { get; } = [];
    public Func<string, PolarExercise>? ExerciseFactory { get; set; }

    /// <summary>Which rider's link the pull was made with — the whole point once there are two.</summary>
    public PolarToken? PulledWith { get; private set; }

    /// <summary>Transactions served per Polar user, for runs that cover more than one rider.</summary>
    public Dictionary<string, PolarTransaction> TransactionsByPolarUser { get; } = [];

    private readonly Dictionary<string, Exception> _failures = [];

    /// <summary>Makes one rider's pull throw, standing in for an expired token or an outage.</summary>
    public void FailsFor(string polarUserId, Exception failure) => _failures[polarUserId] = failure;

    public Task<PolarTransaction?> StartTransactionAsync(PolarToken link, CancellationToken cancellationToken = default)
    {
        PulledWith = link;

        if (_failures.TryGetValue(link.PolarUserId, out var failure))
        {
            return Task.FromException<PolarTransaction?>(failure);
        }

        return Task.FromResult(TransactionsByPolarUser.TryGetValue(link.PolarUserId, out var served)
            ? served
            : Transaction);
    }

    public Task<PolarExercise> GetExerciseAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExerciseFactory is not null ? ExerciseFactory(exerciseUrl) : Exercises[exerciseUrl]);

    public Task<byte[]?> DownloadGpxAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Gpx.GetValueOrDefault(exerciseUrl));

    public Task<byte[]?> DownloadTcxAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tcx.GetValueOrDefault(exerciseUrl));

    public Task CommitTransactionAsync(
        PolarToken link, PolarTransaction transaction, CancellationToken cancellationToken = default)
    {
        Committed.Add(transaction.Id);
        return Task.CompletedTask;
    }
}
