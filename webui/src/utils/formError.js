/**
 * 表单保存失败 toast 文案（round17 收敛）：settings.saveFailed + 后端 400 的
 * {errors} 列表（'; ' 连接）；无错误体时回退原始 message（括号包裹）。
 * 此前四处设置分组各持一份同构拼装（GeneralGroup 的带 message 回退版为超集，
 * 统一采用超集口径）。
 *
 * @param {Function} t i18n 翻译函数
 * @param {Error & {status?: number, body?: {errors?: string[]}}} e 请求层抛出的错误
 * @returns {string} 完整 toast 文案
 */
export function saveErrorText(t, e) {
  const detail = e?.body?.errors?.join('; ') || (e?.message ? ` (${e.message})` : '')
  return t('settings.saveFailed') + (detail ? ` ${detail}` : '')
}
