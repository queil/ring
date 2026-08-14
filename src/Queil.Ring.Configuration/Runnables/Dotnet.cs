// ReSharper disable CollectionNeverUpdated.Global

namespace Queil.Ring.Configuration.Runnables;

public class Dotnet : CsProjRunnable
{
    public List<string> Urls { get; } = [];

    public override string TypeId => nameof(Dotnet).ToLowerInvariant();

    public override bool Equals(object? obj) => obj is Dotnet dotnet && UniqueId == dotnet.UniqueId;
    public override int GetHashCode() => -576574704 + UniqueId.GetHashCode();
}
