// token：生产模式由外壳 URL ?token=xxx 注入；开发模式（Vite 代理直连 DEBUG 后端）不需要
let token = ''
{
  const q = new URLSearchParams(location.search).get('token')
  if (q) {
    token = q
    sessionStorage.setItem('1r-token', token)
    history.replaceState(null, '', location.pathname + location.hash) // 清 token 但保留 hash（深链 ?token=xxx#/settings）
  } else {
    token = sessionStorage.getItem('1r-token') || ''
  }
}

async function request(path, { method = 'GET', body } = {}) {
  const headers = { 'Content-Type': 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  const resp = await fetch(path, { method, headers, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30_000) })
  return handleResponse(resp, path)
}

// 非 JSON 请求共用体（multipart 上传 / blob 下载）：不预设 Content-Type——
// multipart 由浏览器补 boundary；blob=true 时返回 {blob, filename}（下载文件），
// filename 取 Content-Disposition（RFC 5987 filename*= 优先，兼容裸 filename=；
// 无头回退空串由调用方给默认名），否则仍解析 JSON
async function requestRaw(path, { method = 'GET', body, timeout = 120_000, blob = false } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  const resp = await fetch(path, { method, headers, body, signal: AbortSignal.timeout(timeout) })
  if (blob && resp.ok) {
    const blob = await resp.blob()
    const cd = resp.headers.get('Content-Disposition') || ''
    const star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(cd)
    const plain = /filename=(?:"([^"]+)"|([^;]+))/i.exec(cd)
    const raw = (star?.[1] || plain?.[1] || plain?.[2] || '').trim()
    let filename = ''
    try {
      filename = decodeURIComponent(raw) // filename* 的百分号编码；裸 filename 含 % 时无害回退原串
    } catch {
      filename = raw
    }
    return { blob, filename }
  }
  return handleResponse(resp, path)
}

async function handleResponse(resp, path) {
  if (resp.status === 401) throw new Error('unauthorized')
  if (!resp.ok) {
    // 错误体尽量带回：编辑器保存 400 的 {errors} 列表要在抽屉内联展示（err.status/err.body）
    let errBody = null
    try {
      errBody = await resp.json()
    } catch {
      /* 无响应体或非 JSON（404/500 可能是空体）——保持 null */
    }
    const err = new Error(`${resp.status} ${path}`)
    err.status = resp.status
    err.body = errBody
    throw err
  }
  if (resp.status === 204) return null // DELETE 成功无内容（resp.json() 对空体会抛 SyntaxError）
  return resp.json()
}

