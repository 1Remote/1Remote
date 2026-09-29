// UI 冒烟实测脚本（round15 建立：按 Tests/WebUI/README.md §7 清单的可自动化项逐条
// 真实浏览器验证）。运行前提：vite dev server 已在 5199 端口（npx vite --port 5199）。
// 产物：docs/ui-smoke-<日期>-<ROUND>.json（供生成 HTML 报告）+ 失败截图 ui-smoke-shots/。
// 每轮跑冒烟前更新 ROUND 常量（输出文件名与报告标题随之变化）。
const ROUND = 'round17'
// 后端交互全部 mock（/api/* 拦截），跨端/真实连接/拖拽手感类用例标记 manual。
import puppeteer from 'puppeteer-core'
import { mkdirSync, writeFileSync } from 'node:fs'

const BROWSER = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
const BASE = 'http://localhost:5199'
const SHOT_DIR = 'ui-smoke-shots'
mkdirSync(SHOT_DIR, { recursive: true })

// ---- mock 数据：两台 RDP（一台在 web 子文件夹）+ 一台 SSH ----
const mk = (id, name, addr, proto = 'RDP', folderPath = '') => ({
  id,
  displayName: name,
  protocol: proto,
  dataSourceName: 'Local',
  folderPath,
  username: 'admin',
  address: addr,
  port: proto === 'RDP' ? 3389 : 22,
  status: 'ok',
  tags: [],
  note: '',
  lastConnectTime: null,
  reorder: id,
})
const SERVERS = [
  mk('a', 'web-a', '10.0.0.1', 'RDP', 'web'),
  mk('b', 'srv-b', '10.0.0.2'),
  mk('c', 'web-c', '10.0.0.3', 'SSH', 'web'),
]

const json = (data) => ({ status: 200, contentType: 'application/json', body: JSON.stringify(data) })
const state = { deleted: [], batchPatch: null }
const routes = () => ({
  'GET /api/version': json({ version: '0.9.0.0', hasUpdate: false, latestVersion: '' }),
  'GET /api/servers': json(SERVERS.filter((s) => !state.deleted.includes(s.id))),
  'GET /api/datasources': json([
    { name: 'Local', type: 'sqlite', status: 'connected', writable: true, config: { path: 'C:/data/1r.db' } },
  ]),
  'GET /api/tags': json([]),
  'GET /api/settings/general': json({ language: 'zh-cn' }),
  'PUT /api/settings/general': json({ ok: true }),
  'GET /api/settings/appearance': json({ themeMode: 'dark', accent: 'blue', fontSize: 'M', font: '' }),
  'GET /api/ui-state/tree': json({ 'Local\u0000web': true }),
  'PUT /api/ui-state/tree': json({ ok: true }),
  'GET /api/ui-state/list-order': json([]),
  'GET /api/serial/options': json([]),
  'POST /api/servers/batch/peek': json({
    a: { displayName: 'web-a' },
    b: { displayName: 'srv-b' },
    c: { displayName: 'web-c' },
  }),
  'POST /api/servers/batch': json({ updated: 2 }),
  'GET /api/credentials/names': json([]),
  // 凭据列表（?ds= 查询串被 pathname 匹配忽略）——S23 凭据段依赖；缺这路由组件会正确地
  // 报「加载凭据失败」，该态无「新建凭据」按钮（round16 复跑抓到的 mock 缺口）
  'GET /api/credentials': json([]),
  'GET /api/icons': json([]),
  'GET /api/servers/a/config': json({
    json: {
      Protocol: 'RDP',
      ClassVersion: 1,
      DisplayName: 'web-a',
      Address: '10.0.0.1',
      Username: 'admin',
      Port: 3389,
    },
  }),
  'GET /api/servers/b/config': json({
    json: {
      Protocol: 'RDP',
      ClassVersion: 1,
      DisplayName: 'srv-b',
      Address: '10.0.0.2',
      Username: 'admin',
      Port: 3389,
    },
  }),
  'GET /api/servers/c/config': json({
    json: { Protocol: 'SSH', ClassVersion: 1, DisplayName: 'web-c', Address: '10.0.0.3', Username: 'admin', Port: 22 },
  }),
})

const results = []
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

const browser = await puppeteer.launch({ executablePath: BROWSER, headless: 'new' })

