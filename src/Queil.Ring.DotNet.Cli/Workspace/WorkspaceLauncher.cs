namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Configuration;
using Dtos;
using Infrastructure;
using Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Protocol;
using Protocol.Events;
using Tools;

public sealed class WorkspaceLauncher : IWorkspaceLauncher, IDisposable
{
    private readonly IConfigurator _configurator;
    private readonly Func<IRunnableConfig, IRunnable> _createRunnable;
    private readonly IWorkspaceInitHook _initHook;
    private readonly ILogger<WorkspaceLauncher> _logger;
    private readonly Func<ProcessRunner> _newProcRunner;
    private readonly Random _rnd = new();
    private readonly ConcurrentDictionary<string, RunnableContainer> _runnables = new();
    private readonly ISender _sender;
    private readonly int _spreadFactor;
    private CancellationTokenSource _cts = new();
    private int _initCounter;
    private Task? _initHookTask;
    private Task? _startTask;
    private Task? _stopTask;

    public WorkspaceLauncher(IConfigurator configurator,
        ILogger<WorkspaceLauncher> logger,
        Func<IRunnableConfig, IRunnable> createRunnable,
        IWorkspaceInitHook initHook,
        ISender sender,
        Func<ProcessRunner> newProcRunner,
        IOptions<RingConfiguration> options)
    {
        _configurator = configurator;
        _logger = logger;
        _createRunnable = createRunnable;
        _initHook = initHook;
        _sender = sender;
        _newProcRunner = newProcRunner;
        _spreadFactor = options.Value.Workspace.StartupSpreadFactor;
        OnInitiated += WorkspaceLauncher_OnInitiated;
    }

    private WorkspaceInfo CurrentInfo { get; set; } = WorkspaceInfo.Empty;
    private WorkspaceState CurrentStatus { get; set; }
    private string CurrentFlavour { get; set; } = ConfigSet.AllFlavours;

    public void Dispose()
    {
        _cts.Dispose();
    }

    public string WorkspacePath => _configurator.Current.Path;

    public async Task<ExecuteTaskResult> ExecuteTaskAsync(RunnableTask task, CancellationToken token)
    {
        if (!_runnables.TryGetValue(task.RunnableId, out var r)) return ExecuteTaskResult.UnknownRunnable;
        var (func, bringDown) = r.PrepareTask(task.TaskId);
        if (bringDown) await ExcludeAsync(task.RunnableId, token);
        ExecuteTaskResult result;
        using (_logger.WithTaskScope(task.RunnableId, task.TaskId))
        {
            result = await func(_newProcRunner(), token);
        }

        if (bringDown && _configurator.TryGet(task.RunnableId, out var cfg))
        {
            await AddAsync(task.RunnableId, cfg, TimeSpan.Zero, start: result == ExecuteTaskResult.Ok, token: _cts.Token);
        }
        return result;
    }

    public event EventHandler? OnInitiated;

    public async Task<ApplyFlavourResult> ApplyFlavourAsync(string flavour, CancellationToken token)
    {
        if (CurrentFlavour == flavour) return ApplyFlavourResult.Ok;
        if (!CurrentInfo.Flavours.Contains(flavour)) return ApplyFlavourResult.UnknownFlavour;
        var candidates = _configurator.Current.Select(x => (x.Key, x.Value.Tags.Contains(flavour)));
        foreach (var (key, inFlavour) in candidates)
            if (inFlavour)
                await IncludeAsync(key, token);
            else
                await ExcludeAsync(key, token);

        CurrentFlavour = flavour;
        PublishStatusCore(ServerState.RUNNING, true);
        return ApplyFlavourResult.Ok;
    }

    public async Task LoadAsync(ConfiguratorPaths paths, CancellationToken token)
    {
        _configurator.OnConfigurationChanged += async (_, args) =>
        {
            await ApplyConfigChanges(args.Configuration, _cts.Token);
        };

        var configDirectory = new FileInfo(paths.WorkspacePath).DirectoryName;
        if (configDirectory == null)
            throw new InvalidOperationException($"Path '{configDirectory}' does not have directory name");

        Directory.SetCurrentDirectory(configDirectory);

        await _configurator.LoadAsync(paths, _cts.Token);
        using (_logger.WithHostScope(LogEvent.INIT))
        {
            _logger.LogInformation(LogEventStatus.OK);
        }
    }

