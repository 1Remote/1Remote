// 文件夹语义常驻断言（防复发）：显示=直接子级（资源管理器模型），勾选=全部子孙。
// 语义单一来源在 src/composables/folders.js（isDirectChildOf / isInSubtreeOf）。
// 运行: node scripts/semantics-test.mjs（任何输出 PASS 即通过；断言失败非零退出）
import { isDirectChildOf, isInSubtreeOf } from '../src/composables/folders.js'

let failed = 0
function ok(cond, name) {
  if (!cond) {
    failed++
    console.error('FAIL:', name)
  } else console.log('PASS:', name)
}

// owner 实测数据形状：2389 在 323/67；323 直接子级若干台
ok(isDirectChildOf('323/67', '323') === false, '显示: 323/67 的服务器不出现在 323 视图（本轮 BUG）')
ok(isDirectChildOf('323/67', '323/67') === true, '显示: 323/67 的服务器出现在 323/67 视图')
ok(isDirectChildOf('323', '323') === true, '显示: 直接子级出现在本文件夹视图')
ok(isDirectChildOf('', '') === true, '显示: 根级服务器出现在根视图')
ok(isDirectChildOf('323', '') === false, '显示: 323 内的服务器不出现在根视图')
ok(isDirectChildOf('3234', '323') === false, '显示: 同名前缀不误匹配（3234 ≠ 323 子级）')
ok(isDirectChildOf(undefined, '') === true, '显示: folderPath undefined 归一化为根级')

ok(isInSubtreeOf('323/67', '323') === true, '勾选: 勾 323 级联到 323/67（含子文件夹）')
ok(isInSubtreeOf('323', '323') === true, '勾选: 勾 323 含直接子级')
ok(isInSubtreeOf('3234', '323') === false, '勾选: 3234 不被 323 级联误命中')
ok(isInSubtreeOf('', '323') === false, '勾选: 根级不被 323 级联')
ok(isInSubtreeOf('323/67/x', '323/67') === true, '勾选: 深层子孙级联')

if (failed) {
  console.error(failed + ' 项失败')
  process.exit(1)
}
console.log('全部通过')
