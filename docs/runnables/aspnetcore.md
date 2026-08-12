# ASP.NET Core runnable

It runs ASP.NET Core and other .NET Core apps. 

## Syntax

```toml
[[aspnetcore]]
sshRepoUrl = "git@your.git.server:path/to/repo.git"
csproj = "path/to/your/project.name.csproj"
urls = ["http://localhost:6201/"]
```
## Config keys

* `sshRepoUrl` (optional `string`) - if set Ring clones the default branch (usually `main`) of the specified repo and attempts to build and execute the project specified by the `csproj` key.
Ring clones into `git.clonePath` (default `$HOME/.ring/repos`, see [configuration](../configuration.md#configuration-keys)). If the clone already exists Ring performs `git pull` instead.

* `csproj` (mandatory `string`) - if `sshRepoUrl` is used then `csproj` must be a relative path and the project is loaded from `${git.clonePath}/path/to/repo/${csproj}`. If `sshRepoUrl` is not set then
`csproj` may be either absolute or relative.

* `urls` (optional `string[]`) - one or more URLs that are passed to the `ASPNETCORE_URLS` env variable

* `configuration` (optional `string`) - the build configuration to build and run. Default: `Debug`

Plus the keys common to all apps: `id`, `friendlyName`, `tags`, `workingDir`, `env`, `tasks` -
see [authoring workspaces](../authoring-workspaces.md).

## How it works

Given project name is `project.name`
Ring scans project's build ouput for either a `project.name.exe` file (.NET Core 3.1) or `project.name.dll`. Exes are run directly whereas dlls
are executed using `dotnet exec`.

Ring only builds the project if the output assembly is missing. To force a rebuild use a
[task](../authoring-workspaces.md#tasks) with `bringDown = true`.

## Environment variables

Ring passes the following env variables to the spawned process:

* `ASPNETCORE_ENVIRONMENT` = `Development`
* `ASPNETCORE_URLS` = the value of `urls` from runnable configuration (values joined by `;`)
* everything from the `env` table of the app

## Health check

Ring does a simple *"is the process alive"* check. 
