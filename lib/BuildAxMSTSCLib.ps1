# from https://github.com/BornToBeRoot/NETworkManager/blob/main/Scripts/PreBuildEventCommandLine.ps1 thx to BornToBeRoot
# Visual Studio pre build event:
# PowerShell.exe -ExecutionPolicy Bypass -NoProfile -File "$(ProjectDir)..\..\Scripts\PreBuildEventCommandLine.ps1" "$(TargetDir)"

# # to input the path to the output folder
# param(
#     [Parameter(
#         Position = 0,
#         Mandatory = $true)]
#     [String]$OutPath
# )
# # Fix wrong path (if there is no blank in the path, a quote will be added to the end...)
# if (-not($OutPath.StartsWith('"'))) {
#     $OutPath = $OutPath.TrimEnd('"')
# }

# using the current directory as output path
$OrgPath = ($pwd).Path
$OutPath = ($pwd).Path


# NetBeauty will move all dependencies to the lib folder
$OutPath = $OutPath + "\AxMSTSCLibOutput"

# Create folder
if (-not(Test-Path -Path $OutPath -PathType Container)) {
    New-Item -ItemType Directory -Path $OutPath
}

################################################
### Generate MSTSCLib.dll and AxMSTSCLib.dll ###
################################################

# Detect x86 or x64
$ProgramFiles_Path = ${Env:ProgramFiles(x86)}
if ([String]::IsNullOrEmpty($ProgramFiles_Path)) {
    $ProgramFiles_Path = $Env:ProgramFiles
}
Write-Host "ProgramFiles_Path: $ProgramFiles_Path"

# Get aximp.exe, ildasm.exe and ilasm.exe from sdk
$AximpPath = ((Get-ChildItem -Path "$ProgramFiles_Path\Microsoft SDKs\Windows" -Recurse -Filter "aximp.exe" -File) | Sort-Object CreationTime -Descending | Select-Object -First 1).FullName
$IldasmPath = ((Get-ChildItem -Path "$ProgramFiles_Path\Microsoft SDKs\Windows" -Recurse -Filter "ildasm.exe" -File) | Sort-Object CreationTime | Select-Object -First 1).FullName
$IlasmPath = ((Get-ChildItem -Path "$($Env:windir)\Microsoft.NET\Framework\" -Recurse -Filter "ilasm.exe" -File) | Sort-Object CreationTime | Select-Object -First 1).FullName

if ([String]::IsNullOrEmpty($AximpPath) -or [String]::IsNullOrEmpty($IldasmPath) -or [String]::IsNullOrEmpty($IlasmPath)) {
    Write-Host "Could not find sdk tools:`naximp.exe`t=>`t$AximpPath`nildasm.exe`t=>`t$IldasmPath`nilasm.exe`t=>`t$IlasmPath"
    return
}
Write-Host "Using aximp.exe from: $AximpPath"
Write-Host "Using ildasm.exe from: $IldasmPath"
Write-Host "Using ilasm.exe from: $IlasmPath"


$MstscaxDll32Path = "$($Env:windir)\system32\mstscax.dll"
$MstscaxDll64Path = "$($Env:windir)\SysWOW64\mstscax.dll"

$MstscaxDllPaths = @($MstscaxDll32Path, $MstscaxDll64Path)
$OutPaths = @("$OutPath\x86", "$OutPath\x64")
for ($i = 0; $i -lt $MstscaxDllPaths.Length; $i++) {
    $MstscaxDllPath = $MstscaxDllPaths[$i]
    $OutPath = $OutPaths[$i]

    # print version of $MstscaxDllPath
    Write-Host "Version of $MstscaxDllPath"
    Write-Host (Get-Command -Name $MstscaxDllPath).FileVersionInfo.FileVersion

    # delete out dir if exists
    if (Test-Path -Path $OutPath -PathType Container) {
        Remove-Item -Path $OutPath -Recurse -Force
    }

    # Create folder
    if (-not(Test-Path -Path $OutPath -PathType Container)) {
        New-Item -ItemType Directory -Path $OutPath
    }

    # Change location to output folder...
    Set-Location -Path $OutPath

    # Create MSTSCLib.dll and AxMSTSCLib.dll
    Write-Host "Build MSTSCLib.dll and AxMSTSCLib.dll... from $MstscaxDllPath"

    Start-Process -FilePath $AximpPath -ArgumentList $MstscaxDllPath -Wait -NoNewWindow

    # Modify MSTSCLib.dll to fix an issue with SendKeys (See: https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/9095625c-4361-4e0b-bfcf-be15550b60a8/imsrdpclientnonscriptablesendkeys?forum=windowsgeneraldevelopmentissues&prof=required)
    Write-Host "Modify MSTSCLib.dll..."

    $MSTSCLibDLLPath = "$OutPath\MSTSCLib.dll"
    $MSTSCLibILPath = "$OutPath\MSTSCLib.il"

    Write-Host "Start-Process -FilePath $IldasmPath -ArgumentList """"$MSTSCLibDLLPath"" /out=""$MSTSCLibILPath"""" -Wait -NoNewWindow"
    Start-Process -FilePath $IldasmPath -ArgumentList """$MSTSCLibDLLPath"" /out=""$MSTSCLibILPath""" -Wait -NoNewWindow

    Write-Host "Replace ""[in] bool& pbArrayKeyUp"" with ""[in] bool[] marshal([+0]) pbArrayKeyUp"""
    (Get-Content -Path $MSTSCLibILPath).Replace("[in] bool& pbArrayKeyUp", "[in] bool[] marshal([+0]) pbArrayKeyUp") | Set-Content -Path $MSTSCLibILPath

    Write-Host "Replace ""[in] int32& plKeyData"" with ""[in] int32[] marshal([+0]) plKeyData"""
    (Get-Content -Path $MSTSCLibILPath).Replace("[in] int32& plKeyData", "[in] int32[] marshal([+0]) plKeyData") | Set-Content -Path $MSTSCLibILPath

    Start-Process -FilePath $IlasmPath -ArgumentList "/dll ""$MSTSCLibILPath"" /output:""$MSTSCLibDllPath""" -Wait -NoNewWindow

    Write-Host "Remove temporary files..."
    Remove-Item -Path "$MSTSCLibILPath"
    Remove-Item -Path "$OutPath\MSTSCLib.res"
}

Write-Host "Done!"
Set-Location -Path $OrgPath