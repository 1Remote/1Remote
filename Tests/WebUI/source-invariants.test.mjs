// ────────────────────────────────────────────────────────────────────────────
// 新人导览 · source-invariants.test.mjs · L3 源码不变量
// ▸ 本文件测什么：不模拟行为，只断言"定案代码形态仍在源码里"（守卫/顺序/选择器/CSS）。
//   按轮次分组（round6 K 系列、round11 U/A 系列、round12、round13……）。
//   红了 ≠ 一定是 bug：先按 README §8 判断是否设计有意变更。
// ▸ 单独运行：cd webui && node --test ../Tests/WebUI/source-invariants.test.mjs
// ▸ 全量运行与结果怎么看：Tests/WebUI/README.md §0；断言红了怎么办：README §8
// ────────────────────────────────────────────────────────────────────────────
// 组件接线防复发锚点用例：把历史轮次反复出现的「接线级」回归固化成源码断言。
// 这类 bug（守卫遗漏、事件顺序、判定先后）在纯逻辑层测不到，但也不需要起浏览器——
// 断言的目标模式都取自修复报告的定案形态；若未来有意改变实现，应同步更新本文件
//（每条断言注明来源轮次，先核对设计是否变更再改断言）。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { read, vueSections, stripJsComments, stripHtmlComments, SRC } from './helpers.mjs'

/** 剥注释后的「用户可见源码」（脚本 // /* *\/ 注释 + 模板 <!-- --> 注释）——
 *  断言「不存在某词」时必须剥注释，否则历史注释里的词会误报 */
function visibleText(rel) {
  const { template, script } = vueSections(read(rel))
  return (script ? stripJsComments(script) : '') + ' ' + (template ? stripHtmlComments(template) : '')
}

const st = read('src/components/ServerTable.vue')
const tree = read('src/components/SideTree.vue')
const row = read('src/components/ServerRow.vue')
const frow = read('src/components/FolderRow.vue')
const toolbar = read('src/components/TableToolbar.vue')
const list = read('src/views/ServerListView.vue')
const drawer = read('src/components/editor/EditorDrawer.vue')
const api = read('src/api/index.js')
const bulk = read('src/components/editor/BulkEditForm.vue')

/** 截取 startMarker 与 endMarker 之间的源码段（含 start 不含 end）；任一标记缺失即抛错 */
function between(src, startMarker, endMarker, label) {
  const a = src.indexOf(startMarker)
  assert.ok(a >= 0, `源码中找不到锚点: ${label || startMarker}`)
  const b = src.indexOf(endMarker, a + startMarker.length)
  assert.ok(b > a, `源码中找不到结束锚点: ${label || endMarker}`)
  return src.slice(a, b)
}

// ---------- 键盘流（spec §8.2；round4-#5、H6、E3-1、G2） ----------
describe('ServerTable 键盘守卫', () => {
  const fn = between(st, 'function onGlobalKey', 'function keyTargetServer', 'onGlobalKey')
  it('七类浮层存在时按键整体让位（naive 弹窗 / 三种自绘菜单 / 编辑抽屉 / 列菜单）', () => {
    for (const cls of ['.n-dialog', '.n-modal', '.ctx-menu', '.nf-menu', '.tree-ctx', '.ed-root', '.col-menu']) {
      assert.ok(fn.includes(cls), `onGlobalKey 守卫缺浮层选择器 ${cls}`)
    }
  })
  it('多选（>1）时 Enter / Del / Ctrl+D 禁用（owner round4-#5 决策）', () => {
    const n = fn.split('checked.value.size > 1').length - 1
    assert.ok(n >= 3, `onGlobalKey 中应有三处多选禁用判定，实得 ${n}`)
  })
  it('表格内控件聚焦时不抢按键（Enter/空格留给原生行为）', () => {
    assert.ok(fn.includes('input, textarea, select, button'))
  })
  it('Menu 键 / Shift+F10 呼出行右键菜单（键盘优先气质，spec §2）', () => {
    assert.ok(fn.includes('ContextMenu'))
  })
  it('按住不放的自动重复只对 ↑↓ 生效（Enter 重复连接、Ctrl+A 重复全选被忽略）', () => {
    assert.ok(fn.includes("e.repeat && e.key !== 'ArrowDown'"))
  })
})

// ---------- 树内拖拽落点顺序（round5-BUG1 根因：置空先于 canDrop 判定） ----------
describe('SideTree 树内拖拽', () => {
  const fn = between(tree, 'function onRowDrop', '// 目标父路径', 'onRowDrop')
  it('drop 处理器内 canDrop 判定先于 dragRow 置空（round5-BUG1 的根因顺序）', () => {
    const a = fn.indexOf('if (!canDrop(row, zone)) return')
    const b = fn.indexOf('dragRow.value = null')
    assert.ok(a >= 0 && b > a, 'canDrop 判定必须先于 dragRow.value = null')
  })
  it('树右键菜单纵向防溢出（J2，对齐 ServerTable 定位标准；round17 起三处共用 utils/contextMenu）', () => {
    // round17 收敛：定位内收块上收 clampMenuPos（菜单预算 150×130），断言调用形态
    assert.ok(tree.includes('clampMenuPos(e.clientX, e.clientY, rootEl.value, 150, 130)'))
    const cm = read('src/utils/contextMenu.js')
    assert.ok(cm.includes('Math.min(clientY - r.top, r.height - menuH)'), '共享实现应保留 y 上限内收')
  })
  it('数据源状态点 title 走 i18n（H31：不再直出英文裸枚举）——round8 起三处共用 utils/dsTitle 单一实现', () => {
    // round12 起 makeDsDotTitle 带 live 上下文（重连倒计时本地倒数），匹配放宽为前缀
    assert.ok(tree.includes('makeDsDotTitle(t,'), 'SideTree 应经 makeDsDotTitle 构造')
    const dst = read('src/utils/dsTitle.js')
    assert.ok(dst.includes('statusbar.dsConnected'))
    assert.ok(read('src/views/ServerListView.vue').includes('makeDsDotTitle(t,'))
    assert.ok(read('src/components/settings/DataSourceGroup.vue').includes('makeDsDotTitle(t,'))
  })
  it('无标签整区隐藏（H32 owner 决策）；真无数据源给引导', () => {
    assert.ok(tree.includes('v-if="tags.length"'))
    assert.ok(tree.includes('v-if="!datasources.length"'))
    assert.ok(tree.includes('tree.noDsHint'))
  })
})

// ---------- 行组件（round2 附加 + round5-BUG2） ----------
describe('行组件：复选框列与地址列', () => {
  it('复选框列从双击连接/双击进入识别区域排除（round2 附加；ServerRow 与 FolderRow 对齐）', () => {
    assert.ok(row.includes('cell cell-check" @dblclick.stop'), 'ServerRow 复选框列缺 @dblclick.stop')
    assert.ok(frow.includes('cell cell-check" @dblclick.stop'), 'FolderRow 复选框列缺 @dblclick.stop')
  })
  it('空文件夹复选框可用（round5-BUG2：勾选=文件夹本身，无 :disabled）', () => {
    const input = between(frow, '<input', '/>', 'FolderRow checkbox')
    assert.ok(input.includes(':checked="!!checkState?.checked"'))
    assert.ok(!input.includes(':disabled'))
    assert.ok(input.includes('selectEmptyFolder'))
  })
  it('地址/文件夹列长文本省略号三件套在内层文字元素上（J3：flex 容器上不生效）', () => {
    for (const cls of ['addr-text', 'folder-text']) {
      assert.ok(row.includes(`class="${cls}"`) || row.includes(`class="${cls} `), `模板缺 .${cls}`)
      const css = between(row, `.${cls} {`, '}', `.${cls} 样式`)
      assert.ok(css.includes('text-overflow: ellipsis'))
    }
  })
  it('地址列用户名段弱化（J5）', () => {
    const css = between(row, '.addr-user {', '}', '.addr-user 样式')
    assert.ok(css.includes('--text-3'))
  })
})

