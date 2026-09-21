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
  it('数据源状态点 title 走 i18n（H31：不再直出英文裸枚举）', () => {
    const fn = between(tree, 'const dsDotTitle', 'sortedTags', 'dsDotTitle')
    assert.ok(fn.includes('statusbar.dsConnected'))
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
