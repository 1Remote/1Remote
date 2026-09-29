// 生成 round15 易用性/功能性评审汇总 HTML 报告（数据来自 4 个评审 agent 的交叉结论）。
// 跑完即保留在 scripts/（报告数据内联，重跑可再生成）。
import { writeFileSync } from 'node:fs'

// ---------- 新发现（A/B/C 三域走查，B1 已当场修复移入回归区） ----------
const findings = [
  {
    id: 'A1',
    sev: '高',
    who: '评审员 A（主列表与树域）发现 · 交叉核验确认',
    title: '批量删除的执行锁与文件夹操作的忙碌锁互不感知，两路保存并发会互相覆盖（文件夹结构可能丢失）',
    loc: 'webui/src/views/ServerListView.vue:489-536 · webui/src/composables/folderOps.js:38-53',
    desc: '批量删除执行期间（batchDeleting=true），文件夹的重命名/删除/移动/拖拽入口照常可用；反过来文件夹长操作执行期间，批量条「删除」按钮也不禁用。两路操作都会把整份文件夹字典全量保存到后端，并发时后完成的一方会用旧内容覆盖先完成的一方——结果可能是：刚重命名的文件夹在刷新后变回旧名字，或刚删掉的空文件夹又"复活"。',
    repro:
      '1) 勾选若干台服务器点「删除」并确认（进度提示滚动中）；2) 立即右键任一文件夹选「重命名」并确认；3) 两路进度都结束后刷新页面：重命名丢失或删除的文件夹复活（随两路保存的完成顺序而定）。',
    why: '两把锁各自独立：批量删除锁只禁用批量条按钮，文件夹忙碌锁只禁用树/列表的文件夹操作入口，两者互不检查对方；而保存是"整份字典全量覆盖"，并发写入没有冲突检测，纯以最后写入者为准。',
    fix: '统一锁源：批量删除执行期直接借用文件夹操作的模块级忙碌锁（或两者取"或"后接到所有入口），让批量删除执行期间文件夹操作按钮同步禁用，反之亦然。',
    fig: 'lock-split',
  },
  {
    id: 'A2',
    sev: '中',
    who: '评审员 A 发现 · 交叉核验确认',
    title: '状态栏数据源点在窗口变窄时不会收进「+N」，而是五个点互相挤压、名称变成省略号',
    loc: 'webui/src/views/ServerListView.vue:259-268, 1206-1227',
    desc: 'round14 的"放不下才折叠"机制靠检测内容宽度是否超出容器，但数据源点的名称区域允许被压缩（省略号截断），所以无论窗口多窄，五个点都"塞得下"，检测永远不触发。实际表现：窗口较窄时五个点全部保留、名称全部压缩成"●DataSour…"式省略号，「+N」在宽度维度上永不出现；只有数据源总数超过 5 个时才会出现「+N」（数量维度正常）。',
    repro:
      '配置 5 个长名字数据源（每名 20 字符以上），把窗口缩到约 700 像素宽：状态栏 5 个点全部保留、名称互相挤压出省略号，不出现「+N」；继续缩窄仍是 5 个挤压点。',
    why: '溢出检测看的是"内容总宽是否超过容器"，但弹性布局允许子项收缩，宽度不足被转成了"子项内部截断"，不会产生真实溢出，检测因此失效。',
    fix: '给数据源点设置"禁止收缩"（flex: 0 0 auto），让宽度不足真实转化为容器溢出，检测随之生效；或改为逐个累加子项实际宽度与容器宽度比较。',
    fig: 'shrink',
  },
  {
    id: 'A3',
    sev: '中',
    who: '评审员 A 发现 · 交叉核验确认',
    title: '空文件夹删除确认框打开期间被另一端放入服务器，确认后仍按"空文件夹"执行，内容被静默搬到上级',
    loc: 'webui/src/views/ServerListView.vue:498-535 · folderOps.js:456-476',
    desc: '批量删除前会检查"勾选的文件夹确实还是空的"，但这个检查发生在确认框弹出之前；确认框打开期间如果另一端（桌面端或另一个网页）往该文件夹移入了服务器，点「删除」确认时仍按打开确认框时的旧快照执行——把刚放进去的服务器静默搬到上级文件夹，然后删除文件夹。这违背了"承诺删空壳、不搬内容"的既有设计。',
    repro:
      '1) 勾选一个空文件夹，点批量条「删除」弹出确认框；2) 保持确认框不动，在另一端往该文件夹移入一台服务器；3) 回来点「删除」确认——该服务器被静默移到上级文件夹，文件夹被删。',
    why: '确认框打开时的快照被直接用于执行，确认与执行之间没有再次校验；显示侧（勾选列表）会自动剔除已非空的文件夹，但执行侧用的是旧数据，两边不同源。',
    fix: '执行删除前（确认回调里或逐个删除循环内）用同一"是否仍为空"的判定再查一次，已非空的跳过并在结果提示中说明"已跳过 N 个非空文件夹"。',
    fig: 'stale-snapshot',
  },
  {
    id: 'A4',
    sev: '低',
    who: '评审员 A 发现 · 交叉核验确认',
    title: '启动时的语言同步请求晚归会覆盖用户刚切换的语言：当下界面与已保存值不一致',
    loc: 'webui/src/App.vue:98-104 · ServerListView.vue:287-295',
    desc: '页面启动时会异步读取后端保存的语言并应用。如果用户在页面刚打开的瞬间（数百毫秒内）点了语言切换：界面先切到新语言并保存到后端，随后启动请求带着旧语言返回，把界面又弹回旧语言——结果是当下显示旧语言、后端已存新语言，重启后语言又变一次，两头对不上。',
    repro:
      '1) 后端语言为英文，浏览器默认中文；2) 刷新页面后立刻点状态栏语言钮切英文；3) 界面先变英文，随后闪回中文（启动请求返回旧值），但后端已存英文；4) 重启后界面是英文。',
    why: '启动同步对"用户是否已手动切换过"没有标记，晚归的响应无条件覆盖当前语言。',
    fix: '语言模块加一个"用户已手动切换"标志，手动切换后置位；启动请求返回时只在标志未置位时才应用。',
    fig: 'race',
  },
  {
    id: 'A5',
    sev: '低',
    who: '评审员 A 发现 · 交叉核验确认',
    title: '批量删除的进度提示只数成功数：连续失败时进度长时间不动疑似卡死；混合批次会叠两根进度条',
    loc: 'webui/src/views/ServerListView.vue:540-564 · folderOps.js:399,461',
    desc: '逐台删除的进度文案只随"成功台数"推进——若某台持续失败，进度会停在原地数秒，长列表下用户会误判为卡死；失败台数在结束前完全不可见。另外混合批次（服务器+空文件夹）会先后弹两根进度条，第一根的结果提示还没消失时第二根又叠在下面。',
    repro:
      '1) 勾选 5 台，其中 1 台所在数据源已故障；2) 批量删除：进度在故障台前后停顿数秒；3) 勾选 2 台服务器+2 个空文件夹混合删除：两根进度条先后叠放。',
    why: '进度推进数只传了成功数（同项目的文件夹移动用的是"已处理数"口径，两处不一致）；混合批次的两个执行体各自创建进度提示，没有统一宿主。',
    fix: '进度推进改为"成功+失败"的已处理数；混合批次合并为一根进度条（总数含两类），或第二根等第一根结束后再创建。',
    fig: 'progress',
  },
  {
    id: 'B2',
    sev: '中',
    who: '评审员 B（编辑器域）发现 · 交叉核验确认',
    title: '子表单（备用凭据）行内必填未填时无定位：折叠行看不到红标，强点保存的聚焦还会落错行',
    loc: 'webui/src/composables/useRequiredGuard.js:54-63 · SubformList.vue:66,142',
    desc: '备用凭据的行默认折叠，行内的红色必填标记只在展开时才渲染。强点保存时页面滚到备用凭据区域，但①折叠行没有任何错误指示，用户不知道哪一行缺名称；②如果恰好有别的行已展开，聚焦会落到第一展开行的输入框——聚焦到已经填好的字段，反而误导。',
    repro:
      '编辑 RDP 服务器 → 备用凭据添加两行 → 第一行填好名称并折叠，第二行只填密码不填名称再折叠 → 点保存：页面滚到备用凭据区并聚焦第一行的名称（已填），折叠行无任何标记。',
    why: '定位键带行号在页面中没有对应元素，回退逻辑取整个子表单区域的第一个输入框，既不区分行也不会展开折叠行。',
    fix: '强点保存遇到行内缺失时先自动展开目标行再聚焦；至少给折叠行标题加错误标记（红点/红字）并把缺失项清单放到该区域附近。',
    fig: 'collapsed-row',
  },
  {
    id: 'B3',
    sev: '中',
    who: '评审员 B 发现 · 交叉核验确认',
    title: '保存被拦时的提示文案仍引导用户去看"表单顶部横幅"，但该横幅本轮已删除',
    loc: 'webui/src/locales/zh-CN.json:497 · EditorDrawer.vue:97',
    desc: '必填未填强点保存时的弹出提示文案是「有 {n} 项需要修正，请检查表单顶部提示」——而顶部的必填提示横幅已在本轮删除，用户按文案指引去找必然扑空。实际的引导机制已改为"聚焦+警告色高亮"，文案与机制脱节。',
    repro: '新建服务器，清空名称 → 点保存 → 提示说去顶部找 → 顶部没有任何横幅。',
    why: '删除横幅时没有同步更新这条文案。',
    fix: '按语言文件管线改文案（如「有 {n} 项需要修正，已高亮未填字段」）并重新生成 14 语言文件；顺带清理已无目标的"滚动到横幅"逻辑。',
    fig: 'stale-copy',
  },
  {
    id: 'B4',
    sev: '低',
    who: '评审员 B 发现 · 交叉核验确认',
    title: '批量编辑里键值行"只填值没填名"的问题定位失效：字段查找大小写对不上，聚焦跳过',
    loc: 'webui/src/components/editor/BulkEditForm.vue:199',
    desc: '批量编辑时"额外指令"等键值行只填值不填名，保存会被正确拦下，但缺失项的字段查找用小写开头的键去比对大写开头的自报键，永远对不上——相关标签退化为显示英文原始键名；又因该类字段不在必填定位体系里，聚焦引导也不发生，只剩计数提示。',
    repro:
      '批量勾选 RDP 服务器 → 高级设置把「额外指令」切到覆盖 → 只填值不填属性名 → 保存：计数正确，但看不到是哪个字段的问题，聚焦也不发生。',
    why: '行编辑器按数据库字段原样（大写开头）自报问题，批量表单的查找表按接口字段（小写开头）建立，两套命名在接线时没有对齐。',
    fix: '查找时同时按两种命名形态匹配；并给键值类字段挂上定位属性以支持聚焦。',
    fig: 'case-mismatch',
  },
  {
    id: 'B5',
    sev: '低',
    who: '评审员 B 发现 · 交叉核验确认',
    title: '必填警告边框没有覆盖数字输入框和带候选的输入框：高亮观感割裂',
    loc: 'webui/src/components/editor/FormField.vue:352-383',
    desc: '强点保存后未填项会整行警告色高亮，但只有普通文本框和密码框带了警告边框；数字输入框（如端口）和带候选的输入框（如串口号）漏接——这些字段只有左侧标签变色，输入框本身无标记，同一屏里高亮观感不一致。',
    repro:
      '新建 Serial 服务器（串口号/波特率为空）→ 强点保存：名称/地址的输入框带警告边框，串口号/波特率的输入框无变化。',
    why: '接线时只给三类控件分支加了警告档，其余分支漏接。',
    fix: '数字分支补上警告档状态；带候选的输入组件增加警告支持并透传。',
    fig: 'partial-warn',
  },
  {
    id: 'C1',
    sev: '中',
    who: '评审员 C（设置与凭据库域）发现 · 交叉核验确认',
    title: '添加运行器弹窗按 Esc 直接丢弃已输入的名称，绕过了统一的"未保存更改"确认',
    loc: 'webui/src/components/settings/RunnerGroup.vue:223',
    desc: '添加运行器弹窗里点遮罩/✕/取消（有输入时）都会弹「未保存的更改」确认，唯独按 Esc 直接关闭、草稿无提示丢失——同一弹窗四条关闭路径行为分叉。统一关闭语义改造时漏掉了这个弹窗的 Esc 接线。',
    repro: '设置 → 运行器 →「+ 添加运行器」→ 输入名称 → 按 Esc：弹窗直接关闭，无确认；对比点遮罩会弹确认。',
    why: 'Esc 由父组件的统一处理器直接关闭弹窗，没有像同类弹窗一样走关闭守卫。',
    fix: '把 Esc 的关闭动作改为调用弹窗内部的关闭守卫（与遮罩/✕ 同一入口）。',
    fig: 'esc-bypass',
  },
  {
    id: 'C2',
    sev: '中',
    who: '评审员 C 发现 · 交叉核验确认 · round14 新校验引入',
    title: '路径必须 .db 的前端校验把存量非 .db 数据源锁死在编辑弹窗（连改名都保存不了）',
    loc: 'webui/src/components/settings/DataSourceGroup.vue:263',
    desc: '编辑已有的 sqlite 数据源时，前端把"路径必须以 .db 结尾"当成保存的硬条件，但后端本来只要求路径非空（.sqlite/.db3 等合法库文件在用）。存量数据源一打开编辑弹窗就被标红+保存钮真禁用——用户只想改个名字也无法保存，除非先去重命名数据库文件。',
    repro:
      '在桌面端建一个路径为 D:\\data\\mydb.sqlite 的数据源（后端接受、可正常连接）→ Web 设置→数据源→编辑该源：路径行标红，保存钮禁用，任何字段都改不了。',
    why: 'round14 新加的 .db 校验是纯前端加严，比后端更严格；编辑弹窗没有区分"路径未改动"（存量值豁免）与"新输入的路径"（校验）。',
    fix: '编辑弹窗中路径与打开时一致（未修改）时豁免 .db 校验，仅对新输入的路径校验；或放宽为与后端一致（非空即可，.db 仅提示）。',
    fig: 'legacy-locked',
  },
  {
    id: 'C3',
    sev: '中',
    who: '评审员 C 发现 · 交叉核验确认 · round14 接线疏漏',
    title: 'sqlite 路径为空时保存钮真禁用：统一的"假禁用+强点聚焦引导"不可达，红字文案也误导',
    loc: 'webui/src/components/settings/DataSourceGroup.vue:168',
    desc: '路径为空（=必填未填）与路径非法（非 .db）被合并进同一条真禁用分支：空路径时保存钮灰死点不动，"强点聚焦+警告高亮"的引导完全不发生；且输入框下的红字显示"路径必须指向 .db 文件"而不是"请填写"。与同批改造的凭据库/运行器弹窗（名称空=假禁+旁侧提示+强点聚焦）行为不一致。',
    repro:
      '设置→数据源→添加→类型 sqlite→路径留空：保存钮灰死点不动，旁侧显示「有必填项未填」但聚焦引导不发生，红字是"路径必须指向 .db 文件"。',
    why: '真禁条件把"空路径"与"非法路径"合在一个表达式里，必填未填场景被当作校验非法处理。',
    fix: '真禁条件中 sqlite 分支改为"路径非空且非 .db 才禁"；空路径交给必填守卫（假禁+聚焦），红字换成必填提示文案。',
    fig: 'empty-vs-invalid',
  },
  {
    id: 'C4',
    sev: '中',
    who: '评审员 C 发现 · 交叉核验确认 · round14 删行级明文查看的回归',
    title: '只读数据源的凭据失去了查看明文的能力（查看是读操作，本不依赖可写）',
    loc: 'webui/src/components/settings/CredentialVaultGroup.vue:321',
    desc: '上一轮把"查看明文"从列表行内删除、收敛到编辑弹窗内，但编辑按钮对只读数据源一直是禁用的——于是只读源（如数据库文件被设为只读）在网页端彻底无法查看凭据明文，而删除前的行内查看不受只读限制。',
    repro:
      '把某 sqlite 数据源的库文件设为只读属性 → 设置→凭据库→切到该数据源：行内仅剩的编辑/删除按钮全部禁用，无任何入口可查看密码明文。',
    why: '把唯一明文入口收敛进编辑弹窗时，没有同步放开只读源进入编辑（或提供只读查看形态）。',
    fix: '只读源开放编辑弹窗为"只读+可查看明文"形态（字段禁用、眼睛按钮保留、保存按钮隐藏）；或对只读源保留一个纯查看入口。',
    fig: 'readonly-blind',
  },
  {
    id: 'C5',
    sev: '低',
    who: '评审员 C 发现 · 交叉核验确认（round13 R6 原样）',
    title: '添加数据源弹窗切换类型后，上一类型的测试连接结果残留在表单底部',
    loc: 'webui/src/components/settings/DataSourceGroup.vue:108',
    desc: '先对 mysql 草稿点了「测试连接」（结果行显示成功/失败），再把类型切到 sqlite——mysql 的测试结果仍挂在表单底部，描述的是一个已不存在的配置，可能被误读为当前表单已通过测试。',
    repro: '添加数据源→类型 mysql→填参数→点「测试连接」出结果→类型切到 sqlite：底部仍显示刚才 mysql 的测试结果。',
    why: '类型切换处理只调整默认端口，不清测试结果状态。',
    fix: '类型切换时清空测试结果（编辑弹窗同款处理）。',
    fig: 'stale-result',
  },
  {
    id: 'C6',
    sev: '低',
    who: '评审员 C 发现 · 交叉核验确认',
    title: '凭据库切换数据源的一瞬间显示的是上一个数据源的凭据，且无加载指示',
    loc: 'webui/src/components/settings/CredentialVaultGroup.vue:292',
    desc: '切换数据源后到新列表到达前，表格照常渲染旧数据源的凭据行，没有任何"加载中"过渡——旧凭据短暂冒名顶替新数据源；此时点编辑会以新数据源的参数去请求旧条目名，触发"已被删除"的静默刷新，体验混乱。',
    repro: '在两个数据源间快速切换：切换瞬间表格仍是上一个数据源的凭据，无加载指示，约百毫秒后跳变为新列表。',
    why: '加载时不清空旧列表，加载提示的条件又要求列表为空才显示，非首次加载没有任何过渡态。',
    fix: '加载期间对表格加半透明/禁用过渡，或切换时先清空列表再加载。',
    fig: 'stale-list',
  },
  {
    id: 'C7',
    sev: '低',
    who: '评审员 C 发现 · 交叉核验确认',
    title: '外观组的强调色圆点悬停提示直出英文色名，未走多语言',
    loc: 'webui/src/components/settings/AppearanceGroup.vue:87',
    desc: '六个强调色圆点的悬停提示是 blue/violet/pink/orange/green/slate 原样英文，14 种界面语言下均显示英文色名；同页其它可本地化元素均已走词条。',
    repro: '界面语言切中文 → 设置→外观→悬停强调色圆点：提示为英文 blue 等。',
    why: '色名数组键直接兼作提示文案，没有映射到词条。',
    fix: '为六色补词条（走语言文件管线），提示改用词条。',
    fig: 'i18n-gap',
  },
]

