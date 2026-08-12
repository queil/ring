module Ring.Tests.Integration.LegacyConfig

open System
open Expecto
open Queil.Ring.Configuration
open Ring.Tests.Integration.TestContext

let private load (path: string) =
    let reader = ConfigurationTreeReader(ConfigurationLoader())
    reader.GetConfigTree(ConfiguratorPaths(WorkspacePath = path)) |> ignore

let private expectFails (fixture: string) (expected: string) () =
    let dir = new TestDir()
    let path = dir.InSourceDir $"../resources/legacy/{fixture}"

    Expect.throwsC (fun () -> load path) (fun ex ->
        "Expected a WorkspaceConfigException" |> Expect.isTrue (ex :? WorkspaceConfigException)

        $"Expected the error to mention: {expected}"
        |> Expect.isTrue (ex.Message.Contains(expected, StringComparison.Ordinal)))

[<Tests>]
let tests =
    testList
        "Legacy runnable types"
        [ testCase "aspnetcore is reported as renamed"
          <| expectFails "aspnetcore.toml" "`aspnetcore` was renamed to `dotnet` in v7"

          testCase "netexe is reported as removed"
          <| expectFails "netexe.toml" "`netexe` was removed in v7"

          testCase "iisexpress is reported as removed"
          <| expectFails "iisexpress.toml" "`iisexpress` was removed in v7"

          testCase "iisxcore is reported as removed"
          <| expectFails "iisxcore.toml" "`iisxcore` was removed in v7"

          testCase "workspace-level env and tasks are reported too"
          <| expectFails "env-and-tasks.toml" "`aspnetcore` was renamed to `dotnet` in v7"

          testCase "an imported workspace is reported by its own path"
          <| expectFails "import.toml" "netexe.toml" ]

    |> testLabel "legacy-config"
