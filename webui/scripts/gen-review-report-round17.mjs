// 生成 round17 易用性/功能性评审汇总 HTML 报告（数据来自 4 个评审专家走查 + 主会话逐条读码交叉核验）。
// 跑完保留在 scripts/（报告数据内联，重跑可再生成）。
import { writeFileSync } from 'node:fs'

// ---------- 新发现（W 系列 = round17；专家发现 · 主会话交叉核验确认） ----------
// 与已知重复不计入新发现：专家 A 再发现的「树菜单禁用项无悬停说明」= round16-V8（owner
// 未勾选）、专家 C 再发现的「凭据列表无密码/私钥存在性指示」= round16-V10（owner 未勾选）。
// 专家 C/D 同时发现的「设置页白屏（computed 未导入）」为本轮 round17 重构引入的回归，
// 评审进行中已当场修复并补防回归用例（见已落地 R4），不计入待裁决清单。
const findings = [
  {
    id: 'W1',
    sev: '中',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认（修正：busyGuard 实有 info 提示，非全静默）',
    title: '文件夹长操作进行中，列表行拖到树上仍高亮「可移入」，松手只有一条轻提示、不发生移动',
    loc: 'webui/src/components/SideTree.vue:180-185（listDropAdmit）· webui/src/composables/folderOps.js:49-54（busyGuard）',
    desc: '文件夹重命名/移动等长操作执行期间（数秒），从列表拖服务器行/文件夹行到树节点，树行照常出现「可放入」的虚线高亮——视觉上承诺了移入；松手后只弹出一条浅色 info 提示「操作进行中」，移动不发生。列表侧同类场景早在 K10 定案中明确「不高亮、浏览器拒收」（ServerTable 的 folderDropOk 第一行即查 opsBusy），树侧漏掉了同一把锁，两侧口径分叉。',
    repro:
      '1) 右键一个含大量服务器的文件夹执行「重命名」（长操作数秒）；2) busy 期间从列表拖一台服务器到树上任一文件夹行——出现 drop-into 高亮；3) 松手——只有一条 info 提示，服务器没有被移动。',
    why: '树侧落区判定 listDropAdmit 只查树内重排锁 moving 与「全部数据」根，不查 folderOps.busy；drop 执行体（moveServersToFolder/moveFolder）开头的 busyGuard 此时才拦下并给 info 提示。高亮（承诺）与结果（拒绝）不一致正是 K10 定案要杜绝的形态。',
    fix: 'listDropAdmit 增加 folderOps.busy 为真 → 返回 null（dragover 不 preventDefault、不高亮、浏览器拒收），与列表侧 K10 同款。',
    fig: 'w1-tree-busy-drop',
  },
  {
    id: 'W2',
    sev: '中',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认',
    title: '右键「全部数据」根行或树空白处弹出浏览器原生菜单，WebView2 下含「刷新」可整页重载',
    loc: 'webui/src/components/SideTree.vue:331-333（onRowContext 对 all 根直接 return 未 preventDefault）· :397（.tree-scroll 无空白 contextmenu 处理）',
    desc: '多数据源模式下右键树首行「全部数据」、或任意模式下右键树的空白区域，弹出的不是应用自绘菜单而是 WebView2/浏览器原生菜单。原生菜单里的「刷新」一旦误点会整页重载——丢勾选、丢滚动位置、丢菜单状态。树内其余行都是自绘菜单，列表侧连「..」上级行和空白处都有处理，唯独这两处漏网。',
    repro: '1) 多数据源环境；2) 右键「全部数据」根行（或树底部空白处）；3) 弹出原生菜单（含刷新/检查等浏览器项）。',
    why: 'onRowContext 对 row.kind === "all" 的分支直接 return，既不弹自绘菜单也不阻止浏览器默认行为；.tree-scroll 容器没有空白区 contextmenu 处理器。WPF 壳只关闭了加速键（AreBrowserAcceleratorKeysEnabled），WebView2 默认右键菜单未关，宿主态同样命中。',
    fix: '「全部数据」分支加 e.preventDefault()（与列表「..」上级行同款：只抑制不弹菜单）；.tree-scroll 补空白右键处理——最低限度 preventDefault，有余力可对齐列表空白菜单提供「新建文件夹」入口。',
    fig: 'w2-native-menu',
  },
  {
    id: 'W3',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: 'RDP「添加参数」按两列直觉填写时，值会并入名称、该行实际失效，保存重开前后双重静默',
    loc: 'webui/src/components/editor/KeyValueLines.vue:77-82（serializeRows 原样拼接）· :55-74（parseRows 无分隔符整行进名称）',
    desc: '「添加参数」行有名称/值两列输入框，用户自然按列填写：名称输 Foo、值输 1。但存储格式是 WPF 的单行文本「名称:类型:值」，序列化时直接把两列原样拼接成 Foo1——重开编辑器该行显示名称 Foo1、值为空；且这行没有 :s:/:i:/:b: 类型分隔符，桌面端解析器按格式错误处理。用户以为参数生效了，连接时实际无效，保存时和重开时都没有任何提示。',
    repro:
      '1) 编辑一台 RDP 服务器；2) 高级组点「添加参数」；3) 名称列输 Foo、值列输 1；4) 保存（无提示）；5) 重新打开编辑器：该行名称变成 Foo1、值为空。',
    why: '为逐字节兼容 WPF 存储格式，序列化是 name+value 字符串原样拼接；web 把 WPF 单行文本拆成了两列输入，名称列却不校验「须含类型分隔符」。候选补全插入的是带 :i: 的完整串所以没事，手输名称没有这层保障；hint 文案只有示例没有格式规则。',
    fix: '名称框失焦时检测缺 :s:/:i:/:b: 分隔符，给行内黄色/红色提示；或提交时按值形态自动补默认类型（字符串 :s:）；hint 词条补一句「名称需以 :s: / :i: / :b: 结尾」。',
    fig: 'w3-kv-sep',
  },
  {
    id: 'W4',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '后端校验失败（如端口非法）只落顶部横幅英文原文，无行内定位——WPF 的行内红字语义在网页版丢失',
    loc: 'webui/src/components/editor/EditorDrawer.vue:466-474（saveErrors 横幅）· FormField.vue（顶层字段 invalid 无数据源）· 后端 WebUiEditorService.cs（errors 为英文串无字段键）',
    desc: '必填缺失有守卫聚焦+高亮（round14 已做），但非必填类后端校验失败——如端口输 abc、名称重复——只有 toast 计数和抽屉顶部的英文技术文案横幅，出错的输入框没有任何红标。WPF 版这类错误是行内红字紧贴字段；网页版用户需要自己长滚动表单逐个字段找。RDP 表单很长，找错成本高。',
    repro: '1) 新建 RDP 服务器；2) 端口输 abc；3) Ctrl+S 保存；4) 观察出错框无红标，只有顶部横幅英文。',
    why: '后端 400 返回的 errors 是纯英文字符串数组（无字段键），前端 invalid 属性在抽屉渲染链路上没有数据源可接，只能整串落横幅。',
    fix: '短期：横幅里命中字段名前缀的行加粗并提供点击定位（滚动到对应字段）；中期：后端 400 返回 {field, message} 结构，前端接 invalid 做行内红字（WPF 平价）。',
    fig: 'w4-400-banner',
  },
  {
    id: 'W5',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '批量编辑「覆盖」切回「保持不变」会静默丢弃已输入的内容，再切回也不恢复',
    loc: 'webui/src/components/editor/BulkEditForm.vue:154-164（toggleOverwrite）',
    desc: '批量勾选若干台，在备注字段点「覆盖」并输入了半篇内容，误点到旁边的「保持不变」——输入立即被丢弃，无任何确认；再点回「覆盖」，输入框被重置为共享值/默认值从头再来，原文不可恢复。两步误触即丢稿。',
    repro:
      '1) 勾选 3 台 → 批量编辑；2) 备注行点「覆盖」，输入一段文字；3) 误点「保持不变」——文字消失无确认；4) 再点「覆盖」——输入框被重置。',
    why: '切回只翻 bulkOverwrite[key]=false 标志；再进覆盖态时 bulkValues[key] 被 deepClone(共享值) 无条件重置，用户输入没有任何暂存。',
    fix: '覆盖态有未保存输入时切回给丢弃确认（与抽屉脏关闭确认同款）；或暂存上次输入，再切回覆盖时作为初值恢复。',
    fig: 'w5-bulk-overwrite',
  },
  {
    id: 'W6',
    sev: '中',
    who: '评审员 C（设置与凭据库域）发现 · 主会话读码核验确认',
    title: '凭据编辑里切换「密码/私钥」侧后，隐藏侧已输入的修改被静默丢弃（保存不提交、无提示）',
    loc: 'webui/src/components/settings/CredentialVaultGroup.vue:140-144（setAuthMode 仅切展示）· :213-241（save 隐藏侧提交 null/空串）',
    desc: '编辑凭据时在密码侧 reveal 后改了新密码，切到「私钥路径」侧再保存——提示「已保存」，但密码仍是旧值，刚输入的新密码无声丢失。反方向（私钥侧改完切回密码侧）同样成立。切换本身是浏览动作的直觉，用户不知道切走=放弃。',
    repro:
      '1) 编辑一条凭据；2) 点 👁 reveal 出密码并改成新值；3) 切到「私钥路径」侧；4) 保存——提示成功；5) 重新打开编辑：密码仍是旧值。',
    why: '保存只提交当前 authMode 可见侧，隐藏侧一律按「保持」处理（文件头声明的保守语义，防切换即清库），但「用户已在隐藏侧输入了修改」这一情形没有任何补偿提示，输入被静默忽略。',
    fix: '保存时检测隐藏侧当前值 ≠ 打开时快照且非掩码，附一条警告「仅保存当前显示侧，另一侧的修改未提交」；或切走时对有改动的隐藏侧弹丢弃确认。',
    fig: 'w6-cred-side',
  },
  {
    id: 'W7',
    sev: '中',
    who: '评审员 D（全局壳层）发现 · 主会话读码核验确认',
    title: '主程序重启后旧网页被「失联」全屏警告永久覆盖且文案误导（token 失效未与真失联二分）',
    loc: 'webui/src/App.vue:41-51（backendLost watch 只看 connected）· :317-324（全屏警告模板）',
    desc: '网页开着时重启 1Remote 主程序：约 33 秒后全屏弹出「与后端服务的连接已中断……请确认 1Remote 应用正在运行」——但应用明明正在运行。警告不可关闭、永不自动消失，只能手动刷新页面。重启后访问令牌已更换，旧网页的请求全部返回「未授权」，与「后端不可达」是两回事，却被同一条路径按失联处理。round16 V14 只修了「从未连上」的列表空态分支，「连上后令牌失效」的全屏分支漏在了外面。',
    repro:
      '1) 打开网页界面；2) 关闭并重新打开 1Remote 主程序；3) 等待约 33 秒（30s 轮询 + 3s 滞回）；4) 全屏警告出现且持续不消失，文案让用户去检查「应用是否在运行」。',
    why: 'api 层已给 401 打 unauthorized 标记、useServers 已记录，但只被列表空态消费；App.vue 的失联 watch 只看 connected/everConnected。旧会话 everConnected=true，401 与真失联在同一路径弹出同一份误导文案。',
    fix: 'App.vue 补取 unauthorized，全屏警告为真时切换文案（如「访问已失效，请刷新页面重新进入」，新词条走语言管线）；真失联维持现文案。',
    fig: 'w7-401-lost',
  },
  {
    id: 'W8',
    sev: '低',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认',
    title: '树右键菜单开着时滚动树不关闭——round16 V7 的定案口径没接到树侧',
    loc: 'webui/src/components/SideTree.vue（.tree-scroll 无 @scroll 处理）· 对照 ServerTable.vue 的 onBodyScroll',
    desc: '树内容超一屏时右键底部节点弹出菜单，随后滚动树，菜单悬空停在原视口位置，下方已是另一行。V7 修列表侧时的定案理由（菜单不随滚动、留着会让动作目标与视觉位置脱节——资源管理器惯例）在树侧完全成立，但当时只改了列表。',
    repro: '1) 树节点超一屏；2) 右键底部节点弹出菜单；3) 滚轮向上滚——菜单悬空，其下方已是另一节点。',
    why: '树菜单同样是绝对定位浮层，不随 .tree-scroll 滚动；V7 修复范围只覆盖了列表滚动容器。',
    fix: '.tree-scroll 加 @scroll 关闭树菜单（ctx.value = null），与 V7 同口径。',
    fig: 'w8-tree-scroll',
  },
  {
    id: 'W9',
    sev: '低',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认（rect undefined → (0,0) 路径存在）',
    title: '虚拟滚动模式下按 Menu 键呼出的行菜单可能落在表格左上角 (0,0)',
    loc: 'webui/src/components/ServerTable.vue:692-701（键盘菜单定位）',
    desc: '超过 500 行启用虚拟滚动后，若勾选了某台服务器（复选框）而光标不在其上，且该行滚出了渲染窗口，此时按 Menu/Shift+F10——菜单出现在表格左上角而非目标行旁。',
    repro:
      '1) 列表超过 500 行；2) 勾选 1 台（复选框，光标不在其上）；3) 滚动使该行滚出渲染窗口；4) 按 Menu 键——菜单出现在表格左上角。',
    why: '键盘菜单目标走 keyTargetServer（勾选优先），不经光标滚入逻辑；虚拟窗口外的行没有 DOM，getBoundingClientRect 为 undefined，定位回退 x:0,y:0。仅「勾选行≠光标行且滚出窗口」组合可达。',
    fix: '行 DOM 不在时按该服务器下标先滚入可视区（virtualScrollToServer）再取 rect；或回退放弃打开。',
    fig: 'w9-menu00',
  },
  {
    id: 'W10',
    sev: '低',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认',
    title: '顶栏「+」在多源「全部数据」视图下静默预填 Local，与空白右键同动作的「先选数据源」规则分叉',
    loc: 'webui/src/views/ServerListView.vue:448（ds: … || "Local"）· 对照 ServerTable.vue:373-382（U4 判据）',
    desc: '多数据源停在「全部数据」根：右键空白「新建服务器」按 round11 U4 定案禁用并提示「先选择数据源」；而顶栏「+」同一意图却打开抽屉、数据源默默选了 Local。同一动作两个入口相反处理。减轻因素：抽屉头部有数据源下拉且新建模式可改选，非纯静默。',
    repro:
      '1) 多数据源环境，停在「全部数据」根；2) 顶栏点「+」→ 抽屉打开、数据源默认 Local；3) 对比：同视图右键空白「新建服务器」→ 禁用 + 提示先选数据源。',
    why: '顶栏入口经 editorBus 走 openCreate，未复用 U4 的判据。',
    fix: '顶栏入口在「全部数据」根下同样提示先选数据源；或接受顶栏作为全局入口的预填默认（把两入口口径差异写进文档定案）。',
    fig: 'w10-plus-local',
  },
  {
    id: 'W11',
    sev: '低',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '子表单删行后，行内瞬态（如密码明文态）按位置串到换位后的行',
    loc: 'webui/src/components/editor/SubformList.vue:135（行 v-for 缺省索引 key）· FormField.vue（showPassword 等本地态）',
    desc: '备用凭据两行，第 1 行密码框点了眼睛（明文态）；删除第 1 行后，原第 2 行的数据落到第 1 位，密码框仍是明文显示（显示的是新行的数据）。与 round17 修的块级 key 问题同源，但外层行 key 仍是索引。仅瞬态串扰，数据本身不错位。',
    repro: '1) 备用凭据加两行；2) 展开第 1 行，密码框点眼睛；3) 删除第 1 行；4) 剩余第一行密码框保持明文态。',
    why: '行 v-for 用索引 key，删行后 Vue 就地复用 FormField 实例，showPassword 等内部状态不重置；数据由属性纠正、瞬态不纠正。',
    fix: '给行注入稳定键（行创建时生成 uid 传给 rowKey），或删行时强制重建受影响行内组件。',
    fig: 'w11-row-transient',
  },
  {
    id: 'W12',
    sev: '低',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '批量编辑勾选超过 50 台时的占位文案误导：让用户以为覆盖能力受限',
    loc: 'webui/src/components/editor/BulkEditForm.vue:72-86, 207-209 · zh-CN.json「当前值未随列表提供…」',
    desc: '勾选超过 50 台时，前端主动不回读当前值（防止大库风暴），字段显示「当前值未随列表提供；选择『覆盖』后设置」——实际覆盖功能完全可用，只是没预填。文案让用户误以为台数多时功能受限。',
    repro: '1) 勾选 60 台；2) 批量编辑；3) 非列表字段显示上述占位文案。',
    why: '>50 台与「列表 DTO 不含该字段」两种未回读原因共用同一条文案。',
    fix: '>50 台时换专属文案（如「选中台数较多，未回读当前值；可直接覆盖设置」），新增词条走语言管线。',
    fig: 'w12-bulk50',
  },
  {
    id: 'W13',
    sev: '低',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '新建时点「添加参数」不输任何内容也会被判脏，Esc 关闭弹「未保存的更改」',
    loc: 'webui/src/components/editor/KeyValueLines.vue:115-120 · KvMapField.vue:98-104（addRow 即 commit）· EditorDrawer.vue 脏检测',
    desc: '新建 RDP 后点一下「添加参数」什么都没输，按 Esc——弹出「有未保存的更改」确认。与代码注释「值不因加空行变脏」的声明不符，用户被无端的确认打断。',
    repro: '1) 新建 RDP；2) 高级组点「添加参数」；3) 不输入任何内容按 Esc；4) 弹出未保存确认。',
    why: '新建的默认配置不含该键（undefined），加空行后空表序列化 emit 空串，空串与 undefined 比对即脏。编辑已有服务器时原值已是空串所以不触发，仅新建场景命中。',
    fix: '序列化结果为空表时 emit undefined（与键缺失等价）；或父级脏检测把空串/空对象与键缺失视为相同。',
    fig: 'w13-emptyrow-dirty',
  },
  {
    id: 'W14',
    sev: '低',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '「已尝试保存」的行内标红门控跨协议切换不清零，新协议未试存就先红',
    loc: 'webui/src/components/editor/EditorDrawer.vue:311-322（onProtocolSwitch 未重置 validate）',
    desc: 'RDP 下点过一次保存（触发行内标红门控），切到 SSH 后——SSH 的备用凭据若有空名行，还没在新表单上试过保存就已经显示红标。违背「红星预告要求、红字留给真实违规」的门控精神。仅状态残留，无功能损失。',
    repro: '1) 编辑 RDP，制造一个必填缺失并点保存（触发红标）；2) 切到 SSH；3) SSH 备用凭据空名行直接显示红标。',
    why: 'validate 门控只在 load()（换服务器/重开抽屉）时重置，onProtocolSwitch 只清 saveErrors 不清 validate。',
    fix: 'onProtocolSwitch 里随 saveErrors 一并 saveIssues.validate.value = false。',
    fig: 'w14-validate-remain',
  },
  {
    id: 'W15',
    sev: '低',
    who: '评审员 B（编辑器域）发现 · 主会话读码确认（浏览器通常按内容嗅探，标「待实测」）',
    title: '图标上传非 PNG 图片（JPEG/WebP）后，渲染处统一拼 image/png 前缀，严格环境可能裂图',
    loc: 'webui/src/components/editor/IconPicker.vue:146,173',
    desc: '上传入口接受 image/*，存储的是裸 base64；渲染时统一拼 data:image/png;base64, 前缀。主流浏览器按内容魔数嗅探通常仍能显示，严格按声明 MIME 解码的环境会裂图；WPF 桌面按内容解码无此问题。',
    repro: '1) 编辑服务器图标 → 上传一张 JPEG；2) 列表通常正常显示；3) 严格 MIME 环境下图片不渲染。',
    why: '上传时没有记录真实格式，渲染端硬编码了 png 前缀。',
    fix: '上传时经 canvas 统一转 PNG 再存（顺带压缩），或存储时记录真实 MIME。',
    fig: 'w15-mime',
  },
  {
    id: 'W16',
    sev: '低',
    who: '评审员 C（设置与凭据库域）发现 · 主会话读码核验确认',
    title: '关于页有新版本但无下载链接时渲染成 href="#" 假链接，点击无动作还留历史记录',
    loc: 'webui/src/components/settings/AboutGroup.vue:101',
    desc: '新版本信息里 newVersionUrl 为空时，新版本号仍渲染为链接但地址是 #——点击无任何动作，URL 尾部还多出一条 # 历史。',
    repro: '1) 有新版本且后端未给出链接；2) 点关于页的新版本号；3) 无动作，地址栏多一个 #。',
    why: ':href="update.newVersionUrl || \'#\'" 的兜底让「无链接」仍渲染为可点的 a 标签。',
    fix: '无 url 时渲染 span（保留红色提示观感），有 url 才渲染链接。',
    fig: 'w16-href',
  },
  {
    id: 'W17',
    sev: '低',
    who: '评审员 C（设置与凭据库域）发现 · 主会话读码核验确认',
    title: '添加运行器后保存失败：新卡片留在界面上可继续编辑，刷新后消失',
    loc: 'webui/src/components/settings/RunnerGroup.vue:171-198（onAddSave 先入列表再保存）',
    desc: '后端不可达或保存失败时，「+ 添加运行器」得到的新卡正常显示、可编辑、有 3 秒高亮，失败 toast 转瞬即逝——用户以为添加成功，刷新页面后运行器消失。添加是离散动作，与文本输入的自动保存心智不同，失败态应驻留可见。',
    repro:
      '1) 停掉后端（或制造保存失败）；2) 设置 → Runner → 添加运行器输入名字；3) 新卡出现可编辑；4) 刷新页面——卡片消失。',
    why: '自动保存体系「失败 toast 外不回滚」（文件头声明，对齐 WPF 内存先行），但 WPF 是显式保存按钮心智，web 是自动保存心智，分歧对用户不可见。',
    fix: '添加/删除这类离散动作失败时，除 toast 外给卡片一个驻留的「未保存」标记，或失败即回滚 splice。',
    fig: 'w17-runner-residue',
  },
  {
    id: 'W18',
    sev: '低',
    who: '评审员 D（全局壳层）发现 · 主会话读码核验确认（可访问性粗查）',
    title: '可访问性粗查四项：窗口控制 aria 英文、搜索框无标签、失联遮罩无焦点陷阱、状态点无读屏语义',
    loc: 'webui/src/App.vue:283-305（aria-label 英文）· :255-264（搜索框）· :317-324（失联 overlay）· StatusDot.vue',
    desc: '①窗口控制最小化/最大化/关闭的 aria-label 硬编码英文，14 语言下读屏用户恒听英文；②顶栏搜索框无 aria-label，仅靠 placeholder 兜底（可用但弱）；③失联全屏警告视觉阻断但无焦点陷阱，Tab/Enter 仍可触达被遮控件（请求只会失败，无实害但与「阻断交互」意图不一致）；④活动连接状态点仅 title 承载状态名，读屏拿不到「已连接」。',
    repro: '用读屏软件（如 NVDA）操作界面逐项验证；或用浏览器辅助功能面板检查。',
    why: '可访问性此前不在评审维度内，属系统性缺位而非个别遗漏。',
    fix: '①③④补词条与 aria 属性（win.minimize 等新词条走语言管线；状态点加 role="img" + aria-label）；②搜索框补 aria-label；③失联时对背景内容设 inert。',
    fig: 'w18-a11y',
  },
  {
    id: 'W19',
    sev: '低',
    who: '四位专家共同发现 · 主会话汇总核对（spec 与实现漂移清单，多数为文档修订项）',
    title: 'spec 与实现的九处漂移：多为「文档超前于实现」，建议逐条拍板修文档还是排期补功能',
    loc: 'docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md 各节 · 对应实现文件',
    desc: '①§3.4 右键「新窗口连接/其他凭据连接/桌面快捷方式」未接线（现为占位禁用+即将推出提示）；②§3.4 列菜单声称可配「显隐与顺序」——列顺序不可配（仅显隐+列宽）；③§8.6「边栏宽度」持久化——边栏定宽不可拖，收起态也不记忆；④§3.2/§10「树节点三态勾选框」——勾选实现在列表文件夹行，树内无勾选（功能等价、位置不同）；⑤§8.7「<900px 批量条折叠为计数徽章」未实现，极窄窗批量条挤压面包屑；⑥§8.4「推送版本号、前端增量刷新」实为 SSE 信号+全量重载；⑦§3.1 顶栏示意图含「排序 列」——实际在面包屑行；⑧§6 凭据表格写了「地址」列——实现与 WPF 均无此列（spec 笔误）；⑨状态点「重连中(reconnecting)」为前端预留态，后端当前不产出该值（断开即隐藏，与需求一致，但 spec 宜注明预留）。',
    repro: '逐条对照 spec 文字与实现（各条均已由专家读码核对；⑤的极窄窗溢出未实测）。',
    why: 'spec 定稿于实现前，历轮演进而文档未同步；部分是能力缺口（①⑤），部分是纯文档修订（②③④⑥⑦⑧⑨）。',
    fix: '纯文档项直接修订 spec 使其与实现对齐（⑧⑨ 可立即改）；能力缺口（①⑤③）由您决定排期或降级 spec 承诺。',
    fig: 'w19-spec-drift',
  },
]

