# Development

This guide provides information on how to setup development environment on local machine.

It assumes no local tools and empty Windows 10 OS.

## Prerequisites

1. [Windows 10] 1703 or later
2. [Microsoft Visual Studio 2019] community edition or higher, with the following workloads:
    - .NET desktop development
    - .NET Framework 4.8 SDK
    - Windows 10 SDK 10.0.17763.0
1. [.NET Framework 4.8 Dev Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48)

The build task `Deps` automates entire installation locally (except OS). More details on running tasks are given bellow.


## Build

### Manual

1. Clone repository: `git clone https://github.com/VShawn/PRemoteM`
2. Open `PRM.sln` in Visual Studio 2019
3. [Restore all NuGet packages](https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore#restore-packages-manually-using-visual-studio)

Now you can build solution.

### Command line

Build is automated using [Invoke-Build] PowerShell module which is included in the repository, but can be also [installed in the system](https://github.com/nightroman/Invoke-Build#install-as-module).

For convenience, set alias to it - open administrative PowerShell, go to repository root and run `Set-Alias ib $pwd\Invoke-Build.ps1`.

Run `ib ?` to get list of available tasks (anywhere in the repository directory hierarchy):

```
PS C:\Projects\PRemoteM> ib ?

Name           Jobs Synopsis
----           ---- --------
Deps           {}   Ensure local dependencies
Build          {}   Build the application
BuildInSandbox {}   Build in Windows Sandbox
Clean          {}   Clean generated data

```

Tasks are defined in the [prm.build.ps1] PowerShell script.

For example, to clean any existing builds and then build fresh PRemoteM as portable Win32 application invoke:

```ps1
ib Clean, Build -aReleaseType R2Win32

# Equivalent without setting alias, must be run in root of the repository
./Invoke-Build.ps1 Clean, Build -aReleaseType R2Win32

# Equivalent with system install of Invoke-Build
Invoke-Build Clean, Build -aReleaseType R2Win32
```

Please check out [invoke-build](https://chocolatey.org/packages/invoke-build) package notes on how to enable task auto completion and other tips.

Task `BuildInSandbox` starts [Windows Sandbox] and executes `ib Deps, Build` tasks. This takes some time (~20 minutes) as all dependencies are downloaded from the Internet and installed, using [Chocolatey] package manager, but it guaranties pristine environment. Note that when you close the sandbox entire environment is gone.

[Microsoft Visual Studio 2019]: https://visualstudio.microsoft.com/vs
[Windows 10]:       https://www.microsoft.com/en-us/software-download/windows10
[Invoke-Build]:     https://github.com/nightroman/Invoke-Build
[Windows Sandbox]:  https://docs.microsoft.com/en-us/windows/security/threat-protection/windows-sandbox/windows-sandbox-overview
[Chocolatey]:       http://chocolatey.org
[prm.build.ps1]:    https://github.com/VShawn/PRemoteM/blob/dev/prm.build.ps1