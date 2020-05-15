#获取解决方案的目录
$SolutionPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
echo $SolutionPath 
#获取所有的 PRMVersion.cs 文件
$FileList = Get-ChildItem $SolutionPath -Recurse PRMVersion.cs
$time = Get-Date -Format 'yyyyMMddHHmmss'
$oldVersion = "public const string ReleaseDate = "".*"";"
$newVersion = "public const string ReleaseDate = """ + $time + """;"
echo $newVersion 
Foreach($filename in $FileList)
{
    $filename = $filename.FullName 
	echo $filename 
    (Get-Content $filename) |  
    Foreach-Object { $_ -replace $oldVersion, $newVersion } |  
    Set-Content $filename 
}