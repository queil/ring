module Ring.Tests.Integration.Shared

open System.IO
open Queil.Ring.Protocol
open Expecto
open Fake.Core
open Ring.Client
open Ring.Tests.Integration.DotNet.Types
open Ring.Tests.Integration.RingControl
open Ring.Tests.Integration.TestContext

let env name = Environment.environVar name

let localOptions (dir: TestDir) =
    { SrcPath = Environment.environVarOrDefault "SRC_PATH" (dir.InSourceDir "../../src/Queil.Ring.DotNet.Cli")
      PackageVersion = Environment.environVarOrDefault "PKGVER" "0.0.0-dev"
      NuGetSourcePath =
        Environment.environVarOrDefault
            "BUILD_ARTIFACTSTAGINGDIRECTORY"
            (dir.InSourceDir "../../src/Queil.Ring.DotNet.Cli/bin/Release")
      NuGetName = "atech.ring.dotnet.cli"
      NuGetConfigPath = dir.InSourceDir "../resources/NuGet.config"
      LocalTool =
        Some
            { InstallPath = dir.WorkPath
              ManifestFilePath = ".config/dotnet-tools.json" }
      WorkingDir = dir.WorkPath
      Env = []
      TestArtifactsDir = "artifacts" }

let withEnv vars options = { options with Env = vars }

let logToFile fileName (options: Options) =
    let vars =
        [ "RING_Serilog__WriteTo__0__Name", "File"
          "RING_Serilog__WriteTo__0__Args__path",
          Path.Combine(Directory.GetCurrentDirectory(), options.TestArtifactsDir, fileName)
          "RING_Serilog__WriteTo__0__Args__outputTemplate",
          "{Timestamp:HH:mm:ss.fff}|{Level:u3}|{LogEvent}|{UniqueId}|{Message}{NewLine}{Exception}"
          "RING_Serilog__WriteTo__1__Name", "" ]

    withEnv vars options

let globalOptions (dir: TestDir) =
    { localOptions dir with
        LocalTool = None }

module Expect =
    let forId (id: string) (events: (Msg option * M) seq) =

        for event in events do
            let event, typ = event

            let scope =
                ($"Should receive a message of type {typ}" |> Expect.wantSome event).Scope

            match scope with
            | Runnable expectedId -> "Runnable Id should be correct" |> Expect.equal expectedId id
            | s -> failtestf $"Expected runnable scope but got: %A{s}"

type Ring with

    member x.Stream = x.Client.NewEvents
