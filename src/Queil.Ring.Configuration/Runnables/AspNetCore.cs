// ReSharper disable CollectionNeverUpdated.Global
namespace Queil.Ring.Configuration.Runnables;

public class AspNetCore : CsProjRunnable
{
    public List<string> Urls { get; } = [];
    public override bool Equals(object? obj) => obj is AspNetCore core && Csproj == core.Csproj;
    public override int GetHashCode() => -576574704 + Csproj.GetHashCode();
}
