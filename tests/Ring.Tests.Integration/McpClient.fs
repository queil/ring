namespace Ring.Tests.Integration

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Ring.Tests.Integration.DotNet.Types
open Ring.Tests.Integration.RingControl

module McpClient =

    type McpProcess(options: Options, ?workspacePath: string) =
        let mutable msgId = 0
        let proc = new Process()

        let nextId () =
            msgId <- msgId + 1
            msgId

        let sendMessage (msg: string) =
            let bytes = Encoding.UTF8.GetBytes(msg)
            let header = $"Content-Length: {bytes.Length}\r\n\r\n"
            proc.StandardInput.Write(header)
            proc.StandardInput.Write(msg)
            proc.StandardInput.Flush()

        let readMessage () =
            task {
                let mutable contentLength = 0
                let mutable line = proc.StandardOutput.ReadLine()
                while line <> null && line <> "" do
                    if line.StartsWith("Content-Length:") then
                        contentLength <- int (line.Substring("Content-Length:".Length).Trim())
                    line <- proc.StandardOutput.ReadLine()
                if contentLength > 0 then
                    let buf = Array.zeroCreate<char> contentLength
                    let! _ = proc.StandardOutput.ReadAsync(buf, 0, contentLength) |> Async.AwaitTask
                    return String(buf)
                else
                    return ""
            }

        let callRpc (method: string) (paramsObj: obj) =
            task {
                let id = nextId ()
                let msg =
                    JsonSerializer.Serialize(
                        {| jsonrpc = "2.0"
                           id = id
                           method = method
                           ``params`` = paramsObj |}
                    )
                sendMessage msg
                let! response = readMessage ()
                return JsonDocument.Parse(response)
            }

        member _.Start() =
            let name, cmdArgs =
                match options.LocalTool with
                | None -> "ring", [ "mcp"; "--no-logo" ]
                | Some _ -> "dotnet", [ "ring"; "mcp"; "--no-logo" ]

            let allArgs =
                cmdArgs
                @ (match workspacePath with
                   | Some p -> [ "--workspace"; p ]
                   | None -> [])

            proc.StartInfo <- ProcessStartInfo(name, allArgs)
            proc.StartInfo.UseShellExecute <- false
            proc.StartInfo.RedirectStandardInput <- true
            proc.StartInfo.RedirectStandardOutput <- true
            proc.StartInfo.WorkingDirectory <- options.WorkingDir

            for k, v in options.Env do
                proc.StartInfo.EnvironmentVariables[k] <- v

            proc.Start() |> ignore

        member _.Initialize() =
            task {
                let! _ =
                    callRpc
                        "initialize"
                        {| protocolVersion = "2024-11-05"
                           capabilities = {||}
                           clientInfo = {| name = "ring-test"; version = "1.0" |} |}

                sendMessage
                    (JsonSerializer.Serialize(
                        {| jsonrpc = "2.0"
                           method = "notifications/initialized"
                           ``params`` = {||} |}
                    ))
            }

        member _.ListTools() =
            task {
                let! doc = callRpc "tools/list" {||}
                return
                    doc.RootElement
                        .GetProperty("result")
                        .GetProperty("tools")
                        .EnumerateArray()
                    |> Seq.map (fun t -> t.GetProperty("name").GetString())
                    |> Seq.toList
            }

        member _.CallTool(name: string, ?args: (string * string) list) =
            task {
                let arguments =
                    match args with
                    | None -> dict []
                    | Some pairs -> pairs |> List.map (fun (k, v) -> k, v :> obj) |> dict

                let! doc =
                    callRpc
                        "tools/call"
                        {| name = name
                           arguments = arguments |}

                let result = doc.RootElement.GetProperty("result")
                return
                    result.GetProperty("content").EnumerateArray()
                    |> Seq.map (fun c -> c.GetProperty("text").GetString())
                    |> String.concat ""
            }

        interface IAsyncDisposable with
            member _.DisposeAsync() =
                try
                    if not proc.HasExited then proc.Kill()
                    proc.Dispose()
                with _ ->
                    ()
                ValueTask.CompletedTask

        interface IDisposable with
            member this.Dispose() =
                (this :> IAsyncDisposable).DisposeAsync().GetAwaiter().GetResult()

    type Ring with

        member x.McpProcess(?workspacePath: string) =
            new McpProcess(x.Options, ?workspacePath = workspacePath)
