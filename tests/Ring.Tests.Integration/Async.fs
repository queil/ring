module Ring.Tests.Integration.Async

type Async with

    static member AsTaskTimeout(computation: Async<'a>) =
        async {
            let! r = Async.StartChild(computation, millisecondsTimeout = 30000)
            return! r
        }
        |> Async.StartAsTask
