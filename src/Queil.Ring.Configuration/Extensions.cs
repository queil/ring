namespace Queil.Ring.Configuration;

internal static class Extensions
{
    internal static Dictionary<string, Dictionary<string, T>> DeepMerge<T>(
        this Dictionary<string, Dictionary<string, T>> target,
        Dictionary<string, Dictionary<string, T>> source)
    {
        var result = new Dictionary<string, Dictionary<string, T>>();

        foreach (var kvp in target)
        {
            result[kvp.Key] = new Dictionary<string, T>(kvp.Value);
        }

        foreach (var kvp in source)
        {
            if (result.TryGetValue(kvp.Key, out var existingDict))
            {
                foreach (var innerKvp in kvp.Value)
                {
                    existingDict[innerKvp.Key] = innerKvp.Value;
                }
            }
            else
            {
                result[kvp.Key] = new Dictionary<string, T>(kvp.Value);
            }
        }

        return result;
    }
}