// ---------- 列表过滤与视图语义（round4-反馈/H38、BUG3、H9） ----------
describe('列表视图语义', () => {
  it('文件夹视图显示口径 = isDirectChildOf 单一来源（round4-反馈定案）', () => {
    assert.ok(st.includes('isDirectChildOf(s.folderPath, target)'))
  })
  it('搜索/标签激活时文件夹列强制显示（H9 + round11 U5：子树命中的来处标注）', () => {
    // round11 U5 起标签过滤同口径（subtreeScope），prettier 折行后按片段断言
    const decl = between(st, 'const showFolder', 'const showDs', 'showFolder')
    assert.ok(decl.includes('searchedIds.value != null'))
    assert.ok(decl.includes('props.subtreeScope'))
  })
  it('非根视图列菜单「文件夹」项禁用并说明（H9 另一半）', () => {
    assert.ok(toolbar.includes(':disabled="k === \'folder\' && folderColLocked"'))
    assert.ok(toolbar.includes('cols.folderRootOnly'))
  })
  it('搜索 chip 带全库范围标注（H9 第三面）', () => {
    assert.ok(list.includes('crumb.searchScope'))
  })
  it('拖拽归因顺序：跨库判定先于重排模式提示（H27）', () => {
    const fn = between(st, 'function onRowDragOver', 'async function onRowDrop', 'onRowDragOver')
    const crossFolder = fn.indexOf('dragFolder.value.dsName !== server.dataSourceName')
    const crossSrv = fn.indexOf('dragServer.value.dataSourceName !== server.dataSourceName')
    assert.ok(crossFolder >= 0 && crossFolder < fn.indexOf('folderReorderHintShown'), '文件夹分支跨库判定须先于提示')
    assert.ok(crossSrv >= 0 && crossSrv < fn.indexOf('!isCustom.value'), '服务器分支跨库判定须先于模式提示')
  })
  it('跨库 dragend 兜底监听挂在 ServerTable（H18：随 SideTree 卸载会失效）', () => {
    assert.ok(st.includes("window.addEventListener('dragend', onListDragEndGlobal)"))
    assert.ok(!tree.includes("addEventListener('dragend'"), 'SideTree 不应再挂 window dragend 兜底')
  })
})

// ---------- Esc 链与浮层互斥（第三轮 G 系列、H21、round3 链序） ----------
describe('全局 Esc 链（ServerListView onGlobalEsc）', () => {
  const fn = between(list, 'function onGlobalEsc', 'onMounted(() => window.addEventListener', 'onGlobalEsc')
  it('链序：行菜单 → 文件夹菜单 → 树菜单 → 列菜单 → 勾选 → 搜索 → 标签 → 光标', () => {
    const order = [
      'tb?.closeMenuIfOpen()',
      'tb?.closeNfMenuIfOpen()',
      'sideTree.value?.closeCtxIfOpen()',
      'tb?.closeColMenuIfOpen()',
      'tb?.clearCheckedIfAny()',
      'if (searchQuery.value)',
      'if (activeTag.value)',
      'tb?.clearCursorIfAny()',
    ]
    let pos = -1
    for (const marker of order) {
      const i = fn.indexOf(marker, pos + 1)
      assert.ok(i > pos, `Esc 链序断裂：${marker} 应在其前项之后`)
      pos = i
    }
  })
  it('模态在开时整链让位（编辑抽屉 / 标签管理 / 导入 / naive 对话框）', () => {
    const head = fn.slice(0, fn.indexOf('tb?.closeMenuIfOpen()'))
    for (const marker of [
      'if (editor.value) return',
      'if (tagManager.value) return',
      'if (importModal.value) return',
      "if (document.querySelector('.n-dialog')) return",
    ]) {
      assert.ok(head.includes(marker), `Esc 链头部缺让位守卫: ${marker}`)
    }
  })
  it('树右键菜单可被 Esc 消费（closeCtxIfOpen 经 defineExpose 暴露）', () => {
    assert.ok(tree.includes('defineExpose({ closeCtxIfOpen })'))
  })
})

// ---------- 编辑抽屉（H21、H26、J1、J17） ----------
describe('编辑抽屉 EditorDrawer', () => {
  it('未保存确认开着时 Ctrl+S 让位（H21：确认框悬空漂浮）', () => {
    assert.ok(drawer.includes("if (document.querySelector('.n-dialog, .n-modal')) return"))
  })
  it('复制预填名后缀剥净再补一个（H26：连续复制不再无限叠加）', () => {
    assert.ok(drawer.includes('while (base.endsWith(suffix))'))
  })
  it('抽屉宽度窄窗自适应（J1：下限取 min(560px, 100%)）', () => {
    assert.ok(drawer.includes('clamp(min(560px, 100%), 68vw, 900px)'))
  })
  it('抽屉三区域横向内距一致（J17：.ed-fields 与头/底同 16px；round14 M6 起单点在 theme.css）', () => {
    const themeCss = read('src/themes/theme.css')
    const css = between(themeCss, '.ed-fields {', '}', '.ed-fields 样式')
    assert.ok(css.includes('padding: 8px 16px 20px'), '.ed-fields 横向内距应为 16px')
    // 两侧不再各持拷贝（M6 收敛后 scoped 里不应再定义）
    for (const src of [drawer, bulk]) {
      assert.ok(!src.includes('.ed-fields {'), 'ed-fields 应上收 theme.css（M6），组件内不应再有拷贝')
    }
  })
})

// ---------- 批量条与批量删除（round5-BUG2） ----------
describe('批量操作条', () => {
  it('服务器与空文件夹混合勾选时批量条出现且计数齐全', () => {
    assert.ok(toolbar.includes('v-if="checkedCount || folderCount"'))
    assert.ok(toolbar.includes('batch.selectedFolders'))
    assert.ok(st.includes(':folder-count="checkedFolders.size"'))
  })
  it('连接按钮图形统一为 ▸（J8：批量条与行内一致）', () => {
    assert.ok(toolbar.includes("▸ {{ t('batch.connect') }}"))
    assert.ok(!toolbar.includes('▶'), '批量条不应再有大三角 ▶')
  })
  it('清除 ✕ 为圆形（J9：对齐全站移除小钮语言）', () => {
    const css = between(toolbar, '.bb-x {', '}', '.bb-x 样式')
    assert.ok(css.includes('border-radius: 50%'))
  })
  it('批量删除混合确认（仅服务器 / 仅空文件夹 / 两者 三档文案）', () => {
    const fn = between(list, 'function onBatchDelete', 'async function runBatchDelete', 'onBatchDelete')
    for (const key of ['batchDelete.confirmMixed', 'batchDelete.confirmText', 'batchDelete.confirmFolders']) {
      assert.ok(fn.includes(key))
    }
  })
})

