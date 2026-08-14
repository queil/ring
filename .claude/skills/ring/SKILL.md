---
name: ring
description: Use when authoring or debugging ring workspaces (ring.toml / workspace TOML files), running the ring CLI (run, headless, clone, config-*), driving ring over its MCP server (--mcp), or configuring ring itself (settings.toml, RING_* env vars). Covers runnable types (proc, dotnet, dockercompose, kustomize), imports, tags/flavours, per-runnable env and tasks, and migrating pre-v7 workspaces.
---

# ring

Meta-orchestrator: runs and health-checks a workspace of apps (*runnables*) declared in TOML.
Single workspace per instance; websocket server (default port 7999) for clients (VS/VS Code extensions).

Note: `docs/` in this repo is marked WIP and partly stale (e.g. it still says clones land in `%TEMP%/ring/repos`).
Source of truth: `src/Queil.Ring.Configuration/` (schema) and `src/Queil.Ring.DotNet.Cli/` (behaviour).

## CLI

```
ring run [-w path] [-l secs] [-p port] [-d] [-n]   # stand-alone: load workspace + start
ring headless [-p port] [-d] [-n]                  # serve; a client loads the workspace
ring clone [-w path] [-o dir]                      # clone repos of runnables with sshRepoUrl
ring config-path (--local|--user|--default)
ring config-create (--local|--user)
ring config-dump
```

* `-w` defaults to `ring.toml` in cwd; missing file → error, no fallback.
* `-d` debug logging, `-n` hide logo, `-l` delay load+start, `-p` port (default 7999).
* `--mcp` on `run`/`headless` serves MCP over stdio instead of the console client — see below.

## MCP server

`ring headless --mcp` (no workspace) or `ring run --mcp -w path` (loads + starts that workspace on
startup). Transport is stdio, so stdout carries JSON-RPC only — ring nulls `Console.Out` and skips the
logo in this mode. The websocket server still starts, so pass `-p 0` unless you want it on 7999; two ring
instances on the default port collide.

Editor config looks like:

```json
{ "command": "ring", "args": ["headless", "--mcp", "--port", "0", "--no-logo"] }
```

Tools (all return plain text; each has a proper MCP description):

| tool | args | returns |
|---|---|---|
| `get_workspace_info` | – | the `WorkspaceInfo` JSON (runnables, states, flavours, tasks) |
| `load_workspace` | `workspacePath` | `loaded`, `not found: <path>`, or a timeout message (see below) |
| `start_workspace` / `stop_workspace` / `unload_workspace` | – | `started` / `stopped` / `unloaded` |
| `include_runnable` / `exclude_runnable` | `id` | `included` / `excluded`, or `not found: <id>` |
| `apply_flavour` | `flavour` | `applied` or `not found: <flavour>` |
| `list_tasks` | – | `<runnableId>/<taskId>` per line, or `no tasks available` |
| `execute_task` | `runnableId`, `taskId` | `ok`, `task failed`, or `not found: <id>/<task>` |

Workflow: `load_workspace` → `start_workspace` → poll `get_workspace_info` until the runnables report
`HEALTHY` (states are the same `RunnableState` values the websocket clients see). There are no
notifications — polling is the only way to observe progress.

Caveats worth knowing:

* `load_workspace` on an invalid workspace (bad TOML, legacy v7 type names) returns a timeout message
  after 15s while ring keeps retrying every 5s in the background — fix the file and it loads on its own.
  A missing file returns `not found` immediately.
* `start/stop/unload_workspace` always report success — the underlying server has no failure signal there.
* Logs never go to stdout in this mode; they land in `$TMPDIR/ring-mcp.log` (warnings+, or everything with `-d`).

## Workspace TOML

Runnables are arrays of tables. `[<type>.env]` / `[<type>.tasks.<name>]` attach to the **last declared**
element of that array (plain TOML semantics) — declare them right after the runnable they belong to.

```toml
[[proc]]
id = "api"
command = "dotnet"
args = ["watch", "--project", "src/api"]
tags = ["backend"]

[proc.env]
URLS = "https://localhost:8080"
```

### Types and keys

Every runnable: `id`, `friendlyName`, `tags` (string[]), `[<type>.env]`, `[<type>.tasks.<name>]`.

| type | keys | notes |
|---|---|---|
| `proc` | `command` (req), `args`, `workingDir` | `id ?? command` is the identity |
| `dotnet` | `csproj` (req), `urls`, `args`, `sshRepoUrl`, `configuration` (default `Debug`), `workingDir` | `urls` joined by `;` into `ASPNETCORE_URLS` |
| `dockercompose` | `path` (req), `sshRepoUrl`, `workingDir` | `docker compose rm` + `pull` on init |
| `kustomize` | `path` (req), `sshRepoUrl`, `workingDir` | any go-getter path; `git@`/`ssh://` = remote |