// ---------- 本轮修复的回归（用户报告 + 实测发现，已修） ----------
const fixed = [
  {
    id: 'X1',
    title: '批量编辑「覆盖」按钮全部失效（用户报告）',
    loc: 'webui/src/components/editor/BulkEditForm.vue:196',
    desc: '上一轮把批量必填校验挂上响应式链后，一处旧笔误（对象键多套了一层）在渲染期抛出类型错误，中断了点击覆盖按钮后的整批界面更新——按钮看似全部失效。该笔误 round13 即潜伏在保存路径，round14 挂上响应式链后才在打开表单时爆发。',
    fix: '修正为正确的对象键读取；真实浏览器实测验证覆盖按钮恢复正常。',
  },
  {
    id: 'X2',
    title: '批量删除确认后什么都不执行（UI 冒烟实测发现）',
    loc: 'webui/src/views/ServerListView.vue:536',
    desc: 'round14 把执行锁的设置提前到确认回调，但删除函数内部保留了旧的"已在执行就直接返回"自查——确认回调刚设完锁就调删除函数，被自己拦下：确认删除后进度不出现、请求不发、按钮也不禁用。',
    fix: '删除函数内的自查（锁已由确认回调统一管理）；实测确认执行期批量条按钮全部禁用、删除正常执行。',
  },
  {
    id: 'X3',
    title: '服务器配置加载失败后抽屉彻底关不掉，用户被锁死在错误页（评审 B1/U28）',
    loc: 'webui/src/components/editor/EditorDrawer.vue:312',
    desc: 'round14 收编关闭守卫时把「加载失败」也当成了"忙碌"（忙碌时忽略一切关闭尝试）——加载失败是终态不是瞬态，结果是 ✕/取消/Esc/点遮罩全部无响应，唯一出路是刷新整个页面。',
    fix: '忙碌判定收窄为"保存中/批量保存中/加载中"三种瞬态；同时补齐批量保存中与取消按钮一致的忽略口径。实测加载失败态可正常关闭。',
  },
]

