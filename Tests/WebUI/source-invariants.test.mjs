// 组件接线防复发锚点用例：把历史轮次反复出现的「接线级」回归固化成源码断言。
// 这类 bug（守卫遗漏、事件顺序、判定先后）在纯逻辑层测不到，但也不需要起浏览器——
// 断言的目标模式都取自修复报告的定案形态；若未来有意改变实现，应同步更新本文件
//（每条断言注明来源轮次，先核对设计是否变更再改断言）。
import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { read, vueSections, stripJsComments, stripHtmlComments } from './helpers.mjs'

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
  it('树右键菜单纵向防溢出（J2，对齐 ServerTable 定位标准）', () => {
    assert.ok(tree.includes('Math.min(py, r.height - 130)'))
  })
  it('数据源状态点 title 走 i18n（H31：不再直出英文裸枚举）——round8 起三处共用 utils/dsTitle 单一实现', () => {
    assert.ok(tree.includes('makeDsDotTitle(t)'), 'SideTree 应经 makeDsDotTitle 构造')
    const dst = read('src/utils/dsTitle.js')
    assert.ok(dst.includes('statusbar.dsConnected'))
    assert.ok(read('src/views/ServerListView.vue').includes('makeDsDotTitle(t)'))
    assert.ok(read('src/components/settings/DataSourceGroup.vue').includes('makeDsDotTitle(t)'))
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
  it('搜索激活时文件夹列强制显示（H9：全库命中的来处标注）', () => {
    const decl = between(st, 'const showFolder', '\n', 'showFolder')
    assert.ok(decl.includes('searchedIds.value != null ||'))
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
  it('抽屉三区域横向内距一致（J17：.ed-fields 与头/底同 16px，两份拷贝同步）', () => {
    for (const src of [drawer, bulk]) {
      const css = between(src, '.ed-fields {', '}', '.ed-fields 样式')
      assert.ok(css.includes('padding: 8px 16px 20px'), '.ed-fields 横向内距应为 16px')
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
  it('L2: 四处移除钮 hover 中性（不再红字）', () => {
    for (const [f, sel] of [
      ['src/components/editor/KeyValueLines.vue', '.kvl-del:hover'],
      ['src/components/editor/KvMapField.vue', '.kvm-del:hover'],
      ['src/components/editor/SubformList.vue', '.sf-del:hover'],
      ['src/components/editor/IconPicker.vue', '.ip-clear:hover'],
    ]) {
      const src2 = read(f)
      const a = src2.indexOf(sel)
      assert.ok(a >= 0, `${f} 缺 ${sel}`)
      const block = src2.slice(a, src2.indexOf('}', a))
      assert.ok(!block.includes('var(--danger)'), `${f} 的 ${sel} 仍是红字 hover`)
      assert.ok(block.includes('var(--text-1)'), `${f} 的 ${sel} 应为中性 hover`)
    }
  })
  it('L3: 脚本测试弹窗走标准 create + 无图标 + 禁回车误触', () => {
    const stt = read('src/editor/scriptTest.js')
    assert.ok(!stt.includes('dialog.info('))
    assert.ok(stt.includes('showIcon: false'))
    assert.ok(stt.includes('autoFocus: false'))
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