// ---------- SVG 示意图（三框流程模板；每条 [fill, stroke, 标题, [行...]]） ----------
const YELLOW = ['#fdf6e8', '#b9770e']
const RED = ['#fdf1ef', '#c0392b']
const GREEN = ['#eef8f2', '#1e8e5a']
function fig(caption, boxes) {
  const box = (x, w, fill, stroke, title, lines) => {
    const texts = [title, ...lines]
      .map(
        (t, i) =>
          `<text x="${x + 14}" y="${44 + i * 20}" font-size="12" fill="${i === 0 ? stroke : '#5a6270'}" ${i === 0 ? 'font-weight="700"' : ''}>${t}</text>`
      )
      .join('')
    return `<rect x="${x}" y="18" width="${w}" height="${Math.max(texts.length * 20 + 36, 110)}" rx="8" fill="${fill}" stroke="${stroke}"/>${texts}`
  }
  const arrow = (x) => `<path d="M${x} 82 L${x + 42} 82" stroke="#8a92a0" stroke-width="2" marker-end="url(#ar17)"/>`
  const w = 226
  const parts = [
    box(10, w, ...boxes[0]),
    arrow(10 + w),
    box(10 + w + 52, w, ...boxes[1]),
    arrow(10 + (w + 52) * 2 - 10),
    box(10 + (w + 52) * 2, w, ...boxes[2]),
  ]
  return `<div class="fig"><div class="cap">${caption}</div><svg width="760" height="150" viewBox="0 0 760 150" role="img">${parts.join('')}<defs><marker id="ar17" markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 z" fill="#8a92a0"/></marker></defs></svg></div>`
}
const F = {
  'w1-tree-busy-drop': () =>
    fig('高亮承诺移入 → 松手只有轻提示 → 未移动', [
      [...YELLOW, '长操作进行中', ['拖服务器到树节点', '树行出现「可放入」高亮']],
      [...YELLOW, '松手', ['只弹一条 info 提示', '「操作进行中」']],
      [...RED, '移动没有发生', ['列表侧同场景不高亮', '（K10 定案两侧分叉）']],
    ]),
  'w2-native-menu': () =>
    fig('右键全部数据根/树空白 → 原生菜单 → 误点刷新丢状态', [
      [...YELLOW, '右键「全部数据」根', ['或右键树空白处', '分支直接 return']],
      [...YELLOW, '浏览器默认行为', ['弹出原生菜单', '（刷新/检查等）']],
      [...RED, '误点「刷新」', ['整页重载', '丢勾选/滚动/菜单态']],
    ]),
  'w3-kv-sep': () =>
    fig('按两列直觉填写 → 原样拼接 → 重开并入名称且失效', [
      [...YELLOW, '名称输 Foo', ['值输 1', '（两列直觉填写）']],
      [...YELLOW, '序列化', ['name+value 原样拼接', '存成 Foo1（无类型分隔符）']],
      [...RED, '重开编辑器', ['名称 Foo1 / 值空', '桌面端按格式错误处理']],
    ]),
  'w4-400-banner': () =>
    fig('后端 400 英文串 → 仅顶部横幅 → 无行内定位', [
      [...YELLOW, '端口输 abc', ['保存 → 后端校验失败']],
      [...YELLOW, 'errors 无字段键', ['纯英文串整批返回', '前端接不了行内红标']],
      [...RED, '用户自己找错', ['长表单滚动逐个看', 'WPF 是行内红字']],
    ]),
  'w5-bulk-overwrite': () =>
    fig('覆盖态输入 → 误点保持不变 → 静默丢弃不可恢复', [
      [...YELLOW, '点「覆盖」', ['输入半篇备注']],
      [...YELLOW, '误点「保持不变」', ['无任何确认', '输入立即被丢弃']],
      [...RED, '再点回「覆盖」', ['重置为共享值起编', '原文不可恢复']],
    ]),
  'w6-cred-side': () =>
    fig('改密码 → 切私钥侧 → 保存只提交可见侧', [
      [...YELLOW, 'reveal 后改密码', ['新值已输入']],
      [...YELLOW, '切到「私钥路径」侧', ['仅切展示（浏览直觉）']],
      [...RED, '保存', ['隐藏侧按「保持」提交', '新密码无声丢失']],
    ]),
  'w7-401-lost': () =>
    fig('重启主程序 → 401 持续 → 全屏警告误导', [
      [...YELLOW, '主程序重启', ['访问令牌已更换', '旧网页请求全 401']],
      [...YELLOW, '30s 轮询 + 3s 滞回', ['connected 恒 false', '走失联路径']],
      [...RED, '全屏警告', ['「请确认应用正在运行」', '其实正在运行，永不消失']],
    ]),
  'w8-tree-scroll': () =>
    fig('右键节点 → 滚动树 → 菜单悬空（V7 未接树侧）', [
      [...YELLOW, '右键底部节点', ['菜单弹出锚定该行']],
      [...YELLOW, '滚轮向上', ['菜单停在原视口位置']],
      [...RED, '视觉脱节', ['菜单下方已是另一行', '列表侧 V7 已修，树侧漏']],
    ]),
  'w9-menu00': () =>
    fig('虚拟窗口外行 → rect undefined → 菜单 (0,0)', [
      [...YELLOW, '勾选行 ≠ 光标行', ['且勾选行滚出渲染窗口']],
      [...YELLOW, '按 Menu 键', ['querySelector 查不到行 DOM', 'rect = undefined']],
      [...RED, '回退 x:0, y:0', ['菜单落在表格左上角', '远离目标行']],
    ]),
  'w10-plus-local': () =>
    fig('同一意图两个入口相反处理', [
      [...GREEN, '空白右键「新建」', ['禁用 + 提示', '「请先选择数据源」（U4）']],
      [...RED, '顶栏「+」', ['静默预填 Local', '未复用 U4 判据']],
      [...YELLOW, '减轻因素', ['抽屉头部可改选数据源', '非纯静默，口径需拍板']],
    ]),
  'w11-row-transient': () =>
    fig('删行 → 就地复用 → 瞬态串到新行', [
      [...YELLOW, '第 1 行点眼睛', ['密码框明文态']],
      [...YELLOW, '删除第 1 行', ['第 2 行数据落到第 1 位', '索引 key 就地复用组件']],
      [...RED, '明文态保留', ['显示的是新行数据', '（round17 块级 key 同源）']],
    ]),
  'w12-bulk50': () =>
    fig('>50 台不回读 → 文案误称能力受限', [
      [...YELLOW, '勾选 60 台', ['主动不回读（防风暴）', '覆盖功能可用']],
      [...YELLOW, '字段占位显示', ['「当前值未随列表提供」', '与 DTO 不含字段共用文案']],
      [...RED, '用户误解', ['以为台数多时功能受限', '实际直接覆盖即可']],
    ]),
  'w13-emptyrow-dirty': () =>
    fig('加空行 → 空表 emit 空串 → 与 undefined 比即脏', [
      [...YELLOW, '新建 RDP', ['点「添加参数」不输入']],
      [...YELLOW, '空表序列化', ['emit ""（原值是 undefined）', '注释声明不脏仅对已有服务器成立']],
      [...RED, 'Esc 关闭', ['弹「有未保存的更改」', '无端确认打断']],
    ]),
  'w14-validate-remain': () =>
    fig('RDP 试保存 → 切 SSH → 红标门控未清零', [
      [...YELLOW, 'RDP 制造必填缺失', ['点保存 → validate=true', '行内红标亮起']],
      [...YELLOW, '切到 SSH', ['只清 saveErrors', 'validate 残留']],
      [...RED, '未试存先红', ['备用凭据空名行', '直接显示红标']],
    ]),
  'w15-mime': () =>
    fig('上传 JPEG → 存裸 base64 → 渲染拼 image/png', [
      [...YELLOW, '上传入口', ['accept="image/*"', 'JPEG/WebP 可上传']],
      [...YELLOW, '存储', ['裸 base64 不记格式', '渲染统一拼 png 前缀']],
      [...RED, '严格 MIME 环境', ['按声明解码 → 裂图', '（主流浏览器嗅探可显示）']],
    ]),
  'w16-href': () =>
    fig('无链接 → href="#" → 点击无动作', [
      [...YELLOW, '有新版本', ['但 newVersionUrl 为空']],
      [...YELLOW, '兜底渲染', [':href="… || \'#\'"', '仍是可点的链接']],
      [...RED, '点击', ['无任何动作', '地址栏多一条 # 历史']],
    ]),
  'w17-runner-residue': () =>
    fig('PUT 失败 → 卡片残留可编辑 → 刷新消失', [
      [...YELLOW, '添加运行器', ['先入本地列表再保存', '3 秒高亮正常']],
      [...YELLOW, '保存失败', ['仅一条转瞬 toast', '不回滚（自动保存体系）']],
      [...RED, '用户以为成功', ['继续编辑新卡', '刷新后卡片消失']],
    ]),
  'w18-a11y': () =>
    fig('可访问性四项缺位（读屏粗查）', [
      [...YELLOW, '窗口控制', ['aria-label 硬编码英文', '14 语言恒英文']],
      [...YELLOW, '搜索框/状态点', ['无 aria-label', '状态名仅 title 承载']],
      [...RED, '失联遮罩', ['无焦点陷阱', 'Tab 仍可触达被遮控件']],
    ]),
  'w19-spec-drift': () =>
    fig('spec 九处漂移：文档超前于实现', [
      [...YELLOW, '能力缺口', ['右键三项占位/批量条窄窗折叠', '边栏宽度持久化']],
      [...YELLOW, '文档修订项', ['列顺序/树勾选位置/顶栏布局', '增量刷新措辞/凭据地址列笔误']],
      [...RED, '待拍板', ['逐条决定：修文档', '还是排期补功能']],
    ]),
}

