$SolutionPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
echo $SolutionPath 
$time = Get-Date -Format 'yyMMddHHmm'
echo $time


# PRMVersion.cs
$FileList = Get-ChildItem $SolutionPath -Recurse PRMVersion.cs
$oldVersion = "public const int ReleaseDate = .*;"
$newVersion = "public const int ReleaseDate = " + $time + ";"
echo $oldVersion 
echo $newVersion 
Foreach($filename in $FileList)
{
    $filename = $filename.FullName 
	echo $filename 
    (Get-Content $filename) |  
    Foreach-Object { $_ -replace $oldVersion, $newVersion } |  
    Set-Content $filename 
}

# readme.md
$FileList = Get-ChildItem $SolutionPath\.. -Recurse readme.md
$oldVersion = "\.\d{10}$"
$newVersion = "." + $time
echo $oldVersion 
echo $newVersion 
Foreach($filename in $FileList)
{
    $filename = $filename.FullName 
	echo $filename 
    (Get-Content $filename) |  
    Foreach-Object { $_ -replace $oldVersion, $newVersion } |  
    Set-Content $filename 
}
