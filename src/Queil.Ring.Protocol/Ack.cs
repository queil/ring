namespace Queil.Ring.Protocol;

public enum Ack : byte
{
    None = 0,
    Ok = 1,
    ExpectedEndOfMessage = 2,
    NotSupported = 3,
    ServerError = 4,
    Terminating = 5,
    NotFound = 6,
    Alive = 7,
    TaskFailed = 8,
    TaskOk = 9
}
