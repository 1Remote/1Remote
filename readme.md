# PRemoteM
English | [中文](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md)



```
PRemoteM = Personal Remote Manager
```

PRemoteM is a modern remote session manager and launcher, which allows you to open a remote session at any time and anywhere. Fornow PRemoteM supports multiple protocols such as RDP, VNC, SSH, Telnet, sFtp.

  
<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/maindemo.png"/>
</p>


## Make PRemoteM stronger
If you like **PRemoeM**, let's make **PRemoeM** stronger together by

1. simply star this repository.
2. [help translation](https://github.com/VShawn/PRemoteM/wiki/Help-wanted:-Translation)
3. [buy a coffee](https://ko-fi.com/VShawn)

## Overview


<p align="center">
    Launcher(Alt + M)
</p>
<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/quickstart.gif" width="600"/>
</p>

<p align="center">
    Tab detach & SSH auto command after connected
</p>
<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/tab.gif" width="600"/>
</p>

<p align="center">
    RDP with Multi-monitors
</p>
<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/multi-screen.jpg" width="600"/>
</p>


## Featutes

----

- Quick and convenient remote session lancher.
- Support RDP with multi-screen and HiDpi(Testd on **Win10 + 4k*2** to **Win2016**)
- SSH Telnet support via PuTTY, auto-cmd after connect is supported
- File transmit: FTP SFTP supported
- [Migrate remote connections from mRemoteNG](https://github.com/VShawn/PRemoteM#Migrate-from-mRemoteNG)
- [Password can be encrypted by RSA](https://github.com/VShawn/PRemoteM#Encryption)

# Lastet
Latest Version: 0.5.5.2101121716

- [Microsoft Store(testing)](https://www.microsoft.com/store/productId/9PNMNF92JNFP)
- [Download exe](https://github.com/VShawn/PRemoteM/releases)
- Install using [Chocolatey](https://chocolatey.org/packages/premotem): `choco install premotem`

## Requirements

----

- [Windows10 17763 and above](https://support.lenovo.com/us/en/solutions/ht502786)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)




## Usage

----

### open remote connection

1. run PRemoteM.exe.

2. click "+" button to add connection info.

3. double click the **Server Card** to open a remote session.

4. or you can open a session by <kbd>Alt</kbd> + <kbd>M</kbd> and keyword.

### Encryption

By encrypting data, no one can get your remote password by open database directly.

1. In <kbd>Setting</kbd> -> <kbd>Data & Security</kbd> page
2. click <kbd>Encrypt</kbd> button and select a proper place to store your **Key File**.

<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/Encryption.jpg" width="300"/>
</p>

### Migrate from mRemoteNG

<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/Migrate.jpg"/>
</p>
