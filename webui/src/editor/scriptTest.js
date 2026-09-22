/**
 * 脚本字段的「测试」结果弹窗（自 FormField 抽出的纯渲染辅助，无 Vue 响应式依赖）。
 *
 * 呈现 = 命令行 + 输出（pre 滚动区）+ 退出码行——WPF 脚本测试（RunScriptBeforeConnect 的
 * isTestRun 消息盒："We will run..." 提示 + 控制台窗口 + "The exit code..."）的 web 等价：
 *  - 后端失败（resp.error 非空）→ 错误行替代输出区；
 *  - 超时（resp.timedOut）→ 超时提示行，退出码行不展示；
 *  - 连接前脚本（CommandBeforeConnected）的退出码带「非 0 中止连接」括注（WPF 消息盒
 *    文案同语义），连接后脚本用普通文案。
 * dialog 内容经 h() 组装、inline style——弹窗 teleport 到 body，scoped 样式作用不到。
 */
import { h } from 'vue'

/**
 * @param {Object} deps
 * @param {Object} deps.dialog naive-ui useDialog() 实例（弹窗宿主）
 * @param {(key: string, params?: Object) => string} deps.t i18n t 函数（editor.* 文案）
 * @param {string} deps.fieldKey 字段 PascalCase 键（CommandBeforeConnected → 中止括注）
 * @param {string} deps.command 被测试的单行命令文本
 * @param {{error?: string, output?: string, exitCode?: number, timedOut?: boolean}} deps.resp
 *   POST /api/scripts/test 的回传体
 */
export function showScriptTestResult({ dialog, t, fieldKey, command, resp }) {
  const line = (text) =>
    h('div', { style: 'font-size:var(--fs-body);line-height:1.6;color:var(--text-2);word-break:break-all;' }, text)
  const rows = [line(t('editor.scriptTestCmd', { cmd: command }))]
  if (resp?.error) {
    rows.push(line(`${t('editor.scriptTestStartFailed')}: ${resp.error}`))
  } else {
    rows.push(
      h(
        'pre',
        {
          style:
            'margin:8px 0;max-height:240px;overflow:auto;white-space:pre-wrap;word-break:break-all;' +
            // 字号/圆角走令牌（与 .note-md pre 同款 ctrl 档），仅剩布局属性内联——
            // 弹窗内容 teleport 到 body，scoped 样式作用不到（见文件头）
            'border:1px solid var(--border);border-radius:var(--radius-ctrl);background:var(--bg-hover);padding:8px;font-size:var(--fs-caption);',
        },
        resp?.output || ' '
      )
    )
  }
  if (resp?.timedOut) {
    rows.push(line(t('editor.scriptTestTimeout')))
  } else if (!resp?.error) {
    const code = resp?.exitCode ?? -1
    rows.push(
      line(
        fieldKey === 'CommandBeforeConnected'
          ? t('editor.scriptTestExitAbort', { code })
          : t('editor.scriptTestExit', { code })
      )
    )
  }
  // L3：dialog.info 预设自带蓝色 ⓘ 图标，是全站唯一违反「无图标」三档策略的对话框
  //（round2 J29 清点时它走 info 预设未被数入）——改标准 create + showIcon:false，
  // 并补 autoFocus:false 对齐全库「禁回车误触」纪律（信息框仅一个确定钮，属低风险顺带）。
  // N1：确认钮补中性档（非破坏性确认应配 default 钮，同导出/批量连接确认）——此前漏设
  // 时 naive 默认渲染 primary 实底，是 L4 统一描边后全站仅存的实底主按钮
  dialog.create({
    title: t('editor.scriptTestTitle'),
    content: () => h('div', null, rows),
    showIcon: false,
    positiveText: t('common.ok'),
    positiveButtonProps: { type: 'default' },
    autoFocus: false,
  })
}
