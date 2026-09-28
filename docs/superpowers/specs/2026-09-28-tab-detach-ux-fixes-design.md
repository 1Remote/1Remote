# Tab 拖动 Detach 体验修复设计

日期：2026-09-28
状态：已与维护者确认范围（方案 B：P1–P4 在 1Remote 侧，P5–P6 在 Dragablz fork 内）

## 背景

拖动 tab 标签 detach 成新窗口时体验不佳。经代码调研，detach 链路为：
Dragablz `Thumb` 拖动（约 4px 启动阈值）→ `TabablzControl.MonitorBreach` 判定鼠标越界即创建新窗口 →
1Remote `InterTabClient.GetNewHost` 创建 `TabWindowView` → Dragablz `ReceiveDrag` 将 tab 移入新窗口，
之后拖动表现为移动新窗口（`MoveWindowWithSolitaryTabs`）。

调研发现的问题清单（按用户感知排序）：

| # | 问题 | 根因位置 | 处理 |
|---|------|----------|------|
| P1 | detach 误触发太容易：越界宽容区仅 8px，以 30px 高的 tab 标题条为基准，鼠标朝内容区偏 8px 即 detach | `Dragablz\Dragablz\InterTabController.cs:9-10` 默认值；`TabablzControl.cs:1223` `MonitorBreach` | 修（1Remote 侧） |
| P2 | 新窗口初始位置/状态错乱：Dragablz 在 `Show()` 前设置 Left/Top，但窗口构造设置了 `WindowStartupLocation=CenterScreen`（Show 时强制居中），`Loaded` 里 `InitWindowSizeOnLoaded()` 又覆盖为记忆位置并无条件恢复 `WindowState`（含 Maximized），最大化窗口 detach 出来直接全屏且此后鼠标跟随失效 | `TabWindowView.xaml.cs:39`、`:165`、`:131-134` | 修（1Remote 侧） |
| P3 | detach 后 `host.ParentWindow`/`ParentWindowHandle` 不更新：detach 走 `ReceiveDrag → AddToSource` 直接改 ItemsSource，绕过 `TabWindowViewModel.AddItem` 里的 `SetParentWindow(View)`，RDP 全屏分辨率按旧窗口所在屏计算 | `TabWindowViewModel.cs:238`；`AxMsRdpClient09Host.xaml.cs:1062` | 修（1Remote 侧） |
| P4 | 按标题 `FindWindow` 找宿主 HWND：detach 后新旧窗口标题相同，焦点观察定时器可能把前台切到错误窗口 | `TabWindowViewModel.cs:152` `GetCurrentTabContentWindow` | 修（1Remote 侧） |
| P5 | `Native.ToWpf` 用主屏 DC 的 DPI 换算所有屏幕坐标 | `Dragablz\Dragablz\Core\Native.cs:59` | 修（Dragablz fork，防御性） |
| P6 | 跨窗口合并为两跳式（先 popout 再吸回），且 `MonitorReentry` 判定区域仅目标 tab 头矩形（30px 高） | `TabablzControl.cs:1057-1115` | 修（Dragablz fork） |
| P7 | RDP/SSH 等嵌入 HWND 的会话在 detach 瞬间黑屏/闪断（WinFormsHost 跨窗口 reparent 的 airspace 限制） | WPF 平台限制 | 不修（P1–P6 完成后重估） |
| P8 | 单 tab 窗口拖 tab 头 = 移动窗口，无法 detach 唯一 tab（`MoveWindowWithSolitaryTabs=true`） | 设计取舍 | 不修（保持现状） |

关于 P5 的前置查证：WPF（含 .NET 9）默认 System DPI Aware，需 manifest 显式声明才是 PMv2；
1Remote 未声明。System DPI Aware 下全进程按主屏 DPI 统一换算，`ToWpf` 用主屏 DPI 与 WPF 坐标系自洽，
因此 P5 目前不会产生用户可见偏移，修法定位为"行为不变的防御性修正"，保证将来启用 PMv2 时不踩坑。

## 修复设计

### P1 · 调大 detach 宽容区

`Ui\View\Host\TabWindowView.xaml`（`<dragablz:InterTabController>` 处）：

```xml
<dragablz:InterTabController InterTabClient="{Binding InterTabClient}"
                             VerticalPopoutGrace="80" HorizontalPopoutGrace="40" />
```

