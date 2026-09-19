@echo off
rem ============================================================
rem build-release.cmd - 1Remote 正式发布版一键构建脚本（双击即用）
rem 用法: build-release.cmd        默认=单文件 exe（自包含, 免装 .NET, 一个 1Remote.exe）
rem       build-release.cmd folder 框架依赖目录版（需目标机装 .NET 9, 体积最小）
rem       build-release.cmd sc     自包含目录版（免装 .NET, 整目录分发）
rem 步骤: 1) 构建前端 webui/dist（csproj 会复制进发布目录 wwwroot）
rem       2) dotnet publish -c Release（对齐 GitHub CI 的 net9.0 x64 配方）
rem 注意: single 模式显式指定 PublishDir，不依赖 TFM 目录命名（TFM 全名会随 SDK 变化）
rem 注意: 本文件是 ANSI(GBK) 编码，勿另存为 UTF-8（cmd 解析批处理会碎行）
rem ============================================================
setlocal
cd /d "%~dp0"

set MODE=%1
if /i "%MODE%"=="" set MODE=single

if /i %MODE%==single (
    echo [模式] 单文件 exe（自包含 .NET 运行时 + WebView2 + wwwroot 全部打入 1Remote.exe）
    set PUBLISHARGS=-p:PublishSingleFile=true -p:SelfContained=true -p:RuntimeIdentifier=win-x64 -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishDir=bin/Release/publish-single/
    set OUTDIR=Ui\bin\Release\publish-single
) else if /i %MODE%==folder (
    echo [模式] 框架依赖目录版（默认 CI 配方；需要目标机器安装 .NET 9 桌面运行时）
    set PUBLISHARGS=-p:PublishProfile=./Ui/Properties/PublishProfiles/x64-net90.pubxml
    set OUTDIR=Ui\bin\Release\net9.0-windows\publish\win-x64
) else if /i %MODE%==sc (
    echo [模式] 自包含目录版（免装 .NET；整个目录分发）
    set PUBLISHARGS=-p:PublishProfile=./Ui/Properties/PublishProfiles/x64-net90-self-contained.pubxml
    set OUTDIR=Ui\bin\Release\net9.0-windows-self-contained\publish\win-x64
) else (
    echo 未知参数 %MODE%（可用: 空=single / folder / sc）
    pause
    exit /b 1
)

echo [1/2] 构建前端 (Vite 生产构建)...
pushd webui
if not exist node_modules (
    echo node_modules 缺失，先安装依赖（仅首次较慢）...
    call npm install
    if errorlevel 1 goto :fail
)
call npm run build
if errorlevel 1 goto :fail
popd

echo [2/2] dotnet publish (Release, net9.0 x64)...
dotnet publish Ui/Ui.csproj -c Release %PUBLISHARGS%
if errorlevel 1 goto :fail

echo.
echo ============================================================
echo 构建成功！发布产物目录：
echo   %~dp0%OUTDIR%
echo ============================================================
if not exist "%~dp0%OUTDIR%" (
    echo [警告] 未找到产物目录 %OUTDIR%，请检查上方 dotnet 输出的实际发布路径。
    pause
    exit /b 1
)
start "" "%~dp0%OUTDIR%"
pause
exit /b 0

:fail
popd 2>nul
echo.
echo 构建失败，请检查上方错误信息。
pause
exit /b 1