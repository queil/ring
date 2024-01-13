namespace Queil.Ring.Configuration;

using System.Runtime.InteropServices;

public class ConfiguratorPaths
{
    private const string WslMnt = "/mnt/";
    private readonly string _path = string.Empty;
    public string WorkspacePath
    {
        get => _path;
        init
        {
            if (value.StartsWith(WslMnt) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var parts = value.Replace(WslMnt, "").Split("/");
                _path = parts[0] + ":\\" + string.Join("\\", parts[1..]);
            }
            else
            {
                _path = value;
            }
        }
    }
}