- 垂直 80px：鼠标朝内容区拖出明显距离才触发 detach；水平 40px。
- 数值为初值，不引入设置项，按用户反馈再调。

### P2 · detach 创建的窗口跳过位置/状态恢复

1. `TabWindowView` 增加 `internal bool IsCreatedByDragDetach`（默认 false）。
2. `InterTabClient.GetNewHost`（`TabWindowViewModel.cs:423`）创建 `TabWindowView` 后置
   `IsCreatedByDragDetach = true`，并设 `WindowStartupLocation = Manual`，使 Dragablz 在
   `Show()` 前设置的 Left/Top 生效。
3. `TabWindowView` 的 `Loaded` 事件处理中：仅当 `!IsCreatedByDragDetach` 时执行
   `InitWindowSizeOnLoaded()` 与 `TabWindowState` 恢复；其余逻辑（WNDPROC hook、尺寸/位置记忆、
   状态变化处理、关闭确认等）保持不变。detach 后用户自行最大化/移动窗口时，记忆逻辑照常更新。

### P3 · detach/合并后更新 ParentWindow

`TabWindowViewModel.ItemsOnCollectionChanged`：对 `e.NewItems` 中的 `TabItemViewModel` 调用
`item.Content.SetParentWindow(View)`。`HostBase.SetParentWindow`（`HostBase.cs:41`）对相同值
早退，幂等，与 `AddItem` 现有调用不冲突。移出（`e.OldItems`）不需要处理——同一个集合变化周期内
新窗口侧必然收到 Add 并设置正确的 `View`。

### P4 · 消除按标题 FindWindow 的歧义

`TabWindowViewModel.GetCurrentTabContentWindow`（`TabWindowViewModel.cs:150-168`）：
去掉 `Win32Api.FindWindow(null, this.Title)`，父窗口句柄改用 `View` 自身 HWND
（`WindowInteropHelper(View).Handle`，或复用 `TabWindowView._myHandle` 暴露的只读属性），
后续 `FindWindowEx` 枚举第一个可见子窗口的逻辑保持不变。调用点均在 UI 线程且窗口已加载
（`SelectedItem` setter 与 `AwaitTabSwitching`），句柄为 `IntPtr.Zero` 时维持现有"返回 Zero"的降级行为。

### P5 · `Native.ToWpf` 增加按 Visual 的 DPI 换算（防御性）

`Dragablz\Core\Native.cs` 新增重载：

```csharp
public static Point ToWpf(this Point pixelPoint, Visual visual)
```

实现用 `PresentationSource.FromVisual(visual).CompositionTarget.TransformFromDevice`
（`FromVisual` 或 `CompositionTarget` 为 null 时回退原 desktop DC 实现）。Dragablz 内现有调用点
（`MonitorBreach`、`PreviewItemDragDelta`、`ReceiveDrag` 等）逐个改为传入所在窗口/控件；
无上下文的调用点保留原方法。System DPI Aware 下数值与现状一致。

### P6 · 扩大窗口间合并判定区域

1. `InterTabController` 新增依赖属性 `ReentryGrace`（double，默认 0 = 现状不变）。
2. `TabablzControl.MonitorReentry` 构造目标矩形时：
   `topLeft.Y -= ReentryGrace * 0.25; bottomRight.Y += ReentryGrace;`（DIP）。
3. 1Remote 的 XAML 里为 `InterTabController` 设 `ReentryGrace="40"`：tab 条下方 40px 内松手即可吸入
   目标窗口，上方 10px 余量。水平方向不扩展，避免误吸到目标窗口内容区中部。

## 测试与验证

UI 拖拽行为无法单元测试，采用手动验证矩阵：

- 会话类型：RDP（AxMsRdpClient09）、SSH/Integrate、VNC、文件传输
- 场景：tab 重排不误触；单 tab 窗口拖动；多 tab 窗口 detach；detach 后拖回原窗口；跨窗口合并；
  最大化窗口 detach；detach 后 F11 全屏（多屏）
- 环境：单屏；混合 DPI 双屏（验证 P5 行为不变）
- 构建：主仓 net9/10/11 多 TFM 构建通过；Dragablz 子模块 net48 目标构建通过

## 提交策略

Dragablz 为子模块：先在 `Dragablz/` 内提交（P5、P6），再在主仓提交 1Remote 侧改动并更新子模块指针。
遵循仓库既有的 two-repo patch 工作流（1Remote2 通过编号 patch 应用上游变更）。
