namespace Queil.Ring.DotNet.Cli.CsProj;

using System;
using System.IO;
using System.Xml.XPath;
using Configuration;

public static class UseCsProjFileExtensions
{
    public static string GetWorkingDir(this IUseCsProjFile proj) =>
        new FileInfo(proj.FullPath).DirectoryName
        ?? throw new InvalidOperationException($"Path '{proj.FullPath}' doesn't have directory name");

    public static string GetProjName(this IUseCsProjFile proj) => Path.GetFileNameWithoutExtension(proj.FullPath);

    public static (string framework, string? runtime) GetTargetFrameworkAndRuntime(this IUseCsProjFile proj)
    {
        if (proj == null) throw new ArgumentNullException(nameof(proj));
        var xp = new XPathDocument(proj.FullPath);
        var n = xp.CreateNavigator();
        var tf = n.SelectSingleNode("/Project/PropertyGroup/TargetFramework");
        if (tf == null) throw new InvalidOperationException($"TargetFramework is not defined in {proj.FullPath}");

        var ri = n.SelectSingleNode("/Project/PropertyGroup/RuntimeIdentifier");
        return (framework: tf.Value, runtime: ri?.Value);
    }
}