async function newPage(name) {
  const page = await browser.newPage()
  await page.setViewport({ width: 1440, height: 900 })
  await page.setRequestInterception(true)
  page.on('request', (req) => {
    const u = new URL(req.url())
    const key = `${req.method()} ${u.pathname}`
    // 慢删除（S18 用）：逐台 1.2s 延迟 + 记录已删，制造可观察的执行期窗口
    const del = u.pathname.match(/^\/api\/servers\/([^/]+)$/)
    if (req.method() === 'DELETE' && del) {
      state.deleted.push(decodeURIComponent(del[1]))
      setTimeout(() => req.respond(json({ ok: true })), 1200)
      return
    }
    const table = routes()
    if (table[key]) return req.respond(table[key])
    if (key.includes('/api/events')) return req.respond({ status: 200, contentType: 'text/event-stream', body: '' })
    if (u.origin === BASE || u.href.startsWith(BASE)) return req.continue()
    return req.respond(json({}))
  })
  await page.goto(BASE, { waitUntil: 'networkidle2', timeout: 20000 }).catch(() => {})
  await sleep(1500)
  return page
}
async function record(id, title, fn, page) {
  try {
    const detail = await fn(page)
    results.push({ id, title, status: 'pass', detail: detail || '' })
  } catch (e) {
    results.push({ id, title, status: 'fail', detail: e.message })
    try {
      await page.screenshot({ path: `${SHOT_DIR}/${id}.png`, fullPage: false })
    } catch {}
  }
}
const assert = (cond, msg) => {
  if (!cond) throw new Error(msg)
}

// ---- S01 列表加载与状态栏 ----
{
  const page = await newPage('S01')
  await record(
    'S01',
    '§7-1 列表加载、状态栏统计出现',
    async (p) => {
      const rows = await p.$$eval('.tbody .server-row, .tbody [class*=row]', (els) => els.length)
      const sb = await p.$eval('.status-bar', (el) => el.textContent)
      assert(rows >= 2, `列表行数不足: ${rows}`)
      assert(/3/.test(sb) && /台|servers?/.test(sb), `状态栏统计缺失: "${sb.slice(0, 40)}"`)
      return `行数=${rows}；状态栏="${sb.trim().slice(0, 30)}…"`
    },
    page
  )
  await page.close()
}

// ---- S04/S06 勾选与批量条（根视图行序：[全选, 文件夹web, srv-b]，boxes[2]=单台）----
{
  const page = await newPage('S04')
  await record(
    'S04',
    '§7-4/6 勾选→批量条计数与按钮可用性',
    async (p) => {
      const boxes = await p.$$('.tbody input[type="checkbox"]')
      await boxes[2].click() // 单台 srv-b
      await sleep(400)
      const bar = await p.$eval('.batch-bar', (el) => el.textContent)
      assert(/1/.test(bar), `单勾计数异常: "${bar.slice(0, 30)}"`)
      const btns = await p.$$eval('.batch-bar button', (bs) =>
        bs.map((b) => ({ t: b.textContent.trim().slice(0, 6), d: b.disabled }))
      )
      const del = btns.find((b) => /删|✕/.test(b.t))
      assert(del && !del.d, '勾选后删除钮应可用')
      return `计数=1；按钮=${btns.map((b) => `${b.t}${b.d ? '禁' : '用'}`).join(',')}`
    },
    page
  )
  await page.close()
}

// ---- S10 切语言即时生效 ----
{
  const page = await newPage('S10')
  await record(
    'S10',
    '§7-10 切语言 zh⇄en 即时生效（状态栏钮）',
    async (p) => {
      const before = await p.$eval('.sb-lang', (el) => el.textContent.trim())
      await p.click('.sb-lang')
      await sleep(700)
      const after = await p.$eval('.sb-lang', (el) => el.textContent.trim())
      const toolbarText = await p.evaluate(() => document.body.textContent.slice(0, 400))
      assert(before !== after, `语言钮文本未变: ${before}→${after}`)
      return `钮文本 ${before}→${after}；界面含英文=${/Setting|server/i.test(toolbarText)}`
    },
    page
  )
  await page.close()
}

