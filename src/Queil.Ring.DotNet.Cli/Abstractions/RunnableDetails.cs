using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Queil.Ring.DotNet.Cli.Abstractions;

public class RunnableDetails : ReadOnlyDictionary<string, object>
{
    public RunnableDetails(IDictionary<string, object> dictionary) : base(dictionary)
    {
    }
    public static RunnableDetails Empty = new RunnableDetails(new Dictionary<string, object>());
}