# PRemoteM
English | [中文](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md)



```
PRemoteM = Personal Remote Manager
```
[![version](https://img.shields.io/github/v/release/vshawn/premotem?color=Green&include_prereleases)](https://github.com/VShawn/PRemoteM/releases)
[![codebeat badge](https://codebeat.co/badges/c3e71ab7-bf2c-455b-9886-d5956723eed5)](https://codebeat.co/projects/github-com-vshawn-premotem-dev)
[![issues](https://img.shields.io/github/issues/vshawn/premotem)](https://github.com/VShawn/PRemoteM/issues)
[![license](https://img.shields.io/github/license/vshawn/premotem?color=blue)](https://github.com/VShawn/PRemoteM/blob/dev/LICENSE)
![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2Fvshawn%2Fpremotem&count_bg=%23E83D61&title_bg=%23102B3E&icon=github.svg&icon_color=%23CED8E1&title=&edge_flat=false)

PRemoteM is a modern remote session manager and launcher, which allows you to open a remote session at any time and anywhere. Fornow PRemoteM supports multiple protocols such as RDP, VNC, SSH, Telnet, SFTP, FTP, [RemoteApp](https://github.com/VShawn/PRemoteM/wiki/RemoteApp-program).


<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/maindemo.png"/>
</p>


## Make PRemoteM stronger
If you like **PRemoeM**, let's make **PRemoeM** stronger together by

1. simply star this repository.
2. [help translation](https://github.com/VShawn/PRemoteM/wiki/Help-wanted:-Translation)
3. [buy a coffee](https://ko-fi.com/VShawn)
4. [Join DEV](https://github.com/VShawn/PRemoteM/issues/58#issuecomment-759422795)

## Overview

<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/quickstart.gif" width="400"/>
</p>

<p align="center">
    ↑ Launcher(Alt + M) open RDP connection & resizing
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/tab.gif" width="500"/>
</p>

<p align="center">
    ↑ Tab detach & SSH auto command after connected
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/multi-screen.jpg" width="500"/>
</p>

<p align="center">
    ↑ RDP with Multi-monitors
</p>


<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/RemoteApp/demo.jpg" width="800"/>
</p>

<p align="center">
    ↑ RemoteApp via RDP
</p>


## Featutes

----

- Quick and convenient remote session lancher.
- Support RDP with multi-screen and HiDpi(Testd on **Win10 + 4k*2** to **Win2016**)
- SSH Telnet support via PuTTY, auto-cmd after connect is supported
- File transmit: FTP SFTP supported
- [Migrate remote connections from mRemoteNG](https://github.com/VShawn/PRemoteM#Migrate-from-mRemoteNG)
- [Password can be encrypted by RSA](https://github.com/VShawn/PRemoteM/wiki/Password-Security)
- RemoteApp via RDP

# Lastet
Latest Version: 0.5.8.2101161513

- [Microsoft Store(testing)](https://www.microsoft.com/store/productId/9PNMNF92JNFP)
- [Download exe](https://github.com/VShawn/PRemoteM/releases)
- [Chocolatey](https://chocolatey.org/packages/premotem): `choco install premotem`

## Requirements

----

- [Windows10 17763 and above](https://support.lenovo.com/us/en/solutions/ht502786)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)




## Quick start

----


1. run PRemoteM.exe.

2. click "+" button to add connection info.

3. double click the **Server Card** to open a remote session.

4. or you can open a session by <kbd>Alt</kbd> + <kbd>M</kbd> and keyword.

## Security

[RSA database encryption](https://github.com/VShawn/PRemoteM/wiki/Security#append-how-to-enable-rsa-encryption)


## Migrate from mRemoteNG

<p align="center">
    <img src="https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/Migrate.jpg"/>
</p>
