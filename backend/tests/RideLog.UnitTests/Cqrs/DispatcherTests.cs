using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RideLog.Application.Messaging;
using RideLog.Application.Validation;

namespace RideLog.UnitTests.Cqrs;

public class DispatcherTests
{
    private static IDispatcher BuildDispatcher(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallRecorder>();
        services.AddSingleton(typeof(ILogger<>), typeof(RecordingLogger<>));
        services.AddCqrs(typeof(DispatcherTests).Assembly);
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task Query_round_trips_through_the_dispatcher()
    {
        var dispatcher = BuildDispatcher();

        var answer = await dispatcher.QueryAsync(new EchoQuery("hello"));

        Assert.Equal("echo: hello", answer);
    }

    [Fact]
    public async Task Command_reaches_its_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallRecorder>();
        services.AddSingleton(typeof(ILogger<>), typeof(RecordingLogger<>));
        services.AddCqrs(typeof(DispatcherTests).Assembly);
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var recorder = provider.GetRequiredService<CallRecorder>();

        await dispatcher.SendAsync(new PingCommand());

        Assert.Contains("handler:PingCommand", recorder.Calls);
    }

    [Fact]
    public async Task Query_without_a_handler_fails_with_a_clear_error()
    {
        var dispatcher = BuildDispatcher();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new OrphanQuery()));
    }

    [Fact]
    public async Task Pipeline_runs_logging_outside_validation_outside_the_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallRecorder>();
        services.AddSingleton(typeof(ILogger<>), typeof(RecordingLogger<>));
        services.AddCqrs(typeof(DispatcherTests).Assembly);
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var recorder = provider.GetRequiredService<CallRecorder>();

        await dispatcher.SendAsync(new PingCommand());

        Assert.Equal(
            ["log", "validator:PingCommand", "handler:PingCommand", "log"],
            recorder.Calls);
    }

    [Fact]
    public async Task Failing_validation_stops_the_command_before_the_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<CallRecorder>();
        services.AddSingleton(typeof(ILogger<>), typeof(RecordingLogger<>));
        services.AddCqrs(typeof(DispatcherTests).Assembly);
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var recorder = provider.GetRequiredService<CallRecorder>();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(new BrokenCommand()));

        Assert.Contains("Name is required", ex.Errors);
        Assert.DoesNotContain("handler:BrokenCommand", recorder.Calls);
    }

    [Fact]
    public async Task Validation_also_guards_queries()
    {
        var dispatcher = BuildDispatcher();

        await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.QueryAsync(new EchoQuery("")));
    }
}

// --- test fixtures picked up by assembly scanning ---

public sealed class CallRecorder
{
    public List<string> Calls { get; } = [];
}

public sealed class RecordingLogger<T>(CallRecorder recorder) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => recorder.Calls.Add("log");
}

public sealed record EchoQuery(string Text) : IQuery<string>;

public sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
{
    public Task<string> HandleAsync(EchoQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult($"echo: {query.Text}");
}

public sealed class EchoQueryValidator : IValidator<EchoQuery>
{
    public Task<IReadOnlyCollection<string>> ValidateAsync(EchoQuery instance, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(
            string.IsNullOrWhiteSpace(instance.Text) ? ["Text is required"] : []);
}

public sealed record OrphanQuery : IQuery<int>;

public sealed record PingCommand : ICommand;

public sealed class PingCommandHandler(CallRecorder recorder) : ICommandHandler<PingCommand>
{
    public Task HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
    {
        recorder.Calls.Add("handler:PingCommand");
        return Task.CompletedTask;
    }
}

public sealed class PingCommandValidator(CallRecorder recorder) : IValidator<PingCommand>
{
    public Task<IReadOnlyCollection<string>> ValidateAsync(PingCommand instance, CancellationToken cancellationToken = default)
    {
        recorder.Calls.Add("validator:PingCommand");
        return Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}

public sealed record BrokenCommand : ICommand;

public sealed class BrokenCommandHandler(CallRecorder recorder) : ICommandHandler<BrokenCommand>
{
    public Task HandleAsync(BrokenCommand command, CancellationToken cancellationToken = default)
    {
        recorder.Calls.Add("handler:BrokenCommand");
        return Task.CompletedTask;
    }
}

public sealed class BrokenCommandValidator : IValidator<BrokenCommand>
{
    public Task<IReadOnlyCollection<string>> ValidateAsync(BrokenCommand instance, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(["Name is required"]);
}
