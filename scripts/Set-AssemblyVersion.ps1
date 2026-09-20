# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

cd ..


# Check if the file Ui/AppVersion.cs exists
if (!(Test-Path -Path "Ui/AppVersion.cs" -PathType Leaf)) {
    Write-Host "Error: Ui/AppVersion.cs does not exist."
    Set-Location $originalDirectory
    exit 2
}

# Check if the file Ui/AppVersion.cs exists
if (!(Test-Path -Path "Ui/Ui.csproj" -PathType Leaf)) {
    Write-Host "Error: Ui/Ui.csproj does not exist."
    Set-Location $originalDirectory
    exit 2
}

# �� AppVersion.cs �ж�ȡ�汾�ţ����� $version
# AppVersion.cs �д���Ϊ�����ǣ� Ӧ���� $version Ϊ "1.2.3"
# public const uint Major = 1;
# public const uint Minor = 2;
# public const uint Patch = 3;
$versionString = Get-Content Ui/AppVersion.cs -Raw
$majorMatch = [regex]::Match($versionString, 'public const uint Major = (\d+);')
if(!$majorMatch.Success){
    Write-Error "Major not found"
    exit 3
}
$major = $majorMatch.Groups[1].Value

$minorMatch = [regex]::Match($versionString, 'public const uint Minor = (\d+);')
if(!$minorMatch.Success){
    Write-Error "Minor not found"
    exit 3
}
$minor = $minorMatch.Groups[1].Value

$patchMatch = [regex]::Match($versionString, 'public const uint Patch = (\d+);')
if(!$patchMatch.Success){
    Write-Error "Patch not found"
    exit 3
}
$patch = $patchMatch.Groups[1].Value

$version = "$major.$minor.$patch"

# echo $version
$date = (Get-Date)
# $date = (Get-Date -Day 1 -Month 1 -Year 2026) # test code

# ��ǰʱ��תΪ�ַ��������� $tineStr����ǰʱ��Ϊ 2023��1��14�� 14��56��23�룬����23��ĵ�14�죩��Ӧ���� $tineStr Ϊ "23014"
$year = $date.Year % 10 # ��ݵ����һ������
# ���ڰ汾�Ų��ܴ��� 65535���������������ڵ��� 6 ���ȥ 5��6��7��8��9�ֱ��Ϊ1��2��3��4��
if($year -ge 6){
    $year = $year - 5
}
$month = $date.Month
if($month -lt 10){
    $month = "0"+$month
}
$day = $date.Day
if($day -lt 10){
    $day = "0"+$day
}
$tineStr = "$year$month$day"


# ��� assemblyVerison Ϊ $version.$tineStr���� "1.2.3.23014"
$assemblyVerison = "$version.$tineStr"


# Fail-safe collision check. Set-BuildDate.ps1 (which runs earlier in PreBuild)
# should already have bumped Patch in AppVersion.cs when the year code wraps.
# If a collision is still detected here, that fix did not happen - stop the
# build instead of writing a version that would compare as OLDER than the
# previous release. The check runs before Ui.csproj is modified, so on failure
# the csproj keeps its previous, still-valid version.
$csprojContent = Get-Content Ui/Ui.csproj -Raw
$oldVersionMatch = [regex]::Match($csprojContent, '<AssemblyVersion>(\d+)\.(\d+)\.(\d+)\.(\d+)</AssemblyVersion>')
if($oldVersionMatch.Success){
    $oldVersionPrefix = "$($oldVersionMatch.Groups[1].Value).$($oldVersionMatch.Groups[2].Value).$($oldVersionMatch.Groups[3].Value)"
    $oldRevision = [int]$oldVersionMatch.Groups[4].Value
    if(($oldVersionPrefix -eq $version) -and ($oldRevision -gt [int]$tineStr)){
        Write-Error "AssemblyVersion collision: revision would go back from $oldRevision to $tineStr while prefix $version is unchanged. Set-BuildDate.ps1 should have bumped Patch - make sure it runs before this script and encodes dates the same way."
        Set-Location $originalDirectory
        exit 5
    }
}

echo $assemblyVerison


# �� Ui.csproj �е� <AssemblyVersion>xxxxx</AssemblyVersion> �滻Ϊ <AssemblyVersion>$assemblyVerison</AssemblyVersion>
(Get-Content Ui/Ui.csproj) -replace '<AssemblyVersion>.*</AssemblyVersion>',"<AssemblyVersion>$assemblyVerison</AssemblyVersion>" | Set-Content Ui/Ui.csproj -Encoding UTF8

# Set the current directory back to the original location
Set-Location $originalDirectory
