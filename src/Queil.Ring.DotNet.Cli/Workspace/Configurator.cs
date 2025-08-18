namespace Queil.Ring.DotNet.Cli.Workspace;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Logging;
using Microsoft.Extensions.Logging;

public sealed class Configurator(IConfigurationTreeReader configReader, ILogger<Configurator> logger)
    : IConfigurator, IDisposable
{
    private static readonly object FswLock = new();
    private List<FileSystemWatcher> _currentWatchers = [];
    public ConfigSet Current { get; private set; } = ConfigSet.Empty;
    public event EventHandler<ConfigurationChangedArgs>? OnConfigurationChanged;

    public bool TryGet(string key, [NotNullWhen(true)] out IRunnableConfig? cfg) => Current.TryGetValue(key, out cfg);

    public async Task LoadAsync(ConfiguratorPaths paths, CancellationToken token)
    {
        Current = await LoadConfiguration(paths, token) ?? Current;
        _currentWatchers =
        [
            ..from path in Current.AllPaths
            select Watch(path,
                async () =>
                {
                    OnConfigurationChanged?.Invoke(this,
                        new ConfigurationChangedArgs(await LoadConfiguration(paths, token) ?? Current));
                })
        ];
    }

    public Task UnloadAsync(CancellationToken token)
    {
        foreach (var watcher in _currentWatchers) watcher.Dispose();
        _currentWatchers.Clear();
        Current = ConfigSet.Empty;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var watcher in _currentWatchers) watcher.Dispose();
        _currentWatchers.Clear();
    }

    private FileSystemWatcher Watch(string path, Func<Task> react)
    {
        using var _ = logger.WithHostScope(LogEvent.CONFIG);
        logger.LogDebug("Watching file: {Path}", path);
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

    private async Task<ConfigSet?> LoadConfiguration(ConfiguratorPaths paths, CancellationToken token)
    {
        using var _ = logger.WithHostScope(LogEvent.CONFIG);
        while (true)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var tree = configReader.GetConfigTree(paths);
                var effectiveConfig = tree.ToEffectiveConfig();
                logger.LogInformation("Workspace: {WorkspaceFile}", paths.WorkspacePath);
                logger.LogInformation(LogEventStatus.OK);
                Current = effectiveConfig;
                return effectiveConfig;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (FileNotFoundException fx)
            {
                using var __ = logger.WithLogErrorScope();
                logger.LogError("File not found: {FilePath} when loading workspace: {WorkspacePath}", fx.FileName, paths.WorkspacePath);
                await Task.Delay(5000, token);
            }
            catch (Tomlyn.TomlException tx)
            {
                using var __ = logger.WithLogErrorScope();
                logger.LogError("Invalid workspace TOML: {Message}", tx.Message);
                await Task.Delay(5000, token);
            }
            catch (Exception ex)
            {
                using var __ = logger.WithLogErrorScope();
                logger.LogError(ex, "Workspace loading failed: '{WorkspacePath}'",
                    paths.WorkspacePath);
                await Task.Delay(5000, token);
            }
        }
    }
}