function card(f) {
  const sevClass = f.sev === '高' ? 'p1' : f.sev === '中' ? 'p2' : 'p3'
  return `  <div class="issue" id="${f.id.toLowerCase()}">
    <div class="issue-head">
      <input type="checkbox" data-id="${f.id}" data-title="${f.title}"/>
      <h3><span class="sev ${sevClass}">${f.sev}</span>${f.id} ${f.title}</h3>
    </div>
    <span class="who">${f.who}</span>
    <span class="loc">${f.loc}</span>
    <dl>
      <dt>问题说明</dt><dd>${f.desc}</dd>
      <dt>如何复现</dt><dd>${f.repro}</dd>
      <dt>解释</dt><dd>${f.why}</dd>
      <dt>如何修复</dt><dd>${f.fix}</dd>
    </dl>
    ${F[f.fig] ? F[f.fig]() : ''}
  </div>`
}

const sevCount = { 高: 0, 中: 0, 低: 0 }
for (const f of findings) sevCount[f.sev]++
const tocNew = findings
  .map(
    (f) => `<a href="#${f.id.toLowerCase()}">${f.id} ${f.title.length > 42 ? f.title.slice(0, 42) + '…' : f.title}</a>`
  )
  .join('')

// ---------- 本轮已落地的修复（无需决策，供追溯） ----------
const fixed = [
  {
    id: 'R0',
    title: '评审中当场修复：设置页白屏（computed 未导入——round17 重构引入的回归）',
    loc: 'webui/src/views/SettingsView.vue:19',
    desc: 'round17 重构把 activeGroup 从函数改成 computed，但 import 行漏加 computed——点 ⚙ 进设置页即 ReferenceError 整页白屏。四位专家中两位（C/D）独立发现。node 测试不编译 SFC、prettier/词条检查不查语义、vite 构建对运行时未定义标识符不报错，四层门禁全部漏网。',
    fix: '已补 import 并新增防回归用例：source-invariants 新增「SFC 未定义标识符粗检」（扫描全部 .vue 的 script setup，vue 组合式 API 名必须出现在 import 或本地声明中），同类回归今后进门禁即红。',
  },
  {
    id: 'R1',
    title: 'round16 评审 V1-V5/V7/V11-V14 修复（本轮用户勾选执行）',
    loc: 'webui/src/components/ServerTable.vue（V1/V7）· editor/EditorDrawer.vue（V2）· editor/KvMapField.vue（V3）· settings/RunnerGroup.vue（V4）· 设计文档/测试指南（V5/V13）· 后端 WebUiSettingsService.General + WebUiEndpoints.Aux（V11 logFilePath + /api/logs/open）· App.vue（V12 ⚙ 两档提示）· api/useServers/ServerListView（V14 未授权二分）',
    desc: 'V1 列宽热区补 @click.stop（6 处）；V2 凭据切换备份/恢复库引用（对齐 WPF _credentialNameCache）；V3 取值表自报键带实例号；V4 添加运行器不再写默认运行器；V5 Ctrl+K 从设计文档与测试指南废弃（实现仅 Ctrl+F）；V7 列表滚动即收起右键菜单；V11 常规组加「日志文件位置」只读行 + 打开所在文件夹端点 + 启动器组检索引擎说明（8 个新词条走语言管线，14 语言 × 584 键平价）；V12 ⚙ 两档悬停提示；V13 测试指南去词条数字；V14 未授权（401）与后端不可达二分独立文案。',
    fix: '已全部落地并经自动化回归（新增 9 条 V 系列断言）；后端 dotnet test 207/207。',
  },
  {
    id: 'R2',
    title: '新需求：连接状态指示器按需显示（2026-09-25）',
    loc: 'webui/src/components/StatusDot.vue · ServerListView.vue（骨架屏）· spec §3.4 · 测试指南 TC-LIST-38',
    desc: '未连接（disconnected/未知）时行内指示点隐藏不可见；connecting/reconnecting=黄、connected=绿点+光晕才显示；断开后恢复隐藏。列位与表头保留（行布局稳定），骨架屏该列同口径留空（不预告可能不会出现的点），idle 灰空心圈样式随之删除。',
    fix: '已落地（含 2 条 L3 回归断言），设计与测试文档同步更新。',
  },
  {
    id: 'R3',
    title: 'round17 前端代码审查与重构（不改功能）',
    loc: 'webui/src/（22 文件）+ 测试锚点同步',
    desc: '修正错误 6 处：单数据源根视图 SSE 重载误清勾选（selection 语义比较）、CredentialPicker 数据源快切竞态（请求序号）、编辑器块循环索引 key 瞬态串位（块身份 key）、启动器 409 冲突提示滞留（保存成功即清）、运行器主题色点 0×0 不可见（scoped 对 h() 不生效，样式内联）、凭据模态重开 revealing/browsing 假死（一并复位）。删除无用代码：4 个 composable 死返回键、TagManagerModal 两个永不触发的 watch、.ed-banner-required 死类、CredentialPicker allowClear 死 prop、4 处字面 NUL 字节转义（文件曾被判二进制）。代码复用：右键菜单定位收敛 utils/contextMenu（3 处）、布尔确认收敛 confirmBool（2 处）、文件选择收敛 pickInto（2 处）、保存失败文案收敛 saveErrorText（4 处）、.hint 样式上收 theme.css（5 份）。清晰度：热键修饰键顺序统一、options 去掉 computed 包装、activeGroup 改 computed、valueChanged 化简等。',
    fix: '全部经 215/215 自动化测试 + vite 生产构建验证；与主流 Vue 3 写法对齐（computed 优先、模块级共享单例、乱序完成保护模式统一）。',
  },
]
const fixedCards = fixed
  .map(
    (f) => `  <div class="issue" id="${f.id.toLowerCase()}">
    <div class="issue-head"><input type="checkbox" data-id="${f.id}" data-title="${f.title}" checked disabled />
      <h3><span class="sev p1" style="background:#1e8e5a">已落地</span>${f.id} ${f.title}</h3></div>
    <span class="who">主会话执行 · 本轮任务范围</span>
    <span class="loc">${f.loc}</span>
    <dl><dt>内容</dt><dd>${f.desc}</dd><dt>验证</dt><dd>${f.fix}</dd></dl>
  </div>`
  )
  .join('\n')

