namespace Queil.Ring.DotNet.Cli.Tools;

using System;
using System.Diagnostics;

public static class ProcessExtensions
{
    public static bool IsProcessRunning(int processId)
    {
        try
        {
            return !Process.GetProcessById(processId).HasExited;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    public static void KillProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            process.Kill();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
