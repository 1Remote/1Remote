RemoteApp 功能可以将远程计算机上安装的程序（如 Office、Eagle、AutoCAD、PhotoShop、3DMax、浏览器任意软件）“拿来”放到自己的电脑上运行和操作，像本机原生的程序一样“无缝”运行，与本机系统完美融合。它基于RDP协议，但无需传输整个桌面的数据，因此对带宽、系统资源占用更少，并且它与本地程序在外观上并无二样，所以在使用上也不会有太大的割裂感。

# 如何使用

## Step1

参考自：(allway2)[https://blog.csdn.net/allway2/article/details/104672610]

1. 登录服务器后，在“  服务器管理器”上，单击“  远程桌面服务”；
2. 然后单击  QuickSessionCollection  以进行下一个配置；
3. 选择“RemoteApp程序”；
4. 在 “RemoteApp程序” 列上，单击 “任务”，然后单击 “发布RemoteApps程序”；
5. 在弹出的对话框中点击“添加”；
6. 选择对应的 **EXE**；
7. 点下一步；
8. 确认要发布的程序， 然后单击“ 发布” 按钮；
9. 完成。

## Step1 Another way

另一种配置方式

1. 在服务器上下载并安装 [RemoteApp Tool](http://www.kimknight.net/remoteapptool)；
2. 用它来创建你需要远程访问程序的远程文件，工具将帮你完成所有配置，具体步骤请参考工具文档。

## Step2

在 [PRemoteM](https://github.com/VShawn) 中添加一份 RemoteApp 的配置。
1. 点击 “+” 按钮，进入配置新增页面；
2. 在上方选择 “RemoteApp” 协议;
3. 输入配置名称、远程地址、端口、账号、密码等信息；
4. 输入远程APP在服务器上的名称以及完整路径；
5. 保存后在 launcher 或 主界面中启动远程会话。


# 错误提示：

## The following RemoteApp program is not in the list of authorized programs
check: 

http://sbsfaq.com/the-following-remoteapp-program-is-not-in-the-list-of-authorized-programs-on-windows-essential-server/

https://www.beyondtrust.com/docs/privileged-identity/app-launcher-and-recording/installation/set-up-rds.htm