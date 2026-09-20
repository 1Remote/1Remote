@echo off
rem ============================================================
rem dev-webui.cmd - 1Remote WebUI 一键开发环境启动脚本
rem   前端: webui/ (Vite dev server, 端口 5173, 热重载)
rem   后端: Ui 项目 Debug 构建 (进程内 Kestrel API, 端口 17321)
rem   端口预清理: 启动前自动结束占用 5173/17321 的旧进程
rem   (5173 仅杀 node.exe 残留 vite; 17321 仅杀 1Remote.exe 残留 Debug;
rem   其他程序占用只提示不强杀, 避免误杀无关软件)
rem   注意: 本文件是 ANSI(GBK) 编码。勿另存为 UTF-8 (cmd 读取批处理
rem   有行偏移 bug, 会导致中文行被执行碎片)
rem ============================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem [步骤0] 端口预清理（owner 2026-09-20 要求。起因：合并分支后旧 vite
rem  dev 进程仍存活, 模块缓存被合并污染, 报 invalid JS syntax 误导错误）
echo [步骤0] 检查端口占用 (5173 Vite / 17321 后端)...
rem --- 端口 5173：期望残留进程 node.exe ---
set "VKILL="
for /f "tokens=5" %%p in ('netstat -ano ^| findstr LISTENING ^| findstr :5173') do (
  if /i not "%%p"=="!VKILL" (
    set "VKILL=%%p"
    set "KIMG="
    for /f "delims=, tokens=1" %%i in ('tasklist /FI "PID eq %%p" /FO CSV /NH') do set "KIMG=%%i"
    echo !KIMG! | findstr /i ".exe" >nul
    if !errorlevel! == 0 echo !KIMG! | findstr /i "node.exe" >nul && (
      echo   端口 5173 被旧进程 !KIMG! [PID %%p] 占用，正在结束...
      taskkill /F /PID %%p >nul 2>&1
    )
  )
)
rem --- 端口 17321：期望残留进程 1Remote.exe ---
set "BKILL="
for /f "tokens=5" %%p in ('netstat -ano ^| findstr LISTENING ^| findstr :17321') do (
  if /i not "%%p"=="!BKILL" (
    set "BKILL=%%p"
    set "KIMG="
    for /f "delims=, tokens=1" %%i in ('tasklist /FI "PID eq %%p" /FO CSV /NH') do set "KIMG=%%i"
    echo !KIMG! | findstr /i ".exe" >nul
    if !errorlevel! == 0 echo !KIMG! | findstr /i "1Remote.exe" >nul && (
      echo   端口 17321 被旧进程 !KIMG! [PID %%p] 占用，正在结束...
      taskkill /F /PID %%p >nul 2>&1
    )
  )
)
echo 端口检查完成。

echo 1Remote WebUI 开发环境: 前端 Vite(端口 5173) + 后端 Debug(API 端口 17321)。
echo "webui - vite dev" 窗口是 Vite 前端服务器; 本窗口运行 Debug 后端。
echo 界面引擎=Web(DEBUG)时应用内窗口直接加载 Vite 即热重载; 引擎=WPF 时用浏览器页调试前端。
echo 退出: 直接关闭对应窗口即可。
echo.

rem [步骤1] 新开窗口启动 Vite 前端服务器(webui\node_modules 缺失时先自动安装依赖)
start "webui - vite dev" cmd /k "cd /d %~dp0webui && (if not exist node_modules npm install) && npm run build && npm run dev"

rem [步骤2] 等约 5 秒让 Vite 先起来（ping 代替 timeout：非交互 stdin 下 timeout 会报错）
ping -n 6 127.0.0.1 >nul

rem [步骤3] 打开浏览器访问前端(若页面空白, 等 Vite 就绪后刷新即可)
start http://localhost:5173

echo 正在启动后端 Debug, 构建错误会显示在本窗口...
echo.

rem [步骤4] 本窗口运行后端; 阻塞直到应用退出, 因此必须放在最后
cd .\Ui\bin\Debug\net9.0-windows10.0.19041.0 && .\1Remote.exe

rem 后端已退出, 暂停以免窗口闪退, 便于查看错误信息
pause