// ---------- API 契约锚点（H4、H15 前端半边） ----------
describe('API 契约', () => {
  it('复制密码端点存在（H4，受 TokenMiddleware + 30s 验证门保护）', () => {
    assert.ok(api.includes('copy-password'))
  })
  it('导入请求携带 folder 目标参数（H15 的前端半边；后端做前缀拼接）', () => {
    assert.ok(api.includes('&folder='))
  })
})

// ---------- 后端锚点（round5-BUG1 附带加固、H15 后端半边） ----------
describe('后端 C# 锚点', () => {
  const server = read('../Ui/Service/WebUi/WebUiServer.cs')
  const imp = read('../Ui/Service/WebUi/WebUiImportExportService.cs')
  it('index.html 响应 no-cache（round5-BUG1 附带加固：升级后不再读旧缓存）', () => {
    assert.ok(server.includes('CacheControl = "no-cache"'))
  })
  it('导入 = 目标文件夹作前缀拼接（H15：按 JSON 原有目录结构放进当前文件夹）', () => {
    assert.ok(imp.includes('targetNodes.Concat(server.TreeNodes'))
  })
})

// ---------- 术语与死词条（H30、H13） ----------
describe('术语统一与清理', () => {
  it('跨库批量编辑禁存链已移除（H13；剥注释后源码不再出现 dsMixed）', () => {
    const v = visibleText('src/components/editor/BulkEditForm.vue')
    assert.ok(!v.includes('dsMixed'))
    assert.ok(!v.includes('bulkMixedDs'))
  })
})

// ---------- round6（2026-09-21 K/L 系列定案） ----------
describe('round6 K1/K2：重排整库基准与计数口径', () => {
  it('K1: 自定义顺序 POST 基准 = 共享单例全量 servers（allServers），非 props.servers', () => {
    const seg = between(st, 'const fullOrder', '// ---- 行拖拽', 'fullOrder')
    assert.ok(
      seg.includes('allServers.value.slice()'),
      'fullOrder 应以 allServers 为基准（过滤下重排曾清空其余服务器顺序）'
    )
    assert.ok(!seg.includes('props.servers'), 'fullOrder 不应消费 props.servers（已被父级标签/搜索过滤收窄）')
  })
  it('K2: 面包屑计数无过滤时走递归口径（folderTotal → countHolderServers），过滤时用命中行数', () => {
    const seg = between(list, 'const folderTotal', 'const noMatchDetail', 'folderTotal')
    assert.ok(seg.includes('countHolderServers(holder)'), 'folderTotal 应与树徽标同函数同数字')
    assert.ok(seg.includes('searchActive.value || activeTag.value'), '过滤激活时计数应为命中结果集行数')
  })
})

describe('round6 键盘/焦点（K5/K6/K8）', () => {
  it('K5: 设置页 Esc 在 naive 对话框/模态开着时让位（不再连设置页一起退出）', () => {
    const fn = between(read('src/views/SettingsView.vue'), 'function onKey', '// 「← 返回」', 'SettingsView.onKey')
    assert.ok(fn.includes('.n-dialog, .n-modal'))
  })
  it('K6: 服务器行操作列双击不冒泡（双击 ▸ 不再 2×click+1×dblclick 合计触发 3 次连接）', () => {
    assert.ok(row.includes('@click.stop @dblclick.stop'), 'ServerRow cell-act 应补 dblclick.stop')
  })
  it('K8: 搜索框 ↓/↑ 移交前判输入法组字（isComposing 选词键不移交焦点）', () => {
    const app = read('src/App.vue')
    assert.ok(app.includes('!$event.isComposing && handoffTableFocus(1)'))
    assert.ok(app.includes('!$event.isComposing && handoffTableFocus(-1)'))
  })
})

describe('round6 反馈与提示（K10/K11/K13/K14/K15/K16/K26）', () => {
  it('K10: 文件夹行内按钮 busy 禁用 + 拖放落区 busy 前置拒绝', () => {
    assert.ok(frow.includes('busy: { type: Boolean, default: false }'), 'FolderRow 应声明 busy prop')
    assert.ok(frow.includes("t('tree.folderBusyHint')"), 'busy 禁用态应有 title 解释')
    const drop = between(st, 'function folderDropOk', 'function onFolderDragOver', 'folderDropOk')
    assert.ok(drop.indexOf('props.opsBusy') < drop.indexOf('dragServer.value'), 'opsBusy 拒绝应在落区判定最前')
  })
  it('K11: 树内同级重排落盘失败回滚 + 报错（不再假成功）', () => {
    const seg = between(tree, 'const prev = orderMap.value', 'await reload()', 'reorder persist')
    assert.ok(seg.includes('orderMap.value = prev'))
    assert.ok(seg.includes("t('toast.reorderFailed')"))
  })
  it('K13: 搜索失败不再伪装零命中（失败保留旧过滤态 + tick 信号弹提示）', () => {
    const us = read('src/composables/useServers.js')
    assert.ok(us.includes('searchFailedTick.value++'))
    assert.ok(
      !visibleText('src/composables/useServers.js').includes('searchedIds.value = new Set() // 失败按零命中'),
      '失败清空集的旧逻辑应已移除'
    )
    assert.ok(list.includes('watch(searchFailedTick'), 'ServerListView 应 watch 失败信号弹 toast')
  })
  it('K14: 导入进行中 Esc/遮罩/卡片 × 三路不可关', () => {
    const imp2 = read('src/components/ImportModal.vue')
    assert.ok(imp2.includes(':mask-closable="!importing"'))
    assert.ok(imp2.includes(':close-on-esc="!importing"'))
    assert.ok(imp2.includes(':closable="!importing"'))
  })
  it('K15: 外观保存失败有提示信号（connected 豁免纯预览）', () => {
    const th = read('src/themes/index.js')
    assert.ok(th.includes('appearanceSaveFailedTick'))
    assert.ok(read('src/components/settings/AppearanceGroup.vue').includes("message.error(t('settings.saveFailed'))"))
  })
  it('K16: 数据源卡片状态点 title 走词条（不直出英文枚举）', () => {
    const dsg = read('src/components/settings/DataSourceGroup.vue')
    assert.ok(dsg.includes(':title="dsDotTitle(d)"'))
    assert.ok(!visibleText('src/components/settings/DataSourceGroup.vue').includes(':title="d.status"'))
  })
  it('K26: 导出前明文警告确认框', () => {
    const seg = between(list, 'function confirmExport', 'async function onExport', 'confirmExport')
    assert.ok(seg.includes("t('batch.exportConfirm', { n })"))
    assert.ok(list.includes('if (!(await confirmExport(ids.length))) return'))
  })
})

describe('round6 K17：后端失联全屏警告取代状态栏 SSE 指示', () => {
  it('冷启动豁免（everConnected 后才可能弹）+ 3 秒滞回', () => {
    const app = read('src/App.vue')
    assert.ok(app.includes('everConnected.value'))
    assert.ok(app.includes('setTimeout(() => (backendLost.value = true), 3000)'))
  })
  it('全屏不可关闭 overlay 存在；状态栏 SSE 小字已删', () => {
    assert.ok(read('src/App.vue').includes('class="offline-overlay"'))
    const v = visibleText('src/views/ServerListView.vue')
    assert.ok(!v.includes('statusbar.sseOk') && !v.includes('statusbar.sseOff'), '状态栏不应再渲染 SSE 词条')
  })
})