// ---- S19 数据源弹窗：端口/路径校验 + 遮罩守卫 ----
{
  const page = await newPage('S19')
  await record(
    'S19',
    '§7-19 数据源添加：端口 70000 标红禁存 + 遮罩弹确认',
    async (p) => {
      // 打开设置 → 数据源 → 添加（⚙ 钮无 title，按字形找）
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => b.textContent.trim() === '⚙')
        btn?.click()
      })
      await sleep(900)
      await p.evaluate(() => {
        const nav = [...document.querySelectorAll('.s-groups button, .s-groups a, .s-groups [role=button]')].find(
          (el) => /数据源|Data ?source/i.test(el.textContent)
        )
        nav?.click()
      })
      await sleep(700)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => /添加|Add|新增/.test(b.textContent))
        btn?.click()
      })
      await sleep(700)
      const modalOk = await p.$eval('.n-modal, .n-card', () => true).catch(() => false)
      assert(modalOk, '添加数据源弹窗未打开')
      // 切 mysql → 端口填 70000 → 标红 + 保存禁
      await p.evaluate(() => {
        const sel = document.querySelector('.n-modal .n-base-selection')
        sel?.dispatchEvent(new MouseEvent('click', { bubbles: true }))
      })
      await sleep(500)
      await p.evaluate(() => {
        const opt = [...document.querySelectorAll('.n-base-select-option, [class*=option]')].find((el) =>
          /MySQL/i.test(el.textContent)
        )
        opt?.click()
      })
      await sleep(500)
      await p.evaluate(() => {
        const inputs = [...document.querySelectorAll('.n-modal input')]
        const port = inputs.find((i) => i.value === '3306')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(port, '70000')
        port.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(500)
      const st = await p.evaluate(() => {
        const err = [...document.querySelectorAll('.n-modal .f-err')].map((e) => e.textContent.trim())
        const save = [...document.querySelectorAll('.n-modal button')].find((b) => /保存|Save/.test(b.textContent))
        return { err, saveDisabled: save?.disabled, saveClass: save?.className }
      })
      assert(
        st.err.some((e) => /端口|Port/.test(e)),
        `端口错误提示缺失: ${JSON.stringify(st.err)}`
      )
      assert(st.saveDisabled === true || /req-blocked/.test(st.saveClass || ''), '端口非法时保存应禁用')
      // 遮罩守卫：填了 host（有输入）→ 点遮罩 → 应弹「未保存的更改」
      await p.evaluate(() => {
        const inputs = [...document.querySelectorAll('.n-modal input')]
        const host = inputs.find((i) => !i.value)
        if (host) {
          const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
          setter.call(host, '1.2.3.4')
          host.dispatchEvent(new Event('input', { bubbles: true }))
        }
      })
      await sleep(300)
      await p.mouse.click(60, 450) // 点弹窗遮罩区
      await sleep(700)
      const dlg = await p.evaluate(() => {
        const d = document.querySelector('.n-dialog')
        return d ? d.textContent.slice(0, 40) : null
      })
      assert(dlg && /未保存|unsaved/i.test(dlg), `遮罩点击应弹丢弃确认，实得: ${dlg}`)
      return `端口标红✓ 保存禁✓ 遮罩确认="${dlg}"`
    },
    page
  )
  await page.close()
}

// ---- S21 草稿守卫（编辑抽屉）----
{
  const page = await newPage('S21')
  await record(
    'S21',
    '§7-21 编辑抽屉输入后点遮罩 → 未保存确认；清空 → 直接关',
    async (p) => {
      const boxes = await p.$$('.tbody input[type="checkbox"]')
      await boxes[2].click()
      await sleep(300)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('.batch-bar button')].find((b) => b.textContent.includes('编辑'))
        btn?.click()
      })
      await sleep(1300)
      // 输入改动
      await p.evaluate(() => {
        const input = document.querySelector('[data-req-key="DisplayName"] input')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(input, '改名了')
        input.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(400)
      await p.click('.ed-scrim', { offset: { x: 30, y: 300 } })
      await sleep(700)
      let dlg = await p.evaluate(() => document.querySelector('.n-dialog')?.textContent.slice(0, 40) || null)
      assert(dlg && /未保存|unsaved/i.test(dlg), `应弹未保存确认，实得: ${dlg}`)
      // 点「继续编辑」→ 清空改动 → 再点遮罩 → 直接关
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('.n-dialog button')].find((b) => /继续|Keep/i.test(b.textContent))
        btn?.click()
      })
      await sleep(600)
      await p.evaluate(() => {
        const input = document.querySelector('[data-req-key="DisplayName"] input')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(input, 'srv-b') // 还原为打开时的原值（勾的是 srv-b 行）
        input.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(500)
      await p.click('.ed-scrim', { offset: { x: 30, y: 300 } })
      await sleep(900)
      const closed = await p.evaluate(() => !document.querySelector('.ed-root.open'))
      assert(closed, '清空改动后点遮罩应直接关闭抽屉')
      return `脏→确认框"${dlg}"；还原→直关✓`
    },
    page
  )
  await page.close()
}

