/**
 * 凭据组「手动输入 ⇄ 从凭据库选择」二选一的模式派生（fix-batch1 Task 3 #7，owner 确认方案）。
 *
 * 纯函数（无 Vue 依赖，node 可直接断言）。约定与 WPF 双 Tab 对齐：
 *  - json.InheritedCredentialName 非空串 → 'vault'（凭据从库条目继承）；
 *  - 空串/缺失 → 'manual'（UserName/Password/PrivateKey 等手动字段生效）。
 *
 * 该函数只做「由值派生初始模式」；编辑期间的切换是用户意图（抽屉内 ref 状态），
 * 切到 manual 时由抽屉清空 InheritedCredentialName（清引用 = 回到手动语义），
 * 切到 vault 时保留手动字段值不动（透传保真，与 WPF 双 Tab 共存语义一致）。
 */

/** @param {string|undefined|null} inheritedCredentialName json 的 InheritedCredentialName */
export function deriveCredentialMode(inheritedCredentialName) {
  return typeof inheritedCredentialName === 'string' && inheritedCredentialName !== '' ? 'vault' : 'manual'
}
