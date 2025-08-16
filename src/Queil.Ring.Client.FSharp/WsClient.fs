namespace Ring.Client

open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Channels
open Queil.Ring.Protocol
open Queil.Ring.Protocol.Events
open System
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks

open FSharp.Control

type ClientOptions =
    { RingUrl: Uri
      CancellationToken: CancellationToken option
      ClientId: Guid
      LogOutputDir: string }

type Msg =
    { Timestamp: DateTime
      Payload: byte[]
      Type: M
      Scope: MsgScope }

and MsgScope =
    | Server
    | Workspace
    | Runnable of id: string
    | Ack of Ack
    | Unknown

module Patterns =

    let (|Runnable|_|) =
        function
        | { Scope = Runnable id } -> Some id
        | _ -> None

    let (|RunnableHealthy|_|) =
        function
        | { Type = M.RUNNABLE_HEALTHY
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableHealthCheck|_|) =
        function
        | { Type = M.RUNNABLE_HEALTH_CHECK
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableInitiated|_|) =
        function
        | { Type = M.RUNNABLE_INITIATED
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableStarted|_|) =
        function
        | { Type = M.RUNNABLE_STARTED
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableStopped|_|) =
        function
        | { Type = M.RUNNABLE_STOPPED
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableDestroyed|_|) =
        function
        | { Type = M.RUNNABLE_DESTROYED
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let (|RunnableRecovering|_|) =
        function
        | { Type = M.RUNNABLE_RECOVERING
            Scope = MsgScope.Runnable id } -> Some id
        | _ -> None

    let private WsSerializerOptions =
        let opts = JsonSerializerOptions()
        opts.Converters.Add(JsonStringEnumConverter())
        opts

    let (|WorkspaceInfo|_|) (msg: Msg) =
        match msg with
        | { Type = M.WORKSPACE_INFO_PUBLISH } ->
            Some(JsonSerializer.Deserialize<WorkspaceInfo>(msg.Payload, WsSerializerOptions))
        | _ -> None

    let (|ServerShutdown|_|) (msg: Msg) =
        match msg with
        | { Type = M.SERVER_SHUTDOWN } -> Some()
        | _ -> None

    let (|Ack|_|) (msg: Msg) =
        match msg with
        | { Scope = Ack ack } -> Some ack
        | _ -> None

    type Ack =
        static member value(expectedId: Queil.Ring.Protocol.Ack) =
            function
            | Ack actualId when actualId = expectedId -> true
            | _ -> false

        static member taskOk = Ack.value Ack.TaskOk

    type Runnable =

        static member private idOrAny actual expected =
            match expected with
            | Some id -> id = actual
            | _ -> true

        static member healthy(?expectedId) =
            function
            | RunnableHealthy actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member started(?expectedId) =
            function
            | RunnableStarted actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member healthCheck(?expectedId) =
            function
            | RunnableHealthCheck actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member stopped(?expectedId) =
            function
            | RunnableStopped actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member initiated(?expectedId) =
            function
            | RunnableInitiated actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member destroyed(?expectedId) =
            function
            | RunnableDestroyed actualId when Runnable.idOrAny actualId expectedId -> true
            | _ -> false

        static member byId expectedId =
            function
            | Runnable x when x = expectedId -> true
            | _ -> false

[<RequireQualifiedAccess>]
module Workspace =
    open Patterns

    let infoLike predicate =
        function
        | WorkspaceInfo info when predicate info -> true
        | _ -> false

    let info =
        function
        | WorkspaceInfo info -> Some info
        | _ -> None

[<RequireQualifiedAccess>]
module Server =
    open Patterns

    let shutdown =
        function
        | ServerShutdown _ -> true
        | _ -> false

type WsClient(options: ClientOptions) =
    let buffer = Channel.CreateUnbounded<Msg>()
    let cache = Channel.CreateUnbounded<Msg>()

    let events =
        buffer.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.map (fun m ->
            printfn "%A" m.Type
            cache.Writer.TryWrite(m) |> ignore
            m)

    let allEvents =
        cache.Reader.ReadAllAsync() |> AsyncSeq.ofAsyncEnum |> AsyncSeq.cache

    let cancellationToken =
        options.CancellationToken |> Option.defaultValue CancellationToken.None

    let mutable listenTask = Task.CompletedTask
    let mutable terminateRequested = false

    let socket =
        lazy
            (task {
                let mutable s = new ClientWebSocket()
                use connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20))

                while s.State <> WebSocketState.Open
                      && not <| connectionTimeout.IsCancellationRequested do
                    try
                        do! s.ConnectAsync(Uri(options.RingUrl, $"ws?clientId={options.ClientId}"), cancellationToken)
                    with :? WebSocketException as ex ->
                        printfn $"Test client failed to connect to Ring: {ex.Message}. Reconnecting..."
                        s.Dispose()
                        s <- new ClientWebSocket()
                        do! Task.Delay(TimeSpan.FromSeconds(1))

                listenTask <-
                    s.ListenAsync(
                        WebSocketExtensions.HandleMessage(fun m t ->

                            try
                                let msg =
                                    { Timestamp = DateTime.Now.ToLocalTime()
                                      Type = m.Type
                                      Payload = m.Payload.ToArray()
                                      Scope =
                                        match m.Type with
                                        | M.RUNNABLE_HEALTHY
                                        | M.RUNNABLE_HEALTH_CHECK
                                        | M.RUNNABLE_STARTED
                                        | M.RUNNABLE_STOPPED
                                        | M.RUNNABLE_DESTROYED
                                        | M.RUNNABLE_INITIATED
                                        | M.RUNNABLE_RECOVERING
                                        | M.RUNNABLE_UNRECOVERABLE -> MsgScope.Runnable m.PayloadString
                                        | M.WORKSPACE_INFO_PUBLISH -> Workspace
                                        | M.SERVER_IDLE
                                        | M.SERVER_LOADED
                                        | M.SERVER_RUNNING
                                        | M.SERVER_SHUTDOWN -> Server
                                        | M.ACK ->
                                            Ack(
                                                if m.Payload.Length > 0 then
                                                    LanguagePrimitives.EnumOfValue m.Payload[0]
                                                else
                                                    Ack.Ok
                                            )
                                        | _ -> Unknown }

                                if not <| buffer.Writer.TryWrite(msg) then
                                    failwithf $"Could not write: %A{msg}"
                            with ex ->
                                eprintfn $"%A{ex}"

                            Task.CompletedTask

                        ),
                        WebSocketRole.Client,
                        cancellationToken
                    )

                return s
            })

    member _.AllEvents = allEvents

    member _.NewEvents = events

    member _.LoadWorkspace(path: string) =
        task {
            let! s = socket.Value
            do! s.SendMessageAsync(Message(M.LOAD, path))
        }

    member _.StartWorkspace() =
        task {
            let! s = socket.Value
            do! s.SendMessageAsync(M.START)
        }

    member _.StopWorkspace() =
        task {
            let! s = socket.Value
            do! s.SendMessageAsync(M.STOP)
        }

    member _.RequestWorkspaceInfo() =
        task {
            let! s = socket.Value
            do! s.SendMessageAsync(M.WORKSPACE_INFO_RQ)
        }

    member _.ExecuteTask(runnableId: string, taskId: string) =
        task {
            let! s = socket.Value

            do!
                s.SendMessageAsync(
                    Message(M.RUNNABLE_EXECUTE_TASK, RunnableTask(RunnableId = runnableId, TaskId = taskId).Serialize())
                )
        }

    member _.Terminate() =
        task {
            if terminateRequested then
                ()
            else
                terminateRequested <- true
                let! s = socket.Value
                do! s.SendMessageAsync(M.TERMINATE, cancellationToken)
        }

    member _.Connect() =
        task {
            let! _ = socket.Value
            ()
        }

    member _.HasEverConnected = socket.IsValueCreated

    interface IAsyncDisposable with
        member x.DisposeAsync() =
            ValueTask(
                task {
                    if socket.IsValueCreated then
                        try
                            let! s = socket.Value
                            do! listenTask
                            buffer.Writer.Complete()
                            cache.Writer.Complete()

                            let eventLog =
                                x.AllEvents
                                |> AsyncSeq.map (fun m ->
                                    match m with
                                    | x when x.Payload.Length = 0 -> m.Type |> string
                                    | x -> $"%A{x.Type}|%s{System.Text.Encoding.UTF8.GetString x.Payload}"
                                    |> fun pretty -> $"{m.Timestamp:``HH:mm:ss.fff``}|{pretty}")
                                |> AsyncSeq.toListAsync

                            let log = Async.RunSynchronously(eventLog, 10000)
                            Directory.CreateDirectory(options.LogOutputDir) |> ignore
                            File.AppendAllLines($"{options.LogOutputDir}/{options.ClientId}.client.log", log)
                        with :? WebSocketException as wx ->
                            printfn $"%s{wx.ToString()}"

                        let! s = socket.Value
                        s.Dispose()
                }
            )