describe('round6 K23/K24：标签过滤隐藏文件夹行 + 国际化集中', () => {
  it('K23: 标签过滤沿用搜索做法（currentFolders 对 activeTag 也返回空）', () => {
    assert.ok(list.includes('searchedIds.value != null || activeTag.value'))
  })
  it('K24: 组件库语言映射集中 locales/naive.js（14 语言全配 + enUS 回落）', () => {
    const nv = read('src/locales/naive.js')
    for (const code of [
      'zh-CN',
      'zh-TW',
      'en-US',
      'ja-JP',
      'ru-RU',
      'de-DE',
      'fr-FR',
      'es-AR',
      'it-IT',
      'pl-PL',
      'pt-BR',
      'pt-PT',
      'cs-CZ',
      'gl-ES',
    ]) {
      assert.ok(nv.includes(`'${code}'`), `naive.js 缺 ${code} 映射`)
    }
    assert.ok(nv.includes('|| enUS'), '无映射语言应回落 enUS')
    assert.ok(read('src/App.vue').includes('naiveLocaleOf(locale.value)'), 'App.vue 应经 naiveLocaleOf 取包')
  })
})

describe('round6 K27：Web 自启开关（前后端）', () => {
  it('前端：常规组表单含 appStartAutomatically 并走差量自动保存', () => {
    assert.ok(read('src/components/settings/GeneralGroup.vue').includes('appStartAutomatically'))
  })
  it('后端：DTO 双向字段 + 写走 WPF 同入口（SetSelfStart），读为注册表实况', () => {
    const dto = read('../Ui/Service/WebUi/WebUiDto.cs')
    assert.ok(dto.includes('public bool AppStartAutomatically { get; set; }'))
    assert.ok(dto.includes('public bool? AppStartAutomatically { get; set; }'))
    const svc = read('../Ui/Service/WebUi/WebUiSettingsService.General.cs')
    assert.ok(svc.includes('ConfigurationService.SetSelfStart(input.AppStartAutomatically.Value)'))
    assert.ok(svc.includes('SetSelfStartingHelper.IsSelfStart(Assert.APP_NAME)'))
  })
})

describe('round6 L2/L3/L4：美学三项', () => {
  it('L2: 四处移除钮 hover 中性（不再红字；round14 M5 起 kvl/kvm/kvp/sf 单点在 theme.css）', () => {
    // theme.css 全局并集块：四个删除钮 hover 同一中性语言
    const themeCss = read('src/themes/theme.css')
    const a = themeCss.indexOf('.kvl-del:hover:not(:disabled)')
    assert.ok(a >= 0, 'theme.css 应有删除钮 hover 全局并集')
    const block = themeCss.slice(a, themeCss.indexOf('}', a))
    assert.ok(!block.includes('var(--danger)'), '删除钮 hover 仍是红字')
    assert.ok(block.includes('var(--text-1)'), '删除钮 hover 应为中性')
    // IconPicker 的 ip-clear 仍是组件本地（非键值族）
    const src2 = read('src/components/editor/IconPicker.vue')
    const b = src2.indexOf('.ip-clear:hover')
    assert.ok(b >= 0, 'IconPicker 缺 .ip-clear:hover')
    const block2 = src2.slice(b, src2.indexOf('}', b))
    assert.ok(!block2.includes('var(--danger)'), 'IconPicker 的 ip-clear:hover 仍是红字 hover')
    assert.ok(block2.includes('var(--text-1)'), 'IconPicker 的 ip-clear:hover 应为中性 hover')
  })
  it('L3: 脚本测试弹窗走 confirmDialog 辅助（无图标 + 禁回车误触收敛于单一实现，round11 A2）', () => {
    const stt = read('src/editor/scriptTest.js')
    assert.ok(!stt.includes('dialog.info('))
    assert.ok(stt.includes('confirmDialog(dialog, {'))
  })
  it('L4: 主按钮统一描边——组件里不再有「无 ghost 的 type=primary」；ghost 文字走 accent-text 镜像', () => {
    for (const f of [
      'src/components/ImportModal.vue',
      'src/components/settings/CredentialVaultGroup.vue',
      'src/components/settings/DataSourceGroup.vue',
      'src/components/settings/RunnerAddModal.vue',
      'src/components/settings/RunnerGroup.vue',
    ]) {
      // 归一化换行：prettier 会把多属性按钮拆行（type 与 ghost 不同行），单行正则会误报
      const src2 = read(f).replace(/\s+/g, ' ')
      const solid = (src2.match(/type="primary"(?! ghost)/g) || []).length
      assert.ok(solid === 0, `${f} 仍有 ${solid} 处实底主按钮`)
    }
    const th = read('src/themes/index.js')
    assert.ok(th.includes('textColorGhostPrimary: accentTextHex()'), 'ghost 主按钮文字应走 accent-text 镜像')
    assert.ok(th.includes("border: '1px solid var(--border)'"), '默认钮边框应统一 --border 令牌')
  })
})

// ---------- round9（2026-09-22 N 系列定案：round4 报告 N1-N4） ----------
describe('round9 N1-N4：主按钮悬停统一 / 星标统一 / 三档补漏', () => {
  it('N1: 脚本测试确认钮进三档映射（中性 default，非破坏性确认——round11 A2 起由 confirmDialog 统一承载）', () => {
    const stt = read('src/editor/scriptTest.js')
    assert.ok(stt.includes('confirmDialog(dialog, {'), '脚本测试确认应走 confirmDialog（缺省 danger=default）')
    const cd = read('src/utils/confirmDialog.js')
    assert.ok(cd.includes("type: danger ? 'error' : 'default'"), '辅助内三档映射')
  })
  it('N2: ghost 主按钮悬停边框钉住强调色 + 悬停补浅底（全局规则）', () => {
    const th = read('src/themes/index.js')
    assert.ok(th.includes("borderHoverPrimary: '1px solid var(--accent)'"), 'ghost 悬停边框应钉住 --accent')
    const css = read('src/themes/theme.css')
    assert.ok(
      css.includes('.n-button.n-button--primary-type.n-button--ghost:not(.n-button--disabled):hover'),
      'theme.css 应有 ghost 悬停浅底规则'
    )
  })
  it('N3: naive 默认钮悬停文字归主文字色、底浮浅底（无后缀键只喂 default 型）', () => {
    const th = read('src/themes/index.js')
    assert.ok(th.includes("textColorHover: 'var(--text-1)'"))
    assert.ok(th.includes("colorHover: 'var(--bg-hover)'"))
  })
  it('N4: 设置页必填星标统一为红色星元素（全局 .req-star，与编辑器同语言）', () => {
    const css = read('src/themes/theme.css')
    assert.ok(css.includes('.req-star'), 'theme.css 应有 .req-star 工具类')
    for (const f of [
      'src/components/settings/DataSourceGroup.vue',
      'src/components/settings/CredentialVaultGroup.vue',
    ]) {
      const v = read(f)
      assert.ok(!/\}\s*\*<\/label>/.test(v), `${f} 仍有裸文本星`)
      assert.ok(v.includes('req-star'), `${f} 应使用红色星元素`)
    }
  })
})

