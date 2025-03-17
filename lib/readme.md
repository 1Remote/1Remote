## How to create MSTSCLib.dll and AxMSTSCLib.dll

You will need to .NET SDK to create the AxMSTSCLib.dll DLL. To create it you'll need to run aximp from the SDK on mstscax.dll. %<SDK dir>%\aximp.exe %windir%\system32\mstscax.dll. Those DLLs will need to be referenced by the project to get the Interop DLLs created. You will also need to compress the DLLs with Deflate and name them AxInterop.MSTSCLib.dll.bin and Interop.MSTSCLib.dll.bin

> just run BuildAxMSTSCLib.ps1