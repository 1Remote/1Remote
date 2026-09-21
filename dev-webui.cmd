@echo off
rem ============================================================
rem dev-webui.cmd - 1Remote WebUI 一键开发环境启动脚本
rem   前端: webui/ (Vite dev server, 端口 5173, 独立窗口)
rem   后端: Ui 项目 Debug 产物 (内嵌 Kestrel API, 端口 17321, 独立窗口)
rem   端口预清理: 启动前自动清理占用 5173/17321 的旧进程
rem   (5173 只杀 node.exe —— 正在跑的 vite; 17321 只杀 1Remote.exe 来自 Debug;
rem    其它进程占用只提示不强杀)
rem   2026-09-21 修复: 原脚本先开浏览器后启动后端，页面首屏所有 /api 请求
rem   必然命中 ECONNREFUSED(后端未监听) —— 改为: 后端先行启动，轮询 17321
rem   端口就绪后再打开浏览器（最长等待 120s）
rem   注意: 本文件是 ANSI(GBK) 编码。请勿保存为 UTF-8 (cmd 读取乱码偏移
rem   bug, 导致中文注释变成乱码碎片)
rem ============================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem [步骤0] 端口预清理 (owner 2026-09-20 要求; 原因: 合并分支后 vite
rem dev 服务源码, 模块缓存被合并渲染, 曾出 invalid JS syntax 错导重启)
echo [步骤0] 端口预清理 (5173 Vite / 17321 后端)...
rem --- 端口 5173: 只清理 node.exe ---
set "VKILL="
for /f "tokens=5" %%p in ('netstat -ano ^| findstr LISTENING ^| findstr :5173') do (
  if /i not "%%p"=="!VKILL" (
    set "VKILL=%%p"
    set "KIMG="
    for /f "delims=, tokens=1" %%i in ('tasklist /FI "PID eq %%p" /FO CSV /NH') do set "KIMG=%%i"
    echo !KIMG! | findstr /i ".exe" >nul
    if !errorlevel! == 0 echo !KIMG! | findstr /i "node.exe" >nul && (
      echo   端口 5173 已被 node.exe [PID %%p] 占用, 正在结束...
      taskkill /F /PID %%p >nul 2>&1
    )
  )
)
rem --- 端口 17321: 只清理 1Remote.exe ---
set "BKILL="
for /f "tokens=5" %%p in ('netstat -ano ^| findstr LISTENING ^| findstr :17321') do (
  if /i not "%%p"=="!BKILL" (
    set "BKILL=%%p"
    set "KIMG="
    for /f "delims=, tokens=1" %%i in ('tasklist /FI "PID eq %%p" /FO CSV /NH') do set "KIMG=%%i"
    echo !KIMG! | findstr /i ".exe" >nul
    if !errorlevel! == 0 echo !KIMG! | findstr /i "1Remote.exe" >nul && (
      echo   端口 17321 已被 1Remote.exe [PID %%p] 占用, 正在结束...
      taskkill /F /PID %%p >nul 2>&1
    )
  )
)
echo 端口检查完成。

echo 1Remote WebUI 开发环境: 前端 Vite(端口 5173) + 后端 Debug(API 端口 17321)。
echo "webui - vite dev" 窗口承载 Vite 前端服务; 后端在独立窗口运行。
echo 启动方式=Web(DEBUG)时应用程序会直接加载 Vite 开发服务器; 界面=WPF 时使用内嵌前端。
echo 退出: 直接关闭对应窗口即可。

rem [步骤1] 前端: Vite dev server (webui\node_modules 缺失时自动安装依赖)
start "webui - vite dev" cmd /k "cd /d %~dp0webui && (if not exist node_modules npm install) && npm run build && npm run dev"

rem [步骤2] 后端先行启动 (独立窗口保留崩溃信息; 不再阻塞本脚本)
set "BACKEND_DIR=%~dp0Ui\bin\Debug\net9.0-windows10.0.19041.0"
if not exist "%BACKEND_DIR%\1Remote.exe" (
  echo [错误] 未找到后端程序: %BACKEND_DIR%\1Remote.exe
  echo 请先编译 Ui 项目 ^(生成 Debug 产物^) 再运行本脚本。
  pause
  exit /b 1
)
echo 正在启动后端 Debug (独立窗口)...
start "1Remote backend - Debug" cmd /k "cd /d %BACKEND_DIR% && 1Remote.exe"

rem [步骤3] 等待后端端口就绪 (最长约 120 秒) —— 就绪后再开浏览器, 避免首屏
rem 一批 /api 请求打到未监听端口出现 ECONNREFUSED 错误风暴
set /a WAITED=0
echo 正在等待后端 API (127.0.0.1:17321) 就绪...
:wait_backend
netstat -ano | findstr LISTENING | findstr :17321 >nul 2>&1
if !errorlevel! == 0 goto backend_ready
ping -n 2 127.0.0.1 >nul
set /a WAITED+=1
if !WAITED! GEQ 120 goto backend_timeout
goto wait_backend

:backend_timeout
echo [警告] 等待后端超时^(约120秒^), 仍将打开前端页面 —— 若页面提示"后端不可达",
echo 请查看 "1Remote backend - Debug" 窗口的输出排查启动失败原因。
goto open_browser

:backend_ready
echo 后端 API 已就绪 ^(等待约 !WAITED! 秒^)。

:open_browser
rem [步骤4] 打开前端页面
start http://localhost:5173
echo 开发环境已启动: 前端 http://localhost:5173 / 后端 127.0.0.1:17321
echo 本窗口可以关闭; 关闭 "webui - vite dev" 或后端窗口即停止对应服务。
pause
