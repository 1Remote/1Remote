# Development

This guide takes you from an empty Windows machine to a running build of 1Remote.

**Short version:** install Git and [Visual Studio 2026] with the **.NET desktop development** workload (a workload is a set of features that you choose in the Visual Studio Installer), clone the repository together with its submodules, open `1Remote.sln`, make `Ui` the startup project and press `F5`.

Visual Studio 2022 no longer works. 1Remote is built on .NET 10, and the .NET 10 SDK requires Visual Studio 2026. The reasons are in [Why Visual Studio 2022 no longer works](#why-visual-studio-2022-no-longer-works).

## Prerequisites

This guide was verified on Windows 11 only. On Windows 10, .NET 10 supports only the Enterprise edition and LTSC (Long-Term Servicing Channel, the Windows 10 versions that get security fixes but no new features), see [Microsoft's support table][dotnet-windows]. On other Windows 10 editions the steps below may not work.

| What | Version | Needed for |
| --- | --- | --- |
| [Git] | current version | Getting the code and the submodules |
| [Visual Studio 2026] | 18.0 or later | Required. Build and debug the app |
| Workload **.NET desktop development** | | Required to build and run the app. It brings the .NET SDK (the compiler and build tools for .NET) and the tools for WPF (Windows Presentation Foundation) and Windows Forms, the two interface frameworks that the app uses |
| Workload **Universal Windows Platform development**, plus the individual component **Windows 10 SDK (10.0.19041.0)** | | Only the `Installer` project. Its build needs a signing certificate that is not in the repository, see [The `Installer` project](#the-installer-project). Leave both out |
| [.NET 11 SDK] | 11.0 or later | Only to build for .NET 11, see [Packages](#packages). The .NET 10 SDK builds the .NET 9 packages |

### Get the code

```powershell
git clone --recursive https://github.com/1Remote/1Remote.git
```

`--recursive` also downloads the submodules. [Submodules](#submodules) explains what they are and what to do if you cloned without this switch.

### Why Visual Studio 2022 no longer works

1Remote targets .NET 10 by default (`Ui/Ui.csproj`). Microsoft lists Visual Studio 2026 as the minimum version for the .NET 10 SDK ([table ".NET versions and Visual Studio"][dotnet-vs]). In line with that, a build in Visual Studio 2022 fails. MSBuild (the build engine inside Visual Studio) from Visual Studio 2022 cannot use the .NET SDK for .NET 10. It uses the newest older .NET SDK instead, and the build stops with:

```
error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.
```

## Submodules

Several customized libraries are pinned as submodules (a submodule is a separate Git repository that this repository includes at one fixed commit). They are forks (copies of other people's repositories that 1Remote can change on its own), mostly under the [1Remote org](https://github.com/orgs/1Remote/repositories) (plus [VShawn/Shawn.Utils](https://github.com/VShawn/Shawn.Utils)). Their customizations live on the forks' default branches (`master`, or `main` for the bundled PuTTY). For example, `Dragablz` adds `InterTabController.ReentryGrace` (a wider hit zone when dragging a detached tab back into a window), and the libraries are set up to build for several .NET versions.

After cloning, run `git submodule update --init --recursive` (or clone with `--recursive`), or the build will fail on missing projects.

When changing a submodule, push the fork's default branch **before** bumping the pointer in the parent repo (that is, before you commit the new commit hash of the submodule in the parent repo). Otherwise the parent references a commit nobody else can fetch.

## Build in Visual Studio

`Ui` is the application itself. The other C# projects are the submodule libraries described above, plus `Installer`, which is covered [in the next section](#the-installer-project).

1. Open `1Remote.sln` in Visual Studio 2026.
2. In Solution Explorer, right-click the `Ui` project and choose **Set as Startup Project**.
3. Keep **Debug** as the configuration and **Any CPU** as the platform, then press `F5`. Visual Studio restores the NuGet packages (it downloads the libraries that the code depends on) when it builds.

1Remote keeps its data in one of two places: next to `1Remote.exe` (**Portable mode**, selected by default when that folder is writable) or in a folder under `%AppData%` (**Install for current windows account**). On its first start, the app asks you to choose between the two.

A Debug build uses the name `1Remote_Debug` for its settings file (`1Remote_Debug.json`), its database (`1Remote_Debug.db`) and its folder under `%AppData%` (`1Remote_Debug`). So a Debug build keeps its data apart from an installed 1Remote, and you can run both at the same time.

### The `Installer` project

`Installer` builds the version of the app that goes to the Microsoft Store. You do not need it to build or run 1Remote:

- Building it takes the **Universal Windows Platform development** workload.
- Signing the result takes the certificate `Installer/Installer_TemporaryKey.pfx`. The certificate is not in the repository, so only someone with that certificate can finish the build.
- The `Debug` configuration with the `Any CPU` platform leaves the project out. Most other configurations, `Release` for example, build it too and then fail. Which error you get depends on what is missing. Two that were seen are `NETSDK1047` and, at the signing step, `APPX0104` (certificate file not found).

To get an optimized build, build the `Ui` project on its own or [publish](#packages) the app.

## Command line

Run these commands from the repository root:

```powershell
# Debug build of the app and the libraries it uses
dotnet build 1Remote.sln -c Debug

# Optimized build of the app only
dotnet build Ui/Ui.csproj -c Release
```

For `Release`, pass the `Ui` project and not the solution. The `Release` configuration of the solution also builds `Installer`, which the `dotnet` command cannot build (error `MSB4019`).

### Tests

```powershell
dotnet test Tests/Tests.csproj
```

The tests are in the `Tests` folder. That project is not part of `1Remote.sln`, and CI (short for continuous integration: the automatic builds that GitHub runs for this repository) does not run it. Run the command yourself when you change code. All tests should pass.

## Packages

A package is a single file, `1Remote.exe`, for 64-bit Windows, ready to be copied to another computer. 1Remote is published for three .NET versions, each in two forms:

- **Framework-dependent**: about 50 MB (measured on the .NET 10 package). The target computer needs the .NET Desktop Runtime of the same version (the part of .NET that runs WPF and Windows Forms apps).
- **Self-contained**: about 215 MB (measured on the .NET 10 package). It carries the runtime, so nothing else has to be installed.

A publish profile is a saved set of settings for the `dotnet publish` command. The profiles in `Ui/Properties/PublishProfiles/` produce the six packages. In a profile name, `net90` stands for .NET 9, `net100` for .NET 10 and `net110` for .NET 11. A name without `-self-contained` gives the framework-dependent package.

```powershell
# .NET 10 package that needs the .NET 10 Desktop Runtime
dotnet publish Ui/Ui.csproj -p:PublishProfile=./Ui/Properties/PublishProfiles/x64-net100.pubxml
```

| Publish profile | .NET version | Form | Output folder under `Ui/bin/Release/` |
| --- | --- | --- | --- |
| `x64-net90.pubxml` | .NET 9 | Framework-dependent | `net9.0-windows/publish/win-x64/` |
| `x64-net90-self-contained.pubxml` | .NET 9 | Self-contained | `net9.0-windows-self-contained/publish/win-x64/` |
| `x64-net100.pubxml` | .NET 10 | Framework-dependent | `net10.0-windows/publish/win-x64/` |
| `x64-net100-self-contained.pubxml` | .NET 10 | Self-contained | `net10.0-windows-self-contained/publish/win-x64/` |
| `x64-net110.pubxml` | .NET 11 | Framework-dependent | `net11.0-windows/publish/win-x64/` |
| `x64-net110-self-contained.pubxml` | .NET 11 | Self-contained | `net11.0-windows-self-contained/publish/win-x64/` |

The GitHub workflows run these same commands. `.github/workflows/build-on-dev-push.yml` runs the .NET 10 ones on every push to a `main` or `master` branch and all six when a tag is pushed. `.github/workflows/build-manual.yml` runs them when someone starts it by hand.

The .NET 10 SDK builds the .NET 9 packages too. Only .NET 11 needs its own SDK: without the [.NET 11 SDK], the build stops with `NETSDK1045`.

`Ui.csproj` also has the configurations `ReleaseNet9` and `ReleaseNet11`, which target .NET 9 and .NET 11 instead of .NET 10. You need them only to build inside Visual Studio for those versions, because the publish profiles switch the .NET version by themselves.

### What a Release or publish build changes

Every configuration except `Debug` runs three PowerShell scripts from the `scripts` folder before the app is compiled (target `PreBuild` in `Ui/Ui.csproj`):

| Script | What it does | What is left over after the build |
| --- | --- | --- |
| `Set-Secret.ps1` | Replaces the placeholders in `Ui/Assert.cs` with the telemetry keys (for App Center and Sentry, the services that collect usage data and error reports) and the encryption salt from `C:\1Remote_Secret\`, if that folder exists | Nothing. `PostBuild` puts the placeholders back |
| `Set-BuildDate.ps1` | Writes the date and time of the build into `Ui/AppVersion.cs` | Nothing. `PostBuild` empties that value again |
| `Set-AssemblyVersion.ps1` | Writes the build date as a number into `<AssemblyVersion>` in `Ui/Ui.csproj` | This line. `git status` shows `Ui/Ui.csproj` as modified |

That change is only the build date, and it does not belong in your commit. If you did not edit `Ui/Ui.csproj` yourself, undo the change with `git restore Ui/Ui.csproj`. If you did, keep your edits and discard only the build date, for example with `git restore -p Ui/Ui.csproj`, which asks you for each part of the change whether to discard it.

`C:\1Remote_Secret\` exists only on the maintainer's computer, so only there does the script insert the real values. CI passes the values to `Set-Secret.ps1` as arguments instead. It takes them from GitHub secrets (encrypted values that only the build servers of GitHub can read). On any other computer the placeholders stay as they are. That has two effects:

- The telemetry stays off. The app starts App Center and Sentry only when the real keys are present.
- The salt keeps its placeholder value. The salt is a secret string, and the encryption of saved passwords and other secrets depends on it. So a build made from a fresh clone cannot read a database that an official release has written, and an official release cannot read a database that such a build has written. 1Remote notices the mismatch and refuses the database with the message *your database is encrypted by a third-part build*.

The second effect matters when you run a build that you made yourself in any configuration other than `Debug`, for example a `Release` build or a published package. Such a build uses the name `1Remote`, the same name as an installed release. So run it from a folder that you can write to, and on its first start keep **Portable mode** selected (it is selected by default when that folder is writable). Then the data is saved next to `1Remote.exe`.

Do not pick **Install for current windows account**, and do not run the build from a folder that you cannot write to. In both cases the data goes to `%AppData%\1Remote`. The Microsoft Store version of 1Remote keeps its data there too, and other installed releases can. There your build can meet a database that it cannot read. A `Debug` build does not have this problem: it uses the name `1Remote_Debug`, so its data is apart.

## Invoke-Build tasks

The `dotnet` commands in [Command line](#command-line) are enough to build and to publish. The tasks below are an alternative. They build with the MSBuild of Visual Studio, and they also clean up, install the tools, or build inside Windows Sandbox (a temporary, clean copy of Windows).

The repository includes the [Invoke-Build] PowerShell module. Its four tasks are defined in the [1remote.build.ps1] script in the repository root:

```
PS C:\Projects\1Remote> ib ?

Name           Jobs Synopsis
----           ---- --------
Deps           {}   Ensure local dependencies
Build          {}   Build the application
BuildInSandbox {}   Build in Windows Sandbox
Clean          {}   Clean generated data

```

Open PowerShell in the repository root and give `Invoke-Build.ps1` the short name `ib`. The alias lasts only in this PowerShell window, so run the line again when you open a new one:

```powershell
Set-Alias ib $pwd\Invoke-Build.ps1

ib ?                             # list the tasks
ib Build -aReleaseType Debug     # build the app in the Debug configuration
ib Clean, Build                  # clean, then build the app in the Release configuration
```

- `Build` builds the `Ui` project and restores the NuGet packages itself. The configuration is `Release` unless you pass `-aReleaseType Debug`. The result is a normal build: `1Remote.exe` in a folder together with its `.dll` files, in `Ui/bin/Release/net10.0-windows10.0.19041.0/` or in `Ui/bin/Debug/net10.0-windows10.0.19041.0/`. It is not the single-file package from [Packages](#packages), which goes to another folder.
- `Clean` deletes the files that `Build` created and keeps everything else in that folder, for example data that 1Remote saved next to `1Remote.exe`.
- `Deps` installs the tools automatically: [Chocolatey], Visual Studio 2026 with the two workloads from the table in [Prerequisites](#prerequisites) (but not the Windows 10 SDK component), and the .NET 9, .NET 10 and .NET 11 SDKs. That is more than the tasks need: the **Universal Windows Platform development** workload and the .NET 9 SDK are not needed to build and clean the app. So you do not need `Deps` when you have installed Visual Studio as the table describes. `Deps` needs a PowerShell that runs as administrator.
- `BuildInSandbox` runs `Deps` and `Build` inside [Windows Sandbox], which shows that a clean computer can build the repository. Nothing is installed on your own computer, and the sandbox is empty again each time it starts. It needs the Windows Sandbox feature. The repository folder is shared with the sandbox, so the build result is in the same place as after `Build`.

`Build` takes the `Ui` project and not the whole solution, because the `Release` configuration of the solution also builds `Installer` (see [The `Installer` project](#the-installer-project)).

[Git]:                https://git-scm.com/download/win
[Visual Studio 2026]: https://visualstudio.microsoft.com/vs/
[.NET 11 SDK]:        https://dotnet.microsoft.com/download/dotnet/11.0
[dotnet-windows]:     https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions
[dotnet-vs]:          https://learn.microsoft.com/en-us/dotnet/core/install/windows#net-versions-and-visual-studio
[Invoke-Build]:       https://github.com/nightroman/Invoke-Build
[1remote.build.ps1]:  https://github.com/1Remote/1Remote/blob/main/1remote.build.ps1
[Chocolatey]:         https://community.chocolatey.org
[Windows Sandbox]:    https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/