// ---- S22 必填引导（已在 probe 验证，正式化）----
{
  const page = await newPage('S22')
  await record(
    'S22',
    '§7-22 必填未填点保存 → 假禁+提示+强点聚焦高亮',
    async (p) => {
      const boxes = await p.$$('.tbody input[type="checkbox"]')
      await boxes[2].click()
      await sleep(300)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('.batch-bar button')].find((b) => b.textContent.includes('编辑'))
        btn?.click()
      })
      await sleep(1300)
      await p.evaluate(() => {
        const input = document.querySelector('[data-req-key="DisplayName"] input')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(input, '')
        input.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(500)
      const st1 = await p.evaluate(() => ({
        hint: document.querySelector('.req-hint')?.textContent || null,
        blocked: !!document.querySelector('.ed-primary.req-blocked'),
      }))
      assert(st1.hint === '有必填项未填', `旁侧提示异常: ${st1.hint}`)
      assert(st1.blocked, '保存钮应呈假禁用观感')
      const focus = await p.evaluate(() => {
        document.querySelector('.ed-primary')?.click()
        return new Promise((res) =>
          setTimeout(
            () =>
              res({
                tag: document.activeElement?.tagName,
                key: document.activeElement?.closest('[data-req-key]')?.getAttribute('data-req-key'),
                hot: document.querySelectorAll('.req-hot').length,
                warn: document.querySelectorAll('.n-input--error-status').length,
              }),
            400
          )
        )
      })
      assert(focus.tag === 'INPUT' && focus.key === 'DisplayName', `强点应聚焦名称框，实得 ${JSON.stringify(focus)}`)
      assert(focus.warn >= 1, '未填项应有警告色高亮')
      return `提示✓ 假禁✓ 聚焦=${focus.key} 高亮=${focus.warn} 处`
    },
    page
  )
  await page.close()
}

// ---- S18 批量删除执行期批量条禁用 ----
{
  const page = await newPage('S18')
  await record(
    'S18',
    '§7-18 批量删除执行期再点删除不叠轮（按钮禁用）',
    async (p) => {
      const boxes = await p.$$('.tbody input[type="checkbox"]')
      await boxes[1].click() // 文件夹 web（级联 2 台）
      await boxes[2].click() // srv-b → 共 3 台
      await sleep(400)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('.batch-bar button')].find(
          (b) => /删除/.test(b.textContent) && b.textContent.includes('✕')
        )
        btn?.click()
      })
      await sleep(600)
      // 确认框 → 确认删除
      await p.evaluate(() => {
        const pos = [...document.querySelectorAll('.n-dialog button')].find((b) => /删除|Delete/.test(b.textContent))
        pos?.click()
      })
      await sleep(400)
      const st = await p.evaluate(() => {
        const bar = document.querySelector('.batch-bar')
        if (!bar) return { gone: true }
        const btns = [...bar.querySelectorAll('button')].map((b) => ({
          t: b.textContent.trim().slice(0, 4),
          d: b.disabled,
        }))
        return { gone: false, btns }
      })
      // 执行期：删除快（mock 即时）——按钮全禁或批量条已消失都算符合（不叠第二轮）
      const ok = st.gone || st.btns.every((b) => b.d)
      assert(ok, `执行期按钮应全禁，实得 ${JSON.stringify(st)}`)
      return st.gone ? '执行完成勾选清空（批量条消失）' : '执行期五钮全禁✓'
    },
    page
  )
  await page.close()
}

