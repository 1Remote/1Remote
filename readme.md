# PRemoteM
English | [中文](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md)


<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/PRemoteM.png" width="50" />
</p>

```
PRemoteM = Personal Remote Manager
```

PRemoteM is a modern remote session manager and launcher, which allows you to open a remote session at any time and anywhere. Fornow PRemoteM supports multiple protocols such as RDP, VNC, SSH, Telnet, sFtp.

- :smiley:I am the old user of mRemoteNG.
- :disappointed_relieved:I decided make a new remote tool in WPF after I bought two 4k monitors.

<p align="center">
    Lancher(alt+M)
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
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/multi-screen.jpg" width="700"/>
</p>


## Featutes

----

- Quick and convenient remote session lancher.
- Support RDP with multi-screen and HiDpi(Testd on **Win10 + 4k*2** to **Win2016**)
- SSH Telnet support via PuTTY, auto-cmd after connect is supported
- Tab support
- [Migrate remote connections from mRemoteNG](https://github.com/VShawn/PRemoteM#Migrate-from-mRemoteNG)
- [Password can be encrypted by RSA](https://github.com/VShawn/PRemoteM#Encryption)

# Lastet
Latest Version: 0.5.1.2011261907

- [Download](https://github.com/VShawn/PRemoteM/releases)

## Requirements

----

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## Usage

----

### open remote connection

1. run PRemote.exe.

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

## Make contribution
Help to make this tool more powerful.
Your name woluld be shown below as a contributor.

1. Propose new improvements, including but not limited to bugs, new requirements, design, and interactive suggestions.
2. Help to translate, duplicate the lang file [en-us.js](https://github.com/VShawn/PRemoteM/blob/dev/PRM.Core/Languages/en-us.json), and translate it to your language.
3. [donations are welcome.](https://www.paypal.me/ShawnVeck)