// ---------- round12（2026-09-22 用户报告：数据源模态保存瞬间的两处缺陷） ----------
describe('round12：数据源添加模态保存瞬间行为', () => {
  it('保存成功的淡出期间不闪「名称已存在」：查重带模态开态守卫', () => {
    const dsg = read('src/components/settings/DataSourceGroup.vue')
    // 开态守卫（与凭据库 round11 同款）：模态关闭后 addNameExists 恒 false，
    // 列表重载撞上表单残留名不再闪红字
    assert.ok(
      dsg.includes('computed(() => (adding.value ? nameExistsAmong(addForm.name, null) : false))'),
      'addNameExists 应以 adding.value 为门（模态关闭不参与查重）'
    )
  })
  it('保存成功即重置表单（关闭动画显示空白表单，不残留已存内容）', () => {
    const dsg = read('src/components/settings/DataSourceGroup.vue')
    const seg = between(dsg, 'async function addSave', '// ---- 新建/编辑模态', 'addSave')
    assert.ok(
      seg.includes('Object.assign(addForm, blankAddForm())'),
      'addSave 成功路径应重置表单（淡出期间无中间形态）'
    )
  })
  it('凭据库/Runner 同类入口守卫仍在位（round11 修复不回退）', () => {
    const cvg = read('src/components/settings/CredentialVaultGroup.vue')
    assert.ok(cvg.includes('if (!editing.value) return false'), '凭据库查重应保留模态开态守卫')
    const ram = read('src/components/settings/RunnerAddModal.vue')
    assert.ok(ram.includes('name.value = ' + String.fromCharCode(39, 39)), 'Runner 添加模态关闭应即清空名称草稿')
  })
})

