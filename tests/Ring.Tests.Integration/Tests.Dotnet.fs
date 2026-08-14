module Ring.Tests.Integration.DotnetRunnable

open Expecto
open FSharp.Control
open Arquidev.Tools
open Queil.Ring.Protocol
open Ring.Client
open Ring.Client.Patterns
open Ring.Tests.Integration.Async
open Ring.Tests.Integration.RingControl
open Ring.Tests.Integration.Shared
open Ring.Tests.Integration.TestContext

[<Tests>]
let tests =
    testList
        "Dotnet runnable tests"
        [ testTask "should override url via Urls and pass args" {
              use ctx = new TestContext(localOptions >> logToFile "dotnet-urls.ring.log")
              let! (ring: Ring, dir: TestDir) = ctx.Init()

              ring.Headless(debugMode = true)
              do! ring.Client.Connect()
              do! ring.Client.LoadWorkspace(dir.InSourceDir "../resources/dotnet-urls.toml")
              do! ring.Client.StartWorkspace()

              let! healthy =
                  ring.Client.NewEvents
                  |> AsyncSeq.exists (Runnable.healthy "webapp")
                  |> Async.AsTaskTimeout

              "Dotnet runnable expected healthy" |> Expect.isTrue healthy

              let! response = fetchTask<string> { GET "http://localhost:7123" }


              "Response on port 7123 should be OK" |> Expect.equal response "OK"

              let! args = fetchTask<string> { GET "http://localhost:7123/args" }

              "Args should be passed to the app" |> Expect.equal args "--ring-test-arg=42"
          }

          testTask "should execute shell task" {
              use ctx =
                  new TestContext(localOptions >> logToFile "dotnet-exec-shell-task.ring.log")

              let! (ring: Ring, dir: TestDir) = ctx.Init()

              ring.Headless(debugMode = true)
              do! ring.Client.Connect()
              do! ring.Client.LoadWorkspace(dir.InSourceDir "../resources/dotnet-urls.toml")
              do! ring.Client.StartWorkspace()

              let! events =
                  (ring.Stream
                   |> AsyncSeq.mapAsync (function
                       | RunnableHealthy "webapp" as x ->
                           async {
                               do! ring.Client.ExecuteTask("webapp", "build") |> Async.AwaitTask
                               return x
                           }

                       | x -> async { return x })
                   |> AsyncSeq.takeWhileInclusive (not << Ack.taskOk)
                   |> AsyncSeq.map (fun m -> (m.Type, m.Scope))
                   |> AsyncSeq.toListAsync
                   |> Async.AsTaskTimeout)

              "Unexpected events sequence"
              |> Expect.sequenceContainsOrder
                  events
                  [ M.RUNNABLE_STOPPED, MsgScope.Runnable "webapp"
                    M.RUNNABLE_DESTROYED, MsgScope.Runnable "webapp"
                    M.RUNNABLE_INITIATED, MsgScope.Runnable "webapp"
                    M.RUNNABLE_STARTED, MsgScope.Runnable "webapp"
                    M.ACK, MsgScope.Ack Ack.TaskOk ]
          } ]

    |> testLabel "dotnet"
