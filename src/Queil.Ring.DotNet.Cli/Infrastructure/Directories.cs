namespace Queil.Ring.DotNet.Cli.Infrastructure;

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static System.IO.Path;

internal static class Directories
{
    internal static WorkingDir Working(string path) => new(path);

    internal static string GetOsPath() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
        throw new NotSupportedException("Platform not supported");
}

internal class InstallationDir
{
    private static string Path =>
        GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? throw new InvalidOperationException("Can't determine the executing assembly location");

    internal static string SettingsPath => Combine(Path, $"app.{Directories.GetOsPath()}.toml");
    internal static string LoggingPath => Combine(Path, $"logging.{Directories.GetOsPath()}.toml");
}

internal class UserSettingsDir
{
    private static string Path =>
        Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".ring");

    internal static string SettingsPath => Combine(Path, "settings.toml");
}

internal class WorkingDir
{
    private readonly string _path;

    internal WorkingDir(string path) => _path = path;

    private string Path => Combine(_path, ".ring");
    internal string SettingsPath => Combine(Path, "settings.toml");
}
