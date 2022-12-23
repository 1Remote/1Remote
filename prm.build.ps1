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

        choco install -y visualstudio2022community
        choco install -y dotnet-6.0-sdk
        choco install -y visualstudio2022-workload-manageddesktop
        choco install -y visualstudio2022-workload-universal

        # optional
        # choco install -y git
    }
}

# Synopsis: Build the application
task Build {
    if (($f = [System.IO.Directory]::GetFiles($pwd, '*.sln')).Length -eq 1) {
        $SolutionPath = $f[0]
    }
    exec { MSBuild $SolutionPath /t:Build /ignoreprojectextensions:.csproj /property:Configuration=$aReleaseType }
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
    if (($f = [System.IO.Directory]::GetFiles($pwd, '*.sln')).Length -eq 1) {
        $SolutionPath = $f[0]
    }
    exec { MSBuild $SolutionPath /t:Clean /property:Configuration=$aReleaseType  }
}


# Test for administration privileges
function Test-Admin() {
    $usercontext = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    $usercontext.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}