// ---------- 遗留复核（round13 报告 34 条逐条读码） ----------
const recheck = {
  已消失: [
    [
      'R1',
      '批量编辑半行拦截横幅显示未翻译键名——横幅本体随 round15 删除，未翻译文案不再出现在任何界面（查找错位代码仍在，若恢复逐项展示需先修）',
    ],
    ['N14', '子表单内嵌键值半行报错显示未翻译键名——同上，随横幅消失（根因代码仍在）'],
  ],
  减轻: [
    [
      'N13',
      '子表单行内必填：强点保存现在会滚动到子表单区域（round15 聚焦回退），但折叠行仍无错误指示、需手动展开——见本轮 B2',
    ],
    ['N17', '保存在途 ✕/Esc：单机模式已与取消按钮一致（忽略关闭）；批量保存在途已随本轮 X3 补齐'],
  ],
  仍在: [
    ['R2', '只剩键值半行时切换协议，半行无声丢失（EditorDrawer.vue:292 无脏检查）'],
    ['R6', '添加数据源切换类型后测试结果残留（=本轮 C5）'],
    ['N9', '超 500 行列表按 Menu 键，右键菜单钉在左上角（ServerTable.vue:691）'],
    ['N10', '空文件夹删除确认等待期内被填入服务器仍按空执行（单删路径；批量路径见本轮 A3）'],
    ['N11', '长操作中列表空白右键菜单各项禁用但无解释（ServerTable.vue:379）'],
    ['N15', 'Serial 新建默认值已预填，候选下拉只剩自身（SuggestInput.vue:34）'],
    ['N16', '协议切换后行内标红门控不复位，新协议表单直接标红（EditorDrawer.vue:292）'],
    ['N18', '启动器热键冲突时飞行中开关翻转被旧响应覆盖并写回（LauncherGroup.vue:93）'],
    ['N21', '运行器字号输入框清空即回弹旧值（RunnerCard.vue:110）'],
    ['N22', '标签管理重命名失败后输入框滞留无焦点无行内错误（TagManagerModal.vue:144）'],
    ['N23', '访问凭证失效被全屏「连接已中断」误报，401 无特判（api/index.js:58）'],
    ['N24', '导出全程无在途状态与防重入（ServerListView.vue:359）'],
    ['N25', '导入超 2 分钟前端报失败但后端仍在执行（api/index.js:216 默认超时）'],
    ['U8', '登录凭证失效后各处只报「加载失败」无统一引导（与 N23 同根）'],
    ['U9', '数据源根视图「文件夹」列整列显示占位符「—」（ServerRow.vue:59）'],
    ['U12', '纯键盘用户无法进入子文件夹（FolderRow/SideTree 无键盘通道）'],
    ['U13', '数据源分组加载中显示「还没有数据源」误导（DataSourceGroup.vue:441）'],
    ['U14', '凭据库加载失败时数据源切换器一起消失且无重试（CredentialVaultGroup.vue:293）'],
    ['U18', '运行器字号清空后显示为空但保存的仍是旧值（与 N21 同根）'],
    ['U20', '切换设置分组后内容区停留在上次滚动位置（SettingsView.vue:78）'],
    ['U22', '批量编辑清空数字字段提示文案误说「必填项未填」（BulkEditForm.vue:189）'],
    ['U23', '批量编辑超 50 台字段显示「未随列表提供」没说明原因（BulkEditForm.vue:210）'],
    ['U24', '图标本地上传没有大小限制，大图直接入库（IconPicker.vue:72）'],
    ['U26', '子表单行头标了按钮角色键盘却无法操作（SubformList.vue:137）'],
    ['U27', '批量编辑「备用凭据」组描述仍提到已隐藏的子表单（BulkEditForm.vue:290）'],
    ['U28', '编辑器加载失败后没有「重试」按钮——关闭锁死部分已随本轮 X3 修复，重试按钮仍缺'],
    ['U29', '数字字段输入非法值无即时提示（FormField.vue:352 无状态绑定——本轮 B5 同位）'],
  ],
}

