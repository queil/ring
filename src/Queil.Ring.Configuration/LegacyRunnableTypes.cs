namespace Queil.Ring.Configuration;

using System.Linq;
using Tomlyn.Model;

internal static class LegacyRunnableTypes
{
    private static readonly (string TypeId, string Message)[] All =
    [
        ("aspnetcore",
            "`aspnetcore` was renamed to `dotnet` in v7. Rename `[[aspnetcore]]` to `[[dotnet]]` (and any `[aspnetcore.env]`, `[aspnetcore.tasks.*]`, `[env.aspnetcore]`, `[tasks.aspnetcore.*]` accordingly)"),
        ("netexe",
            "`netexe` was removed in v7 - .NET Framework is no longer supported. Use `[[proc]]` to run an arbitrary executable"),
        ("iisexpress",
            "`iisexpress` was removed in v7 - IIS Express and .NET Framework are no longer supported"),
        ("iisxcore",
            "`iisxcore` was removed in v7 - IIS Express is no longer supported. Use `[[dotnet]]` to run the app on Kestrel")
    ];

    public static void Validate(TomlTable doc, string path)
    {
        var problems = All.Where(x => IsUsed(doc, x.TypeId)).Select(x => x.Message).ToArray();
        if (problems.Length == 0) return;
        throw new WorkspaceConfigException(path, problems);
    }

    private static bool IsUsed(TomlTable doc, string typeId) =>
        doc.ContainsKey(typeId) || IsKeyOf(doc, "env", typeId) || IsKeyOf(doc, "tasks", typeId);

    private static bool IsKeyOf(TomlTable doc, string key, string typeId) =>
        doc.TryGetValue(key, out var value) && value is TomlTable table && table.ContainsKey(typeId);
}
