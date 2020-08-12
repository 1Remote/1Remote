# PRemoteM
[English](https://github.com/VShawn/PRemoteM#PRemoteM) | 中文


<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/PRemoteM.png" width="50" />
</p>

```
PRemoteM = Personal Remote Manager
```


PRemoteM 是一款帮助你管理远程会话的小工具，它允许你进行多屏幕全屏RDP远程，也允许你在 Tab 窗体中管理、切换多个远程会话。它的快速启动器能帮助你在任何时候任何位置方便而快速地开启远程会话，省去查找服务器的烦恼。





<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/quickstart.gif" width="300"/>
</p>

<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/tab.jpg" width="300"/>
</p>

<p align="center">
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/multi-screen.jpg" width="700"/>
</p>


## Featutes
----
- RDP 支持多屏幕全屏远程(测试基于 **Win10 + 4k显示器*2** 远程至 **Win2016**)
- 基于 PuTTY 的 SSH Telnet 会话
- Tab 页中管理远程会话
- UI 颜色可自定义
- 通过快捷键打开启动器，进而快速搜索查找开启会话
- [可从 mRemoteNG 迁移会话](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md#从-mRemoteNG-迁移数据)
- [基于 RSA 的敏感数据保护](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md#数据加密)

# Lastet

最新版本: 0.4.8.2008150945

- [下载](https://github.com/VShawn/PRemoteM/releases/tag/0.4.8.2008150945)
  
## 系统要求
----
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)

## 使用方法
----

### 启动远程会话
1. 运行 PRemote.exe.
2. 通过 "+" 按钮新增远程会话信息
   
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/step1.jpg" width="200"/>

3. 通过双击 **会话卡片** 以开启一个远程会话
   
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/step2.jpg" width="200"/>

4. 或者你可以通过 <kbd>Alt</kbd> + <kbd>M</kbd> 打开快速启动器键入关键字并回车以启动远程会话

    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/step3.jpg" width="300"/>

### 数据加密

<img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/Encryption.jpg"/>

数据加密后，即便你的数据库被非法窃取，偷窃者在未获得私钥的情况下也无法得到你的敏感数据。
设置方法：
1. 在 <kbd>设置</kbd> -> <kbd>数据与安全</kbd> 界面
2. 点击 <kbd>生成加密</kbd> 按钮后，你的数据将被加密，请将生成的**私钥**妥善保管.

### 从 mRemoteNG 迁移数据
<img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/Migare.jpg"/>

## 添砖加瓦
Help to make this tool more powerful.
Your name woluld be shown below as a contributor.

### Code
- Pull request are welcome.

### Translate

- UI
  - Just duplicate the lang file [en-us.js](https://github.com/VShawn/PRemoteM/blob/Doc/ReadMe_zh-cn/readme.md/blob/master/PRM.Core/Languages/en-us.json), and translate it to your language. 
  - Put your lang file to **C:\Users\YourName\AppData\Roaming\PRemoteM\Languages** for preview.
    - click this button can go to the directory **C:\Users\YourName\AppData\Roaming\PRemoteM**
    
    <img src="https://github.com/VShawn/PRemoteM/raw/Doc/DocPic/GoToAppData.jpg" width="300"/>
  - Share your lang file after job is done.
- Doc
  - [WIP]


## Todo list

- [ ] VNC
- [ ] sFtp
- [ ] upload PRemoteM to Microsoft Store
- [ ] more features
