namespace Queil.Ring.DotNet.Cli.Logging;

using System.IO;
using System.Text;
using Serilog.Sinks.File;

// ReSharper disable once UnusedType.Global
// This is used in logging.*.toml
public class NewLogFileNotifier : FileLifecycleHooks
{
    public override Stream OnFileOpened(string path, Stream underlyingStream, Encoding encoding)
    {
        Serilog.Log.Logger.Information("Logging to: {CurrentLogFile}", path);
        return base.OnFileOpened(path, underlyingStream, encoding);
    }
    // ReSharper disable once UnusedMember.Global
    // This member is used in logging.*.toml
    public static readonly NewLogFileNotifier Current = new();
}
