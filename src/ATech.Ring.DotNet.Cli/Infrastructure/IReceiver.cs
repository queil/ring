﻿using System;
using System.Threading;
using System.Threading.Tasks;
using ATech.Ring.Protocol;

namespace ATech.Ring.DotNet.Cli.Infrastructure;

public delegate Task OnDequeue(Message message);

public interface IReceiver
{
    Task DequeueAsync(OnDequeue action);
    Task CompleteAsync(TimeSpan timeout);
    Task<bool> WaitToReadAsync(CancellationToken token);
}
