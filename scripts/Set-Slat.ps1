param (
    [string]$Slat
)

# Check if Slat is empty
if (!$Slat) {
    throw "Error: Slat is empty."
}

# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

# Get the file
$file = Get-ChildItem -Path ..\Ui -Filter AppInit.cs

# Check if the file exists
if (!$file) {
    exit
}

# Replace the content of the file (***SALT***) with the input Slat
$file | ForEach-Object {
# 
    (Get-Content $_.FullName) | ForEach-Object { $_ -replace "\*\*\*SALT\*\*\*", $Slat } | Set-Content $_.FullName
}

# Set the current directory back to the original location
Set-Location $originalDirectory
