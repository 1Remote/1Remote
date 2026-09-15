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

## 构建发布

```bash
npm run build
```

产物输出到 `dist/`。此后构建 Ui 项目（如 `dotnet build -c Release Ui/Ui.csproj`）时，
csproj 会把 `webui/dist/**` 复制到输出目录的 `wwwroot/`，根路径 `/` 即入口页面。

注意：

- `dist/` 为构建产物，不入版本库；改动前端后需重新 `npm run build`，桌面端构建才会带上最新产物。
- dist 不存在时 csproj 通配符匹配 0 项，桌面端 Debug 构建不受影响（仅无静态页可托管）。