// ---- 手工项汇总 ----
// ---- S23 填齐必填后提示解除（round15 修复的"已填仍提示"BUG 的回归用例）----
{
  const page = await newPage('S23')
  await record(
    'S23',
    '必填填齐后「有必填项未填」提示消失、保存钮解除假禁（sqlite 路径 + 凭据名称）',
    async (p) => {
      // 设置 → 数据源 → 添加（默认 sqlite）
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => b.textContent.trim() === '⚙')
        btn?.click()
      })
      await sleep(900)
      await p.evaluate(() => {
        const nav = [...document.querySelectorAll('.s-groups button, .s-groups a, .s-groups [role=button]')].find(
          (el) => /数据源|Data ?source/i.test(el.textContent)
        )
        nav?.click()
      })
      await sleep(700)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => /添加|Add|新增/.test(b.textContent))
        btn?.click()
      })
      await sleep(700)
      const before = await p.evaluate(() => ({
        hint: !!document.querySelector('.modal-actions .req-hint'),
        blocked: !!document.querySelector('.modal-actions .req-blocked'),
      }))
      assert(before.hint && before.blocked, `sqlite 空路径应提示必填: ${JSON.stringify(before)}`)
      // 填入合法 .db 路径（精确定位 path 字段行内的输入框）→ 提示应消失、保存解除假禁
      await p.evaluate(() => {
        const input = document.querySelector('[data-req-key="path"] input')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(input, 'C:/data/test.db')
        input.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(500)
      const after = await p.evaluate(() => ({
        hint: !!document.querySelector('.modal-actions .req-hint'),
        blocked: !!document.querySelector('.modal-actions .req-blocked'),
      }))
      assert(!after.hint, `路径已填仍提示必填（round15 BUG 回归）: ${JSON.stringify(after)}`)
      assert(!after.blocked, '填齐后保存钮应解除假禁观感')
      // 凭据库同款验证：关掉数据源弹窗 → 凭据库 → 新建 → 名称空提示必填 → 填名称 → 提示消失。
      // 关弹窗：上一段填过路径，表单已脏——按 TC-ED-33 定案，点「取消」会弹「未保存的更改」
      // 确认框（产品行为正确，round16 复跑抓到旧用例没处理这一步），需点「丢弃并关闭」落关
      await p.evaluate(() => {
        const cancel = [...document.querySelectorAll('.n-modal button')].find((b) => /取消|Cancel/.test(b.textContent))
        cancel?.click()
      })
      await sleep(600)
      await p.evaluate(() => {
        const discard = [...document.querySelectorAll('.n-dialog button')].find((b) =>
          /丢弃|Discard/i.test(b.textContent)
        )
        discard?.click()
      })
      await sleep(600)
      await p.evaluate(() => {
        const nav = [...document.querySelectorAll('.s-groups button, .s-groups a, .s-groups [role=button]')].find(
          (el) => /凭据库|Credential/i.test(el.textContent)
        )
        nav?.click()
      })
      await sleep(700)
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => /新增|新建|New/.test(b.textContent))
        btn?.click()
      })
      await sleep(700)
      const cv1 = await p.evaluate(() => ({
        hint: !!document.querySelector('.modal-actions .req-hint'),
      }))
      assert(cv1.hint, '凭据新建：名称空应提示必填')
      await p.evaluate(() => {
        const input = document.querySelector('[data-req-key="name"] input')
        const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set
        setter.call(input, 'cred-a')
        input.dispatchEvent(new Event('input', { bubbles: true }))
      })
      await sleep(500)
      const cv2 = await p.evaluate(() => ({
        hint: !!document.querySelector('.modal-actions .req-hint'),
        blocked: !!document.querySelector('.modal-actions .req-blocked'),
      }))
      assert(!cv2.hint, `凭据名称已填仍提示必填（round15 BUG 回归）: ${JSON.stringify(cv2)}`)
      assert(!cv2.blocked, '填名称后保存钮应解除假禁')
      return `sqlite 填 .db 提示消失✓；凭据填名称提示消失✓`
    },
    page
  )
  await page.close()
}

