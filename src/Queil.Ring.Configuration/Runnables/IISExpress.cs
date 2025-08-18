namespace Queil.Ring.Configuration.Runnables;

public class IISExpress : CsProjRunnable
{
    public override string TypeId => nameof(IISExpress).ToLowerInvariant();

    public override bool Equals(object? obj) => obj is IISExpress express && Csproj == express.Csproj;
    public override int GetHashCode() => -576574704 + Csproj.GetHashCode();
}
