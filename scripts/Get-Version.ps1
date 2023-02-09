# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

# Get the file
$filePath = "..\Ui\AppVersion.cs"
$fileContent = Get-Content $filePath

# Extract the version numbers and pre-release string from the file
$major = [int] ($fileContent | Select-String 'public const uint Major = (\d+)').Matches[0].Groups[1].Value
$minor = [int] ($fileContent | Select-String 'public const uint Minor = (\d+)').Matches[0].Groups[1].Value
$patch = [int] ($fileContent | Select-String 'public const uint Patch = (\d+)').Matches[0].Groups[1].Value
$build = [int] ($fileContent | Select-String 'public const uint Build = (\d+)').Matches[0].Groups[1].Value
$preRelease = ($fileContent | Select-String 'public const string PreRelease = "(.*)";').Matches[0].Groups[1].Value

# Construct the version string
if ($preRelease -eq "" && $build -eq 0) {
    $versionString = "$major.$minor.$patch"
} elseif ($preRelease -eq "") {
    $versionString = "$major.$minor.$patch.$build"
} else {
    $versionString = "$major.$minor.$patch.$build-$preRelease"
}

Write-Output $versionString

# Set the current directory back to the original location
Set-Location $originalDirectory
