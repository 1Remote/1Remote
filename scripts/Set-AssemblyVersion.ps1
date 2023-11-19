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

# 从 AppVersion.cs 中读取版本号，放入 $version
# AppVersion.cs 中代码为下面是， 应读出 $version 为 "1.2.3"
# public const uint Major = 1;
# public const uint Minor = 2;
# public const uint Patch = 3;
$versionString = Get-Content Ui/AppVersion.cs -Raw
$majorMatch = [regex]::Match($versionString, 'public const uint Major = (\d+);')
if(!$majorMatch.Groups.Count){
    Write-Error "Major not found"
    exit 3
}
$major = $majorMatch.Groups[1].Value

$minorMatch = [regex]::Match($versionString, 'public const uint Minor = (\d+);')
if(!$minorMatch.Groups.Count){
    Write-Error "Minor not found"
    exit 3
}
$minor = $minorMatch.Groups[1].Value

$patchMatch = [regex]::Match($versionString, 'public const uint Patch = (\d+);')
if(!$patchMatch.Groups.Count){
    Write-Error "Patch not found"
    exit 3
}
$patch = $patchMatch.Groups[1].Value

$version = "$major.$minor.$patch"

# echo $version
$date = (Get-Date)
# $date = (Get-Date -Day 1 -Month 1 -Year 2026) # test code

# 当前时间转为字符串，放入 $tineStr，当前时间为 2023年1月14日 14点56分23秒，（即23年的第14天），应读出 $tineStr 为 "23014"
$year = $date.Year % 10 # 年份的最后一个数字
# 由于版本号不能大于 65535，所以年份如果大于等于 6 则减去 5（6、7、8、9分别变为1、2、3、4）
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


# 组合 assemblyVerison 为 $version.$tineStr，即 "1.2.3.23014"
$assemblyVerison = "$version.$tineStr"

echo $assemblyVerison


# 将 Ui.csproj 中的 <AssemblyVersion>xxxxx</AssemblyVersion> 替换为 <AssemblyVersion>$assemblyVerison</AssemblyVersion>
(Get-Content Ui/Ui.csproj) -replace '<AssemblyVersion>.*</AssemblyVersion>',"<AssemblyVersion>$assemblyVerison</AssemblyVersion>" | Set-Content Ui/Ui.csproj -Encoding UTF8

# Set the current directory back to the original location
Set-Location $originalDirectory