// ---------- SVG 示意图（按卡片 fig 类型生成三框流程示意） ----------
function fig(kind, caption, boxes) {
  const [b1, b2, b3] = boxes
  const box = (x, w, fill, stroke, title, lines) => {
    const texts = [title, ...lines]
      .map(
        (t, i) =>
          `<text x="${x + 14}" y="${44 + i * 20}" font-size="12" fill="${i === 0 ? stroke : '#5a6270'}" ${i === 0 ? 'font-weight="700"' : ''}>${t}</text>`
      )
      .join('')
    return `<rect x="${x}" y="18" width="${w}" height="${texts.length * 20 + 36 > 110 ? texts.length * 20 + 36 : 110}" rx="8" fill="${fill}" stroke="${stroke}"/><text x="${x + 14}" y="0" />${texts}`
  }
  const arrow = (x, label) =>
    `<path d="M${x} 82 L${x + 42} 82" stroke="#8a92a0" stroke-width="2" marker-end="url(#ar15)"/><text x="${x + 21}" y="72" text-anchor="middle" font-size="10.5" fill="#8a92a0">${label}</text>`
  const w = 226
  const parts = [
    box(10, w, ...b1),
    arrow(10 + w),
    box(10 + w + 52, w, ...b2),
    arrow(10 + (w + 52) * 2 - 10),
    box(10 + (w + 52) * 2, w, ...b3),
  ]
  return `<div class="fig"><div class="cap">${caption}</div><svg width="760" height="150" viewBox="0 0 760 150" role="img">${parts.join('')}<defs><marker id="ar15" markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 z" fill="#8a92a0"/></marker></defs></svg></div>`
}

