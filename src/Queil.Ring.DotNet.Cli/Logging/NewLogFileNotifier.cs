namespace Queil.Ring.DotNet.Cli.Logging;

using System.IO;
using System.Text;
using Serilog;
using Serilog.Sinks.File;

// ReSharper disable once UnusedType.Global
// This is used in logging.*.toml
public class NewLogFileNotifier : FileLifecycleHooks
{
    // ReSharper disable once UnusedMember.Global
    // This member is used in logging.*.toml
    public static readonly NewLogFileNotifier Current = new();

    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        Log.Logger.Information("Logging to: {CurrentLogFile}", path);
        return base.OnFileOpened(path, underlyingStream, encoding);
    }
}
