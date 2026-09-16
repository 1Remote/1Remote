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
}

/** 订阅数据版本；返回取消函数。onReload 在每次 reload 事件时回调。 */
export function subscribeEvents(onReload) {
  const es = new EventSource('/api/events' + (token ? `?token=${token}` : ''))
  es.addEventListener('reload', onReload)
  return () => es.close()
}