// ---------- 遗留问题复验（本轮主会话逐条亲验；代码未动即继承 round16 结论） ----------
const recheck = {
  亲验仍在: [
    [
      'round15-A1（唯一"高"级）',
      '批量删除执行锁与文件夹操作忙碌锁互不感知：ServerListView.vue:490-534 的 batchDeleting 只守自己重入，ServerTable 的 :ops-busy 只接 folderOps.busy、:batch-busy 只接 batchDeleting——批量删除进行中文件夹重命名/删除/拖拽照常可用（反向亦然），两路全量保存并发后完成者覆盖先完成者。本轮亲读两侧接线确认未变',
    ],
    [
      'round15-A3',
      '空文件夹删除确认框打开期间被另一端放入服务器：folderOps.js deleteFolder 在弹框时算好 count=0，onPositive 直接 runDelete(…, false) 按旧决策执行——内容被静默上移。亲读 272-300 行确认无复核',
    ],
    [
      'round15-A4',
      '启动语言同步晚归覆盖用户刚切换的语言：App.vue onMounted 的 getGeneralSettings().then(setLocale) 无"用户已手动切换"守卫。亲读确认仍在',
    ],
    [
      'round15-C3',
      'sqlite 路径为空时保存钮真禁用（DataSourceGroup addValid/editValid → :disabled），统一"假禁用+强点聚焦"不可达。亲读确认仍在（注释声明属"真错误"口径，语义分歧待 owner 裁决）',
    ],
    [
      'H5 界面引擎单向门',
      'WebUi 后端与前端全库无 InterfaceEngine 相关键（亲 grep 确认零命中）——WPF 可切 Web/桌面，Web 侧无回头入口，唯一回途是手改配置文件',
    ],
    [
      'H23 键盘目标分裂（半边）',
      'Enter 已与光标外框同源；但 E/Del/Ctrl+D/Menu 走 keyTargetServer（恰好单选时优先勾选行）。亲读该函数确认口径未变，源码注释仍标"遗留待裁决"',
    ],
    [
      '--danger 对比度',
      'theme.css 亮色 --danger 仍 #ef6a6a（对白字约 3.02），影响面收窄在批量条删除按钮 hover。值未动',
    ],
    [
      '数据源手动刷新/重连入口',
      'DataSourceGroup 仍只有「测试连接」（不触发重连），同步靠后端 ~10s 定时器。亲读确认未补',
    ],
  ],
  继承仍在: [
    [
      'round15 其余 11 条（A2/A5/B2/B3/B4/B5/C1/C2/C6/C7 + C5 归并）',
      'round16 已逐条复验"仍在"，round16 报告后相关代码除本轮 V 系列修复外未再改动；其中 B2/B5/C1 三条 round16 曾被专家再次独立发现。详见 round15/16 报告卡片',
    ],
    [
      'round13/14 遗留 30 条',
      'round15/16 复验"仍在"的 R2/N9/N10/N11/N15/N16/N18/N21-N25/U8/U9/U12-U14/U18/U20/U22-U24/U26-U29/R6 等全部继承（代码未动）',
    ],
    [
      'SSH 开 SFTP 后原行不变绿（批次7）',
      '读码证据链指向已修复但涉及真实桌面连接管线，仍建议实测后销账（round16 结论继承）',
    ],
    [
      'round17 重构记录的已知项：半行聚焦断链',
      'KeyValueLines/KvMapField 半行自报键进 missing 后无 data-req-key 锚点（非 required 字段），强点保存只滚到顶部/仅 toast 计数——由本轮编辑器域专家复核后列入新发现或遗留',
    ],
  ],
}
const recheckRows = (list, cls, label) =>
  list.map(([id, note]) => `<tr class="${cls}"><td>${id}</td><td>${label}</td><td>${note}</td></tr>`).join('')

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>易用性 / 功能性多专家评审汇总 · 2026-09-25 round17</title>
<style>
  :root { --bg:#f7f8fa; --card:#fff; --line:#e3e6ea; --text:#2c3038; --text2:#5a6270; --muted:#8a92a0; --hi:#c0392b; --mid:#b9770e; --lo:#5a6270; --ok:#1e8e5a; }
  * { box-sizing: border-box; }
  body { margin:0; padding:0 0 120px; background:var(--bg); color:var(--text); font-family:"Segoe UI","Microsoft YaHei",sans-serif; font-size:15px; line-height:1.65; }
  .wrap { max-width:1080px; margin:0 auto; padding:28px 20px; }
  h1 { font-size:24px; margin:0 0 6px; }
  .sub { color:var(--muted); font-size:13.5px; margin-bottom:22px; }
  .panel { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:16px 20px; margin:14px 0; }
  .legend { display:flex; flex-wrap:wrap; gap:14px; font-size:13.5px; color:var(--text2); }
  .sev { display:inline-block; min-width:34px; text-align:center; border-radius:6px; color:#fff; font-size:12.5px; padding:2px 8px; margin-right:8px; vertical-align:2px; }
  .p1 { background:var(--hi); } .p2 { background:var(--mid); } .p3 { background:var(--lo); }
  .issue { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:16px 20px 18px; margin:16px 0; }
  .issue-head { display:flex; align-items:flex-start; gap:10px; }
  .issue-head h3 { margin:0; font-size:16.5px; flex:1; }
  .issue-head input[type=checkbox] { width:20px; height:20px; margin-top:3px; cursor:pointer; accent-color:#2f6fed; }
  .who { display:block; color:var(--muted); font-size:12.5px; margin-top:4px; }
  .loc { display:block; font-family:Consolas,monospace; font-size:12.5px; color:#2f6fed; background:#f0f4ff; border-radius:6px; padding:3px 8px; margin:8px 0; width:fit-content; }
  dl { margin:8px 0 0; display:grid; grid-template-columns:86px 1fr; gap:4px 12px; }
  dt { color:var(--muted); font-weight:600; font-size:13.5px; }
  dd { margin:0; }
  .fig { margin-top:12px; background:#fbfcfd; border:1px dashed var(--line); border-radius:8px; padding:10px; overflow-x:auto; }
  .fig .cap { font-size:12.5px; color:var(--muted); margin-bottom:6px; }
  .picker { position:fixed; left:0; right:0; bottom:0; background:#1f2430; color:#fff; padding:12px 22px; box-shadow:0 -4px 18px rgba(0,0,0,.25); z-index:50; }
  .picker .inner { max-width:1080px; margin:0 auto; }
  .picker h4 { margin:0 0 4px; font-size:14px; }
  .picker .hint { color:#aab3c2; font-size:12.5px; }
  .picker ul { margin:6px 0 0; padding:0 0 0 4px; list-style:none; max-height:120px; overflow-y:auto; display:flex; flex-wrap:wrap; gap:6px 18px; font-size:13.5px; }
  .picker li::before { content:"☑ "; color:#7fd4a8; }
  .picker li.none::before { content:"☐ "; color:#8892a4; }
  .picker .count { color:#7fd4a8; font-weight:700; }
  .toc { columns:2; column-gap:34px; font-size:13.5px; }
  .toc a { color:var(--text2); text-decoration:none; display:block; padding:2px 0; }
  .toc a:hover { color:#2f6fed; }
  .toc .b { font-weight:700; margin-top:6px; color:var(--text); }
  .note { font-size:13.5px; color:var(--text2); }
  table.recheck { width:100%; border-collapse:collapse; font-size:13.5px; }
  table.recheck th, table.recheck td { border:1px solid var(--line); padding:6px 10px; text-align:left; vertical-align:top; }
  table.recheck th { background:#f0f4ff; }
  svg text { font-family:"Segoe UI","Microsoft YaHei",sans-serif; }
</style>
</head>
<body>
<div class="wrap">
<h1>易用性 / 功能性多专家评审汇总 · 2026-09-25 round17</h1>
<div class="sub">2026-09-25 · 4 名专家分域走查（A 主列表与树 / B 编辑器 / C 设置与凭据库 / D 全局壳层与需求符合度）+ 每条发现由主会话逐条读码交叉核验 + 遗留问题定向复验。本轮前置工作：round16 V 系列修复（V1-V5/V7/V11-V14）、新需求「连接状态指示器按需显示」、round17 前端代码审查与重构均已落地并通过 215/215 自动化测试 + 生产构建。新发现见下方卡片（勾选 = 您决定要修）。</div>
<div class="panel"><div class="legend"><span><span class="sev p1">高</span>数据丢失 / 功能明显损坏</span><span><span class="sev p2">中</span>功能不符直觉或反馈缺失，易造成误操作</span><span><span class="sev p3">低</span>体验毛刺 / 提示不到位</span><span>☑ 勾选 = 您决定要修</span></div></div>
<div class="panel"><b>目录</b><div class="toc"><div class="b">新发现（高 ${sevCount['高']} · 中 ${sevCount['中']} · 低 ${sevCount['低']}，共 ${findings.length} 条）</div>${tocNew || '<span class="note">（本轮无新发现）</span>'}<div class="b">本轮已落地（3 项，无需决策）</div>${fixed.map((f) => `<a href="#${f.id.toLowerCase()}">${f.id} ${f.title.slice(0, 40)}…</a>`).join('')}<div class="b">遗留复验（亲验仍在 8 · 继承仍在 4）</div><a href="#recheck">见下方复验总表</a></div></div>

${findings.length ? findings.map(card).join('\n') : '<div class="panel"><span class="note">本轮四位专家走查 + 主会话交叉核验后，没有需要您裁决的新发现问题。</span></div>'}

<div class="panel" style="margin-top:26px"><b>本轮已落地（无需决策，供追溯）</b><div class="note" style="margin-top:6px">round16 勾选修复、连接状态指示器新需求、round17 代码审查与重构——均已通过自动化回归与生产构建。</div></div>
${fixedCards}

<div class="panel" id="recheck" style="margin-top:26px"><b>遗留问题定向复验（本轮逐条验证）</b>
<table class="recheck">
<tr><th style="width:230px">条目</th><th style="width:110px">状态</th><th>结论与证据（本轮亲验/继承说明）</th></tr>
${recheckRows(recheck['亲验仍在'], '', '仍在（亲验）')}
${recheckRows(recheck['继承仍在'], '', '仍在（继承）')}
</table>
<div class="note" style="margin-top:8px">「亲验」= 本轮主会话重新读码确认；「继承」= round16 复验后代码未再改动，结论沿用。如需连同遗留条目一起修，可在底部勾选区输入对应编号（如"round15-A1、H5"）。</div></div>

</div>
<div class="picker"><div class="inner">
<h4>您勾选了 <span class="count">0</span> / ${findings.length} 个新发现问题（刷新后保留）</h4>
<div class="hint">在上方卡片勾选要修的条目；如需连同遗留条目一起修，可在下方输入框追加（如"round15-A1、H5、round13-N15"）。</div>
<ul id="picked"></ul>
<input id="extra" type="text" placeholder="补充要修的遗留条目号（可选）" style="margin-top:8px;width:100%;max-width:520px;padding:6px 10px;border-radius:6px;border:1px solid #454d5e;background:#282f3d;color:#fff;font-size:13px" />
</div></div>
<script>
const KEY = 'round17-review-picks'
const boxes = [...document.querySelectorAll('.issue input[type=checkbox]:not(:disabled)')]
const picked = document.querySelector('#picked')
const count = document.querySelector('.count')
const extra = document.querySelector('#extra')
let saved = {}
try { saved = JSON.parse(localStorage.getItem(KEY) || '{}') } catch {}
function render() {
  const on = boxes.filter(b => b.checked)
  count.textContent = on.length
  picked.innerHTML = on.length ? on.map(b => '<li>' + b.dataset.id + '</li>').join('') : '<li class="none">（尚未勾选任何问题）</li>'
  saved.__extra = extra.value
  try { localStorage.setItem(KEY, JSON.stringify(saved)) } catch {}
}
boxes.forEach(b => { b.checked = !!saved[b.dataset.id]; b.addEventListener('change', () => { saved[b.dataset.id] = b.checked; render() }) })
if (saved.__extra) extra.value = saved.__extra
extra.addEventListener('input', render)
render()
</script>
</body>
</html>`

writeFileSync('../docs/usability-review-2026-09-25-round17.html', html)
console.log('written: docs/usability-review-2026-09-25-round17.html,', findings.length, 'findings')
