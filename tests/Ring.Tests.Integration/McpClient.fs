module Ring.Tests.Integration.McpClient

open System
open System.Diagnostics
open System.Text.Json.Nodes
open System.Threading.Tasks

let private jv<'t> (v: 't) : JsonNode = JsonValue.Create(v) :> JsonNode

type McpProcess(command: string, args: string list, workingDir: string, env: (string * string) list) =
    let proc =
        let psi = ProcessStartInfo(command)
        args |> List.iter psi.ArgumentList.Add
        psi.WorkingDirectory <- workingDir
        psi.UseShellExecute <- false
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        env |> List.iter (fun (k, v) -> psi.EnvironmentVariables.[k] <- v)
        let p = Process()
        p.StartInfo <- psi
        p

    let mutable msgId = 0

    member _.Start() = proc.Start() |> ignore

    member _.SendLine(json: string) =
        task {
            do! proc.StandardInput.WriteLineAsync(json)
            do! proc.StandardInput.FlushAsync()
        }

    member _.ReadResponse(id: int) : Task<JsonNode> =
        task {
            let mutable found = false
            let mutable response = Unchecked.defaultof<JsonNode>
            while not found do
                let! line = proc.StandardOutput.ReadLineAsync()
                if line = null then
                    failwith $"Process exited before response for id={id} was received"
                elif not (String.IsNullOrWhiteSpace line) then
                    try
                        let node = JsonNode.Parse(line)
                        let nodeId =
                            match node.["id"] with
                            | null -> None
                            | n ->
                                try Some(n.GetValue<int>())
                                with _ -> None
                        match nodeId with
                        | Some nid when nid = id ->
                            response <- node
                            found <- true
                        | _ -> ()
                    with _ -> ()
            return response
        }

    member this.Request(method: string, ``params``: JsonNode) : Task<JsonNode> =
        task {
            msgId <- msgId + 1
            let id = msgId
            let msg = JsonObject()
            msg.["jsonrpc"] <- jv "2.0"
            msg.["id"] <- jv id
            msg.["method"] <- jv method
            msg.["params"] <- ``params``
            do! this.SendLine(msg.ToJsonString())
            return! this.ReadResponse(id)
        }

    member this.Notify(method: string, ``params``: JsonNode) =
        task {
            let msg = JsonObject()
            msg.["jsonrpc"] <- jv "2.0"
            msg.["method"] <- jv method
            msg.["params"] <- ``params``
            do! this.SendLine(msg.ToJsonString())
        }

    member this.Initialize() =
        task {
            let p = JsonObject()
            p.["protocolVersion"] <- jv "2024-11-05"
            p.["capabilities"] <- JsonObject() :> JsonNode
            let info = JsonObject()
            info.["name"] <- jv "ring-test"
            info.["version"] <- jv "0.0.1"
            p.["clientInfo"] <- info :> JsonNode
            let! _ = this.Request("initialize", p :> JsonNode)
            do! this.Notify("notifications/initialized", JsonObject() :> JsonNode)
        }

    member this.ListTools() : Task<string list> =
        task {
            let! resp = this.Request("tools/list", JsonObject() :> JsonNode)
            return
                resp.["result"].["tools"].AsArray()
                |> Seq.map (fun t -> t.["name"].GetValue<string>())
                |> List.ofSeq
        }

    member this.CallTool(name: string, ?args: (string * string) list) : Task<string> =
        task {
            let arguments = JsonObject()
            (args |> Option.defaultValue []) |> List.iter (fun (k, v) -> arguments.[k] <- jv v)
            let p = JsonObject()
            p.["name"] <- jv name
            p.["arguments"] <- arguments :> JsonNode
            let! resp = this.Request("tools/call", p :> JsonNode)
            return resp.["result"].["content"].[0].["text"].GetValue<string>()
        }

    member private _.Cleanup() =
        try
            if not proc.HasExited then
                proc.StandardInput.Close()
                if not (proc.WaitForExit 3000) then
                    proc.Kill()
        finally
            proc.Dispose()

    interface IDisposable with
        member this.Dispose() = this.Cleanup()

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            this.Cleanup()
            ValueTask.CompletedTask
