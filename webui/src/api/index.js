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
  if (!resp.ok) throw new Error(`${resp.status} ${path}`)
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
}

/** 订阅数据版本；返回取消函数。onReload 在每次 reload 事件时回调。 */
export function subscribeEvents(onReload) {
  const es = new EventSource('/api/events' + (token ? `?token=${token}` : ''))
  es.addEventListener('reload', onReload)
  return () => es.close()
}
