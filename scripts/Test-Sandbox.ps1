Param(
  [string]   $Script,
  [string[]] $MapFolder = $pwd
)

# Check if Windows Sandbox is enabled
if (-Not (Test-Path "$env:windir\System32\WindowsSandbox.exe")) {
  Write-Error -Category NotInstalled -Message @'
Windows Sandbox does not seem to be available. Check the following URL for prerequisites and further details:
https://docs.microsoft.com/en-us/windows/security/threat-protection/windows-sandbox/windows-sandbox-overview

You can run the following command in an elevated PowerShell for enabling Windows Sandbox:
Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM'
'@ -ErrorAction Stop
}

# Initialize Temp Folder

$tempFolder = Join-Path -Path $PSScriptRoot -ChildPath 'Test-Sandbox_Temp'
New-Item $tempFolder -ItemType Directory -ea 0 | Out-Null

# Create Bootstrap script
$bootstrapPs1Content = @"
# cd ~\Desktop
# Write-Host 'Installing Chocolatey'
# Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))
# choco feature enable -n=allowGlobalConfirmation

$Script
"@

$bootstrapPs1FileName = 'Bootstrap.ps1'
$bootstrapPs1Content | Out-File (Join-Path -Path $tempFolder -ChildPath $bootstrapPs1FileName)

# Create Wsb file

$tempFolderInSandbox = Join-Path -Path 'C:\Users\WDAGUtilityAccount\Desktop' -ChildPath (Split-Path $tempFolder -Leaf)

$mapFolders = foreach ($f in $MapFolder) { "<MappedFolder><HostFolder>$f</HostFolder></MappedFolder>" }
$sandboxTestWsbContent = @"
<Configuration>
  <MappedFolders>
    <MappedFolder><HostFolder>$tempFolder</HostFolder></MappedFolder>
    $mapfolders
  </MappedFolders>
  <LogonCommand>
  <Command>PowerShell Start-Process PowerShell -WorkingDirectory '$tempFolderInSandbox' -ArgumentList '-ExecutionPolicy Bypass -NoExit -File $bootstrapPs1FileName'</Command>
  </LogonCommand>
</Configuration>
"@
$sandboxTestWsbFileName = 'SandboxTest.wsb'
$sandboxTestWsbFile = Join-Path -Path $tempFolder -ChildPath $sandboxTestWsbFileName
$sandboxTestWsbContent | Out-File $sandboxTestWsbFile

Write-Host 'Starting Windows Sandbox'
WindowsSandbox $SandboxTestWsbFile
