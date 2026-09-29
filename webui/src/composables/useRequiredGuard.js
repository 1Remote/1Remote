import { computed, reactive, watch } from 'vue'

/** 编辑器表单树的高亮注入键（EditorDrawer provide → FormField/BulkEditField inject），
 *  与 editor/saveIssues.js 同款模式（避免 4 层 props 链）；无提供方时安全退化为不高亮 */
export const REQUIRED_HOT_KEY = Symbol('required-hot')

/**
 * 必填项守卫（round14 需求：所有添加/编辑界面统一必填处理逻辑，spec §表单必填项）：
 * - 必填未填：保存按钮呈禁用观感（.req-blocked 假禁用——aria-disabled 语义，可点击）
 *   + 按钮旁提示「有必填项未填」（modal.requiredHint，i18n）；
 * - 强行点击：不执行保存——焦点跳到第一个未填的必填项 + 所有未填项红色高亮
 *   （[data-req-key].req-hot，输入填上后自动解除）；
 * - 其他校验（重名/端口非法等）不归本守卫——仍由各表单真 disabled（本守卫只管"必填未填"），
 *   保存中（saving）同样真禁（点击无意义）。
 *
 * 接线形状（各表单一致，改行为只改本文件）：
 *   const guard = useRequiredGuard({ getMissing })        // () => string[] 未填键（表单序）
 *   保存钮：:disabled="saving || 其他真禁"  :class="{ 'req-blocked': guard.blocked }"
 *           @click="guard.trySave(save, { onMissing })"   // onMissing 可选（编辑器用它设横幅）
 *   提示：  <span v-if="guard.blocked" class="req-hint">{{ t('modal.requiredHint') }}</span>
 *   字段行：<div data-req-key="name" :class="{ 'req-hot': guard.isHot('name') }">
 *   （key 建议与表单模型键一致；聚焦取该元素内首个 input/textarea/select）
 */
export function useRequiredGuard({ getMissing }) {
  // 强点后高亮的键集（红色）；watch getMissing 自动移除已填上的键——表单零额外接线
  const hot = reactive(new Set())
  const blocked = computed(() => getMissing().length > 0)

  watch(
    () => getMissing(),
    (missing) => {
      for (const k of [...hot]) if (!missing.includes(k)) hot.delete(k)
    }
  )

  function isHot(k) {
    return hot.has(k)
  }

  /**
   * 保存按钮点击入口：未填全 → 引导（全部高亮 + 聚焦第一个在 DOM 中可寻的字段）并拦下；
   * 填齐 → 执行 save()。返回 true 表示已拦下（调用方一般无需用返回值，按钮语义已完整）。
   */
  function trySave(save, { onMissing } = {}) {
    const missing = getMissing()
    if (!missing.length) {
      save()
      return false
    }
    for (const k of missing) hot.add(k)
    onMissing?.(missing)
    // 聚焦第一个未填（getMissing 的键序即表单序）。子表单行内项键带 #行号 无独立 DOM——
    // 回退按前缀定位到子表单字段容器（round15：横幅已删，容器滚动定位 + 行内红标指位）
    for (const k of missing) {
      const row =
        document.querySelector(`[data-req-key="${CSS.escape(k)}"]`) ||
        document.querySelector(`[data-req-key="${CSS.escape(String(k).split('#')[0])}"]`)
      if (!row) continue
      const input = row.querySelector('input, textarea, select')
      if (input) input.focus()
      else row.scrollIntoView({ behavior: 'smooth', block: 'center' }) // 容器无输入框（子表单）：滚到可见
      break
    }
    return true
  }

  // round15 BUG 修复：返回 reactive 包装——此前返回普通对象，模板里 guard.blocked 拿到的
  // 是 ComputedRef 本身（普通对象属性不参与模板 ref 解包），恒真值导致"路径/名称已填仍
  // 显示有必填项未填、保存钮恒假禁"（编辑器抽屉因显式 .value 读而幸免）。reactive 后
  // guard.blocked 在模板与脚本中都是解包后的布尔值，勿再写 .value（会是 undefined）
  return reactive({ blocked, hot, isHot, trySave })
}
