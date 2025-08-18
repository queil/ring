namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Logging;
using Microsoft.Extensions.Logging;

public sealed class Configurator(IConfigurationTreeReader configReader, ILogger<Configurator> logger)
    : IConfigurator, IDisposable
{
    private static readonly object FswLock = new();
    private FileSystemWatcher? _currentWatcher;
    public ConfigSet Current { get; private set; } = ConfigSet.Empty;
    public event EventHandler<ConfigurationChangedArgs>? OnConfigurationChanged;

    public bool TryGet(string key, [NotNullWhen(true)] out IRunnableConfig? cfg) => Current.TryGetValue(key, out cfg);

    public async Task LoadAsync(ConfiguratorPaths paths, CancellationToken token)
    {
        Current = await LoadConfigurationAsync(paths);
        _currentWatcher = Watch(paths.WorkspacePath,
            async () =>
            {
                OnConfigurationChanged?.Invoke(this, new ConfigurationChangedArgs(await LoadConfigurationAsync(paths)));
            });
    }

    public Task UnloadAsync(CancellationToken token)
    {
        _currentWatcher?.Dispose();
        Current = ConfigSet.Empty;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _currentWatcher?.Dispose();
    }

    private static FileSystemWatcher Watch(string path, Func<Task> react)
    {
        var directoryName = Path.GetDirectoryName(path);

        if (directoryName == null)
            throw new InvalidOperationException($"Workspace path does not have a directory ({path}).");

        var fsw = new FileSystemWatcher
        {
            Path = directoryName,
            Filter = Path.GetFileName(path),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };

        fsw.Changed += async (sender, _) =>
        {
            var w = (FileSystemWatcher)sender;
            if (!w.EnableRaisingEvents) return;
            lock (FswLock)
            {
                if (!w.EnableRaisingEvents) return;
                w.EnableRaisingEvents = false;
            }

            try
            {
                await react();
            }
            finally
            {
                w.EnableRaisingEvents = true;
            }
        };

        fsw.EnableRaisingEvents = true;
        return fsw;
    }

    private async Task<ConfigSet> LoadConfigurationAsync(ConfiguratorPaths paths)
    {
        WorkspaceConfig? tree = null;
        const int retries = 5;
        var tryCount = retries;
        while (tryCount > 0)
            try
            {
                tryCount--;
                tree = configReader.GetConfigTree(paths);
                break;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not open workspace '{WorkspacePath}'. {RetriesLeft} retries left",
                    paths.WorkspacePath, tryCount);
                await Task.Delay(1000);
            }

        if (tree == null)
            throw new FileLoadException(
                $"Could not (re)load workspace after {retries} tries. Path: '{paths.WorkspacePath}'");
        var effectiveConfig = tree.ToEffectiveConfig();

        using (logger.WithHostScope(LogEvent.CONFIG))
        {
            logger.LogInformation("Workspace: {WorkspaceFile}", paths.WorkspacePath);
            logger.LogInformation(LogEventStatus.OK);
        }

        Current = effectiveConfig;
        return effectiveConfig;
    }
}