    public Task StartAsync(CancellationToken token)
    {
        _cts = new CancellationTokenSource();
        _startTask = Task.Factory.StartNew(async () => await ApplyConfigChanges(_configurator.Current, _cts.Token),
            _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public async Task UnloadAsync(CancellationToken token)
    {
        await _configurator.UnloadAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken token)
    {
        await _cts.CancelAsync();
        if (_initHookTask != null) await _initHookTask;
        _initCounter = 0;
        _stopTask = Task.WhenAll(_runnables.Keys.Select(RemoveAsync));
        await (_startTask ?? throw new InvalidOperationException($"{nameof(_startTask)} must not be null"));
    }

    public async Task<ExcludeResult> ExcludeAsync(string id, CancellationToken token) =>
        await RemoveAsync(id) ? ExcludeResult.Ok : ExcludeResult.UnknownRunnable;

    public async Task<IncludeResult> IncludeAsync(string id, CancellationToken token)
    {
        if (!_configurator.TryGet(id, out var cfg)) return IncludeResult.UnknownRunnable;
        await AddAsync(id, cfg, TimeSpan.Zero, token: _cts.Token);
        return IncludeResult.Ok;
    }

    public void PublishStatus(ServerState serverState)
    {
        PublishStatusCore(serverState, true);
    }

    public async Task WaitUntilStoppedAsync(CancellationToken token)
    {
        if (_stopTask != null) await _stopTask;
    }


    private void WorkspaceLauncher_OnInitiated(object? sender, EventArgs e)
    {
        _initHookTask = _initHook.RunAsync(_cts.Token);
    }

    private async Task ApplyConfigChanges(IDictionary<string, IRunnableConfig> configs, CancellationToken token)
    {
        try
        {
            var deletions = _runnables.Keys.Except(configs.Keys).ToArray();
            var deletionsTask = deletions.Select(RemoveAsync);
            var additions = configs.Keys.Except(_runnables.Keys).ToArray();
            var additionsTask = additions.Select(key =>
            {
                var delay = CalculateDelay(additions.Length);
                using (_logger.WithScope(key, LogEvent.TRACE))
                {
                    _logger.LogDebug("Starting in {delay}", delay);
                }

                return AddAsync(key, configs[key], delay, token: token);
            });

            await Task.WhenAll(additionsTask.Concat(deletionsTask));
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Workspace cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
        }
    }

    private TimeSpan CalculateDelay(int runnablesCount)
    {
        lock (_rnd)
        {
            return TimeSpan.FromMilliseconds(runnablesCount <= 7
                ? 1000
                : _rnd.Next(0, Math.Max(runnablesCount - 1, 0) * _spreadFactor));
        }
    }

    private WorkspaceInfo Create(WorkspaceState state, ServerState serverState)
    {
        var runnables = _configurator.Current.Select(entry =>
        {
            var (id, cfg) = entry;
            var isRunning = _runnables.TryGetValue(id, out var container);

            var runnableState = isRunning
                ? container!.Runnable switch
                {
                    { State: State.Zero } => RunnableState.ZERO,
                    { State: State.Idle } => RunnableState.INITIATED,
                    { State: State.ProbingHealth } => RunnableState.HEALTH_CHECK,
                    { State: State.Healthy } => RunnableState.HEALTHY,
                    { State: State.Dead } => RunnableState.DEAD,
                    { State: State.Recovering } => RunnableState.RECOVERING,
                    { State: State.Pending } => RunnableState.STARTED,
                    _ => RunnableState.ZERO
                }
                : RunnableState.ZERO;

            var details = isRunning ? container!.Runnable.Details : DetailsExtractors.Extract(cfg);

            var runnableInfo = new RunnableInfo(id,
                [.. cfg.DeclaredPaths],
                cfg.GetType().Name,
                runnableState,
                [.. cfg.Tags],
                details,
                [.. cfg.Tasks.Keys]);

            return runnableInfo;
        }).OrderBy(x => x.Id).ToArray();

        return new WorkspaceInfo(WorkspacePath, runnables, [.. _configurator.Current.Flavours], CurrentFlavour,
            serverState, state);
    }

    private async Task AddAsync(string id, IRunnableConfig cfg, TimeSpan delay, bool start = true, CancellationToken token = default)
    {
        if (_runnables.ContainsKey(id)) return;
        var container = await RunnableContainer.CreateAsync(cfg, _createRunnable, delay, token);

        container.Runnable.OnHealthCheckCompleted += OnPublishStatus;
        container.Runnable.OnInitExecuted += OnRunnableInitExecuted;

        _runnables.TryAdd(id, container);

        await container.ConfigureAsync();
        if (start) container.Start();
    }

    private async Task<bool> RemoveAsync(string key)
    {
        if (!_runnables.TryRemove(key, out var container)) return false;

        Interlocked.Decrement(ref _initCounter);
        container.Runnable.OnHealthCheckCompleted -= OnPublishStatus;
        container.Runnable.OnInitExecuted -= OnRunnableInitExecuted;
        try
        {
            await container.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Runnable disposal failed");
            return false;
        }
        return true;
    }

    private void OnRunnableInitExecuted(object? sender, EventArgs e)
    {
        if (_configurator.Current.Count != Interlocked.Increment(ref _initCounter)) return;
        using var _ = _logger.WithHostScope(LogEvent.INIT);
        OnInitiated?.Invoke(this, EventArgs.Empty);
    }

    private void OnPublishStatus(object? sender, EventArgs args)
    {
        PublishStatusCore(ServerState.RUNNING, false);
    }

    private void PublishStatusCore(ServerState serverState, bool force)
    {
        var state = serverState == ServerState.IDLE ? WorkspaceState.NONE :
            _runnables.IsEmpty ? WorkspaceState.IDLE :
            _runnables.Values.Select(x => x.Runnable)
                .All(r => r.State == State.ProbingHealth || r.State == State.Healthy) ? WorkspaceState.HEALTHY :
            WorkspaceState.DEGRADED;

        if (!force && state == CurrentStatus) return;
        CurrentStatus = state;
        var info = Create(state, serverState);
        if (!force && info.Equals(CurrentInfo)) return;
        CurrentInfo = info;
        _sender.Enqueue(Message.WorkspaceInfo(CurrentInfo));
    }
}
