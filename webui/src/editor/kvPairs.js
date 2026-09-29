/**
 * 结构化键值行（Runner 环境变量/特殊字符）的纯逻辑：
 * 存储 = PascalCase 数组 [{Key, Value}]（ExternalRunner.EnvironmentVariables /
 * SpecialCharacters 的序列化形态，WPF ListBox 行式编辑同源）。
 *
 * 设计要点（round11 U6/用户报告"输入被清空"的根因修复）：
 * - 不再有"行文本解析"环节——行编辑器直接产出行对象，格式非法（无等号）的
 *   半行不可能出现，"解析丢行→保存回读覆盖输入框"的清空链路从结构上移除。
 * - 半填行（value 非空、key 为空）**保留在输出里**：随自动保存入库（模型层与
 *   WPF 自身的加行按钮同样容忍空 Key 行，运行时应用侧跳过空 Key），用户输入
 *   不丢；UI 侧对半填行标红提示（名称为空不生效）。
 * - 全空行（key 与 value 均为空白）**不进输出**：加行后未输入任何内容的行不
 *   产生数据（与 KeyValueLines/KvMapField 的"空名行序列化丢弃"同语义——但这里
 *   行可见、由用户显式删除或留空，不存在静默丢失）。
 * - Key/Value 均不做 trim/归一（WPF TextBox 直接绑定、原样存储的平价）；空置性
 *   判定（rowIsHalf/isFullEmpty）用 trim 后结果。
 * - mergeExternal：自动保存成功后服务端回读（applyState 整体替换）与本地行的
 *   合并规则——外部值为主（服务器归一/winscp 旁路注入的行照单全收），但本地
 *   半填行不在此刻被覆盖掉（否则"保存成功瞬间半填行消失"＝清空 bug 换个形态
 *   复活）；与外部内容重复的半填行不追加（回声里已含同一行）。
 */

/** 入参 [{Key,Value}]|null|undefined → 行态 [{key,value}]（值归一字符串，保持顺序）。
 *  round17 曾误去 export：kv-pairs 测试直接消费，已回滚。 */
export function rowsFromPairs(pairs) {
  if (!Array.isArray(pairs)) return []
  return pairs.map((p) => ({
    key: p && p.Key != null ? String(p.Key) : '',
    value: p && p.Value != null ? String(p.Value) : '',
  }))
}

/** 行态 → PascalCase 输出（全空行丢弃；半填行保留，见文件头）。 */
export function pairsFromRows(rows) {
  return (Array.isArray(rows) ? rows : [])
    .filter((r) => !isFullEmpty(r))
    .map((r) => ({ Key: String(r.key ?? ''), Value: String(r.value ?? '') }))
}

/** 全空行：key 与 value trim 后均为空（新加未输入的行）。round17 曾误去 export：
 *  kv-pairs 测试直接消费，已回滚。 */
export function isFullEmpty(row) {
  return String(row?.key ?? '').trim() === '' && String(row?.value ?? '').trim() === ''
}

/** 半填行：value 非空但 key 为空（UI 标红"名称为空不生效"；仍随保存入库）。 */
export function rowIsHalf(row) {
  return String(row?.key ?? '').trim() === '' && String(row?.value ?? '').trim() !== ''
}

/** 内容比较（回环挡板：emit 后的回声不再触发重新解析，半输入态不被打断）。 */
export function samePairs(a, b) {
  const norm = (x) => pairsFromRows(rowsFromPairs(x))
  const na = norm(a)
  const nb = norm(b)
  if (na.length !== nb.length) return false
  for (let i = 0; i < na.length; i++) {
    if (na[i].Key !== nb[i].Key || na[i].Value !== nb[i].Value) return false
  }
  return true
}

/**
 * 外部值变化时的行态合并：外部（回读/重载）行照单全收，本地半填行若不在外部
 * 值里则追加在尾部（去重键=内容完全相同）。全空行不参与合并（无内容可保留）。
 * @param {Array} currentRows 本地当前行态（[{key,value}]）
 * @param {Array} incoming 外部 [{Key,Value}] 数组
 * @returns {Array} 新行态 [{key,value}]
 */
export function mergeExternal(currentRows, incoming) {
  const rows = rowsFromPairs(incoming)
  const have = new Set(rows.map((r) => r.key + '\u0000' + r.value))
  for (const r of Array.isArray(currentRows) ? currentRows : []) {
    if (!rowIsHalf(r)) continue
    const k = String(r.key ?? '')
    const v = String(r.value ?? '')
    if (!have.has(k + '\u0000' + v)) {
      rows.push({ key: k, value: v })
      have.add(k + '\u0000' + v)
    }
  }
  return rows
}
