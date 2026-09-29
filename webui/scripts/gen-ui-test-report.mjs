// 生成 UI 冒烟测试 HTML 报告（数据来自 scripts/ui-smoke.mjs 的实测 JSON）。跑完保留。
// 每轮生成前更新 ROUND（须与 ui-smoke.mjs 的输出文件一致）；bugFigs 为本轮实测中
// 发现并已修复的缺陷卡片数据（无则空数组，报告整区不渲染）。
import { readFileSync, writeFileSync } from 'node:fs'

const ROUND = 'round17'
const results = JSON.parse(readFileSync(`../docs/ui-smoke-2026-09-25-${ROUND}.json`, 'utf8'))
const pass = results.filter((r) => r.status === 'pass')
const fail = results.filter((r) => r.status === 'fail')
const manual = results.filter((r) => r.status === 'manual')

// 本轮实测中发现并已修复的缺陷（三框示意图卡片；round16 起数据驱动，round15 曾内联 2 张）
// —— round16 的这一张是「测试脚本自身的缺陷」（非产品缺陷），修复后复跑 8/8 全过
const bugFigs = [
  {
    id: 'S23',
    title: '测试脚本缺陷（非产品缺陷）：冒烟用例 S23 两次复跑失败，根因全在测试脚本与模拟数据侧',
    steps: [
      { fill: '#fdf6e8', stroke: '#b9770e', title: '用例走到一半', lines: ['数据源弹窗里填了路径', '表单已「脏」'] },
      {
        fill: '#fdf1ef',
        stroke: '#c0392b',
        title: '两处测试侧缺陷',
        lines: [
          '① 点「取消」按定案会弹',
          '「未保存的更改」确认框，旧用例没处理',
          '② 模拟后端缺凭据列表路由，',
          '凭据页正确地显示「加载凭据失败」',
        ],
      },
      {
        fill: '#eef8f2',
        stroke: '#1e8e5a',
        title: '修复后',
        lines: ['用例补点「丢弃并关闭」，', '模拟后端补凭据列表路由', '复跑 8/8 全过（产品零改动）'],
      },
    ],
  },
]

const row = (r) => {
  const badge =
    r.status === 'pass'
      ? '<span class="b ok">通过</span>'
      : r.status === 'fail'
        ? '<span class="b bad">失败</span>'
        : '<span class="b man">需人工</span>'
  return `<tr><td>${r.id}</td><td>${r.title}</td><td class="c">${badge}</td><td>${r.detail || '—'}</td></tr>`
}

// 实测中发现并已修复的缺陷（三框流程示意图）
const bugFig = (id, title, steps) => `
  <div class="issue">
    <h3><span class="b bad">实测发现</span>${id} ${title}<span class="b ok" style="margin-left:8px">已修复并复测通过</span></h3>
    <div class="fig"><div class="cap">问题链路示意</div>
    <svg width="760" height="150" viewBox="0 0 760 150" role="img">
      ${steps
        .map((s, i) => {
          const x = 10 + i * (226 + 52)
          const texts = s.lines
            .map((l, j) => `<text x="${x + 14}" y="${64 + j * 20}" font-size="12" fill="#5a6270">${l}</text>`)
            .join('')
          const arrow =
            i < steps.length - 1
              ? `<path d="M${x + 226} 82 L${x + 268} 82" stroke="#8a92a0" stroke-width="2" marker-end="url(#ar)"/>`
              : ''
          return `<rect x="${x}" y="18" width="226" height="112" rx="8" fill="${s.fill}" stroke="${s.stroke}"/><text x="${x + 14}" y="44" font-size="12.5" font-weight="700" fill="${s.stroke}">${s.title}</text>${texts}${arrow}`
        })
        .join('')}
      <defs><marker id="ar" markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 z" fill="#8a92a0"/></marker></defs>
    </svg></div>
  </div>`

