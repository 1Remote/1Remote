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

# ---------------------------------------------------------------------------
# Year-code wrap check (pre-build only). The YMMDD encoding here must stay in
# sync with Set-AssemblyVersion.ps1: year%10, >=6 minus 5, zero-padded MM DD.
# If the date revision would go backwards while Major.Minor.Patch is unchanged
# (the year code wraps, e.g. 2029 -> 2030), bump Patch in AppVersion.cs so
# AssemblyVersion comparison stays monotonic. Set-AssemblyVersion.ps1 runs
# later in PreBuild and acts as a fail-safe: if it still sees a collision it
# stops the build.
# ---------------------------------------------------------------------------
if (!$isRevert -and (Test-Path "Ui/Ui.csproj" -PathType Leaf)) {
    $date = Get-Date
    $y = $date.Year % 10
    if($y -ge 6){
        $y = $y - 5
    }
    $m = $date.Month
    if($m -lt 10){
        $m = "0"+$m
    }
    $d = $date.Day
    if($d -lt 10){
        $d = "0"+$d
    }
    $dateCode = [int]"$y$m$d"

    $csprojContent = Get-Content Ui/Ui.csproj -Raw
    $oldVersionMatch = [regex]::Match($csprojContent, '<AssemblyVersion>(\d+)\.(\d+)\.(\d+)\.(\d+)</AssemblyVersion>')
    $srcContent = Get-Content $filePath -Raw
    $majorMatch = [regex]::Match($srcContent, 'public const uint Major = (\d+);')
    $minorMatch = [regex]::Match($srcContent, 'public const uint Minor = (\d+);')
    $patchMatch = [regex]::Match($srcContent, 'public const uint Patch = (\d+);')
    if($oldVersionMatch.Success -and $majorMatch.Success -and $minorMatch.Success -and $patchMatch.Success){
        $oldVersionPrefix = "$($oldVersionMatch.Groups[1].Value).$($oldVersionMatch.Groups[2].Value).$($oldVersionMatch.Groups[3].Value)"
        $currentPrefix = "$($majorMatch.Groups[1].Value).$($minorMatch.Groups[1].Value).$($patchMatch.Groups[1].Value)"
        $oldRevision = [int]$oldVersionMatch.Groups[4].Value
        if(($oldVersionPrefix -eq $currentPrefix) -and ($oldRevision -gt $dateCode)){
            $newPatch = [int]$patchMatch.Groups[1].Value + 1
            (Get-Content $filePath) -replace 'public const uint Patch = \d+;', "public const uint Patch = $newPatch;" | Set-Content $filePath
            Write-Host "Year code wrapped: AssemblyVersion revision would go back from $oldRevision to $dateCode, auto bumped Patch to $newPatch"
        }
    }
}

$time = Get-Date -Format 'yyyy-MM-ddTHH:mm:ss.fffzzz'
$time = "Built at: $time"
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