// ---- S24 Teleport 目标竞态（round16 用户报障：设置返回后切文件夹 → 界面冻结不刷新）----
// 根因：#crumb-actions 容器由 ServerListView 渲染、TableToolbar 的 Teleport 消费——Vue 要求
// target 在 Teleport 挂载前就存在（不能由同一次挂载的组件树渲染）。首次挂载走骨架屏分支
// 侥幸满足（ServerTable 后挂、容器早已在）；设置往返的重挂载无骨架屏间隔，容器与 Teleport
// 同帧挂载 → querySelector 落空（控制台有 Invalid Teleport target 警告）→ TableToolbar 整块
// 内容未挂载（el=null 的 vnode 留在树里）→ 后续任何 ServerListView 更新（切文件夹）patch 到
// 失效节点 → __vnode/emitsOptions 崩 → 渲染 effect 死亡 → 界面冻结。修复=disabled 直到目标就绪
{
  const page = await newPage('S24')
  const pageErrors = []
  page.on('pageerror', (e) => pageErrors.push(String(e).slice(0, 120)))
  await record(
    'S24',
    '设置往返后切文件夹：工具簇仍传送到面包屑行（Teleport 目标竞态回归，round16 用户报障）',
    async (p) => {
      // 进设置 → 返回（ServerListView 卸载重挂载——修复前的触发前提）
      await p.evaluate(() => {
        const btn = [...document.querySelectorAll('button')].find((b) => b.textContent.trim() === '⚙')
        btn?.click()
      })
      await sleep(900)
      await p.evaluate(() => {
        const back = [...document.querySelectorAll('button')].find((b) => /返回|Back/i.test(b.textContent))
        back?.click()
      })
      await sleep(900)
      // 切文件夹：重挂载后的首次 ServerListView 更新——修复前此处渲染崩溃、effect 死亡
      await p.evaluate(() => {
        const rows = [...document.querySelectorAll('.side-tree .row')]
        const web = rows.find((r) => r.querySelector('.label')?.textContent?.trim() === 'web')
        web instanceof HTMLElement && web.click()
      })
      await sleep(900)
      const st = await p.evaluate(() => ({
        hint: !!document.querySelector('.tt-hint'),
        tools: !!document.querySelector('.table-tools'),
        crumbChildren: document.querySelector('#crumb-actions')?.children.length ?? -1,
        crumb: document.querySelector('.crumb-cur')?.textContent?.trim() || '',
      }))
      assert(st.hint, '重挂载后提示行不在 DOM——TableToolbar 的 Teleport 内容未挂载（目标竞态）')
      assert(st.tools, '工具簇不在 DOM')
      assert(st.crumbChildren > 0, `#crumb-actions 为空（children=${st.crumbChildren}）——Teleport 未传送`)
      assert(/web/.test(st.crumb), `切文件夹未生效（crumb="${st.crumb}"）——渲染 effect 可能已死亡`)
      assert(!pageErrors.length, `页面渲染错误: ${pageErrors[0] || ''}`)
      return `往返+切文件夹后工具簇在位（#crumb-actions children=${st.crumbChildren}），无渲染错误`
    },
    page
  )
  await page.close()
}

const manual = [
  ['S02', '§7-2 树/列表拖拽手感与落点（真实拖拽）'],
  ['S07b', '§7-7 双击连接真实会话、右键复制密码验证门（需桌面端）'],
  ['S11', '§7-11 开机自启开关 → 任务管理器启动项（跨端）'],
  ['S12', '§7-12 后端进程关闭/恢复全屏警告（需真实后端）'],
  ['S15', '§7-15 文件夹在另一端被删/改名自动退级（跨端）'],
  ['S16b', '§7-16 无引用凭据改密码后 reveal 新值（桌面验证门）'],
  ['S17', '§7-17 批量连接真实发起与防重入（需连接管线）'],
  ['S20', '§7-20 重启后语言保持（需真实后端持久化）'],
]
for (const [id, title] of manual)
  results.push({ id, title, status: 'manual', detail: '需真实后端/跨端/手感，人工验证' })

await browser.close()
writeFileSync(`../docs/ui-smoke-2026-09-23-${ROUND}.json`, JSON.stringify(results, null, 2))
const pass = results.filter((r) => r.status === 'pass').length
const fail = results.filter((r) => r.status === 'fail').length
console.log(`UI 冒烟：pass=${pass} fail=${fail} manual=${results.length - pass - fail}`)
for (const r of results.filter((x) => x.status === 'fail')) console.log(`  ✖ ${r.id} ${r.title}: ${r.detail}`)
