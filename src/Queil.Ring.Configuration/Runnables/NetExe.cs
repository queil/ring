namespace Queil.Ring.Configuration.Runnables;

public class NetExe : CsProjRunnable
{
    public override string TypeId => nameof(NetExe).ToLowerInvariant();

    public override bool Equals(object? obj) => obj is NetExe exe && Csproj == exe.Csproj;
    public override int GetHashCode() => -576574704 + Csproj.GetHashCode();
}
