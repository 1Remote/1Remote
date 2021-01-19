# 开发环境

本文档提供如何搭建本地开发环境的指导，在开始之前我们假设您已经准备好了一台尚未安装开发工具的 Windows 10 系统机器。

## 环境要求

1. [Windows 10] 1703 及以上版本
2. [Microsoft Visual Studio 2019] 社区版并安装以下组件:
    - .NET 桌面开发
    - .NET Framework 4.8 SDK
    - Windows 10 SDK 10.0.17763.0
3. [.NET Framework 4.8 Dev Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48)



## 编译

### 手动编译

1. Clone 仓库: `git clone https://github.com/VShawn/PRemoteM`
2. 在 VS2019 中打开解决方案 `PRM.sln`
3. [还原所有Neget包](https://docs.microsoft.com/en-us/nuget/consume-packages/package-restore#restore-packages-manually-using-visual-studio)

此时您应当可以已经完成后续的编译。

### 命令行

命令行编译基于附带的 [Invoke-Build] PowerShell 模块，同时它也可以以安装到系统内：[installed in the system](https://github.com/nightroman/Invoke-Build#install-as-module).

为方便使用，先给它起个别名 - 在项目根目录以管理员权限启动 PowerShell 并执行 `Set-Alias ib $pwd\Invoke-Build.ps1`.

执行 `ib ?` 查看可选 Task，您应当可以看到以下内容：

```
PS C:\Projects\PRemoteM> ib ?

Name           Jobs Synopsis
----           ---- --------
Deps           {}   Ensure local dependencies
Build          {}   Build the application
BuildInSandbox {}   Build in Windows Sandbox
Clean          {}   Clean generated data

```

以上 Task 在脚本 [prm.build.ps1] 中定义。

例如，如果您希望重新编译一个便携版本的 PRemoteM，您可以:

```ps1
ib Clean, Build -aReleaseType R2Win32

# Equivalent without setting alias, must be run in root of the repository
./Invoke-Build.ps1 Clean, Build -aReleaseType R2Win32

# Equivalent with system install of Invoke-Build
Invoke-Build Clean, Build -aReleaseType R2Win32
```

其他编译选项请自行查看 [invoke-build](https://chocolatey.org/packages/invoke-build) 中的配置和技巧。

`BuildInSandbox` 选项将打开 [Windows Sandbox] 并执行 `ib Deps, Build` 任务. 该任务将在沙盒中通过 [Chocolatey] 包管理器下载并安装所有项目依赖，它将耗费一定时间 (~20 minutes)，但可以给您提供一个纯净的编译环境而不会污染主机系统。注意，一旦您关闭 [Windows Sandbox]，本次搭建的所有环境都将被清理掉。

[Microsoft Visual Studio 2019]: https://visualstudio.microsoft.com/vs
[Windows 10]:       https://www.microsoft.com/en-us/software-download/windows10
[Invoke-Build]:     https://github.com/nightroman/Invoke-Build
[Windows Sandbox]:  https://docs.microsoft.com/en-us/windows/security/threat-protection/windows-sandbox/windows-sandbox-overview
[Chocolatey]:       http://chocolatey.org
[prm.build.ps1]:    https://github.com/VShawn/PRemoteM/blob/dev/prm.build.ps1