const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>UI 冒烟测试报告 · 2026-09-25 ${ROUND}</title>
<style>
  :root { --bg:#f7f8fa; --card:#fff; --line:#e3e6ea; --text:#2c3038; --muted:#8a92a0; }
  * { box-sizing:border-box; }
  body { margin:0; padding:0 0 80px; background:var(--bg); color:var(--text); font-family:"Segoe UI","Microsoft YaHei",sans-serif; font-size:15px; line-height:1.65; }
  .wrap { max-width:1080px; margin:0 auto; padding:28px 20px; }
  h1 { font-size:24px; margin:0 0 6px; }
  .sub { color:var(--muted); font-size:13.5px; margin-bottom:20px; }
  .panel { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:16px 20px; margin:14px 0; }
  .b { display:inline-block; border-radius:6px; color:#fff; font-size:12.5px; padding:2px 10px; }
  .ok { background:#1e8e5a; } .bad { background:#c0392b; } .man { background:#5a6270; }
  table { width:100%; border-collapse:collapse; font-size:13.5px; }
  th, td { border:1px solid var(--line); padding:7px 10px; text-align:left; vertical-align:top; }
  th { background:#f0f4ff; }
  td.c { text-align:center; white-space:nowrap; }
  td:first-child { white-space:nowrap; font-family:Consolas,monospace; }
  .issue { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:16px 20px; margin:16px 0; }
  .issue h3 { margin:0 0 8px; font-size:16px; }
  .fig { background:#fbfcfd; border:1px dashed var(--line); border-radius:8px; padding:10px; overflow-x:auto; }
  .fig .cap { font-size:12.5px; color:var(--muted); margin-bottom:6px; }
  .note { font-size:13.5px; color:#5a6270; }
  svg text { font-family:"Segoe UI","Microsoft YaHei",sans-serif; }
</style>
</head>
<body>
<div class="wrap">
<h1>UI 冒烟测试报告 · 2026-09-25 ${ROUND}</h1>
<div class="sub">依据 Tests/WebUI/README.md §7 人工冒烟清单（22 步），以真实浏览器（无界面模式 Edge）加载开发服务器页面、模拟全部后端接口后逐项执行可自动化用例；需真实后端/跨端/真实拖拽手感的条目标记「需人工」。</div>

<div class="panel"><b>结果概览</b>
<table style="margin-top:8px">
<tr><th style="width:110px">自动化执行</th><th style="width:110px">失败</th><th style="width:110px">需人工</th><th>说明</th></tr>
<tr><td class="c"><b style="font-size:16px;color:#1e8e5a">${pass.length} 通过</b></td>
<td class="c"><b style="font-size:16px;color:${fail.length ? '#c0392b' : '#1e8e5a'}">${fail.length} 失败</b></td>
<td class="c">${manual.length} 项</td>
<td class="note">${fail.length === 0 ? '全部可自动化用例通过。' : `有 ${fail.length} 条失败（见明细表红色行与下方卡片）。`}${bugFigs.length ? `测试过程中发现并当场修复 ${bugFigs.length} 个测试脚本侧缺陷（非产品缺陷，见下方卡片，修复后复跑全过）。` : ''}</td></tr>
</table></div>

<div class="panel"><b>用例明细</b>
<table style="margin-top:8px">
<tr><th style="width:64px">编号</th><th style="width:44%">用例（对应冒烟清单步骤）</th><th style="width:82px">结果</th><th>实测详情</th></tr>
${results.map(row).join('\n')}
</table>
<div class="note" style="margin-top:8px">「需人工」的 8 项涉及：真实拖拽手感、双击连接的真实会话、Windows 凭据验证门、开机自启的跨端生效、后端进程关闭/恢复、另一端删改文件夹的联动、批量连接真实发起、重启后语言保持。这些无法在模拟后端环境下验证，建议按 README §7 原步骤人工过一遍。</div></div>

${
  bugFigs.length
    ? `<div class="panel"><b>测试中发现并修复的缺陷（${bugFigs.length} 个）</b>
<div class="note" style="margin-top:6px">以下缺陷在本次冒烟执行中暴露、定位根因后当场修复，并重新跑完整冒烟确认（当前报告即修复后的结果）。</div></div>

${bugFigs.map((b) => bugFig(b.id, b.title, b.steps)).join('\n')}`
    : ''
}

<div class="panel"><b>附：测试方法说明（非界面工程师可读）</b>
<div class="note" style="margin-top:6px">
测试用的是一个由程序自动操控的真实浏览器窗口：先启动本项目的网页开发服务，再让程序伪装成后端接口（提供固定假数据：3 台服务器、1 个文件夹、1 个数据源），然后像真人一样点击、输入、勾选，并在每一步后检查页面上的实际状态是否符合预期（例如"勾选后批量条是否出现"、"点保存后焦点是否落在第一个没填的输入框上"）。这种方法能覆盖界面行为的绝大多数问题；只有"真实连接远程电脑""真实拖拽手感"这类必须依赖真实环境的项目留作人工检查。</div></div>

</div>
</body>
</html>`

writeFileSync(`../docs/ui-test-report-2026-09-25-${ROUND}.html`, html)
console.log(`written: docs/ui-test-report-2026-09-25-${ROUND}.html`)
