// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · required-guard.test.mjs · L2 组件模型
// ▸ 本文件测什么：必填项守卫（useRequiredGuard）：未填禁存、强点保存滚动引导到首个
//   未填项；末段锚点确认所有添加/编辑界面接线同一守卫。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/required-guard.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 必填项守卫用例（round14 需求：所有添加/编辑界面统一必填处理逻辑，spec §8.1）。
// 语义单一来源：src/composables/useRequiredGuard.js（纯 JS 可直测；focus/querySelector
// 依赖 DOM 的引导分支用全局桩验证调用契约）。
import { describe, it, afterEach } from 'node:test'
import assert from 'node:assert/strict'
// vue 经 webui 的 node_modules 入口引入（index.mjs——与 src/ 下模块裸导入 'vue' 在
// node 环境解析到的同一实例，响应式系统才能互通；Tests 目录自身不在 webui 包内）
import { reactive, nextTick } from '../../webui/node_modules/vue/index.mjs'
import { useRequiredGuard } from '../../webui/src/composables/useRequiredGuard.js'

function fixture({ missing = [] } = {}) {
  // 响应式源：watch(getMissing) 需要响应式依赖才能触发（真实表单读 reactive 表单对象）
  const state = reactive({ missing: [...missing] })
  const guard = useRequiredGuard({ getMissing: () => state.missing })
  return { guard, setMissing: (m) => (state.missing = [...m]) }
}

// DOM 桩：记录 focus 调用与 data-req-key 元素注册表
const focused = []
const registry = new Map() // key -> element stub
globalThis.document = {
  querySelector: (sel) => {
    const m = sel.match(/^\[data-req-key="(.+)"\]$/)
    return m ? registry.get(m[1]) || null : null
  },
}
globalThis.CSS = { escape: (s) => s }

function stubEl(key) {
  const input = { focus: () => focused.push(key) }
  registry.set(key, { querySelector: () => input })
}

afterEach(() => {
  focused.length = 0
  registry.clear()
})

describe('未填禁存（blocked）', () => {
  it('无必填未填时 blocked=false；有未填时 blocked=true（round15：reactive 返回，直接读布尔）', () => {
    const a = fixture()
    assert.equal(a.guard.blocked, false)
    const b = fixture({ missing: ['name'] })
    assert.equal(b.guard.blocked, true)
    // 解包契约：模板里 guard.blocked 拿到的必须是布尔——普通对象返回会让它恒 truthy
    //（round15 修复的"路径/名称已填仍提示必填"BUG 即此）
    assert.equal(typeof b.guard.blocked, 'boolean')
  })
})

describe('强点引导（trySave）', () => {
  it('填齐时直接执行保存，不高亮不聚焦', () => {
    const { guard } = fixture()
    let saved = 0
    guard.trySave(() => saved++)
    assert.equal(saved, 1)
    assert.equal(guard.hot.size, 0)
    assert.deepEqual(focused, [])
  })
  it('未填时拦下保存：全部未填项高亮 + onMissing 回调收到键序', () => {
    const { guard } = fixture({ missing: ['name', 'host'] })
    let saved = 0
    let got = null
    const blocked = guard.trySave(() => saved++, { onMissing: (keys) => (got = keys) })
    assert.equal(saved, 0, '未填时不得执行保存')
    assert.equal(blocked, true)
    assert.deepEqual(got, ['name', 'host'])
    assert.equal(guard.hot.has('name'), true)
    assert.equal(guard.hot.has('host'), true)
  })
  it('聚焦第一个未填（跳过无 DOM 的键——子表单行内项等由横幅承载）', () => {
    stubEl('host')
    const { guard } = fixture({ missing: ['sub#1', 'host'] })
    guard.trySave(() => {})
    assert.deepEqual(focused, ['host'], '应跳过 sub#1 聚焦 host')
  })
  it('子表单行内项回退定位容器（round15：横幅已删，容器滚动 + 行内红标指位）', () => {
    // 精确键无 DOM、前缀容器存在但无输入框 → scrollIntoView 滚到可见
    const scrolled = []
    registry.set('Cred', { querySelector: () => null, scrollIntoView: () => scrolled.push('Cred') })
    const { guard } = fixture({ missing: ['Cred#0'] })
    guard.trySave(() => {})
    assert.deepEqual(focused, [], '无输入框不聚焦')
    assert.deepEqual(scrolled, ['Cred'], '应滚动定位到子表单容器')
  })
  it('填上后高亮自动解除（watch getMissing 同步）', async () => {
    const { guard, setMissing } = fixture({ missing: ['name', 'host'] })
    guard.trySave(() => {})
    assert.equal(guard.hot.size, 2)
    setMissing(['host']) // name 已填上
    await nextTick()
    assert.equal(guard.hot.has('name'), false, '已填项高亮应解除')
    assert.equal(guard.hot.has('host'), true)
  })
})

describe('源码接线（所有添加/编辑界面走同一守卫）', async () => {
  const { read } = await import('./helpers.mjs')
  const forms = [
    ['src/components/editor/EditorDrawer.vue', '抽屉（单机+批量）'],
    ['src/components/settings/DataSourceGroup.vue', '数据源添加/编辑'],
    ['src/components/settings/CredentialVaultGroup.vue', '凭据库'],
    ['src/components/settings/RunnerAddModal.vue', '添加运行器'],
  ]
  it('五个表单全部接入 useRequiredGuard（假禁用 + trySave + 提示）', () => {
    for (const [f, label] of forms) {
      const src = read(f)
      assert.ok(src.includes('useRequiredGuard'), `${label} 应接入共享守卫`)
      assert.ok(src.includes('req-blocked'), `${label} 保存钮应挂假禁用观感`)
      assert.ok(src.includes('trySave'), `${label} 保存点击应走守卫入口`)
      assert.ok(src.includes("t('modal.requiredHint')"), `${label} 应有按钮旁提示（i18n）`)
      assert.ok(src.includes('data-req-key'), `${label} 必填字段行应有聚焦定位属性`)
    }
  })
  it('编辑器树：FormField 消费 REQUIRED_HOT_KEY（warning 档 + 高亮类），错误档优先', () => {
    const ff = read('src/components/editor/FormField.vue')
    assert.ok(ff.includes('REQUIRED_HOT_KEY'), 'FormField 应注入高亮集合')
    assert.ok(ff.includes("invalid || warnHot ? 'error'"), '必填高亮与行内错误同红色档（round15）')
    assert.ok(ff.includes("'req-hot'"), '字段行应挂高亮类')
    // 抽屉 provide + Ctrl+S 与按钮同入口
    const drawer = read('src/components/editor/EditorDrawer.vue')
    assert.ok(drawer.includes('provide(REQUIRED_HOT_KEY'), '抽屉应 provide 高亮集合')
    assert.ok(drawer.includes('onSaveClick()'), 'Ctrl+S 应与保存钮同走守卫入口')
  })
  it('theme.css 全局样式三件套（假禁用/提示/高亮）', () => {
    const css = read('src/themes/theme.css')
    for (const sel of ['.req-blocked', '.req-hint', '.req-hot']) {
      assert.ok(css.includes(sel), `theme.css 缺 ${sel}`)
    }
  })
})