// ---------- SVG 示意图（三框流程模板；每条 [fill, stroke, 标题, [行...]]） ----------
const YELLOW = ['#fdf6e8', '#b9770e']
const RED = ['#fdf1ef', '#c0392b']
const GREEN = ['#eef8f2', '#1e8e5a']
function mkfig(caption, rows) {
  return fig('fig', caption, rows)
}
const F = {
  'lock-split': () =>
    mkfig('两把锁互不感知 → 并发全量保存互相覆盖', [
      [...YELLOW, '批量删除执行中', ['删除锁 = 开', '文件夹操作照常可用']],
      [...RED, '另一路同时在写', ['重命名/移动并发执行', '全量保存并发在途']],
      [...RED, '后写者覆盖先写者', ['旧基底不含新键', '文件夹结构丢失/复活']],
    ]),
  shrink: () =>
    mkfig('子项可收缩 → 溢出检测永远不触发', [
      [...YELLOW, '窗口缩窄', ['容器宽度不足']],
      [...YELLOW, '五个点各自收缩', ['名称压缩出省略号', '不产生真实溢出']],
      [...RED, '「+N」永不出现', ['溢出检测恒为假', '与设计预期不符']],
    ]),
  'stale-snapshot': () =>
    mkfig('确认前的快照被直接执行', [
      [...YELLOW, '确认框弹出', ['此刻文件夹为空（快照）']],
      [...YELLOW, '等待期间另一端', ['移入一台服务器', '显示侧已自动剔除']],
      [...RED, '确认后按旧快照执行', ['服务器被搬到上级', '文件夹被删']],
    ]),
  race: () =>
    mkfig('启动请求晚归覆盖用户切换', [
      [...YELLOW, '页面刚打开', ['启动读语言（异步）']],
      [...YELLOW, '用户立刻切换', ['界面=新语言', '并已保存到后端']],
      [...RED, '旧值晚归', ['界面弹回旧语言', '与已存值不一致']],
    ]),
  progress: () =>
    mkfig('进度只数成功 + 双进度条叠放', [
      [...YELLOW, '某台持续失败', ['进度停在 2/5', '数秒不动像卡死']],
      [...YELLOW, '混合批次', ['服务器段进度条未消失时']],
      [...RED, '文件夹段又叠一根', ['两根进度条同屏', '口径割裂']],
    ]),
  'collapsed-row': () =>
    mkfig('回退聚焦落错行', [
      [...YELLOW, '第 2 行缺名称', ['（已折叠）', '红标未渲染']],
      [...YELLOW, '强点保存', ['滚到子表单区域', '取区域第一个输入框']],
      [...RED, '聚焦到第 1 行', ['（名称已填好）', '误导用户']],
    ]),
  'stale-copy': () =>
    mkfig('文案指向已删除的横幅', [
      [...YELLOW, '强点保存', ['弹出提示']],
      [...YELLOW, '文案说', ['「请检查表单顶部提示」']],
      [...RED, '顶部什么都没有', ['横幅本轮已删除', '指引扑空']],
    ]),
  'case-mismatch': () =>
    mkfig('两套命名形态对不上', [
      [...YELLOW, '行编辑器自报', ['大写开头键', 'RdpControl…']],
      [...YELLOW, '批量表单查找表', ['小写开头键', 'rdpControl…']],
      [...RED, '永远匹配不到', ['标签退化为英文键名', '聚焦也跳过']],
    ]),
  'partial-warn': () =>
    mkfig('警告边框覆盖不完整', [
      [...GREEN, '文本/密码字段', ['标签警告色 ✓', '输入框警告边框 ✓']],
      [...RED, '数字/候选字段', ['标签警告色 ✓', '输入框无变化 ✗']],
      [...YELLOW, '同一屏观感', ['高亮割裂', '用户疑心漏检']],
    ]),
  'esc-bypass': () =>
    mkfig('同一弹窗四条关闭路径分叉', [
      [...GREEN, '遮罩 / ✕ / 取消', ['有输入 → 弹', '「未保存的更改」确认']],
      [...RED, '按 Esc', ['直接关闭', '草稿无提示丢失']],
      [...YELLOW, '根因', ['Esc 接线漏走', '关闭守卫']],
    ]),
  'legacy-locked': () =>
    mkfig('存量合法配置被新校验锁死', [
      [...YELLOW, '存量数据源', ['mydb.sqlite', '后端合法·在用']],
      [...YELLOW, 'Web 打开编辑', ['前端校验：非 .db', '标红 + 保存真禁']],
      [...RED, '改名也保存不了', ['除非先重命名', '数据库文件']],
    ]),
  'empty-vs-invalid': () =>
    mkfig('空路径与非法路径混进同一分支', [
      [...YELLOW, '路径为空', ['= 必填未填', '应走假禁+聚焦引导']],
      [...RED, '与非法路径同分支', ['保存钮真禁点不动', '引导不可达']],
      [...YELLOW, '红字误导', ['显示「必须 .db」', '而非「请填写」']],
    ]),
  'readonly-blind': () =>
    mkfig('只读源的明文查看入口消失', [
      [...YELLOW, '只读数据源', ['编辑/删除钮禁用', '（旧有行为）']],
      [...RED, '明文查看入口', ['上轮收敛进编辑弹窗', '编辑进不去']],
      [...RED, '查看能力归零', ['查看是读操作', '本不依赖可写']],
    ]),
  'stale-result': () =>
    mkfig('过期测试结果滞留', [
      [...YELLOW, 'mysql 草稿测试', ['结果行：连接成功']],
      [...YELLOW, '类型切到 sqlite', ['表单已是 sqlite 字段']],
      [...RED, 'mysql 结果仍在', ['被误读为', '当前配置已测过']],
    ]),
  'stale-list': () =>
    mkfig('切换瞬间旧列表冒名顶替', [
      [...YELLOW, '切换数据源', ['发起新请求']],
      [...YELLOW, '响应到达前', ['仍显示旧源凭据', '无加载指示']],
      [...RED, '此刻点编辑', ['新源参数请求旧条目', '静默刷新·混乱']],
    ]),
  'i18n-gap': () =>
    mkfig('色名未走多语言', [
      [...YELLOW, '悬停色点', ['提示 = blue/violet', '（原样英文）']],
      [...YELLOW, '界面语言', ['中文/日文/…', '均显示英文']],
      [...GREEN, '修复', ['补 14 语言词条', '提示走词条']],
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
const tocFixed = fixed.map((f) => `<a href="#${f.id.toLowerCase()}">${f.id} ${f.title}</a>`).join('')
const recheckRows = (list, cls, label) =>
  list.map(([id, note]) => `<tr class="${cls}"><td>${id}</td><td>${label}</td><td>${note}</td></tr>`).join('')

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>易用性 / 功能性多专家评审汇总 · 2026-09-23 round15</title>
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
  .fixed-badge { display:inline-block; background:var(--ok); color:#fff; border-radius:6px; font-size:12px; padding:1px 8px; margin-left:8px; vertical-align:2px; }
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
  table.recheck tr.mit td { background:#fdf9ef; }
  svg text { font-family:"Segoe UI","Microsoft YaHei",sans-serif; }
</style>
</head>
<body>
<div class="wrap">
<h1>易用性 / 功能性多专家评审汇总 · 2026-09-23 round15</h1>
<div class="sub">2026-09-23 · 3 名专家分域走查（A 主列表与树 / B 编辑器 / C 设置与凭据库+跨界面流）+ 每条发现独立读码交叉核验 + round13/14 遗留 34 条逐条复核 + 真实浏览器实测交叉印证。新发现 16 条：高 1 · 中 8 · 低 7；另修复 3 个本轮/上轮引入的回归（见「已修复回归」区）。</div>
<div class="panel"><div class="legend"><span><span class="sev p1">高</span>数据丢失 / 功能明显损坏</span><span><span class="sev p2">中</span>功能不符直觉或反馈缺失，易造成误操作</span><span><span class="sev p3">低</span>体验毛刺 / 提示不到位</span><span class="fixed-badge">已修复</span>本轮已修复的回归</span><span>☑ 勾选 = 您决定要修</span></div></div>
<div class="panel"><b>总体结论（三域交叉 + 实测印证后一致）</b><div class="note" style="margin-top:6px">
本轮三项用户指定修复（必填横幅删除+直接聚焦、提示垂直居中、批量覆盖按钮失效）已全部落实：覆盖按钮失效的根因是一处对象键读取笔误在响应式链上爆发（X1），实测修复后批量编辑恢复可用；必填引导经真实浏览器验证为"假禁用+旁侧提示+强点聚焦+警告高亮"完整闭环。但评审与 UI 冒烟实测又抓出两个 round14 引入的回归并已当场修复：批量删除确认后自锁不执行（X2）、加载失败后抽屉关不掉（X3）。遗留面上最重的是 A1：批量删除锁与文件夹操作锁互不感知，两路全量保存并发可互相覆盖（窗口窄但属数据丢失类）；C2/C3/C4 是 round14 新改动（.db 校验、明文收敛）对存量数据/只读源/空路径场景考虑不周的回归，建议优先裁决。round13 遗留 34 条中 2 条随横幅删除消失、2 条减轻、30 条仍在原位（R1/N14 的消失是被动的，错位代码仍在）。</div></div>
<div class="panel"><b>目录</b><div class="toc"><div class="b">新发现（高 1 · 中 8 · 低 7，共 16 条）</div>${tocNew}<div class="b">已修复回归（用户报告 + 实测/评审发现，本轮已修）</div>${tocFixed}<div class="b">round13/14 遗留复核（34 条：仍在 30 · 减轻 2 · 已消失 2）</div><a href="#recheck">见下方复核总表</a></div></div>

${findings.map(card).join('\n')}

<div class="panel" style="margin-top:26px"><b>已修复回归（无需决策，供追溯）</b><div class="note" style="margin-top:6px">三处均为 round13/14 改动引入、本轮任务执行中实测发现并当场修复，经真实浏览器（模拟后端接口的页面实测）与 204 条自动化用例回归验证。</div></div>
${fixed
  .map(
    (f) => `  <div class="issue" id="${f.id.toLowerCase()}">
    <div class="issue-head"><input type="checkbox" data-id="${f.id}" data-title="${f.title}" checked disabled />
      <h3><span class="fixed-badge">已修复</span>${f.id} ${f.title}</h3></div>
    <span class="who">真实浏览器实测 / 多专家评审交叉发现 · 已修复并回归</span>
    <span class="loc">${f.loc}</span>
    <dl><dt>问题说明</dt><dd>${f.desc}</dd><dt>修复</dt><dd>${f.fix}</dd></dl>
  </div>`
  )
  .join('\n')}

<div class="panel" id="recheck"><b>round13/14 遗留问题复核（34 条逐条读码）</b>
<table class="recheck">
<tr><th style="width:52px">编号</th><th style="width:64px">状态</th><th>现状说明（当前代码依据）</th></tr>
${recheckRows(recheck['已消失'], 'gone', '已消失')}
${recheckRows(recheck['减轻'], 'mit', '减轻')}
${recheckRows(recheck['仍在'], '', '仍在')}
</table>
<div class="note" style="margin-top:8px">R1/N14 的"已消失"是被动的：未翻译键名的查找错位代码仍留在代码里（BulkEditForm.vue:198 / EditorDrawer.vue:384），只是随横幅删除不再有渲染出口——若未来恢复任何逐项文案展示会立即复发，建议在下一轮清理。U28 的"关不掉"半边已随 X3 修复，"无重试按钮"半边仍在。N17 的单机半边已一致，批量半边已随 X3 补齐——下轮复核时应转"已消失"。</div></div>

</div>
<div class="picker"><div class="inner">
<h4>您勾选了 <span class="count">0</span> / ${findings.length} 个新发现问题（刷新后保留）</h4>
<div class="hint">在上方卡片勾选要修的条目；已修复的回归默认勾选且不可改。</div>
<ul id="picked"></ul>
</div></div>
<script>
const KEY = 'round15-review-picks'
const boxes = [...document.querySelectorAll('.issue input[type=checkbox]:not(:disabled)')]
const picked = document.querySelector('#picked')
const count = document.querySelector('.count')
let saved = {}
try { saved = JSON.parse(localStorage.getItem(KEY) || '{}') } catch {}
function render() {
  const on = boxes.filter(b => b.checked)
  count.textContent = on.length
  picked.innerHTML = on.length ? on.map(b => '<li>' + b.dataset.id + '</li>').join('') : '<li class="none">（尚未勾选任何问题）</li>'
}
boxes.forEach(b => { b.checked = !!saved[b.dataset.id]; b.addEventListener('change', () => { saved[b.dataset.id] = b.checked; try { localStorage.setItem(KEY, JSON.stringify(saved)) } catch {}; render() }) })
render()
</script>
</body>
</html>`

writeFileSync('../docs/usability-review-2026-09-23-round15.html', html)
console.log(
  'written: docs/usability-review-2026-09-23-round15.html,',
  findings.length,
  'findings +',
  fixed.length,
  'fixed'
)