Identity (used for dedup across imports): `id` if set, else `path` (kustomize/dockercompose), `command` (proc),
or csproj file name without extension. Two runnables with the same identity run once — set distinct `id`s to run
multiple instances of the same app.

`workingDir`: relative to the directory of the TOML file that declares the runnable; that directory is the default.
Relative `path`/`csproj` resolve against `workingDir`.

`sshRepoUrl`: ring clones (or pulls) into `git.clonePath` (default `$HOME/.ring/repos`). With `sshRepoUrl` set,
`csproj` **must** be relative — it resolves inside the clone.

### Imports

```toml
imports = ["a.toml", "b.toml"]      # simplified

[[import]]                          # classic — equivalent
path = "a.toml"
```

Paths are relative to the importing file. Duplicated apps across imports are deduplicated by identity.

### Workspace-level env and tasks

Applied to every runnable of that type in this file **and all files it imports**; a runnable's own
`env`/`tasks` key wins, and a nested workspace's keys win over the parent's.

```toml
[env.dotnet]
SUAVE_PORT = "4444"

[tasks.dotnet.build]
bringDown = true
command = "dotnet"
args = ["build"]
```

`env` only reaches `proc` and `dotnet` — `kustomize`/`dockercompose` ignore it.

### Tasks

Named shell commands triggered by a client (VS Code / VS extension), not by the CLI. Run in the runnable's
working dir. `bringDown = true` stops the app first and restarts it only if the task succeeded — that is the
way to rebuild, because ring builds a dotnet project only when its output assembly is missing.

### Flavours

Tags double as flavours. Applying a flavour keeps apps tagged with it running, stops the rest, starts the
missing ones — cheaper than stop/start of overlapping workspaces. Applied via a client, not the CLI.

## Configuring ring itself

`settings.toml` in three scopes — default (shipped, don't edit) < user < local (cwd); env vars win over all.
Env var form: `RING_` + key with `.` → `__`, e.g. `hooks.init.command` → `RING_HOOKS__INIT__COMMAND`.

* `git.clonePath` — default `$HOME/.ring/repos`
* `kustomize.cachePath` — default `$HOME/.ring/kustomize-cache`
* `kubernetes.configPath` — default `$HOME/.kube/config`; `KUBECONFIG` takes precedence
* `kubernetes.allowedContexts` — default `["docker-desktop", "rancher-desktop", "minikube"]`; cluster changes fail outside this list
* `workspace.startupSpreadFactor` — spreads app startup over time; shipped default `0`
* `hooks.init.command` / `hooks.init.args` — run on workspace init

## Behaviour worth knowing

* Ring watches the workspace file **and all imported files**; edits reload the config live.
* A broken workspace (missing file, invalid TOML) is logged and retried every 5s rather than fatal.
* dotnet runnables: built only if `bin/<configuration>/<tfm>/<proj>.dll` is missing; run via the `.exe` if present,
  otherwise `dotnet exec` (not `dotnet run` — it orphans child processes).
* Health check for proc/dotnet/dockercompose is "is the process alive"; unhealthy apps get restarted.
* The `type` a runnable reports to clients is its TOML type id (`dotnet`, `proc`, ...), not a C# class name.

## Migrating from pre-v7 workspaces

v7 removed `netexe`, `iisexpress` and `iisxcore` (Windows/.NET Framework only) and renamed `aspnetcore` to
`dotnet`. A workspace still using any of them fails to load with a message naming the file and what to change —
including when the name only appears in `[env.<type>]` / `[tasks.<type>.*]`. Fixes: rename `[[aspnetcore]]` (and
its `[aspnetcore.*]` / `[env.aspnetcore]` / `[tasks.aspnetcore.*]` tables) to `dotnet`; replace `netexe` with
`proc`; replace `iisxcore` with `dotnet`. Also in v7: `ASPNETCORE_ENVIRONMENT=Development` is no longer forced —
set it in `env` if the app needs it.

## Troubleshooting

* Run with `-d` first; `ring config-dump` to check effective tool config, `ring config-path --local|--user` for file locations.
* App exits immediately → check its own logs; ring only reports liveness.
* Wrong app started / one instance instead of two → identity collision, set explicit `id`s.
* Env var not applied → check runnable type supports `env` and that `[<type>.env]` follows the intended `[[<type>]]`.
