﻿namespace Queil.Ring.Protocol;

public enum Ack : byte
{
    Ok = 0,
    ExpectedEndOfMessage = 1,
    NotSupported = 2,
    ServerError = 3,
    Terminating = 4,
    NotFound = 5,
    Alive = 6,
    TaskFailed = 7,
    TaskOk = 8
}