export const api = {
  version: () => request('/api/version'),
  servers: () => request('/api/servers'),
  datasources: () => request('/api/datasources'),
  tags: () => request('/api/tags'),
  search: (q) => request(`/api/search?q=${encodeURIComponent(q)}`),
  connect: (id) => request(`/api/connect/${id}`, { method: 'POST' }),
  getAppearance: () => request('/api/settings/appearance'),
  saveAppearance: (a) => request('/api/settings/appearance', { method: 'PUT', body: a }),
  getTreeState: () => request('/api/ui-state/tree'),
  saveTreeState: (s) => request('/api/ui-state/tree', { method: 'PUT', body: s }),
  // 编辑器（Plan 2 Task 8）：config/POST/PUT 的内嵌 json 为 PascalCase 直通域（勿做命名转换），
  // DELETE 成功返回 204 → null（request 内已处理空体）
  getServerConfig: (id, ds) => request(`/api/servers/${encodeURIComponent(id)}/config?ds=${encodeURIComponent(ds ?? 'Local')}`),
  createServer: (json, ds) => request('/api/servers', { method: 'POST', body: { dataSourceName: ds ?? 'Local', json } }),
  updateServer: (id, json, ds) => request(`/api/servers/${encodeURIComponent(id)}?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'PUT', body: { json } }),
  deleteServer: (id, ds) => request(`/api/servers/${encodeURIComponent(id)}?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'DELETE' }),
  // 批量补丁：patch 键为 camelCase（列表 DTO 域，与后端 allow-list 对应）；ds 省略 = Local
  batchUpdate: (ids, patch, ds) =>
    request('/api/servers/batch', { method: 'POST', body: ds ? { ids, patch, ds } : { ids, patch } }),
  icons: () => request('/api/icons'),
  credentialNames: (ds) => request('/api/credentials/names?ds=' + encodeURIComponent(ds)),
  extractIcon: (path) => request('/api/icons/extract-from-exe', { method: 'POST', body: { path } }),
  // 凭据库管理（Plan 3 Task 1）：credential 字段与 WPF 模型一致（PascalCase），
  // password/privateKeyPath 为明文（服务端加密落库）；reveal 受本地二次验证保护（30s 窗口）
  getCredentials: (ds) => request('/api/credentials?ds=' + encodeURIComponent(ds ?? 'Local')),
  createCredential: (credential, ds) => request('/api/credentials', { method: 'POST', body: { ds: ds ?? 'Local', credential } }),
  updateCredential: (name, credential, ds) =>
    request(`/api/credentials/${encodeURIComponent(name)}?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'PUT', body: { credential } }),
  deleteCredential: (name, ds) =>
    request(`/api/credentials/${encodeURIComponent(name)}?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'DELETE' }),
  revealCredential: (name, ds) =>
    request(`/api/credentials/${encodeURIComponent(name)}/reveal?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'POST' }),
  // 设置中心（Plan 3 Task 2）：general/launcher 均为白名单部分更新（缺省键=保持不变）；
  // general.language 用小写码（zh-cn），web locale（zh-CN）由调用方转换；
  // requireSecondaryVerification 写路径落注册表/凭据管理器（机器状态），仅在用户明确操作时随 PUT 提交
  getGeneralSettings: () => request('/api/settings/general'),
  saveGeneralSettings: (g) => request('/api/settings/general', { method: 'PUT', body: g }),
  // launcher 热键：hotKeyModifiers/hotKeyKey 线格式 = WPF 枚举成员名（"ControlAlt"/"M"）；
  // PUT 亦接受 "Ctrl+Alt" 显示形态；注册冲突（被其它程序占用）→ 409 {error:'hotkey conflict'}
  getLauncherSettings: () => request('/api/settings/launcher'),
  saveLauncherSettings: (l) => request('/api/settings/launcher', { method: 'PUT', body: l }),
  // 标签管理：列表按数据源聚合 {name,count,pinned}；pin 幂等置目标值；rename/delete 限定数据源范围
  getTagsManage: (ds) => request('/api/tags/manage?ds=' + encodeURIComponent(ds ?? 'Local')),
  saveTagPin: (name, pinned, ds) =>
    request('/api/tags/manage', { method: 'PUT', body: { ds: ds ?? 'Local', name, pinned } }),
  renameTag: (from, to, ds) =>
    request('/api/tags/rename', { method: 'POST', body: { ds: ds ?? 'Local', from, to } }),
  deleteTag: (name, ds) =>
    request(`/api/tags/${encodeURIComponent(name)}?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'DELETE' }),
  // 数据源管理（Plan 3 Task 3）：type = sqlite|mysql|pgsql（postgresql 同义）；name 缺省时 sqlite 由
  // 后端从 path 文件名推导；config.password 仅写方向（新建必填、PUT 空=保持原密码），任何读接口无密码。
  // POST 保存后即返回实际 status（连接失败不回滚，与 WPF 一致——先 testDataSource 验证再保存）。
  // DELETE：数据源下仍有服务器时返回 409 {serverCount}，keepServers=true 确认后按 WPF 语义移除
  //（服务器留在库文件中，不迁移不删除）。
  addDataSource: (type, config, name) =>
    request('/api/datasources', { method: 'POST', body: { type, name, config } }),
  updateDataSource: (name, config) =>
    request(`/api/datasources/${encodeURIComponent(name)}`, { method: 'PUT', body: { config } }),
  deleteDataSource: (name, keepServers = false) =>
    request(`/api/datasources/${encodeURIComponent(name)}${keepServers ? '?keepServers=true' : ''}`, { method: 'DELETE' }),
  testDataSource: (name, config) =>
    request(`/api/datasources/${encodeURIComponent(name)}/test`, { method: 'POST', body: { config } }),
  // 运行器配置：整体往返 {protocols:{SSH:{selectedRunnerName, runners:[...]}}}——runners 数组为
  // PascalCase + $type 直通域（与 GET 原样往返，勿做命名转换）；PUT 缺失协议=保持，未知协议 400
  getRunners: () => request('/api/settings/runners'),
  saveRunners: (protocols) => request('/api/settings/runners', { method: 'PUT', body: { protocols } }),
  // 导入/导出（Plan 4 Task 2）：
  // 导入 = multipart 上传（FormData 由浏览器补 boundary，勿设 Content-Type）；格式按扩展名嗅探
  //（.json=1Remote 导出、.csv=mRemoteNG、.rdp、.db=PRemoteM/1Remote 双探测）→ {added, skipped, errors}
  importServers: (file, ds) => {
    const form = new FormData()
    form.append('file', file)
    return requestRaw(`/api/servers/import?ds=${encodeURIComponent(ds ?? 'Local')}`, { method: 'POST', body: form })
  },
  // 导出 = 明文 JSON attachment（跨数据源，ids 逗号分隔）——blob 响应不能走 JSON request；
  // 二次验证未通过抛 err.status=403；成功返回 {blob, filename}（调用方 object URL + a[download] 触发保存）
  exportServers: (ids) =>
    requestRaw(`/api/servers/export?ids=${ids.map(encodeURIComponent).join(',')}`, { blob: true }),
}

/** 订阅数据版本；返回取消函数。onReload 在每次 reload 事件时回调。 */
export function subscribeEvents(onReload) {
  const es = new EventSource('/api/events' + (token ? `?token=${token}` : ''))
  es.addEventListener('reload', onReload)
  return () => es.close()
}
