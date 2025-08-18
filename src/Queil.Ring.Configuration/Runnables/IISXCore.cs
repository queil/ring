namespace Queil.Ring.Configuration.Runnables;

public class IISXCore : CsProjRunnable
{
    public override string TypeId => nameof(IISXCore).ToLowerInvariant();

    public override bool Equals(object? obj) => obj is IISXCore express && Csproj == express.Csproj;
    public override int GetHashCode() => -576574704 + Csproj.GetHashCode();
}
