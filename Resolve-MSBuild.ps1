<#PSScriptInfo
.VERSION 1.7.2
.AUTHOR Roman Kuzmin
.COPYRIGHT (c) Roman Kuzmin
.TAGS Invoke-Build, MSBuild
.GUID 53c01926-4fc5-4cbd-aa46-32e415b2373b
.LICENSEURI http://www.apache.org/licenses/LICENSE-2.0
.PROJECTURI https://github.com/nightroman/Invoke-Build
#>

<#
.Synopsis
	Finds the specified or latest MSBuild.

.Description
	The script finds the path to the specified or latest version of MSBuild.
	Designed for MSBuild 18.0, 17.0, 16.0, 15.0, 14.0, 12.0, 4.0, 3.5, 2.0.

	For MSBuild 15+ the command uses the module VSSetup, see PSGallery.
	If VSSetup is not installed then the default locations are used.
	VSSetup is required for not default installations.

	MSBuild 15+ resolution: the latest major version (or absolute if -Latest),
	then Enterprise, Professional, Community, BuildTools, other products.

	For MSBuild 2.0-14.0 the information is taken from the registry.

.Parameter Version
		Specifies the required MSBuild major version. If it is omitted, empty,
		or *, then the command finds and returns the latest installed version.
		The optional suffix x86 tells to use 32-bit MSBuild.
		Versions: 18.0, 17.0, 16.0, 15.0, 14.0, 12.0, 4.0, 3.5, 2.0.

.Parameter MinimumVersion
		Specifies the required minimum MSBuild version. If the resolved version
		is less than the minimum then the commands terminates with an error.

.Parameter Latest
		Tells to select the latest minor version if there are 2+ products with
		the same major version. Note that major versions have higher precedence
		than products regardless of -Latest.

.Outputs
	The full path to MSBuild.exe

.Example
	Resolve-MSBuild 17.0x86
	Gets the location of 32-bit MSBuild of Visual Studio 2022.

.Example
	Resolve-MSBuild -MinimumVersion 16.3.1 -Latest
	Gets the location of the latest MSBuild, and asserts its version is 16.3.1+.

.Example
	Resolve-MSBuild x86 -MinimumVersion 15.0 -Latest
	Gets the location of the latest 32-bit MSBuild, and asserts its version is 15.0+.

.Link
	https://www.powershellgallery.com/packages/VSSetup
#>

[OutputType([string])]
[CmdletBinding()]
param(
	[string]$Version
	,
	[Version]$MinimumVersion
	,
	[switch]$Latest
)

function Get-MSBuild15Path {
	[CmdletBinding()] param(
		[string]$Version,
		[string]$Bitness
	)

	if ([System.IntPtr]::Size -eq 4 -or $Bitness -eq 'x86') {
		"MSBuild\$Version\Bin\MSBuild.exe"
	}
	else {
		"MSBuild\$Version\Bin\amd64\MSBuild.exe"
	}
}

function Get-MSBuild15VSSetup {
	[CmdletBinding()] param(
		[string]$Version,
		[string]$Bitness,
		[switch]$Latest,
		[switch]$Prerelease
	)

	if (!(Get-Module VSSetup)) {
		if (!(Get-Module VSSetup -ListAvailable)) {return}
		Import-Module VSSetup
	}

	$items = @(
		$v = switch($Version) {
			'18.0' {'[18.0,19.0)'}
			'17.0' {'[17.0,18.0)'}
			'16.0' {'[16.0,17.0)'}
			'15.0' {'[15.0,16.0)'}
			default {'[15.0,)'}
		}
		Get-VSSetupInstance -Prerelease:$Prerelease |
		Select-VSSetupInstance -Version $v -Require Microsoft.Component.MSBuild -Product *
	)
	if (!$items) {
		if (!$Prerelease) {
			Get-MSBuild15VSSetup $Version $Bitness -Latest:$Latest -Prerelease
		}
		return
	}

	if ($items.Count -ge 2) {
		$byVersion = if ($Latest) {{$_.InstallationVersion}} else {{$_.InstallationVersion.Major}}
		$byProduct = {
			switch ($_.Product.Id) {
				Microsoft.VisualStudio.Product.Enterprise {4}
				Microsoft.VisualStudio.Product.Professional {3}
				Microsoft.VisualStudio.Product.Community {2}
				Microsoft.VisualStudio.Product.BuildTools {1}
				default {0}
			}
		}
		$items = $items | Sort-Object $byVersion, $byProduct
	}

	$item = $items[-1]
	if ($item.InstallationVersion.Major -eq 15) {
		$Version = '15.0'
	}
	else {
		$Version = 'Current'
	}
	Join-Path $item.InstallationPath (Get-MSBuild15Path $Version $Bitness)
}

