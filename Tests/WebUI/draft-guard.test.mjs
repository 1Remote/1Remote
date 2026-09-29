// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · draft-guard.test.mjs · L2 组件模型
// ▸ 本文件测什么：模态草稿守卫（useDraftGuard）：无改动直接关、有输入弹丢弃确认、
//   忙碌忽略一切；末段锚点确认所有弹窗/抽屉接线同一守卫。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/draft-guard.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 模态草稿守卫用例（round13 统一关闭语义 + round14 遮罩改弹确认：干净点遮罩即关 /
// 有输入点遮罩直接弹「未保存的更改」丢弃确认，与 ✕·取消·Esc 同路径 / 忙碌一切忽略）。
// 语义单一来源：src/composables/useDraftGuard.js（纯 JS：dialog/message/t 全部注入，可直测）。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { useDraftGuard } from '../../webui/src/composables/useDraftGuard.js'

function fixture({ dirty = false, busy = false, doCloseReturn } = {}) {
  const state = { closed: 0, infos: [], dialogs: [] }
  const guard = useDraftGuard({
    isDirty: () => dirty,
    doClose: () => {
      state.closed++
      return doCloseReturn
    },
    isBusy: () => busy,
    dialog: {
      create(opts) {
        state.dialogs.push(opts)
        return {}
      },
    },
    message: { info: (m) => state.infos.push(m) },
    t: (k) => k,
  })
  return { guard, state }
}

describe('干净表单（无修改）', () => {
  it('点遮罩直接关闭（round13 需求主场景）', () => {
    const { guard, state } = fixture()
    guard.onMaskClick()
    assert.equal(state.closed, 1)
    assert.deepEqual(state.infos, [])
  })
  it('✕/取消/Esc（requestClose）直接关闭', () => {
    const { guard, state } = fixture()
    guard.requestClose()
    assert.equal(state.closed, 1)
    assert.equal(state.dialogs.length, 0)
  })
})

describe('有输入（脏表单）', () => {
  it('点遮罩直接弹丢弃确认（round14 需求：与 ✕/Esc 同一确认框，不再 message.info）', () => {
    const { guard, state } = fixture({ dirty: true })
    guard.onMaskClick()
    assert.equal(state.closed, 0, '不得关闭')
    assert.deepEqual(state.infos, [], '不再弹引导提示')
    assert.equal(state.dialogs.length, 1, '直接弹「未保存的更改」确认框')
    assert.equal(state.dialogs[0].title, 'editor.unsavedTitle')
  })
  it('requestClose 弹丢弃确认（danger 档 + 复用 editor.unsaved* 词条）', () => {
    const { guard, state } = fixture({ dirty: true })
    guard.requestClose()
    assert.equal(state.closed, 0)
    assert.equal(state.dialogs.length, 1)
    assert.equal(state.dialogs[0].positiveButtonProps.type, 'error')
    assert.equal(state.dialogs[0].title, 'editor.unsavedTitle')
    assert.equal(state.dialogs[0].autoFocus, false)
    assert.equal(state.dialogs[0].showIcon, false)
  })
  it('确认丢弃后才关闭；确认回调不外泄返回值（round13 BUG3：false 会阻止确认框关闭）', () => {
    // doClose 故意返回 false——守卫必须吞掉，onPositiveClick 的返回值须为 undefined
    const { guard, state } = fixture({ dirty: true, doCloseReturn: false })
    guard.requestClose()
    const ret = state.dialogs[0].onPositiveClick()
    assert.equal(ret, undefined, 'onPositiveClick 返回值必须为 undefined（false=阻止关闭）')
    assert.equal(state.closed, 1)
  })
  it('onAfterLeave 复位防叠框守卫：丢弃后再 requestClose 仍可弹确认', () => {
    const { guard, state } = fixture({ dirty: true })
    guard.requestClose()
    guard.requestClose() // 确认框开着期间：防叠框，不弹第二个
    assert.equal(state.dialogs.length, 1)
    state.dialogs[0].onAfterLeave()
    guard.requestClose()
    assert.equal(state.dialogs.length, 2)
  })
})

describe('忙碌（保存在途）', () => {
  it('一切关闭尝试被忽略（干净/脏两态同口径）', () => {
    for (const dirty of [false, true]) {
      const { guard, state } = fixture({ dirty, busy: true })
      guard.onMaskClick()
      guard.requestClose()
      assert.equal(state.closed, 0)
      assert.equal(state.dialogs.length, 0)
      assert.deepEqual(state.infos, [], '忙碌态连提示也不弹（保存中按钮已禁）')
    }
  })
})

describe('源码接线（所有添加/编辑弹窗与抽屉走同一守卫）', () => {
  it('数据源/凭据库/Runner 添加模态与编辑器抽屉全部接入 mask 语义', async () => {
    const { read } = await import('./helpers.mjs')
    // 三个设置模态：mask-closable=false + @mask-click=守卫 + @update:show=守卫
    for (const f of [
      'src/components/settings/DataSourceGroup.vue',
      'src/components/settings/CredentialVaultGroup.vue',
      'src/components/settings/RunnerAddModal.vue',
    ]) {
      const v = read(f)
      assert.ok(v.includes('@mask-click='), `${f} 应接遮罩点击守卫`)
      assert.ok(v.includes(':mask-closable="false"'), `${f} 遮罩不应直通 naive 自动关闭`)
      assert.ok(v.includes('useDraftGuard'), `${f} 应使用共享守卫`)
    }
    // 编辑器抽屉（无 n-modal，scrim 自绘）：round14 K1 收编——遮罩点击与关闭请求
    // 全部来自共享守卫，不再持手写 closing/确认框
    const drawer = read('src/components/editor/EditorDrawer.vue')
    assert.ok(drawer.includes('@click="onMaskClick"'), '抽屉遮罩应走守卫 onMaskClick')
    assert.ok(drawer.includes('useDraftGuard'), '抽屉应使用共享守卫（K1 收编）')
    assert.ok(!drawer.includes('let closing'), '手写 closing 守卫应删除')
  })
  it('N5：丢弃确认框开着时 Esc 让位（useSettingsEsc 与抽屉 onKey 双通道）', async () => {
    const { read } = await import('./helpers.mjs')
    const use = read('src/composables/useSettingsEsc.js')
    assert.ok(use.includes("document.querySelector('.n-dialog')"), '设置模态 Esc 截停应让位 naive 对话框')
    const drawer = read('src/components/editor/EditorDrawer.vue')
    const escAt = drawer.indexOf("e.key === 'Escape'")
    const seg = drawer.slice(escAt, escAt + 900) // Esc 分支体（到分支结束足够）
    assert.ok(seg.includes("document.querySelector('.n-dialog')"), '抽屉 Esc 应让位 naive 对话框')
  })
})
