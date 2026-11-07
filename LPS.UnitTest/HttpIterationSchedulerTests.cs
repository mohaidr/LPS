using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Extensions;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRun.LPSHttpIteration.Scheduler;
using Moq;

namespace LPS.UnitTest;

public class HttpIterationSchedulerTests : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Mock<IIterationStatusMonitor> _statuses = new();
    private readonly TaskCompletionSource _terminalObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Task> _scheduled = new();
    private readonly HttpIteration _iteration;
    private readonly HttpIteration.ExecuteCommand _command;
    private readonly HttpIterationSchedulerService _scheduler;
    private int _status = (int)EntityExecutionStatus.Ongoing;
    private int _polls;

    public HttpIterationSchedulerTests()
    {
        var logger = Mock.Of<ILogger>();
        var operationId = Mock.Of<IRuntimeOperationIdProvider>();
        var watchdog = Mock.Of<IWatchdog>();
        var metrics = Mock.Of<IMetricsDataMonitor>();
        _iteration = new HttpIteration(new HttpIteration.SetupCommand
        {
            Name = "Watcher lifecycle",
            Mode = IterationMode.R,
            RequestCount = 1
        }, Mock.Of<IExpressionEvaluator>(), logger, operationId);
        _command = new HttpIteration.ExecuteCommand(
            Mock.Of<IClientService<HttpRequest, HttpResponse>>(), logger, watchdog,
            operationId, Mock.Of<ISkippedRequestReporter>(), metrics, _statuses.Object);
        _statuses.Setup(value => value.GetTerminalStatusAsync(_iteration, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref _polls);
                var status = (EntityExecutionStatus)Volatile.Read(ref _status);
                if (status.IsTerminal())
                    _terminalObserved.TrySetResult();
                return ValueTask.FromResult(status);
            });
        _scheduler = new HttpIterationSchedulerService(
            logger, watchdog, operationId, metrics, _statuses.Object,
            Mock.Of<IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>>>(),
            Mock.Of<IClientConfiguration<HttpRequest>>());
    }

    [Theory]
    [InlineData(EntityExecutionStatus.Success)]
    [InlineData(EntityExecutionStatus.Failed)]
    [InlineData(EntityExecutionStatus.Skipped)]
    [InlineData(EntityExecutionStatus.Cancelled)]
    public async Task Watcher_StopsForTerminalStatusWithoutCancellingWaitingClients(EntityExecutionStatus status)
    {
        ScheduleClient();
        ScheduleClient();
        Volatile.Write(ref _status, (int)status);

        await _terminalObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var polls = Volatile.Read(ref _polls);
        await Task.Delay(1100);

        Assert.Equal(polls, Volatile.Read(ref _polls));
        Assert.All(_scheduled, task => Assert.False(task.IsCompleted));
        _statuses.Verify(value => value.IsTerminatedAsync(It.IsAny<HttpIteration>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Watcher_TerminatedStatusCancelsAllWaitingClients()
    {
        ScheduleClient();
        ScheduleClient();
        Volatile.Write(ref _status, (int)EntityExecutionStatus.Terminated);

        await Task.WhenAll(_scheduled).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(_cancellation.IsCancellationRequested);
        Assert.All(_scheduled, task => Assert.True(task.IsCompletedSuccessfully));
        Assert.True(_terminalObserved.Task.IsCompleted);
    }

    [Theory]
    [InlineData(EntityExecutionStatus.Scheduled)]
    [InlineData(EntityExecutionStatus.Ongoing)]
    [InlineData(EntityExecutionStatus.OngingScehduled)]
    public async Task Watcher_ContinuesUntilWholeIterationIsTerminal(EntityExecutionStatus status)
    {
        Volatile.Write(ref _status, (int)status);
        ScheduleClient();
        ScheduleClient();
        var initialPolls = Volatile.Read(ref _polls);
        await Task.Delay(1100);

        Assert.True(Volatile.Read(ref _polls) > initialPolls);
        Assert.All(_scheduled, task => Assert.False(task.IsCompleted));

        Volatile.Write(ref _status, (int)EntityExecutionStatus.Success);
        await _terminalObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var finalPolls = Volatile.Read(ref _polls);
        await Task.Delay(1100);
        Assert.Equal(finalPolls, Volatile.Read(ref _polls));
    }

    private void ScheduleClient()
    {
        _scheduled.Add(_scheduler.ScheduleAsync(
            DateTime.Now.AddHours(1), _command, _iteration, _cancellation.Token));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _cancellation.Cancel();
        await Task.WhenAll(_scheduled).WaitAsync(TimeSpan.FromSeconds(5));
        _cancellation.Dispose();
    }
}