function Get-MSBuild15Guess {
	[CmdletBinding()] param(
		[string]$Version,
		[string]$Bitness,
		[switch]$Latest,
		[switch]$Prerelease
	)

	$Program64 = $env:ProgramFiles
	if (!($Program86 = ${env:ProgramFiles(x86)})) {$Program86 = $Program64}

	$folders = $(
		if ($Prerelease) {
			"$Program64\Microsoft Visual Studio\Preview"
			"$Program86\Microsoft Visual Studio\Preview"
		}
		elseif ($Version -eq '*') {
			"$Program64\Microsoft Visual Studio\2026"
			"$Program86\Microsoft Visual Studio\2026"
			"$Program64\Microsoft Visual Studio\18"
			"$Program86\Microsoft Visual Studio\18"
			"$Program64\Microsoft Visual Studio\2022"
			"$Program86\Microsoft Visual Studio\2022"
			"$Program86\Microsoft Visual Studio\2019"
			"$Program86\Microsoft Visual Studio\2017"
		}
		elseif ($Version -eq '18.0') {
			"$Program64\Microsoft Visual Studio\2026"
			"$Program86\Microsoft Visual Studio\2026"
			"$Program64\Microsoft Visual Studio\18"
			"$Program86\Microsoft Visual Studio\18"
		}
		elseif ($Version -eq '17.0') {
			"$Program64\Microsoft Visual Studio\2022"
			"$Program86\Microsoft Visual Studio\2022"
		}
		elseif ($Version -eq '16.0') {
			"$Program86\Microsoft Visual Studio\2019"
		}
		else {
			"$Program86\Microsoft Visual Studio\2017"
		}
	)
	foreach($folder in $folders) {
		$items = @(Get-Item -ErrorAction 0 @(
			"$folder\*\$(Get-MSBuild15Path Current $Bitness)"
			"$folder\*\$(Get-MSBuild15Path $Version $Bitness)"
		))
		if ($items) {
			break
		}
	}
	if (!$items) {
		if (!$Prerelease) {
			Get-MSBuild15Guess $Version $Bitness -Latest:$Latest -Prerelease
		}
		return
	}

	if ($items.Count -ge 2) {
		$byVersion = if ($Latest) {{[Version]$_.VersionInfo.FileVersion}} else {{([Version]$_.VersionInfo.FileVersion).Major}}
		$byProduct = {
			switch -Wildcard ($_.FullName) {
				*\Enterprise\* {4}
				*\Professional\* {3}
				*\Community\* {2}
				*\BuildTools\* {1}
				default {0}
			}
		}
		$items = $items | Sort-Object $byVersion, $byProduct
	}

	$items[-1].FullName
}

function Get-MSBuild15 {
	[CmdletBinding()] param(
		[string]$Version,
		[string]$Bitness,
		[switch]$Latest
	)

	if ($path = Get-MSBuild15VSSetup $Version $Bitness -Latest:$Latest) {
		$path
	}
	else {
		Get-MSBuild15Guess $Version $Bitness -Latest:$Latest
	}
}

function Get-MSBuildOldVersion {
	[CmdletBinding()] param(
		[string]$Version,
		[string]$Bitness
	)

	if ([System.IntPtr]::Size -eq 8 -and $Bitness -eq 'x86') {
		$key = "HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\MSBuild\ToolsVersions\$Version"
	}
	else {
		$key = "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSBuild\ToolsVersions\$Version"
	}
	$rp = [Microsoft.Win32.Registry]::GetValue($key, 'MSBuildToolsPath', '')
	if ($rp) {
		Join-Path $rp MSBuild.exe
	}
}

function Get-MSBuildOldLatest {
	[CmdletBinding()] param(
		[string]$Bitness
	)

	$rp = @(Get-ChildItem HKLM:\SOFTWARE\Microsoft\MSBuild\ToolsVersions | Sort-Object {[Version]$_.PSChildName})
	if ($rp) {
		Get-MSBuildOldVersion $rp[-1].PSChildName $Bitness
	}
}

function Get-MSBuildAny {
	[CmdletBinding()] param(
		[string]$Bitness,
		[switch]$Latest
	)

	if ($path = Get-MSBuild15 * $Bitness -Latest:$Latest) {
		$path
	}
	else {
		Get-MSBuildOldLatest $Bitness
	}
}

$ErrorActionPreference=1

if ($Version -match '^(.*?)x86\s*$') {
	$Version = $Matches[1]
	$Bitness = 'x86'
}
else {
	$Bitness = ''
}
$Version = $Version.Trim()
if (!$Version) {
	$Version = '*'
}

$v18 = [Version]'18.0'
$v17 = [Version]'17.0'
$v16 = [Version]'16.0'
$v15 = [Version]'15.0'
$vMax = [Version]'9999.0'

if ($Version -eq '*') {
	$vRequired = $vMax
}
else {
	try {
		$vRequired = [Version]$Version
	}
	catch {
		Write-Error "Invalid MSBuild version format: $Version."
	}
}

$path = ''
if ($vRequired -eq $v18 -or $vRequired -eq $v17 -or $vRequired -eq $v16 -or $vRequired -eq $v15) {
	$path = Get-MSBuild15 $Version $Bitness -Latest:$Latest
}
elseif ($vRequired -lt $v15) {
	$path = Get-MSBuildOldVersion $Version $Bitness
}
elseif ($vRequired -eq $vMax) {
	$path = Get-MSBuildAny $Bitness -Latest:$Latest
}

if (!$path) {
	Write-Error "Cannot find MSBuild version: $Version."
}

if ($MinimumVersion) {
	$vResolved = [Version](& $path -version -nologo)
	if ($vResolved -lt $MinimumVersion) {
		Write-Error "MSBuild resolved version $vResolved is less than required minimum $MinimumVersion."
	}
}

$path
