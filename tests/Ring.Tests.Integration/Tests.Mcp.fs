module Ring.Tests.Integration.Mcp

open System.Diagnostics
open System.Threading.Tasks
open Expecto
open Ring.Tests.Integration.McpClient
open Ring.Tests.Integration.RingControl
open Ring.Tests.Integration.Shared
open Ring.Tests.Integration.TestContext

let private expectedTools =
    [ "apply_flavour"
      "execute_task"
      "exclude_runnable"
      "get_workspace_info"
      "include_runnable"
      "load_workspace"
      "start_workspace"
      "stop_workspace"
      "unload_workspace" ]

let private pollUntil (timeoutMs: int) (condition: string -> bool) (poll: unit -> Task<string>) =
    task {
        let sw = Stopwatch.StartNew()
        let mutable ok = false

        while not ok && sw.ElapsedMilliseconds < int64 timeoutMs do
            let! info = poll ()

            if condition info then
                ok <- true
            else
                do! Task.Delay 500

        return ok
    }

[<Tests>]
let tests =
    testList
        "MCP tests"
        [ testTask "tools/list registers all ring tools" {
              use ctx = new TestContext(localOptions)
              let! (ring: Ring, _) = ctx.Init()
              let mcp = ring.McpProcess()
              use _mcp = mcp
              mcp.Start()
              do! mcp.Initialize()
              let! tools = mcp.ListTools()

              for name in expectedTools do
                  $"Tool '{name}' should be listed" |> Expect.contains tools name
          }

          testTask "get_workspace_info returns IDLE state before any workspace is loaded" {
              use ctx = new TestContext(localOptions)
              let! (ring: Ring, _) = ctx.Init()
              let mcp = ring.McpProcess()
              use _mcp = mcp
              mcp.Start()
              do! mcp.Initialize()
              let! (info: string) = mcp.CallTool("get_workspace_info")
              "Should report IDLE server state" |> Expect.isTrue (info.Contains("IDLE"))
          }

          testTask "load_workspace + start_workspace makes services healthy" {
              use ctx = new TestContext(localOptions)
              let! (ring: Ring, dir: TestDir) = ctx.Init()
              let workspace = dir.InSourceDir "../resources/basic/proc.toml"
              let mcp = ring.McpProcess()
              use _mcp = mcp
              mcp.Start()
              do! mcp.Initialize()

              let! (loadResult: string) = mcp.CallTool("load_workspace", [ "workspacePath", workspace ])
              "Load should succeed" |> Expect.isTrue (loadResult.Contains("loaded"))

              let! (startResult: string) = mcp.CallTool("start_workspace")
              "Start should succeed" |> Expect.isTrue (startResult.Contains("started"))

              let! ok =
                  pollUntil
                      30000
                      (fun info -> info.Contains("HEALTHY"))
                      (fun () -> mcp.CallTool "get_workspace_info")

              "Workspace should reach HEALTHY state" |> Expect.isTrue ok

              let! (info: string) = mcp.CallTool("get_workspace_info")
              "Should show proc-1" |> Expect.isTrue (info.Contains("proc-1"))
              "Should show proc-2" |> Expect.isTrue (info.Contains("proc-2"))
          }

          testTask "exclude_runnable drops a service to ZERO state" {
              use ctx = new TestContext(localOptions)
              let! (ring: Ring, dir: TestDir) = ctx.Init()
              let workspace = dir.InSourceDir "../resources/basic/proc.toml"
              let mcp = ring.McpProcess()
              use _mcp = mcp
              mcp.Start()
              do! mcp.Initialize()
              let! _ = mcp.CallTool("load_workspace", [ "workspacePath", workspace ])
              let! _ = mcp.CallTool("start_workspace")
              let! _ = pollUntil 30000 (fun info -> info.Contains("HEALTHY")) (fun () -> mcp.CallTool "get_workspace_info")

              let! (excludeResult: string) = mcp.CallTool("exclude_runnable", [ "id", "proc-1" ])
              "Exclude should succeed" |> Expect.isTrue (excludeResult.Contains("excluded"))

              let! ok =
                  pollUntil
                      10000
                      (fun info -> info.Contains("\"ZERO\""))
                      (fun () -> mcp.CallTool "get_workspace_info")

              "proc-1 should reach ZERO state after exclusion" |> Expect.isTrue ok
          }

          testTask "auto-loads and starts workspace when --workspace flag is provided" {
              use ctx = new TestContext(localOptions)
              let! (ring: Ring, dir: TestDir) = ctx.Init()
              let workspace = dir.InSourceDir "../resources/basic/proc.toml"
              let mcp = ring.McpProcess(workspacePath = workspace)
              use _mcp = mcp
              mcp.Start()
              do! mcp.Initialize()

              let! ok =
                  pollUntil
                      30000
                      (fun info -> info.Contains("proc-1") && info.Contains("proc-2"))
                      (fun () -> mcp.CallTool "get_workspace_info")

              "Workspace should be auto-loaded with both procs visible" |> Expect.isTrue ok
          } ]
    |> testLabel "mcp"
