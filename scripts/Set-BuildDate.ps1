param (
    [string]$filePath,
    [switch]$isRevert # true then replace $Secret with $Pattern
)

# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

cd ..


# Check if the file exists
if (!(Test-Path -Path $filePath -PathType Leaf)) {
    Write-Host "Error: $filePath does not exist."
    Set-Location $originalDirectory
    exit 2
}

$time = Get-Date -Format 'yyMMddHHmm'
$target = "public const string BuildDate = .*;"
$newVersion = "public const string BuildDate = """ + $time + """;"
$newVersion2 = "public const string BuildDate = """";"


$replacement = $newVersion
if ($isRevert) {
    $replacement = $newVersion2
}

$fileLines = Get-Content $filePath
$matched = 0
foreach ($l in $fileLines) {
    if($l -match $target) {
        $matched = 1
        break
    }
}
if (!$matched) {
    if($isRevert) {
        Write-Host "Warning: secret string not found in $filePath"
    }
    else{
        Write-Host "Error: $target not found in $filePath"
        Set-Location $originalDirectory
        exit 4
    }
}

# Replace the content of the file
(Get-Content $filePath) -Replace $target, $replacement | Set-Content $filePath

# Set the current directory back to the original location
Set-Location $originalDirectory
