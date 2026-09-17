@echo off
rem ============================================================
rem dev-webui.cmd - 1Remote WebUI 一键开发环境启动脚本
rem   前端: webui/ (Vite dev server, 端口 5173, 热重载)
rem   后端: Ui 项目 Debug 构建 (进程内 Kestrel API, 端口 17321)
rem   注意: 本文件是 ANSI(GBK) 编码。中文 Windows 控制台(CP936)可直接
rem   正确解析显示; 勿另存为 UTF-8 (无 chcp 会乱码, 加 chcp 65001 又有
rem   cmd 读取批处理文件的行偏移 bug, 会导致部分中文行被执行碎片)
rem ============================================================
setlocal
rem 切到脚本所在目录(仓库根), 双击或从任意工作目录调用均可
cd /d "%~dp0"

echo 1Remote WebUI 开发环境: 前端 Vite(端口 5173) + 后端 Debug(API 端口 17321)。
echo "webui - vite dev" 窗口是 Vite 前端服务器; 本窗口运行 Debug 后端(dotnet run)。
echo 界面引擎=Web(DEBUG)时应用内窗口直接加载 Vite 即热重载; 引擎=WPF 时用浏览器页调试前端。
echo 退出: 直接关闭对应窗口即可。
echo.
echo 注意: 若已有一个 1Remote 在运行, 请先关闭它, 否则 17321 端口被占用, 前端页面没有数据。
echo.

rem [步骤1] 新开窗口启动 Vite 前端服务器(webui\node_modules 缺失时先自动安装依赖)
start "webui - vite dev" cmd /k "cd /d %~dp0webui && (if not exist node_modules npm install) && npm run build && npm run dev"

rem [步骤2] 等约 5 秒让 Vite 先起来。用 ping 代替 timeout: timeout 在 stdin 被重定向的
rem        非交互环境(如管道/CI)下会报错, ping 无此限制, 双击运行时两者等效
ping -n 6 127.0.0.1 >nul

rem [步骤3] 打开浏览器访问前端(若页面空白, 等 Vite 就绪后刷新即可)
start http://localhost:5173

echo 正在启动后端 Debug(dotnet run --project Ui -c Debug), 构建错误会显示在本窗口...
echo.

rem [步骤4] 本窗口运行后端; dotnet run 会阻塞直到应用退出, 因此必须放在最后
rem dotnet run --project Ui -c Debug
cd .\Ui\bin\Debug\net9.0-windows10.0.19041.0 && .\1Remote.exe

rem 后端已退出, 暂停以免窗口闪退, 便于查看错误信息
pause
