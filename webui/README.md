# 1Remote Web UI

1Remote 桌面端内嵌的 Web 前端（Vue 3 + Vite + Naive UI + vue-i18n）。

- 开发模式：独立 Vite dev server 热更新调试，API 经代理转发到桌面端 DEBUG 后端。
- 发布模式：构建为纯静态产物 `dist/`，由 `Ui/Ui.csproj` 复制到输出目录 `wwwroot/`，
  由进程内 Kestrel（`Ui/Service/WebUi/WebUiServer.cs`）托管，WebView2/浏览器直接访问。

## 环境要求

- Node.js >= 20.19（约束见 `package.json` 的 `engines` 字段）

## 开发模式（热更新）

1. 以 **DEBUG** 配置启动桌面端 1Remote（内置 Web 服务固定监听 `127.0.0.1:17321`，免 token）。
2. 安装依赖并启动 dev server：

   ```bash
   npm install
   npm run dev
   ```

3. 浏览器打开 <http://localhost:5173>。`/api/*` 请求由 Vite 代理转发到 17321
   （见 `vite.config.js` 的 `server.proxy`），页面改动即时热更新。

   注意：DEBUG 下应用内 WebView2 切到 Web 引擎时同样导航到 `http://localhost:5173`——
   即**应用内调试也要求 dev server 正在运行**（`npm run dev`），否则 WebView 显示连接失败页；
   Release 形态则由进程内 Kestrel 直接托管静态产物，无需 dev server。

## 构建发布

```bash
npm run build
```

产物输出到 `dist/`。此后构建 Ui 项目（如 `dotnet build -c Release Ui/Ui.csproj`）时，
csproj 会把 `webui/dist/**` 复制到输出目录的 `wwwroot/`，根路径 `/` 即入口页面。

注意：

- `dist/` 为构建产物，不入版本库；改动前端后需重新 `npm run build`，桌面端构建才会带上最新产物。
- dist 不存在时 csproj 通配符匹配 0 项，桌面端 Debug 构建不受影响（仅无静态页可托管）。
- 排障：Release 运行时页面 404/空白 → webui/dist 未构建（先执行 `npm run build` 再
  `dotnet build`/`dotnet publish`；CI 已在 publish 前自动构建 webui，不会出现此问题）。

## 测试与每日回归

```bash
npm run test    # 93 条自动化用例（node:test 零依赖：语义/勾选/schema/接线锚点/i18n）
npm run check   # test + prettier + 14 语言键集平价（每日迭代完成后的最低门禁）
```

- 用例文件在 **`Tests/WebUI/`**（与 C# 测试同级的 WebUI 测试目录；npm 入口仍在本包，
  依赖解析经 `Tests/WebUI/helpers.mjs` 定位到本包的 node_modules）。
- 用例清单、分层说明（L1 逻辑 / L2 组件模型 / L3 源码不变量 / L4 i18n / M 人工）、
  每日回归流程与人工冒烟清单：**`Tests/WebUI/README.md`**。
- 既有语义断言入口 `node scripts/semantics-test.mjs`（12 条）保留在 webui/scripts/，
  内容为 `Tests/WebUI/folders.test.mjs` 的子集。
