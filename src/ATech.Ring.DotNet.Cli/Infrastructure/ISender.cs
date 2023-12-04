﻿using System.Threading;
using System.Threading.Tasks;
using ATech.Ring.Protocol;

namespace ATech.Ring.DotNet.Cli.Infrastructure;

public interface ISender
{
    ValueTask EnqueueAsync(Message message, CancellationToken token);
    void Enqueue(Message message);
}