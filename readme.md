# 1Remote

English | [中文](https://github.com/1Remote/1Remote/wiki/Intro-%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87)

[![version](https://img.shields.io/github/v/release/1Remote/1Remote?color=Green&include_prereleases)](https://github.com/1Remote/1Remote/releases)
[![codebeat badge](https://codebeat.co/badges/93f34fa5-f6e3-476d-a80b-93d3b801e7bf)](https://codebeat.co/projects/github-com-1remote-1Remote-dev_net6)
[![issues](https://img.shields.io/github/issues/1Remote/1Remote)](https://github.com/1Remote/1Remote/issues)
[![license](https://img.shields.io/github/license/1Remote/1Remote?color=blue)](https://github.com/1Remote/1Remote/blob/dev/LICENSE)
![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2Fvshawn%2Fpremotem&count_bg=%23E83D61&title_bg=%23102B3E&icon=github.svg&icon_color=%23CED8E1&title=&edge_flat=false)

1Remote is a modern personal remote session manager and launcher. It is a single place to manage all your remote sessions supporting number of different protocols.

```[SHELL]
Since word `pre-mortem` has a awful meaning and `PRemoteM` is not easy to remember or spell.

This App Will Rename to 1Remote in the feature..
```

## Features

- Supports RDP, SSH, VNC, Telnet, (S)FTP, [RemoteApp](https://github.com/1Remote/1Remote/wiki/%5BProtocol%5D-RemoteApp), [NoMachine and other app](https://github.com/1Remote/1Remote/wiki/%5BProtocol%5D-APP-NoMachine)
- Quick and convenient remote session launcher (Alt + M)
- Multi-screen and HiDPI RDP connection (Test on **Win10 + 4k monitor *2** RDP TO **Win2016**)
- Detailed connection configuration: tags, icons, colors, connection scripts etc.
- Multiple languages, themes and tabbed interface
- [Import connections from mRemoteNG](https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/Migrate.jpg)
- Customizable runners, in SFTP \ FTP \ VNC \ etc. protocols, you can replace the internal runner with your favourite tools.[wiki](https://github.com/1Remote/1Remote/wiki/%5BProtocol%5D--Protocol-Runners)
- Portable - just unpack and run

## 🚩Installation

Latest Version: 0.7.2.8

|| Stable | Preview |
|---| --- | --- |
| Download | <li>[GitHub release](https://github.com/1Remote/1Remote/releases)</li><li>[Microsoft Store](https://www.microsoft.com/store/productId/9PNMNF92JNFP)</li><li>[Winget](https://github.com/microsoft/winget-cli): `winget install premotem`</li><li>[Chocolatey](https://chocolatey.org/packages/premotem): `choco install premotem`</li> | [Nightly](https://github.com/1Remote/1Remote/releases/tag/Nightly) |


💥New Features in Preview:

- [x] [MySQL support](https://1remote.github.io/usage/database/data-synchronization/#by-using-mysql)
- [x] [Custom servers order by drag](https://1remote.github.io/usage/overview/#sorting)
- [x] [Multi-Credentials for RDP\VNC\SHH...(e.g. keep root and normal-user credentials in one server)](https://1remote.github.io/usage/alternative-credential/#addedit)
- [x] Multi-Address for RDP\VNC\SHH... (e.g. 192.168.0.100 for LAN, and xxx.xx.xxx.xx for WAN)
- [x] [Auto switching between multi-addresses (you don't have to select the address manually)](https://1remote.github.io/usage/alternative-credential/#auto-switching-address)
- [x] [Servers sharing within team (e.g. share servers with your colleagues)](https://1remote.github.io/usage/team/team-sharing/)

### ⚠Requirements

- [Windows10 17763 and above](https://support.lenovo.com/us/en/solutions/ht502786)
- [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)

> P.S. You can clone the code and remove all of the Win10 dependencies if you are likely to use 1Remote in Win7.

### 🗺[Quick start](https://1remote.github.io/usage/quick-start/)


## 👓Overview

<img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/maindemo.png" width="800" />


<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/quickstart.gif" width="400"/>
</p>

<p align="center">
    ↑ Launcher(Alt + M) open RDP connection & resizing
</p>


<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/tab.gif" width="500" />
</p>

<p align="center">
    ↑ Tab detach & SSH auto command after connected
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/multi-screen.jpg" width="500"/>
</p>


<p align="center">
    ↑ RDP with Multi-monitors
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/RemoteApp/demo.jpg" width="800"/>
</p>

<p align="center">
    ↑ RemoteApp via RDP
</p>

<p align="center">
    <img src="https://raw.githubusercontent.com/1Remote/PRemoteM/Doc/DocPic/Runner/vnc_runners.jpg" width="800"/>
</p>

<p align="center">
    ↑ Customizable runners
</p>

## Make 1Remote Stronger

If you like **1Remote**, help us make it stronger by doing any of the following:

1. Simply star this repository
2. [Help translation](https://github.com/1Remote/1Remote/wiki/Help-wanted:-Translation)
3. [Buy a coffee](https://ko-fi.com/VShawn)
4. [Join DEV](DEVELOP.md)

## Special thanks

<a href="http://www.jetbrains.com/resharper/"><img src="http://www.tom-englert.de/Images/icon_ReSharper.png" alt="ReSharper" width="64" height="64" /></a>

