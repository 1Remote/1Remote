import { ref } from 'vue'
import { api } from '../api'

/**
 * 标签候选：已有标签名列表，供编辑器 Tags 字段的建议 chips。
 * 对齐 WPF TagsEditor 的 TagsForSelect（ServerEditorPageViewModel.TagSelections =
 * GlobalData.TagList 的名字）——数据源即 GET /api/tags（跨数据源聚合的 TagDto 列表）。
 *
 * 模块级缓存 + stale-while-revalidate：首个 TAGS 字段组件创建时 refresh() 拉一次，
 * 后续打开编辑器立即用缓存渲染、同时后台重拉一次更新（标签增删后下次打开可见）；
 * 拉取失败静默（建议缺失只影响候选 chips，不影响输入），不缓存失败态（下次可重试）。
 */
let cache = []
let inflight = null
// 模块级共享 ref（round17）：此前 tags 是每实例 ref(cache) 而 inflight/then 闭包只绑定
// 首实例的 ref——B 实例在 A 的请求在途时调 refresh() 拿到共享 promise，回调只更新 A 的
// ref，B 的候选停留旧缓存且本轮不再更新。提升为模块级后所有实例同源（同 useServers 模式）
const tags = ref(cache)

export function useTagSuggestions() {
  function refresh() {
    if (inflight) return inflight
    inflight = api
      .tags()
      .then((list) => {
        cache = (Array.isArray(list) ? list : [])
          .map((x) => (typeof x === 'string' ? x : x?.name))
          .filter((s) => typeof s === 'string' && s !== '')
        tags.value = cache
      })
      .catch(() => {
        /* 静默退化：候选为空 */
      })
      .finally(() => {
        inflight = null
      })
    return inflight
  }
  return { tags, refresh }
}
