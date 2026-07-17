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

    public Task<PolarTransaction?> StartTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Transaction);

    public Task<PolarExercise> GetExerciseAsync(string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(ExerciseFactory is not null ? ExerciseFactory(exerciseUrl) : Exercises[exerciseUrl]);

    public Task<byte[]?> DownloadGpxAsync(string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Gpx.GetValueOrDefault(exerciseUrl));

    public Task<byte[]?> DownloadTcxAsync(string exerciseUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tcx.GetValueOrDefault(exerciseUrl));

    public Task CommitTransactionAsync(PolarTransaction transaction, CancellationToken cancellationToken = default)
    {
        Committed.Add(transaction.Id);
        return Task.CompletedTask;
    }
}
