# Dotnet runnable

It runs .NET apps in a console (like ASP.NET Core in Kestrel).

## Syntax

```toml
[[dotnet]]
sshRepoUrl = "git@your.git.server:path/to/repo.git"
csproj = "path/to/your/project.name.csproj"
urls = ["http://localhost:6201/"]
args = ["--my-switch=42"]
```
## Config keys

* `sshRepoUrl` (optional `string`) - if set Ring clones the default branch (usually `main`) of the specified repo and attempts to build and execute the project specified by the `csproj` key.
Ring clones into `git.clonePath` (default `$HOME/.ring/repos`, see [configuration](../configuration.md#configuration-keys)). If the clone already exists Ring performs `git pull` instead.

* `csproj` (mandatory `string`) - if `sshRepoUrl` is used then `csproj` must be a relative path and the project is loaded from `${git.clonePath}/path/to/repo/${csproj}`. If `sshRepoUrl` is not set then
`csproj` may be either absolute or relative.

* `urls` (optional `string[]`) - one or more URLs that are passed to the `ASPNETCORE_URLS` env variable

* `args` (optional `string[]`) - arguments passed to the app. They are joined with spaces, so arguments containing spaces don't work.

* `configuration` (optional `string`) - the build configuration to build and run. Default: `Debug`

Plus the keys common to all apps: `id`, `friendlyName`, `tags`, `workingDir`, `env`, `tasks` -
see [authoring workspaces](../authoring-workspaces.md).

## How it works

Given project name is `project.name`
Ring scans project's build ouput for either a `project.name.exe` file or `project.name.dll`. Exes are run directly whereas dlls
are executed using `dotnet exec`.

Ring only builds the project if the output assembly is missing. To force a rebuild use a
[task](../authoring-workspaces.md#tasks) with `bringDown = true`.

## Environment variables

Ring passes the following env variables to the spawned process:

* `ASPNETCORE_URLS` = the value of `urls` from runnable configuration (values joined by `;`)
* everything from the `env` table of the app

Nothing else is set for you - if your app needs `ASPNETCORE_ENVIRONMENT`, set it in `env`.

## Health check

Ring does a simple *"is the process alive"* check.
