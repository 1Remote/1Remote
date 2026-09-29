// 生成 round16 易用性/功能性评审汇总 HTML 报告（数据来自 4 个评审专家走查 + 主会话逐条读码交叉核验）。
// 跑完保留在 scripts/（报告数据内联，重跑可再生成）。
import { writeFileSync } from 'node:fs'

// ---------- 新发现（V 系列 = round16；专家发现 · 主会话交叉核验确认） ----------
// fig 为三框示意图的行数据；sev: 高/中/低
const findings = [
  {
    id: 'V1',
    sev: '中',
    who: '评审员 A（主列表与树域）发现 · 主会话读码核验确认',
    title: '拖住表头右缘调整列宽，松手后可能同时误触发这一列的排序切换',
    loc: 'webui/src/components/ServerTable.vue:919-947（四列的列宽热区）、815-828（拖拽处理）',
    desc: '用户拖动列头右缘的竖线来调整列宽，松开鼠标后列表顺序突然变化、列头出现排序箭头——像是"调个宽度把排序也改了"。列宽热区上已经特意阻止了双击事件的冒泡（防止"双击重置列宽"连切两次排序），但单击事件的冒泡漏了阻止，松手后浏览器补发的单击会一路冒泡到列头格子，正好命中格子本身绑定的"点击排序"。',
    repro:
      '1) 在「名称」列头右缘按住鼠标左键；2) 横向拖动几十像素调整列宽；3) 松开鼠标。预期只有列宽变化；实际列头亮起升序箭头，整个列表按名称重新排序。',
    why: '列宽热区（resizer）只写了阻止双击冒泡（@dblclick.stop），没有写阻止单击冒泡（@click.stop）；拖拽过程用浏览器的"指针捕获"机制把事件重定向到热区，松手后浏览器派发的单击事件以热区为起点向上冒泡，触发了列头格子的切换排序函数。四个可排序列（名称/地址/协议/最近连接）同构，全部受影响。',
    fix: '给四处列宽热区补上 @click.stop（与已有的 @dblclick.stop 成对），约四行改动，无副作用。',
    fig: 'sort-click',
  },
  {
    id: 'V2',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认（与 WPF 逐行对照）',
    title: '凭据在「手动输入」与「从凭据库选择」之间来回切换后，原来的库凭据引用悄悄丢失',
    loc: 'webui/src/components/editor/EditorDrawer.vue:136-140 · webui/src/editor/credentialMode.js · 对照 WPF Ui/View/Editor/Forms/Utils/CredentialViewModel.cs:227,254',
    desc: '编辑一台"凭据来自凭据库"的服务器时，如果点了「手动输入」再点回「从凭据库选择」，凭据下拉框是空的。用户若没意识到要重选就直接保存，这台服务器的凭据引用会以空字符串写进数据库，下次连接不再自动带凭据（要手动输密码）。桌面版（WPF）没有这个问题：切换时会先把旧选择记在缓存里，切回凭据库页签时自动恢复。',
    repro:
      '1) 编辑一台凭据来自凭据库的 SSH 服务器；2) 凭据组里点「手动输入」；3) 再点回「从凭据库选择」；4) 不重选，直接按 Ctrl+S 保存；5) 重新打开编辑器：凭据引用已经丢了。',
    why: '网页版切到「手动」时把引用字段清成空串（这半边与 WPF 一致），但 WPF 在清空前会把旧选择存进 _credentialNameCache，切回时按缓存恢复选中项；网页版的切回分支没有任何恢复逻辑，空下拉配空值，一保存就生效。',
    fix: '抽屉里暂存"上一次的库引用"（切往手动前备份），切回凭据库且当前为空时自动恢复；至少也要在凭据库模式下引用为空时给一行醒目提示「原引用已清空，请重新选择」。',
    fig: 'cred-lose',
  },
  {
    id: 'V3',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 主会话读码核验确认',
    title: '多行参数各自的取值表共用同一个问题上报键：修好一行的半行会"替"另一行解除保存拦截，数据静默丢失',
    loc: 'webui/src/editor/saveIssues.js:34-40 · webui/src/components/editor/KvMapField.vue:83-90 · webui/src/components/editor/SubformList.vue:142-151',
    desc: 'APP 协议的参数列表里，每一行参数可以各带一张"取值表"（键值对）。如果某行的取值表里留了半行（只填了值没填名），保存会被正确拦下；但只要随后把另一行的半行修好，第一行的半行就不再被拦——保存时被静默丢弃。更糟的是这种半行本来就不进数据，问题表清空后连"未保存的更改"确认都不会弹，直接关抽屉输入就蒸发了。',
    repro:
      '1) 新建 APP 服务器，参数列表加两行，类型都选"下拉选择"；2) 两行各自的取值表里各加一行"只填值、不填名"；3) 点保存——被拦（正确）；4) 把第 2 行那张表的半行删掉修好；5) 再点保存——放行，第 1 行的半行被丢弃。',
    why: '保存问题总线按"字段键"记账（有问题记一笔、没问题删一笔），但子表单里每行的取值表组件向上报的都是同一个键（都叫 Selections）。后报告"我没问题"的那一份会把整条记录删掉，先报告"我有半行"的那一份就被覆盖了。组件卸载时也会报一次"没问题"，同样会误删别人的记录。',
    fix: '上报键带上行身份（例如"字段键#行号"），让每行的报告互不干扰；或把"没问题"的语义从"删整条"改为"只撤销自己那条"。',
    fig: 'shared-key',
  },
  {
    id: 'V4',
    sev: '中',
    who: '评审员 C（设置与凭据库域）发现 · 主会话读码核验确认（与 WPF 逐行对照）',
    title: '添加运行器后，该协议的「默认运行器」被悄悄改成这个还没配置的新运行器（WPF 不会这样改）',
    loc: 'webui/src/components/settings/RunnerGroup.vue:192-198 · 对照 WPF Ui/View/Settings/ProtocolConfig/ProtocolRunnerSettingsPageViewModel.cs:107-117',
    desc: '在某个协议（如 SSH）下点「+ 添加运行器」只想新建一张卡慢慢填程序路径，保存的瞬间该协议的「默认运行器」下拉就被悄悄指向这个可执行文件还是空白的新运行器并立即落盘。之后这个协议的新连接会默认走它，可能直接报"找不到程序"。用户很难想到原因是"刚添加了一个运行器"。WPF 旧版添加只把新运行器放进列表，默认运行器保持不变。',
    repro:
      '1) 设置 → Runner → SSH 页签，确认默认运行器是 PuTTY；2) 点「+ 添加运行器」；3) 输入名字保存；4) 观察「默认运行器」下拉已变成新名字（无需任何额外操作）。',
    why: '网页版把"选中新卡便于继续配置"实现成了"把新卡名字写进默认运行器字段"，而保存的是整份配置对象，选中态随之落盘。WPF 里列表的选中只是查看焦点，与"默认运行器"是两回事，只有用户主动点选才写入。',
    fix: '添加后不写默认运行器字段，仅保留已有的"滚动入视野 + 3 秒高亮"定位效果；若确需切换默认值，弹一次提示「已将默认运行器切换为 X」让用户知情。',
    fig: 'runner-default',
  },
  {
    id: 'V5',
    sev: '中',
    who: '评审员 D（全局壳层与需求符合度域）发现 · 主会话读码核验确认',
    title: '设计文档与测试指南定案的「Ctrl+K 聚焦搜索」快捷键从未实现，文档与代码各说各话',
    loc: 'docs/superpowers/specs/2026-09-15-web-ui-redesign-design.md:45,54,154 · Tests/WebUI/README.md:231（TC-KBD-07M）· webui/src/App.vue:53-75（注释自述"无 Ctrl+K"）',
    desc: '设计文档把「Ctrl K 聚焦搜索」写进了布局图和快捷键表的第一条，测试指南的人工冒烟用例也写着它；但代码里这个快捷键根本不存在——源码注释明确写着"快捷键仅此一条，无 Ctrl+K"。用户按 Ctrl+K 没有任何反应（普通浏览器直开时还会跳去地址栏）。谁按测试指南冒烟，谁就会把这条记成缺陷。这不是评审员把定案误当问题——恰恰是这条定案从未落地。',
    repro:
      '1) 打开 Web 界面；2) 按 Ctrl+K；3) 无任何反应；对照 README §6.3 TC-KBD-07M 行写着"Ctrl+K 聚焦搜索"应有反应。',
    why: '早期修复批次已经发现实现里只有 Ctrl+F，把搜索框占位文案里的"Ctrl K"字样删掉了（现在文案只写 Ctrl+F），但设计文档的快捷键表和测试指南的用例行没有同步删，形成"文案说 Ctrl+F、文档说 Ctrl+K、代码只有 Ctrl+F"的三方不一致。',
    fix: '请从两个方向里选一个并同步全部文档：(a) 补实现——在全局按键处理里加 Ctrl+K 分支，行为与 Ctrl+F 完全相同（聚焦并全选搜索框），约三行改动；(b) 拍板砍掉 Ctrl+K——同步修订设计文档快捷键表与测试指南对应行（文档末尾提到的"内联命令面板"若还想要，应另立需求单独排期）。',
    fig: 'ctrlk-doc',
  },
  {
    id: 'V6',
    sev: '低',
    who: '评审员 A 发现 · 主会话读码核验确认',
    title: '文件夹行单击没有任何选中反馈，与服务器行、资源管理器直觉不一致',
    loc: 'webui/src/components/FolderRow.vue:47-59',
    desc: '同一个列表里，单击服务器行会出现高亮外框（光标落位），单击文件夹行却毫无反应——既不选中也不高亮，用户无法确认"点没点中"，只能靠双击试探。资源管理器的惯例是"单击=选中高亮、双击=进入"，文件夹行缺了前一半。',
    repro: '1) 进入含子文件夹的视图；2) 单击某文件夹行——无任何视觉反馈；3) 紧接着单击服务器行——出现亮框。对比明显。',
    why: '文件夹行的根元素只绑定了双击（进入）、右键和拖拽事件，没有单击处理；服务器行的单击会落光标。两类行行为不一致。',
    fix: '给文件夹行补一个纯视觉的选中态（复用光标外框样式或让悬停底色常驻），单击即亮；不必让它承担键盘光标职责。',
    fig: 'folder-click',
  },
  {
    id: 'V7',
    sev: '低',
    who: '评审员 A 发现 · 主会话读码核验确认',
    title: '右键菜单打开后滚动列表，菜单悬在原地既不跟随也不关闭，动作目标与视觉位置脱节',
    loc: 'webui/src/components/ServerTable.vue:595-598（只有鼠标按下才关菜单）、1090-1102（菜单定位）',
    desc: '右键某行弹出菜单后，用滚轮滚动列表，菜单留在屏幕原位，下面的行"从菜单底下穿过"；此时点菜单里的"连接"，实际连接的仍是当初右键的那一行——视觉上像是作用在菜单位置正下方现在的那行。资源管理器的惯例是滚动即收起菜单。',
    repro:
      '1) 右键第 1 行（A 服务器）弹出菜单；2) 滚轮向下滚 10 行，菜单仍悬在首屏；3) 此时菜单正下方已是 Z 服务器；4) 点"连接"——连的是 A。',
    why: '菜单是绝对定位在表格容器上的浮层，滚动发生在列表体内，两者互不相干；关闭监听只挂在"鼠标按下"上，滚轮不关菜单。菜单内容持有打开时的行快照，动作目标正确但视觉锚点已脱离。',
    fix: '给列表滚动区加滚动监听，滚动时顺手关闭行菜单（或让菜单跟随锚定行重新定位）。',
    fig: 'menu-scroll',
  },
  {
    id: 'V8',
    sev: '低',
    who: '评审员 A 发现 · 主会话读码核验确认',
    title: '右键菜单的禁用项缺少"为什么禁用"的悬停说明：左侧树全缺，列表侧的忙碌场景也缺',
    loc: 'webui/src/components/SideTree.vue:491-503 · 对照 ServerTable.vue:377-381',
    desc: '只读数据源或文件夹长操作进行中时，左侧树右键菜单里"新建/重命名/删除文件夹"整排灰掉，鼠标悬停没有任何解释；而列表侧同类菜单有"只读数据源"提示、行内按钮忙碌时也有"操作进行中"提示，唯独树菜单是哑的。另外列表侧空白菜单在长操作期间禁用时悬停说明也是空的（只判断了只读场景，漏了忙碌场景）。',
    repro:
      '1) 切换到只读数据源；2) 树节点上右键；3) 菜单项灰掉，悬停无任何文字。对比：列表空白处右键的同类禁用项有"只读数据源"气泡。',
    why: '树侧菜单按钮只绑了禁用状态没绑悬停说明；列表侧的说明函数里"数据源可写"就直接返回空串，把"忙碌中"这种情况也落进了空串分支。',
    fix: '树侧菜单补悬停说明（复用现有的"只读数据源"/"操作进行中"词条）；列表侧的说明函数增加忙碌分支。',
    fig: 'no-title',
  },
  {
    id: 'V9',
    sev: '低',
    who: '评审员 A 发现 · 主会话读码核验确认',
    title: '在表头上右键会弹出浏览器原生菜单，与全应用的自绘右键体系脱节',
    loc: 'webui/src/components/ServerTable.vue:388-390',
    desc: '在行上、表格空白处右键都是应用自己的菜单，唯独在表头（"名称""地址"等文字）上右键，弹出的是浏览器原生菜单（在应用内嵌浏览器环境下是"刷新/检查"一类），看起来像网页出错而不是应用功能。',
    repro: '1) 鼠标移到表头"协议"二字上；2) 右键；3) 出现浏览器原生菜单。',
    why: '空白菜单处理函数开头就把命中表头的事件原样放行（既不弹自绘菜单也不阻止浏览器默认行为），浏览器默认菜单于是出现。',
    fix: '表头右键至少阻止默认行为（吞掉浏览器菜单）；有余力可挂"重置列宽/列配置"快捷入口。',
    fig: 'thead-menu',
  },
  {
    id: 'V10',
    sev: '低',
    who: '评审员 C 发现 · 主会话读码核验确认（与 WPF 对照）',
    title: '凭据库列表看不出哪条凭据设了密码/私钥（WPF 有 ****** 指示列）',
    loc: 'webui/src/components/settings/CredentialVaultGroup.vue:307-336 · 对照 WPF Ui/View/Settings/CredentialVault/CredentialVaultView.xaml:72-108',
    desc: 'WPF 凭据库表格里，一条凭据是否设了密码、是否配了私钥，一眼可辨（******）。网页版列表没有这两列也没有替代指示，想知道哪条是"只有用户名没密码"的凭据，只能逐条点"编辑"看输入框是不是空的。列表接口其实已经返回了"是否有密码/是否有私钥"两个布尔标记，展示成本为零，只是没用上。',
    repro: '1) 设置 → 凭据库；2) 看列表：两条凭据外观完全一样，其中一条其实没设密码，无从区分。',
    why: '上一轮把列表上的"明文查看"收进编辑弹窗时，把 WPF 本就有的"存在性指示"（非明文的 ******）一并撤掉了，没有补替代展示。',
    fix: '在表格加一列（或用户名后加小徽标），用已有的布尔标记显示"密码/私钥"存在性（如"密码 · 私钥"小字徽标），不涉及任何明文。',
    fig: 'cred-flag',
  },
  {
    id: 'V11',
    sev: '低',
    who: '评审员 C 发现 · 主会话读码核验确认（与 WPF 对照）',
    title: 'WPF 有、网页版缺的两个设置项：日志文件位置、启动器检索引擎清单',
    loc: 'webui/src/components/settings/GeneralGroup.vue（无日志路径行）· LauncherGroup.vue（无检索引擎区块）· 对照 WPF GeneralSettingView.xaml:148-169、LauncherSettingView.xaml:75-149',
    desc: '①常规组：用户遇到问题想反馈日志时，在网页设置里找不到日志文件放在哪（WPF 有只读路径展示 + 一键打开所在文件夹）。②启动器组：WPF 可以勾选参与快速启动检索的引擎（每项含标题与说明），网页版只有开关和热键，启用启动器后无法调整检索引擎。两处都不在后端设置白名单里，属于整条链路未迁移，而不是前端漏画。',
    repro: '1) 设置 → 常规：逐行看，没有"日志文件路径"行；2) 设置 → 启动器：逐行看，没有检索引擎列表。',
    why: '后端设置接口的字段白名单没有收录这两项；白名单注释只声明了排除"便携模式、SQLite 路径"，未提这两项，看不出是有意排除还是遗漏，需要拍板。',
    fix: '①常规组加一行"日志文件位置"只读展示（后端补打开文件夹接口更好）；②若启动器仍以桌面窗口为主，可先在启动器组顶部加一句"检索引擎设置请在桌面端调整"的说明避免用户找不到，长期再补字段与列表。',
    fig: 'missing-setting',
  },
  {
    id: 'V12',
    sev: '低',
    who: '评审员 D 发现 · 主会话读码核验确认',
    title: '顶栏设置按钮是全顶栏唯一没有悬停提示的控件，更新红点出现时用户无从知道含义',
    loc: 'webui/src/App.vue:275-279',
    desc: '顶栏每个元素都有悬停说明，唯独 ⚙ 没有。它同时承担两件事：进设置、显示"有新版本"的红点。红点出现时悬停其上得不到任何解释（红点本体也没有提示），只能猜——猜不到就要点进设置翻到"关于"才明白。',
    repro: '1) 发布新版本后打开应用（或模拟更新标记）；2) ⚙ 右上角出现红点；3) 悬停 ⚙ 和红点——没有任何提示气泡。',
    why: '模板里设置按钮和红点两个元素都没绑悬停说明，而同排的 + 按钮绑了。属于绑定遗漏而非刻意设计。',
    fix: '给 ⚙ 加悬停说明：无更新时显示"设置"，有更新时显示"有新版本 {版本号}，点击查看"两档文案（新增词条走语言文件管线）。',
    fig: 'gear-title',
  },
  {
    id: 'V13',
    sev: '低',
    who: '评审员 D 发现 · 主会话实测确认（脚本数键 576/576）',
    title: '测试指南宣称的词条总数是 590，语言文件实际是 576（文档数字漂移）',
    loc: 'Tests/WebUI/README.md:6,292（写 590）· webui/src/locales/zh-CN.json 与 en-US.json（实测各 576 键）',
    desc: '测试指南头部基线和用例表都写"590 词条 × 14 语言"，但语言文件在上一轮成组删除死词条后已经是 576（源码注释与实测一致）。自动化测试按实际文件计数不会红，但新同事按 590 去人工核对会对不上数。',
    repro: '数 zh-CN.json 的词条数得 576；对照 README 基线段写的 590。',
    why: 'README 基线段定格在更早的轮次，删词条后没有回头更新这两处数字。',
    fix: '把 README 两处"590"改为 576，或去掉具体数字只保留"14 语言全量平价"的说法（数字会持续漂移，平价由自动化门禁保证即可）。属于纯文档修正，可随任何一轮顺带处理。',
    fig: 'count-drift',
  },
  {
    id: 'V14',
    sev: '低',
    who: '评审员 D 发现 · 主会话读码确认（场景为发布版手动输网址，属边缘场景，建议实测复核）',
    title: '浏览器直接打开发布版页面（没带访问令牌）时，页面误报「后端不可达，请确认应用正在运行」',
    loc: 'Ui/Service/WebUi/TokenMiddleware.cs:28-43 · webui/src/api/index.js:58 · webui/src/composables/useServers.js:34-40',
    desc: '发布形态下页面必须带访问令牌才能调接口；如果用户手动在浏览器输入地址打开页面（没带令牌），所有接口都返回"未授权"，而前端把任何请求失败一律当成"后端不可达"，显示"无法连接 1Remote 后端服务，请确认应用正在运行"——此时应用明明开着，用户照提示去检查却一无所获。正常从主程序进入永远带令牌，所以只影响手动输网址的高级用户与排障场景。',
    repro:
      '1) 启动发布版 1Remote；2) 在浏览器直接输入 http://127.0.0.1:端口（不带 token 参数）；3) 页面显示"后端不可达"。',
    why: '接口层检测到"未授权"时抛的是带专门标记的错误，但整个前端没有任何地方消费这个标记；加载失败分支不区分"未授权"和"网络错误"，统一置为断连态。',
    fix: '加载失败时识别"未授权"标记，空态换一句独立文案（如"会话无效，请从 1Remote 主程序重新打开页面"）；或维持现状但在帮助文档注明必须从主程序打开。',
    fig: 'token-401',
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
  const arrow = (x) => `<path d="M${x} 82 L${x + 42} 82" stroke="#8a92a0" stroke-width="2" marker-end="url(#ar16)"/>`
  const w = 226
  const parts = [
    box(10, w, ...boxes[0]),
    arrow(10 + w),
    box(10 + w + 52, w, ...boxes[1]),
    arrow(10 + (w + 52) * 2 - 10),
    box(10 + (w + 52) * 2, w, ...boxes[2]),
  ]
  return `<div class="fig"><div class="cap">${caption}</div><svg width="760" height="150" viewBox="0 0 760 150" role="img">${parts.join('')}<defs><marker id="ar16" markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 z" fill="#8a92a0"/></marker></defs></svg></div>`
}
const F = {
  'sort-click': () =>
    fig('调列宽 → 松手单击冒泡 → 排序被误切换', [
      [...YELLOW, '拖住列头右缘', ['移动几十像素', '调列宽']],
      [...YELLOW, '松开鼠标', ['浏览器补发单击', '从热区冒泡到列头']],
      [...RED, '排序被切换', ['列头亮起 ▲ 箭头', '整个列表重新排序']],
    ]),
  'cred-lose': () =>
    fig('来回切换 → 引用丢失 → 空值落库', [
      [...YELLOW, '点「手动输入」', ['库引用被清成空串', '（WPF 会先记缓存）']],
      [...YELLOW, '点回「凭据库」', ['下拉框是空的', '（WPF 按缓存恢复）']],
      [...RED, '直接保存', ['引用以空串落库', '下次连接要手输密码']],
    ]),
  'shared-key': () =>
    fig('共用一个上报键 → 修好一行替另一行解锁', [
      [...YELLOW, '两行取值表各留半行', ['保存被拦（正确）']],
      [...YELLOW, '修好第 2 行的半行', ['上报「我没问题」', '把共享记录删掉']],
      [...RED, '第 1 行不再被拦', ['半行保存时丢弃', '关抽屉连确认都不弹']],
    ]),
  'runner-default': () =>
    fig('WPF 只入列表 · web 添加即改默认', [
      [...GREEN, 'WPF：添加运行器', ['只放进列表', '默认仍是 PuTTY']],
      [...YELLOW, 'web：添加运行器', ['默认运行器被写为新卡', '并立即保存落盘']],
      [...RED, '新连接走空运行器', ['程序路径还是空白', '可能报「找不到程序」']],
    ]),
  'ctrlk-doc': () =>
    fig('定案从未落地：文档与代码各说各话', [
      [...YELLOW, '文档写着', ['「Ctrl+K 聚焦搜索」', '（设计文档+测试指南）']],
      [...YELLOW, '代码注释写着', ['「无 Ctrl+K」', '实现只有 Ctrl+F']],
      [...RED, '用户按 Ctrl+K', ['毫无反应', '按指南冒烟即记缺陷']],
    ]),
  'folder-click': () =>
    fig('同类行单击反馈不一致', [
      [...GREEN, '单击服务器行', ['高亮外框 ✓', '确认点中了']],
      [...RED, '单击文件夹行', ['无任何反应 ✗', '不知道点没点中']],
      [...YELLOW, '用户只能', ['靠双击试探', '疑心文件夹行坏了']],
    ]),
  'menu-scroll': () =>
    fig('菜单悬在原地，动作目标与视觉脱节', [
      [...YELLOW, '右键 A 行', ['菜单弹出', '锚定在 A 行']],
      [...YELLOW, '滚轮滚 10 行', ['菜单仍悬在首屏', '下方已是 Z 行']],
      [...RED, '点「连接」', ['连的是 A', '视觉上像在连 Z']],
    ]),
  'no-title': () =>
    fig('禁用项没有"为什么禁用"的解释', [
      [...YELLOW, '只读源 · 树右键', ['三项全灰', '悬停无任何文字']],
      [...GREEN, '列表侧同类菜单', ['有「只读数据源」提示', '行内按钮有「进行中」提示']],
      [...RED, '体验不一致', ['树菜单是哑的', '用户不知道为何点不了']],
    ]),
  'thead-menu': () =>
    fig('表头右键漏拦浏览器默认菜单', [
      [...YELLOW, '表头上右键', ['命中表头直接放行', '不弹自绘菜单']],
      [...YELLOW, '浏览器默认行为', ['弹出原生菜单', '「刷新/检查」一类']],
      [...RED, '与应用风格脱节', ['行/空白右键都是自绘', '唯独表头破功']],
    ]),
  'cred-flag': () =>
    fig('存在性指示列缺失（WPF 有 ******）', [
      [...GREEN, 'WPF 列表', ['密码 ****** 列', '私钥 ****** 列', '一眼可辨']],
      [...RED, 'web 列表', ['两条凭据外观一样', '哪条没密码无从区分']],
      [...YELLOW, '想知道只能', ['逐条点「编辑」', '看输入框是否为空']],
    ]),
  'missing-setting': () =>
    fig('两处 WPF 设置未迁移到 web', [
      [...GREEN, 'WPF 常规组', ['日志文件路径行', '+ 打开所在文件夹']],
      [...YELLOW, 'web 常规组', ['只有日志级别', '找不到日志在哪']],
      [...RED, '启动器组', ['检索引擎清单整块缺失', '无法调整参与检索的引擎']],
    ]),
  'gear-title': () =>
    fig('唯一没有悬停提示的顶栏控件', [
      [...GREEN, '顶栏其它元素', ['+ 按钮、搜索框', '都有悬停说明']],
      [...RED, '⚙ 设置按钮', ['无悬停提示', '红点也无提示']],
      [...YELLOW, '红点出现时', ['含义只能猜', '要进设置→关于才明白']],
    ]),
  'count-drift': () =>
    fig('文档数字定格在旧轮次', [
      [...YELLOW, 'README 写', ['「590 词条 × 14」', '基线段+用例表两处']],
      [...YELLOW, '语言文件实数', ['576 键（脚本实测）', '删死词条后已是 576']],
      [...RED, '新同事核对', ['按 590 数对不上', '自动化不红（按实数计）']],
    ]),
  'token-401': () =>
    fig('未授权被误报为"后端不可达"', [
      [...YELLOW, '浏览器直开', ['手动输网址', '没带访问令牌']],
      [...YELLOW, '全部接口返回', ['401 未授权', '（接口层有专门标记）']],
      [...RED, '页面误报', ['「后端不可达」', '其实应用正开着']],
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
    ${F[f.fig]()}
  </div>`
}

const sevCount = { 高: 0, 中: 0, 低: 0 }
for (const f of findings) sevCount[f.sev]++
const tocNew = findings
  .map(
    (f) => `<a href="#${f.id.toLowerCase()}">${f.id} ${f.title.length > 42 ? f.title.slice(0, 42) + '…' : f.title}</a>`
  )
  .join('')

// ---------- 本轮已修复的用户报障回归（2026-09-24，报告发布后追加） ----------
const fixed = [
  {
    id: 'X1',
    title: '用户报障：设置返回后切文件夹，界面冻结不刷新（控制台报 __vnode / emitsOptions 错误）',
    loc: 'webui/src/components/TableToolbar.vue:57（修复）',
    desc: '面包屑行右侧的工具簇（批量条/提示行/列菜单）通过 Vue 的传送门（Teleport）渲染到 #crumb-actions 容器里，而该容器由主列表组件自己渲染——Vue 要求传送目标在传送发生前就已存在。首次进入主列表时首帧先显示骨架屏（表格组件不挂载），等数据到位再切到表格，此时容器早已就位，传送成功纯属侥幸；但从设置页返回等「整棵主列表重新挂载」的场景没有骨架屏间隔，容器与传送门在同一帧挂载，目标查询落空（控制台同时有 Invalid Teleport target 警告）→ 工具簇整块内容没有挂载，组件树里残留一批"从未挂载"的节点 → 此后主列表的任何更新（如切换文件夹）都会撞上这些失效节点，渲染任务崩溃死亡，界面从此冻结不刷新。',
    fix: '传送门改为「目标就绪前先不传送」：挂载完成后（同一帧内、用户看不到闪烁）拿到容器元素再传送。真实浏览器复现序列（进设置→返回→切文件夹）修复前必现崩溃、修复后零错误；已固化为冒烟用例 S24（README TC-LIST-37）。',
  },
]
const fixedCards = fixed
  .map(
    (f) => `  <div class="issue" id="${f.id.toLowerCase()}">
    <div class="issue-head"><input type="checkbox" data-id="${f.id}" data-title="${f.title}" checked disabled />
      <h3><span class="sev p1" style="background:#1e8e5a">已修复</span>${f.id} ${f.title}</h3></div>
    <span class="who">用户报障（2026-09-24）· 已修复并经真实浏览器复现验证</span>
    <span class="loc">${f.loc}</span>
    <dl><dt>问题说明</dt><dd>${f.desc}</dd><dt>修复</dt><dd>${f.fix}</dd></dl>
  </div>`
  )
  .join('\n')

// ---------- 定向复验的既往遗留（编号沿用历轮出处） ----------
const recheck = {
  已修复或消失: [
    [
      '后端 3 个基线失败',
      'README §9 记录的 GetGeneral_ReturnsAllTenFields 与 RdpConfigTests ×2 已全部转绿——本轮实测 dotnet test 207/207 全过（0 失败）。其一为测试断言过时（工作区已删过时白名单断言），其二为 RDP 空密码序列化回归（工作区 RdpConfig.cs 已改为空密码也写加密行）',
    ],
    [
      'WPF 有 web 缺的 4 个编辑器字段',
      'round7 审计时报告缺失的 AlwaysOpenInNewTabWindow（总在新窗口打开）、连接前/连接后脚本、per-connection runner（每连接运行器）、IsAutoAlternateAddressSwitching（自动备用地址切换）现已全部实现，且与 WPF 模型属性、默认值、批量白名单逐一对上——可从遗留清单销账',
    ],
    [
      'N17（保存在途 ✕/Esc）',
      'round15 X3 补齐批量保存中与取消一致的忽略口径，两半都已修——按 round15 报告预告转"已消失"',
    ],
    [
      'DatabaseCheckPeriod',
      '不成立：WPF 界面本就没有这个设置项（对应 XAML 段落整段被注释），web 不暴露与 WPF 现状一致，不是缺陷',
    ],
    [
      'about.tagline 英文残留',
      '14 个语言该词条全部是英文原句，但按现行规格"12 个生成语言允许回退英文、仅 zh-CN 缺译算缺陷"，zh-CN 已在豁免白名单内——规格内不算新缺陷，仍等您裁决补译或确认为品牌语',
    ],
  ],
  仍在: [
    [
      'round15 新发现 16 条',
      '全部仍在原位（高 1 · 中 8 · 低 7，含 A1 批量删除锁与文件夹锁互不感知、C2 存量非 .db 数据源被锁死等）。本轮对 A1 亲自抽查确认代码未变；其中 3 条（round15-B2/B5/C1）本轮又被专家再次独立发现，佐证仍在。这 16 条等您勾选裁决，本轮报告不再重复展开',
    ],
    [
      'round13/14 遗留 30 条',
      'round15 复核为"仍在"的 30 条（R2/N9/N10/N11/N15/N16/N18/N21/N22/N23/N24/N25/U8/U9/U12/U13/U14/U18/U20/U22/U23/U24/U26/U27/U28/U29/R6 等）全部继承仍在——round15 后相关代码未再改动',
    ],
    [
      'H5 界面引擎单向门',
      '仍存在：WPF 设置页有"桌面/Web"双向切换下拉；Web 侧设置页无此行、后端设置白名单不收该键、前端全库无相关代码——切到 Web 后唯一回头路是手改配置文件。Web 版仍有占位功能（新窗口连接等），建议补切回入口',
    ],
    [
      'H23 键盘目标分裂（半边）',
      '部分修复：Enter 已与光标外框同源（连的就是亮框行）；但 E/Del/Ctrl+D/Menu 走另一条目标选取——勾选恰好 1 台时优先勾选行。复现场景：复选框勾 A 行 → 光标移到 B 行 → Del 删的是 A、Enter 连的是 B。源码注释仍标"遗留待裁决"',
    ],
    [
      'SSH 开 SFTP 后原行不变绿（批次7）',
      '读码证据链指向已修复（SSH 本体克隆仍注册进活动连接集，列表行应转绿；SFTP 克隆用临时 Id 不再干扰），但涉及真实桌面连接管线，建议实测确认后再销账',
    ],
    [
      '--danger 深色实底白字对比度',
      '仍约 3.02（低于正文可读标准 4.5）：影响面已收窄到一处——批量条删除按钮的悬停态。对话框红色确认按钮侧已修（对比度 4.83-6.47 全达标）。仍等您裁决：提亮红色或换深色文字',
    ],
    [
      '数据源手动"刷新/重连"入口',
      'WPF 数据源行首图标悬停可刷新（重连+从库重拉），web 卡片只有"测试连接"（不触发重连），数据同步靠后端约 10 秒定时器兜底。是否补手动入口仍待定夺',
    ],
  ],
}
const recheckRows = (list, cls, label) =>
  list.map(([id, note]) => `<tr class="${cls}"><td>${id}</td><td>${label}</td><td>${note}</td></tr>`).join('')

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>易用性 / 功能性多专家评审汇总 · 2026-09-23 round16</title>
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
  table.recheck tr.gone td { background:#f2fbf6; }
  svg text { font-family:"Segoe UI","Microsoft YaHei",sans-serif; }
</style>
</head>
<body>
<div class="wrap">
<h1>易用性 / 功能性多专家评审汇总 · 2026-09-23 round16</h1>
<div class="sub">2026-09-23 · 4 名专家分域走查（A 主列表与树 / B 编辑器 / C 设置与凭据库 / D 全局壳层与需求符合度）+ 每条发现由主会话逐条读码交叉核验（P1 全量复核、P2 逐条抽查证据）+ 既往遗留定向复验。新发现 14 条：中 5 · 低 9；无新增"高"级（数据丢失类）。另 3 条专家发现与 round15 报告已知问题重复（round15-B2/B5/C1），已归入遗留区不重复计。</div>
<div class="panel"><div class="legend"><span><span class="sev p1">高</span>数据丢失 / 功能明显损坏</span><span><span class="sev p2">中</span>功能不符直觉或反馈缺失，易造成误操作</span><span><span class="sev p3">低</span>体验毛刺 / 提示不到位</span><span>☑ 勾选 = 您决定要修</span></div></div>
<div class="panel"><b>总体结论（四域交叉核验后一致）</b><div class="note" style="margin-top:6px">
本轮无新增"高"级问题，问题面从 round15 的"新改动回归"收敛为长尾交互毛刺与文档漂移。中级的五条里，V1（调列宽误触排序）、V2（凭据切换丢引用）、V3（取值表共用上报键互解锁）都是"用户无感知丢数据/被绕过校验"类，建议优先；V4（添加运行器即改默认）是 web 版独有行为且立即落盘，与 WPF 不一致；V5（Ctrl+K 文档与实现各说各话）需要您拍板方向。遗留面上好消息居多：后端 3 个基线失败实测全部转绿（207/207）、round7 报告缺失的 4 个编辑器字段已全部实现、N17 按预告转已消失；round15 的 16 条新发现全部仍在原位等您勾选（其中 3 条本轮被专家再次独立发现，佐证仍在）。</div></div>
<div class="panel"><b>目录</b><div class="toc"><div class="b">新发现（中 5 · 低 9，共 14 条）</div>${tocNew}<div class="b">已修复的用户报障（1 条，无需决策）</div>${fixed.map((f) => `<a href="#${f.id.toLowerCase()}">${f.id} ${f.title.slice(0, 40)}…</a>`).join('')}<div class="b">既往遗留定向复验（已修复或消失 5 · 仍在 7）</div><a href="#recheck">见下方复验总表</a></div></div>

${findings.map(card).join('\n')}

<div class="panel" style="margin-top:26px"><b>已修复的用户报障（无需决策，供追溯）</b><div class="note" style="margin-top:6px">2026-09-24 用户测试中两次报障（切文件夹后界面不刷新 / 设置返回后界面冻结），经系统排查定位为传送门目标竞态，已当场修复并以真实浏览器复现验证 + 固化回归用例。</div></div>
${fixedCards}

<div class="panel" id="recheck" style="margin-top:26px"><b>既往遗留定向复验（本轮逐条验证）</b>
<table class="recheck">
<tr><th style="width:200px">条目</th><th style="width:110px">状态</th><th>结论与证据</th></tr>
${recheckRows(recheck['已修复或消失'], 'gone', '已修复或消失')}
${recheckRows(recheck['仍在'], '', '仍在')}
</table>
<div class="note" style="margin-top:8px">round15 报告的 16 条新发现（高 1 · 中 8 · 低 7）与 round13/14 的 30 条"仍在"项全部原位保留：round15 报告生成后相关代码未再改动，本轮对唯一"高"级 A1 亲自抽查确认。那 16 条的卡片、复现步骤与示意图见上一轮报告（docs/usability-review-2026-09-23-round15.html，可由 webui/scripts/gen-review-report.mjs 重新生成），如需把其中若干条并入本轮修复，直接在本报告底部勾选区标注"round15-条目号"即可。</div></div>

</div>
<div class="picker"><div class="inner">
<h4>您勾选了 <span class="count">0</span> / ${findings.length} 个新发现问题（刷新后保留）</h4>
<div class="hint">在上方卡片勾选要修的条目；如需连同 round15 的遗留条目一起修，可在下方输入框追加（如"round15-A1、round15-C2"）。</div>
<ul id="picked"></ul>
<input id="extra" type="text" placeholder="补充要修的 round15 条目号（可选）" style="margin-top:8px;width:100%;max-width:520px;padding:6px 10px;border-radius:6px;border:1px solid #454d5e;background:#282f3d;color:#fff;font-size:13px" />
</div></div>
<script>
const KEY = 'round16-review-picks'
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

writeFileSync('../docs/usability-review-2026-09-23-round16.html', html)
console.log('written: docs/usability-review-2026-09-23-round16.html,', findings.length, 'findings')
