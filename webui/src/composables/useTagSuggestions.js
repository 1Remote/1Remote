import { ref } from 'vue'
import { api } from '../api'

/**
 * 标签候选（batch9 #10）：已有标签名列表，供编辑器 Tags 字段的建议 chips。
 * 对齐 WPF TagsEditor 的 TagsForSelect（ServerEditorPageViewModel.TagSelections =
 * GlobalData.TagList 的名字）——数据源即 GET /api/tags（跨数据源聚合的 TagDto 列表）。
 *
 * 模块级缓存 + stale-while-revalidate：首个 TAGS 字段组件创建时 refresh() 拉一次，
 * 后续打开编辑器立即用缓存渲染、同时后台重拉一次更新（标签增删后下次打开可见）；
 * 拉取失败静默（建议缺失只影响候选 chips，不影响输入），不缓存失败态（下次可重试）。
 */
let cache = []
let inflight = null

export function useTagSuggestions() {
  const tags = ref(cache)
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
