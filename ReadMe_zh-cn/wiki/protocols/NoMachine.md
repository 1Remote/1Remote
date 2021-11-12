## 配置 NoMachine
![NoMachine](https://www.nomachine.com/sites/all/themes/frontend/images/logo_footer.png)

[Getting started with NoMachine](https://www.nomachine.com/getting-started-with-nomachine)

此处假设你已经配置好了 NoMachine 并且可以使用 NoMachine 自己的程序开启远程控制。如果你没有配置好 NoMachine，请参考上方的官方文档进行操作。

## 使用 PRemoteM 启动

由于 NoMachine 的最新版本不再提供命令行传递账号密码的会话启动方式，因此在 0.6.0 以上版本的 PRemoteM 中，我们定义了 App 协议来间接实现 NoMachine 会话的启动。

1. 首先在 NoMachine 中完成所有配置，并且确保目标机器能够被连接。右键将 `.nxs` 配置文件保存到便于找到的位置。

![](https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/NoMachine/1.jpg)

2. 在 PRemoteM 中新建一个配置，选择 `APP` 类型。
   - 在 exe 路径栏中填入 nxplayer.exe 的路径
   - 在 parameter 栏中填入刚才保存的 `.nxs` 文件路径
   - 保存即可

![](https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/NoMachine/2.jpg)

3. 之后便可以从 PRemoteM 快速启动你的 NoMachine 会话。

![](https://raw.githubusercontent.com/VShawn/PRemoteM/Doc/DocPic/NoMachine/3.jpg)

该方法同样适用于配置其他支持命令参数的启动器，如 PuTTY、WinSCP等。

甚至你可以使用该方法将一个其他程序（如 Word、Notepad等）添加到 PRemoteM 中用于快速启动。