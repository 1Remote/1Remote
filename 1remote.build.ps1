<# .PARAMETER ib #>
param (
    [ValidateSet('Debug', 'Release')]
    [string] $aReleaseType = 'Release'
)

Enter-Build {
    $ProjectName  = Split-Path $pwd -Leaf
    $msbuild = Resolve-MSBuild
    Write-Host "Using msbuild: $msbuild"
    Set-Alias MSBuild $msbuild -Scope Global
}

# Synopsis: Ensure local dependencies
task Deps {
    if (!(Test-Admin)) { throw "This task must be run in administrative shell" }

    if (!(Get-Command choco.exe -ErrorAction 0)) {
        Invoke-WebRequest https://chocolatey.org/install.ps1 -UseBasicParsing | Invoke-Expression
    } else { Write-Host "Chocolatey already installed" }

    exec {
        #choco install -y dotnetfx --version 4.8.0.20190930
        #choco install -y visualstudio2019buildtools
        #choco install -y visualstudio2019-workload-universal
        #choco install -y windows-sdk-10-version-1809-all

        choco install -y visualstudio2026community
        choco install -y dotnet-9.0-sdk
        choco install -y dotnet-10.0-sdk
        choco install -y dotnet-11.0-sdk
        choco install -y visualstudio2026-workload-manageddesktop
        choco install -y visualstudio2026-workload-universal

        # optional
        # choco install -y git
    }
}

# Synopsis: Build the application
task Build {
    # Ui only: solution Release builds also include Installer, which needs the signing certificate
    exec { MSBuild .\Ui\Ui.csproj /restore /t:Build /property:Configuration=$aReleaseType }
}

# Synopsis: Build in Windows Sandbox
task BuildInSandbox {
    .\scripts\Test-Sandbox.ps1 -MapFolder $pwd -Script "
        cd `$Env:USERPROFILE\Desktop\$ProjectName
        Set-Alias ib `$pwd\Invoke-Build.ps1
        ib Deps
        ib Build
    "
}

# Synopsis: Clean generated data
task Clean {
    # Ui only, like Build: the solution's Release configuration also cleans Installer
    exec { MSBuild .\Ui\Ui.csproj /t:Clean /property:Configuration=$aReleaseType }
}


# Test for administration privileges
function Test-Admin() {
    $usercontext = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    $usercontext.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}
