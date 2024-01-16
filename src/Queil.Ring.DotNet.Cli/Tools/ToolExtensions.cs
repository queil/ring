namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows;
using Abstractions.Tools;
using Logging;
using Microsoft.Extensions.Logging;

public static class ToolExtensions
{
    //TODO: this should be configurable
    private static readonly string[] FailureWords = ["err", "error", "fail"];

    public static async Task<T> TryAsync<T>(int times, TimeSpan backOffInterval, Func<Task<T>> func, Predicate<T> until,
        CancellationToken token)
        where T : new()
    {
        var result = new T();
        var triesLeft = times;
        while (triesLeft > 0)
        {
            result = await func();
            if (until(result)) return result;
            try
            {
                await Task.Delay(backOffInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            triesLeft--;
        }

        return result;
    }

    public static async Task<ExecutionInfo> TryAsync<T>(this T t, int times, TimeSpan backOffInterval,
        Func<T, Task<ExecutionInfo>> func, CancellationToken token) where T : ITool
    {
        return await TryAsync(times, backOffInterval, () => func(t), r => r.IsSuccess, token);
    }

    public static async Task<ExecutionInfo> RunAsync(this ITool tool,
        IEnumerable<string>? args = null,
        string? workingDirectory = null,
        IDictionary<string, string>? envVars = null,
        Action<string>? onErrorData = null,
        bool wait = false,
        bool captureStdOut = true,
        CancellationToken token = default)
    {
        var procUid = Guid.NewGuid().ToString("n").Remove(10);
        try
        {
            var allArgs = string.Join(" ", tool.DefaultArgs.Concat(args ?? Array.Empty<string>()));
            var sb = new StringBuilder();

            var s = new ProcessStartInfo
            {
                FileName = tool.Command,
                Arguments = allArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            if (envVars != null)
                foreach (var (key, value) in envVars)
                    if (s.EnvironmentVariables.ContainsKey(key)) s.EnvironmentVariables[key] = value;
                    else s.EnvironmentVariables.Add(key, value);

            if (workingDirectory != null) s.WorkingDirectory = workingDirectory;
            var ringWorkingDir = Directory.GetCurrentDirectory();

            tool.Logger.LogDebug("{procUid} - Starting process: {Tool} {Args} ({ProcessWorkingDir})", procUid,
                tool.Command, allArgs, workingDirectory ?? ringWorkingDir);

            var p = Process.Start(s);

            if (p == null)
            {
                tool.Logger.LogError("{procUid} - Process failed: {Tool} {Args} ({ProcessWorkingDir})", procUid,
                    tool.Command, allArgs, workingDirectory ?? ringWorkingDir);
                return new ExecutionInfo();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                p.TrackAsChild();
            }

            p.EnableRaisingEvents = true;
            p.OutputDataReceived += OnData;
            p.ErrorDataReceived += OnError;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            tool.Logger.LogDebug("{procUid} - Process started: {Pid}", procUid, p.Id);

            var tcs = new TaskCompletionSource<ExecutionInfo>();

            token.Register(() =>
            {
                if (!tcs.TrySetCanceled()) return;
                try
                {
                    p.Kill();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            });

            p.Exited += OnExit;

            ExecutionInfo result;

            if (wait)
                result = await tcs.Task;
            else
                result = new ExecutionInfo(p.Id, null, sb.ToString().Trim('\r', '\n', ' ', '\t'), tcs.Task);

            return result;

            void OnData(object _, DataReceivedEventArgs line)
            {
                if (line.Data == null) return;
                if (captureStdOut) sb.AppendLine(line.Data);

                if (FailureWords.Any(x => line.Data.Contains(x, StringComparison.OrdinalIgnoreCase)))
                    using (tool.Logger.WithLogErrorScope())
                    {
                        tool.Logger.LogWarning($"{ScopeLoggingExtensions.Red}{line.Data}");
                    }
                else
                    using (tool.Logger.WithLogInfoScope())
                    {
                        tool.Logger.LogInformation($"{ScopeLoggingExtensions.Gray}{line.Data}");
                    }
            }

            void OnExit(object? sender, EventArgs _)
            {
                if (sender is not Process e) return;
                e.OutputDataReceived -= OnData;
                e.ErrorDataReceived -= OnError;
                e.Exited -= OnExit;
                tcs.TrySetResult(new ExecutionInfo(e.Id, e.ExitCode, sb.ToString().Trim('\r', '\n', ' ', '\t'),
                    tcs.Task));
                e.Dispose();
            }

            void OnError(object _, DataReceivedEventArgs x)
            {
                if (string.IsNullOrWhiteSpace(x.Data)) return;
                tool.Logger.LogInformation("ERROR: {Data}", x.Data);
                onErrorData?.Invoke(x.Data);
            }
        }
        catch (OperationCanceledException)
        {
            tool.Logger.LogInformation("Forcefully terminated process {procUid}", procUid);
            return ExecutionInfo.Empty;
        }
        catch (Exception ex)
        {
            tool.Logger.LogCritical(ex, "{procUid} - Unhandled error when starting process", procUid);
            return ExecutionInfo.Empty;
        }
    }
}