// ---------- round11 U 系列（2026-09-22 owner 指定修复项） ----------
describe('round11 U 系列：易用性修复回归', () => {
  const dsg = read('src/components/settings/DataSourceGroup.vue')
  it('U2: 批量连接有进度提示与模块级单飞锁（含确认阶段）', () => {
    const bc = read('src/composables/useBatchConnect.js')
    assert.ok(bc.includes('progressToast'), '应使用进度 toast')
    assert.ok(bc.includes('let batchRunning = false'), '应有模块级在途锁（两入口互斥）')
    assert.ok(bc.includes('toast.batchConnecting'), '进度文案词条')
  })
  it('U3: 虚拟滚动模式键盘光标按行高滚动跟随（不再依赖行 DOM）', () => {
    const seg = between(st, 'function moveCursor', 'function onGlobalKey', 'moveCursor')
    assert.ok(seg.includes('useVirtual.value'), 'moveCursor 应区分虚拟滚动模式')
    assert.ok(st.includes('function virtualScrollToServer'), '应有按行高换算的滚动实现')
  })
  it('U4: 空白菜单"新建服务器"与"新建文件夹"同一禁用判据', () => {
    const m = between(st, 'v-if="nfMenu"', '</div>', 'nfMenu')
    assert.ok(
      m.includes('v-if="!nfMenu.target?.folderPath"') && m.includes(':disabled="!nfMenuOk"'),
      '新建服务器应与新建文件夹共用 nfMenuOk 判据'
    )
  })
  it('U7/U25: 子表单行内必填与键值半行拦截保存（保存问题总线）', () => {
    const si = read('src/editor/saveIssues.js')
    assert.ok(si.includes('SAVE_ISSUES_KEY'), '保存问题总线模块存在')
    const seg = between(drawer, 'function validateRequired', 'function displayName', 'validateRequired')
    assert.ok(seg.includes('FIELD.SUBFORM'), '校验应覆盖子表单行内必填')
    assert.ok(seg.includes('saveIssues.issues'), '校验应消费键值半行自报')
    assert.ok(drawer.includes('saveIssues.validate.value = true'), '保存尝试应开门控（行内标红）')
    assert.ok(read('src/components/editor/SubformList.vue').includes('validateNow && rowFieldInvalid'), '行内标红接线')
    assert.ok(read('src/components/editor/KeyValueLines.vue').includes('rowIsHalf'), 'KeyValueLines 半填行检测')
    assert.ok(read('src/components/editor/KvMapField.vue').includes('rowIsHalf'), 'KvMapField 半填行检测')
  })
  it('U10/U11: 批量删除防叠框 + 空文件夹批量删除有进度', () => {
    assert.ok(list.includes('batchDeleteDialogOpen'), '确认框期间应有防叠框锁')
    // prettier 折行敏感：按片段断言（round13 起该块缩进随 confirmDialog 迁移变浅）
    assert.ok(/onAfterLeave: \(\) => \{\s*\n\s*batchDeleteDialogOpen = false/.test(list), '锁应在离场后复位')
    const fo = read('src/composables/folderOps.js')
    const seg = between(fo, 'async function deleteEmptyFolders', 'return {', 'deleteEmptyFolders')
    assert.ok(seg.includes('progressToast'), '空文件夹批量删除应有进度 toast')
    assert.ok(fo.includes('busyGuard()'), '忙碌守卫应给轻提示（不再静默吞点击）')
    // R3（round14）：新建文件夹是忙碌锁体系最后一个未接入入口，补齐后全入口互斥
    const cf = between(fo, 'async function createFolder', 'affectedServers', 'createFolder')
    assert.ok(cf.includes('busyGuard()'), '新建文件夹应接忙碌守卫（与长操作字典落盘互斥）')
  })
  it('U15→round13: 数据源弹窗关闭语义（mask-closable 禁直通 + 草稿守卫接线）', () => {
    assert.ok(dsg.includes(':mask-closable="false"'), '遮罩不应直通 naive 自动关闭（走守卫二分）')
    assert.ok(dsg.includes('addGuard') && dsg.includes('editGuard'), '关闭应走草稿守卫')
    assert.ok(dsg.includes('@mask-click='), '遮罩点击应接守卫（干净关/脏提示）')
  })
  it('U16: 数据源测试进行中同卡片编辑/删除禁用（卡片级锁）', () => {
    const m = between(dsg, 'class="card-actions"', '</div>', 'card-actions')
    assert.ok(
      (m.match(/:disabled="isLocal\(d\) \|\| testing === d\.name"/g) || []).length >= 2,
      '编辑/删除应在测试期间禁用'
    )
  })
  it('U17: 数据源端口校验 1-65535（标红 + 禁用测试/保存）', () => {
    assert.ok(dsg.includes('const portValid = (p)'), '端口校验函数')
    assert.ok(dsg.includes('addPortOk') && dsg.includes('editPortOk'), '两模态端口状态')
    assert.ok(dsg.includes("t('settings.d.portInvalid')"), '非法端口提示词条')
  })
  it('U19: 热键录制失焦/点击外部退出', () => {
    const lg = read('src/components/settings/LauncherGroup.vue')
    assert.ok(lg.includes('onRecordWindowBlur'), '窗口失焦应退出录制')
    assert.ok(lg.includes('onGlobalDownExitRecord'), '点击录制框外部应退出录制')
  })
  it('U21: 候选输入统一控件 SuggestInput（过滤 + Esc 取消）三处消费', () => {
    const sug = read('src/components/editor/SuggestInput.vue')
    assert.ok(sug.includes('toLowerCase().includes'), '候选应随输入过滤')
    assert.ok(sug.includes("e.key === 'Escape'"), 'Esc 应取消候选显示')
    assert.ok(sug.includes('stopPropagation'), '取消候选的 Esc 不应冒泡到外层 Esc 链')
    for (const f of [
      'src/components/editor/FormField.vue',
      'src/components/editor/KeyValueLines.vue',
      'src/components/editor/KvPairsField.vue',
    ]) {
      assert.ok(read(f).includes('SuggestInput'), `${f} 应使用统一候选控件`)
    }
    assert.ok(
      !read('src/components/editor/FormField.vue').includes('get-show="() => true"'),
      'FormField 不应再持有恒全量配置'
    )
  })
})

// ---------- round11 A 系列（美学收拢：复用与参数统一） ----------
describe('round11 A 系列：美学收拢回归', () => {
  it('A1: 行内小钮 .act 全局类单一定义，四文件不再各持拷贝', () => {
    const css = read('src/themes/theme.css')
    assert.ok(css.includes('.act {'), 'theme.css 应有全局 .act')
    assert.ok(css.includes('.s-form-row {'), 'theme.css 应有全局 .s-form-row（A7）')
    for (const f of [
      'src/components/ServerRow.vue',
      'src/components/FolderRow.vue',
      'src/components/settings/CredentialVaultGroup.vue',
      'src/components/settings/TagManagerModal.vue',
    ]) {
      assert.ok(!read(f).includes('.act {'), `${f} 不应再有本地 .act 拷贝`)
    }
  })
  it('A3+A7: 表单行网格统一 s-form-row（标签列 100px 单值），四文件无 .f-row 网格拷贝', () => {
    for (const f of [
      'src/components/settings/CredentialVaultGroup.vue',
      'src/components/settings/DataSourceGroup.vue',
      'src/components/settings/RunnerAddModal.vue',
      'src/components/settings/RunnerCard.vue',
    ]) {
      const v = read(f)
      assert.ok(!v.includes('.f-row {'), `${f} 不应再有 .f-row 网格拷贝`)
      assert.ok(!v.includes('110px minmax'), `${f} 标签列不得再有 110px 网格漂移值`)
    }
    assert.ok(
      read('src/components/ImportModal.vue').includes('.f-row {'),
      'ImportModal 的 flex .f-row（不同物）不受影响'
    )
  })
  it('A2: 确认框三档收敛 confirmDialog（round13 起丢弃确认并入 useDraftGuard 共享守卫）', () => {
    const cd = read('src/utils/confirmDialog.js')
    assert.ok(cd.includes('autoFocus: false'), '辅助内统一禁回车误触')
    let hits = 0
    for (const f of [
      'src/components/editor/EditorDrawer.vue',
      'src/components/settings/CredentialVaultGroup.vue',
      'src/components/settings/DataSourceGroup.vue',
      'src/components/settings/RunnerGroup.vue',
      'src/components/settings/TagManagerModal.vue',
      'src/composables/folderOps.js',
      'src/composables/useBatchConnect.js',
      'src/composables/useDraftGuard.js',
      'src/editor/scriptTest.js',
      'src/views/ServerListView.vue',
    ]) {
      const v = read(f)
      // round17：布尔确认（Promise+四路 resolve）收敛为 confirmBool（内部仍走 confirmDialog
      // 三档），两口径合并计数
      hits += (v.match(/confirmDialog\(dialog/g) || []).length
      hits += (v.match(/confirmBool\(dialog/g) || []).length
      assert.ok(!v.includes('showIcon: false'), `${f} 不应再手写 showIcon:false`)
    }
    assert.ok(hits >= 14, `confirmDialog/confirmBool 调用应 ≥14 处（round13 起草稿守卫合并三处丢弃确认），实得 ${hits}`)
  })
  it('A4: 设置域单列小表单弹窗统一 480px 档（RunnerAddModal 520 已归档）', () => {
    const ram = read('src/components/settings/RunnerAddModal.vue')
    assert.ok(ram.includes('min(480px, 92vw)'), 'RunnerAddModal 应为 480px')
    assert.ok(!ram.includes('520px'), '不得再有 520 漂移档')
  })
})

// ---------- round13（2026-09-23 用户报告：丢弃确认框残留） ----------
describe('round13：确认框回调返回值契约', () => {
  it('BUG3: 丢弃确认的回调为语句体——值式箭头返回 false 会被 naive 判为"阻止关闭"（round13 起收进 useDraftGuard 共享守卫）', () => {
    const dsg = read('src/components/settings/DataSourceGroup.vue')
    const guard = read('src/composables/useDraftGuard.js')
    // 守卫内：onPositive 语句体吞返回值（行为级测试在 draft-guard.test.mjs）
    assert.ok(/onPositive: \(\) => \{\s*\n\s*doClose\(\)/.test(guard), '守卫的 onPositive 应为语句体')
    assert.ok(!guard.includes('onPositive: () => doClose()'), '不得把 doClose 返回值直传确认按钮回调')
    // 调用点不再自持值式箭头
    assert.ok(!dsg.includes('() => (adding.value = false)'), '不得使用值式箭头（返回 false）')
    assert.ok(!dsg.includes('() => (editing.value = null)'), '不得使用值式箭头')
  })
  it('全仓审计：onPositive/onNegative/onClose 无"赋值表达式"值式箭头（唯一豁免：folderOps 命名输入 submit()||false 的刻意保持）', async () => {
    const { walkFiles } = await import('./helpers.mjs')
    const hits = []
    for (const f of walkFiles('src', ['.vue', '.js'])) {
      const v = read(f)
      for (const m of v.matchAll(/on(Positive|Negative|Close): \(\) => \(([^)]*)\)/g)) {
        if (/\|\| false/.test(m[2])) continue // folderOps:126 刻意保持打开的用法
        hits.push(`${f}: ${m[0]}`)
      }
    }
    assert.deepEqual(hits, [], '值式箭头回调可能返回 false 阻止关闭：' + hits.join(' | '))
  })
})

// ---------- round13 N/M 系列（2026-09-23 owner 指定修复项） ----------
describe('round13 N 系列：易用性修复回归', () => {
  it('N1: folderOps busy 模块级共享（列表与树互斥）+ 树内重排接入同一把锁', () => {
    const fo = read('src/composables/folderOps.js')
    assert.ok(fo.includes('// 模块级共享忙碌锁'), 'busy 应提升为模块级')
    assert.ok(
      fo.indexOf('const busy = ref(false)') < fo.indexOf('export function useFolderOps'),
      '定义须在 composable 外'
    )
    const tree = read('src/components/SideTree.vue')
    assert.ok(tree.includes('folderOps.busy.value) return false'), 'isDraggable 应查共享 busy')
    assert.ok(/applyTreeMove[\s\S]{0,200}folderOps\.busy\.value\) return/.test(tree), '重排执行入口应有兜底守卫')
  })
  it('N3+N4: 批量编辑消费保存问题总线 + 半行纳入脏判定', () => {
    const bulk = read('src/components/editor/BulkEditForm.vue')
    assert.ok(bulk.includes('useSaveIssues'), 'BulkEditForm 应 inject 问题总线')
    // round15：必填横幅删除后 kvIncompleteRow 文案无渲染出口，断言改为半行并入的代码形态
    //（validateBulkRequired 内 Object.keys(saveIssues) 消费总线；勿回退成 saveIssues.issues——
    // round15 修复的覆盖按钮失效 BUG 即源于此错读）
    assert.ok(/Object\.keys\(saveIssues\)\) missing\.push\(issueKey\)/.test(bulk), '半行应并入必填校验列表')
    assert.ok(!bulk.includes('saveIssues.issues'), 'saveIssues 已是 issues 对象本身，不得再套 .issues')
    const seg = between(drawer, 'const dirty = computed', '// ---- 协议切换', 'dirty')
    assert.ok(seg.includes('saveIssues.issues'), '半行应让抽屉判脏（关闭走确认/提示）')
  })
  it('N6→round14 R5: 语言唯一持久化在后端（启动拉后端语言，不读写 localStorage）', () => {
    const app = read('src/App.vue')
    assert.ok(app.includes('backendToWeb(g.language)'), '启动应读后端语言')
    assert.ok(!app.includes("localStorage.getItem('1r-lang')"), '不应再读本地语言记忆（R5 定案）')
    const loc = read('src/locales/index.js')
    assert.ok(!loc.includes("setItem('1r-lang'"), 'setLocale 不得写 localStorage')
    assert.ok(!loc.includes("getItem('1r-lang'"), '探测不得读 localStorage')
    // 显式切换语言的两个入口都 PUT 后端：设置页下拉 + 状态栏切换钮
    assert.ok(
      read('src/components/settings/GeneralGroup.vue').includes('saveNow({ language: webToBackend'),
      '设置页切语言应写后端'
    )
    assert.ok(list.includes('saveGeneralSettings({ language: webToBackend'), '状态栏切语言应写后端')
  })
  it('N7: 导出 ids 走 POST body（GET 查询串 8KB 上限不再踩）', () => {
    const api2 = read('src/api/index.js')
    assert.ok(
      api2.includes("requestRaw('/api/servers/export', { method: 'POST', body: { ids }, blob: true })"),
      '前端应 POST body'
    )
    assert.ok(!api2.includes('export?ids='), '不得再拼 GET 查询串')
  })
  it('N8: 批量删除执行期锁（确认后进行中再点不叠第二轮）', () => {
    assert.ok(list.includes('const batchDeleting = ref(false)'), '应有执行期锁（round14 R4 响应式化）')
    assert.ok(/onPositive[\s\S]{0,200}batchDeleting\.value = true/.test(list), '执行期应置锁（onPositive 全程）')
    // R4：执行期锁接批量条按钮禁用（ServerListView → ServerTable → TableToolbar 三级链）
    assert.ok(list.includes(':batch-busy="batchDeleting"'), '列表应向表格传执行期锁')
    assert.ok(read('src/components/ServerTable.vue').includes(':batch-busy="batchBusy"'), '表格应转发执行期锁')
    const tb = read('src/components/TableToolbar.vue')
    assert.ok(
      (tb.match(/:disabled="batchBusy/g) || []).length >= 5,
      '批量条五钮（连接/编辑/导出/删除/清除）执行期应全禁'
    )
  })
  it('N12: 只勾空文件夹时 Del/Enter 拦截（目标错位防误删）', () => {
    const seg = between(st, "e.key === 'Enter'", "e.key?.toLowerCase() === 'e'", 'Enter-Del 分支')
    const hits = (seg.match(/checkedFolders\.value\.size > 0 && checked\.value\.size === 0/g) || []).length
    assert.ok(hits >= 2, 'Enter 与 Del 两分支都应拦截文件夹勾选语境')
  })
  it('M3: 部分成功统一 warning 档（folderOps 三处 + useBatchConnect）', () => {
    const fo = read('src/composables/folderOps.js')
    assert.ok((fo.match(/toast\.finish\('warning'/g) || []).length >= 4, 'folderOps 四处部分成功应 warning')
    const bc = read('src/composables/useBatchConnect.js')
    assert.ok(bc.includes("'warning'"), '批量连接部分成功应 warning')
  })
})

// ---------- round16 V 系列：易用性修复回归（V1-V5/V7/V11-V14，2026-09-25） ----------
describe('round16 V 系列：易用性修复回归', () => {
  it('V1: 列宽热区阻止单击冒泡（六处 resizer 均 @click.stop，与 @dblclick.stop 成对）', () => {
    const n = (st.match(/class="resizer"/g) || []).length
    const m = (st.match(/@click\.stop\s*\n\s*@pointerdown="onResizeStart/g) || []).length
    assert.ok(n >= 6 && m === n, `resizer ${n} 处应全部带 @click.stop，实得 ${m}`)
  })
  it('V2: 凭据模式切换备份/恢复库引用（对齐 WPF _credentialNameCache）', () => {
    const seg = between(drawer, 'function onCredModeSwitch', 'function blocksOf', 'onCredModeSwitch')
    // 顺序不可反：先备份再清空；切回时仅在为空时恢复
    assert.ok(
      /if \(json\.InheritedCredentialName\) lastVaultRef = json\.InheritedCredentialName\s*\n\s*json\.InheritedCredentialName = ''/.test(
        seg
      ),
      '切手动应先备份再清空'
    )
    assert.ok(
      /else if \(!json\.InheritedCredentialName && lastVaultRef\) \{\s*\n\s*json\.InheritedCredentialName = lastVaultRef/.test(
        seg
      ),
      '切回凭据库且为空时应恢复备份'
    )
    assert.ok(drawer.includes("lastVaultRef = raw.InheritedCredentialName || ''"), 'load() 应重置备份（不跨会话泄漏）')
  })
  it('V3: 取值表自报键带实例号（多行 Selections 互不覆盖对方的保存拦截）', () => {
    const kvm = read('src/components/editor/KvMapField.vue')
    assert.ok(/const issueKey = `\$\{props\.field\.key\}#\$\{\+\+issueUidSeq\}`/.test(kvm), '自报键应为 字段键#实例号')
    assert.ok(/report\(issueKey, has\)/.test(kvm) && /report\(issueKey, false\)/.test(kvm), '自报/卸载均应使用实例键')
  })
  it('V4: 添加运行器不写默认运行器（WPF 平价：仅滚动入视野 + 高亮）', () => {
    const rg = read('src/components/settings/RunnerGroup.vue')
    const seg = between(rg, 'function onAddSave', 'function onDeleteRunner', 'onAddSave')
    assert.ok(!seg.includes('selectedRunnerName ='), '添加流程不得写默认运行器字段')
    assert.ok(seg.includes('justAdded.value = runner.Name'), '定位效果（3 秒高亮）应保留')
  })
  it('V5: Ctrl+K 已废弃——实现仅 Ctrl+F（App.vue 全局键无 ctrlk 分支）', () => {
    const app = read('src/App.vue')
    const seg = between(app, 'function onGlobalKey', 'onMounted(', 'App onGlobalKey')
    assert.ok(!/key === 'k'/.test(stripJsComments(vueSections(app).script || '')), '不得存在 Ctrl+K 分支')
    assert.ok(seg.includes("key === 'f'"), 'Ctrl+F 聚焦搜索应在位')
  })
  it('V7: 列表滚动即收起右键菜单（菜单/nfMenu 都关）', () => {
    assert.ok(st.includes('@scroll="onBodyScroll"'), '滚动容器应挂 onBodyScroll')
    const seg = between(st, 'function onBodyScroll', 'function moveCursor', 'onBodyScroll')
    assert.ok(seg.includes('menu.value = null') && seg.includes('nfMenu.value = null'), '滚动应同时关闭两类菜单')
  })
  it('V11: 日志文件位置只读展示 + 打开接口（GeneralGroup 行 / api openLogFolder）', () => {
    const gg = read('src/components/settings/GeneralGroup.vue')
    assert.ok(gg.includes("form.logFilePath = g.logFilePath || ''"), '常规组应回填 logFilePath')
    assert.ok(gg.includes('api.openLogFolder()'), '打开按钮应调 openLogFolder')
    assert.ok(api.includes("openLogFolder: () => request('/api/logs/open'"), 'api 层应有 /api/logs/open')
    const lg = read('src/components/settings/LauncherGroup.vue')
    assert.ok(lg.includes("t('settings.l.engineNote')"), '启动器组应有检索引擎说明')
  })
  it('V12: ⚙ 两档悬停提示（无更新=设置；有更新=新版本文案）', () => {
    const app = read('src/App.vue')
    assert.ok(app.includes("t('topbar.settings')"), '无更新档应显示「设置」')
    assert.ok((app.match(/topbar\.settingsUpdate/g) || []).length >= 2, '齿轮与红点都应挂新版本档提示')
  })
  it('V14: 未授权（401）与后端不可达二分（api 标记 + 空态独立文案）', () => {
    assert.ok(/err\.unauthorized = true/.test(api), '401 错误应带 unauthorized 标记')
    const us = read('src/composables/useServers.js')
    assert.ok(us.includes('unauthorized.value = e?.unauthorized === true'), 'loadAll 失败应区分未授权')
    const seg = between(list, 'v-else-if="showOffline"', '<!-- 空库引导卡片', '离线空态')
    assert.ok(seg.includes("t('empty.unauthorized')"), '空态应分未授权文案')
  })
})

// ---------- 2026-09-25 需求：连接状态指示器按需显示（spec §3.4 / TC-LIST-38） ----------
describe('连接状态指示器按需显示', () => {
  it('未连接时隐藏：仅 connecting/reconnecting/connected 三态渲染，idle 样式已删', () => {
    const dot = read('src/components/StatusDot.vue')
    const seg = between(dot, 'const visible = computed', 'const cls = computed', 'visible')
    for (const s of ["'connected'", "'connecting'", "'reconnecting'"]) {
      assert.ok(seg.includes(s), `显示判定应包含 ${s}`)
    }
    assert.ok(dot.includes('<span v-if="visible" class="status-dot"'), '指示点应以 v-if=visible 整体隐藏')
    assert.ok(!/\bidle\b\s*\.dot/.test(dot) && !dot.includes('.idle .dot'), 'idle（灰空心圈）样式应已删除')
  })
  it('列位与骨架同口径：状态列保留、骨架屏无状态点占位', () => {
    assert.ok(row.includes('cell cell-status'), '状态列容器应保留（列位不塌缩）')
    const seg = between(list, 'class="skeleton-host"', 'empty-offline', '骨架屏段')
    assert.ok(!seg.includes('sk-dot'), '骨架屏不应再有状态点占位（骨架期不知会话态）')
  })
  it('表头不显示标题 + 列宽 12px（2026-09-26 定案：指示器 8px + 4px）', () => {
    // colVars 单一定宽源；h-status 表头为空元素（title/aria-label 保留语义），不再渲染文字
    assert.ok(st.includes(`'--c-status': '12px'`), 'colVars 状态列应为 12px 定宽')
    assert.ok(
      st.includes('class="hcell h-status" :title="t(\'col.status\')" :aria-label="t(\'col.status\')"'),
      '表头应为空元素 + title/aria-label'
    )
    assert.ok(!st.includes('h-status">{{'), '状态表头不得再渲染标题文字')
    assert.ok(row.includes('flex: 0 0 var(--c-status, 12px)'), '行单元格 fallback 应为 12px')
    assert.ok(row.includes('padding-right: 0'), '行状态格应清零 .cell 的 10px 右内距（12px 即总宽）')
  })
})

// ---------- round17：SFC 未定义标识符粗检（no-undef 级） ----------
// 背景：SettingsView.vue 曾把 activeGroup 改成 computed 却漏加 import，设置页整体白屏——
// node 测试不编译 SFC、prettier/词条检查不查语义、vite 构建对运行时未定义标识符不报错，
// 四层门禁全部漏网（round17 专家评审抓回）。本用例对全部 .vue 的 script setup 做轻量
// 词法检查：vue 组合式 API 名在 script 中出现、却既不在任何 import 行也不在本地声明 → 红。
import { readdirSync, statSync } from 'node:fs'
import { join, extname } from 'node:path'

function walkVueFiles(dir, out = []) {
  for (const f of readdirSync(dir)) {
    const p = join(dir, f)
    if (statSync(p).isDirectory()) walkVueFiles(p, out)
    else if (extname(p) === '.vue') out.push(p)
  }
  return out
}

describe('SFC 未定义标识符粗检（vue API 名必须已 import）', () => {
  const VUE_APIS = [
    'ref',
    'computed',
    'reactive',
    'watch',
    'watchEffect',
    'onMounted',
    'onBeforeUnmount',
    'onUnmounted',
    'nextTick',
    'provide',
    'inject',
    'markRaw',
    'readonly',
    'shallowRef',
    'toRef',
    'toRefs',
  ]
  it('全部 .vue：script 内使用的 vue API 名均出现在 import 声明或本地定义', () => {
    const files = walkVueFiles(SRC)
    assert.ok(files.length >= 30, `应扫到 30+ 个 .vue（基线 35），实得 ${files.length}`)
    const offenders = []
    for (const rel of files) {
      const src = read(rel)
      const { script } = vueSections(src)
      if (!script) continue
      // script 需是 <script setup>（本项目全部 SFC 均为 setup 形态；普通 script 的导出不经此约束）
      if (!/<script[^>]*setup[^>]*>/.test(src)) continue
      // 收集已定义名：import 的具名绑定 + 本地 const/let/function 声明 + defineXxx 宏
      const defined = new Set()
      for (const m of script.matchAll(/import\s*\{([^}]*)\}\s*from/g)) {
        for (const piece of m[1].split(',')) {
          const name = piece
            .trim()
            .split(/\s+as\s+/)
            .pop()
            ?.trim()
          if (name) defined.add(name)
        }
      }
      for (const m of script.matchAll(/import\s+([A-Za-z_$][\w$]*)\s+from/g)) defined.add(m[1])
      for (const m of script.matchAll(/(?:const|let|var)\s+([A-Za-z_$][\w$]*)/g)) defined.add(m[1])
      for (const m of script.matchAll(/function\s+([A-Za-z_$][\w$]*)/g)) defined.add(m[1])
      for (const m of script.matchAll(/\bdefine(?:Props|Emits|Expose|Options)\b/g)) defined.add(m[0])
      // 逐 API 词法探测（剥注释，防历史注释里的词误报）
      const bare = stripJsComments(script)
      for (const api of VUE_APIS) {
        if (defined.has(api)) continue
        if (new RegExp(`\b${api}\b`).test(bare)) offenders.push(`${rel}: ${api}`)
      }
    }
    assert.deepEqual(
      offenders,
      [],
      `以下 .vue 使用了未导入的 vue API（进入对应页面即 ReferenceError）: ${offenders.join('; ')}`
    )
  })
})
