# Tab 拖动 Detach 体验修复（P1–P6）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复拖动 tab detach 成新窗口时的六项体验问题（误触发、新窗口位置/状态错乱、ParentWindow 不更新、焦点 HWND 歧义、DPI 换算防御性修正、合并判定区过小）。

**Architecture:** 改动分布在两个仓库：Dragablz fork（git 子模块 `Dragablz/`，先提交）承担库内修复（P5 DPI 换算重载、P6 ReentryGrace 属性）；1Remote 主仓承担集成层修复（P1 XAML 配置、P2 detach 窗口标记、P3 CollectionChanged、P4 HWND 查找），最后更新子模块指针。

**Tech Stack:** WPF (.NET 9/10/11)，Dragablz 子模块（net6/8/9/10/11-windows + net48），Stylet MVVM。

**Spec:** `docs/superpowers/specs/2026-09-28-tab-detach-ux-fixes-design.md`

**关于 TDD 的说明:** 拖拽/窗口行为无法单元测试（spec 已定），仓库无测试工程。每个任务以「构建通过」为自动化验证，Task 7 为手动验证矩阵。提交粒度保持一任务一提交。

**构建命令（Windows，仓库根目录 `D:\DemoProject\1Remote2`）:**
- Dragablz 快速验证: `dotnet build Dragablz/Dragablz/Dragablz.csproj -f net10.0-windows`（提交前再跑 `-f net48`）
- 主仓: `dotnet build Ui/Ui.csproj`（构建默认 TFM）

---

## Dragablz 子模块（Task 1–2，先于主仓提交）

### Task 1: P5 — `Native.ToWpf` 增加按 Visual 的 DPI 换算

**Files:**
- Modify: `Dragablz\Dragablz\Core\Native.cs`（头部 using + `ToWpf` 方法后新增重载）
- Modify: `Dragablz\Dragablz\TabablzControl.cs:1032`、`:1042`、`:1255`、`:1347`（4 个调用点）

- [x] **Step 1: Native.cs 加 using**

在 `using System.Windows.Interop;`（第 6 行）后加一行：

```csharp
using System.Windows.Media;
```

- [x] **Step 2: 新增 ToWpf(Visual) 重载**

在现有 `public static Point ToWpf(this Point pixelPoint)` 方法（`:59-69`）之后新增：

```csharp
        /// <summary>
        /// Converts a physical-pixel point to the DIP coordinate system of the given visual's window,
        /// using that window's actual DPI (correct under Per-Monitor V2; identical to the parameterless
        /// overload under System DPI Aware). Falls back to the primary-screen DPI if no source is available.
        /// </summary>
        public static Point ToWpf(this Point pixelPoint, Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget == null)
                return ToWpf(pixelPoint);
            return source.CompositionTarget.TransformFromDevice.Transform(pixelPoint);
        }
```

- [x] **Step 3: 改 4 个调用点（TabablzControl.cs）**

1. `PreviewItemDragDelta`（约 `:1032`）：

```csharp
// 旧
                var cursorPos = Native.GetCursorPos().ToWpf();
// 新
                var cursorPos = Native.GetCursorPos().ToWpf(myWindow);
```

2. 同方法内（约 `:1042`）：

```csharp
// 旧
                    var borderVector = myWindow.PointToScreen(new Point()).ToWpf() - new Point(myWindow.Left, myWindow.Top);
// 新
                    var borderVector = myWindow.PointToScreen(new Point()).ToWpf(myWindow) - new Point(myWindow.Left, myWindow.Top);
```

3. `MonitorBreach` 最大化分支（约 `:1255`）：

```csharp
// 旧
                var desktopMousePosition = Native.GetCursorPos().ToWpf();
// 新
                var desktopMousePosition = Native.GetCursorPos().ToWpf(myWindow);
```

4. `ConfigureNewHostSizeAndGetDragStartWindowOffset`（约 `:1347`）：

```csharp
// 旧
            var borderVector = currentWindow.PointToScreen(new Point()).ToWpf() - new Point(currentWindow.GetActualLeft(), currentWindow.GetActualTop());
// 新
            var borderVector = currentWindow.PointToScreen(new Point()).ToWpf(currentWindow) - new Point(currentWindow.GetActualLeft(), currentWindow.GetActualTop());
```

- [x] **Step 4: 构建**

Run: `dotnet build Dragablz/Dragablz/Dragablz.csproj -f net10.0-windows`
Expected: Build succeeded, 0 errors

- [x] **Step 5: 提交（在子模块内）**

```bash
cd Dragablz
git add Dragablz/Core/Native.cs Dragablz/TabablzControl.cs
git commit -m "fix: convert screen coordinates with per-window DPI in inter-tab drag

Add ToWpf(Visual) overload using CompositionTarget.TransformFromDevice and use it
at all four inter-tab drag call sites. Behavior is unchanged under System DPI
Aware; becomes correct if the host app ever enables Per-Monitor V2.

Co-Authored-By: Claude <noreply@anthropic.com>"
cd ..
```

### Task 2: P6 — `InterTabController.ReentryGrace` 扩大合并判定区

**Files:**
- Modify: `Dragablz\Dragablz\InterTabController.cs`（新增依赖属性）
- Modify: `Dragablz\Dragablz\TabablzControl.cs:1068-1089`（`MonitorReentry` 的目标矩形构造）

- [x] **Step 1: InterTabController 新增依赖属性**

在 `VerticalPopoutGrace` 属性块（`:23-30`，即 `MoveWindowWithSolitaryTabsProperty` 之前）新增：

```csharp
        public static readonly DependencyProperty ReentryGraceProperty = DependencyProperty.Register(
            "ReentryGrace", typeof (double), typeof (InterTabController), new PropertyMetadata(0.0));

        /// <summary>
        /// Widen the rectangle used to detect dragging a solitary tab back into another window's
        /// header: the bottom edge is lowered by this value (in DIP) and the top edge is raised by
        /// a quarter of it. Default 0 keeps the exact header rectangle. Read from the *target*
        /// window's controller.
        /// </summary>
        public double ReentryGrace
        {
            get { return (double) GetValue(ReentryGraceProperty); }
            set { SetValue(ReentryGraceProperty, value); }
        }
```

- [x] **Step 2: MonitorReentry 构造矩形时应用 grace**

`TabablzControl.cs` `MonitorReentry` 中 `.Select(tc => { ... })` lambda（约 `:1076`），
在 `var bottomRight = ...;` 之后、`return new { tc, topLeft, bottomRight };` 之前插入：

```csharp
                    var reentryGrace = tc.InterTabController?.ReentryGrace ?? 0;
                    topLeft.Offset(0, -reentryGrace * 0.25);
                    bottomRight.Offset(0, reentryGrace);
```

（`tc.InterTabController` 非空已由上方 `Where` 子句保证 `tc.InterTabController != null`，`?.` 仅为防御。）

- [x] **Step 3: 构建（含 net48，验证新语法兼容）**

Run: `dotnet build Dragablz/Dragablz/Dragablz.csproj -f net10.0-windows && dotnet build Dragablz/Dragablz/Dragablz.csproj -f net48`
Expected: 两次 Build succeeded

- [x] **Step 4: 提交（在子模块内）**

```bash
cd Dragablz
git add Dragablz/InterTabController.cs Dragablz/TabablzControl.cs
git commit -m "feat: add InterTabController.ReentryGrace to widen tab re-dock detection

Co-Authored-By: Claude <noreply@anthropic.com>"
cd ..
```

## 1Remote 主仓（Task 3–6）

### Task 3: P1 + P6 配置 — XAML 宽容区 + 更新子模块指针

**Files:**
- Modify: `Ui\View\Host\TabWindowView.xaml:454-457`
- Modify(指针): `Dragablz`（子模块）

- [x] **Step 1: InterTabController 配置**

```xml
<!-- 旧 -->
            <dragablz:TabablzControl.InterTabController>
                <dragablz:InterTabController InterTabClient="{Binding InterTabClient}" />
                <!--<dragablz:InterTabController />-->
            </dragablz:TabablzControl.InterTabController>
<!-- 新 -->
            <dragablz:TabablzControl.InterTabController>
                <dragablz:InterTabController InterTabClient="{Binding InterTabClient}"
                                             VerticalPopoutGrace="80" HorizontalPopoutGrace="40"
                                             ReentryGrace="40" />
            </dragablz:TabablzControl.InterTabController>
```

（删除注释掉的旧行；新属性来自 Task 1/2 的 Dragablz 改动，此任务之前子模块必须已完成 Task 1–2 提交。）

- [x] **Step 2: 构建**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded（`Ui.csproj` 以 ProjectReference 引用 Dragablz，构建走的是工作树，与子模块是否已提交无关；指针是否正确以 Step 3 提交时 `git status` 中 `Dragablz` 的变更为准）

- [x] **Step 3: 提交（主仓，含子模块指针）**

```bash
git add Ui/View/Host/TabWindowView.xaml Dragablz
git commit -m "feat: enlarge tab popout grace and enable reentry grace

Drag must now leave the tab strip by 80px vertically (40px horizontally) before
detaching, and a solitary dragged tab re-docks when released within ~40px below
another window's tab strip.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

### Task 4: P2 — detach 创建的窗口跳过位置/状态恢复

**Files:**
- Modify: `Ui\View\Host\TabWindowView.xaml.cs:21`（属性）、`:43-46`（Loaded）、`:131-134`（WindowState 恢复）
- Modify: `Ui\View\Host\TabWindowViewModel.cs:423-428`（`InterTabClient.GetNewHost`）

- [x] **Step 1: TabWindowView 加标记属性**

在 `public const double TITLE_BAR_HEIGHT = 30;`（`:21`）之后新增：

```csharp
        /// <summary>
        /// True when this window was created by Dragablz to host a dragged-out tab.
        /// Such windows get their position/size from the drag operation and must not
        /// restore the remembered window placement in Loaded.
        /// </summary>
        internal bool IsCreatedByDragDetach { get; set; }
```

- [x] **Step 2: Loaded 中条件执行 InitWindowSizeOnLoaded**

```csharp
// 旧（:43-46）
            this.Loaded += (_, _) =>
            {
                InitWindowSizeOnLoaded();
                TimerInitOnLoaded();
// 新
            this.Loaded += (_, _) =>
            {
                if (!IsCreatedByDragDetach)
                {
                    InitWindowSizeOnLoaded();
                }
                TimerInitOnLoaded();
```

- [x] **Step 3: WindowState 恢复同样跳过**

```csharp
// 旧（:131-134）
                if (IoC.Get<LocalityService>().TabWindowState != System.Windows.WindowState.Minimized)
                {
                    this.WindowState = IoC.Get<LocalityService>().TabWindowState;
                }
// 新
                if (!IsCreatedByDragDetach
                    && IoC.Get<LocalityService>().TabWindowState != System.Windows.WindowState.Minimized)
                {
                    this.WindowState = IoC.Get<LocalityService>().TabWindowState;
                }
```

- [x] **Step 4: GetNewHost 置标记 + Manual 启动位置**

```csharp
// 旧（TabWindowViewModel.cs:423-428）
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            var v = new TabWindowView();
            IoC.Get<SessionControlService>().AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }
// 新
        public INewTabHost<Window> GetNewHost(IInterTabClient interTabClient, object partition, TabablzControl source)
        {
            var v = new TabWindowView
            {
                IsCreatedByDragDetach = true,
                // Dragablz sets Left/Top/Width/Height before Show(); Manual makes them effective.
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            IoC.Get<SessionControlService>().AddTab(v);
            return new NewTabHost<Window>(v, v.TabablzControl);
        }
```

- [x] **Step 5: 构建**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded

- [x] **Step 6: 提交**

```bash
git add Ui/View/Host/TabWindowView.xaml.cs Ui/View/Host/TabWindowViewModel.cs
git commit -m "fix: detached tab window jumps to remembered placement and may spawn maximized

A drag-detached TabWindowView went through CenterScreen startup, remembered-placement
restore (InitWindowSizeOnLoaded) and remembered WindowState restore, fighting the
placement Dragablz assigns before Show(). Mark such windows and skip both restores.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

### Task 5: P3 — tab 移动后更新 `ParentWindow`

**Files:**
- Modify: `Ui\View\Host\TabWindowViewModel.cs:46-53`（`ItemsOnCollectionChanged`）

- [x] **Step 1: 按 Action 过滤并对新项调用 SetParentWindow**

```csharp
// 旧
        private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(BtnCloseAllVisibility));
            if (Items.Count == 0)
            {
                View.Hide();
            }
        }
// 新
        private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(BtnCloseAllVisibility));
            // Dragablz inter-tab drag adds items straight to the ItemsSource, bypassing AddItem();
            // keep the host's parent window reference in sync (used for RDP fullscreen sizing etc.).
            // Reset (Items.Clear() on Dispose) carries no NewItems.
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<TabItemViewModel>())
                {
                    item.Content.SetParentWindow(View);
                }
            }
            if (Items.Count == 0)
            {
                View.Hide();
            }
        }
```

- [x] **Step 2: 构建**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded

- [x] **Step 3: 提交**

```bash
git add Ui/View/Host/TabWindowViewModel.cs
git commit -m "fix: update host ParentWindow when a tab moves between windows

Inter-tab drag bypasses TabWindowViewModel.AddItem, leaving the host pointing at
the old window (wrong screen for RDP fullscreen sizing). SetParentWindow is
idempotent so AddItem's own call is unaffected.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

### Task 6: P4 — 用本窗口句柄找内容 HWND

**Files:**
- Modify: `Ui\View\Host\TabWindowViewModel.cs:150-168`（`GetCurrentTabContentWindow`）、文件头部 using

- [x] **Step 1: 加 using**

在 `using System.Windows;`（`:6`）后加：

```csharp
using System.Windows.Interop;
```

- [x] **Step 2: 替换 FindWindow 为本窗口句柄**

```csharp
// 旧
        private IntPtr GetCurrentTabContentWindow()
        {
            IntPtr hWndParent = Win32Api.FindWindow(null, this.Title);
            if (hWndParent == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            ...
// 新
        private IntPtr GetCurrentTabContentWindow()
        {
            // Look for the visible child hwnd inside *this* window. Do NOT locate the parent by
            // window title: after a drag-detach two TabWindowViews share the same title.
            IntPtr hWndParent = new WindowInteropHelper(View).Handle;
            if (hWndParent == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            ...
```

（`do/while` 枚举可见子窗口的循环体保持不变。）

- [x] **Step 3: 构建**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded

- [x] **Step 4: 提交**

```bash
git add Ui/View/Host/TabWindowViewModel.cs
git commit -m "fix: find tab content hwnd from own window instead of by window title

After a drag-detach the source and new window share one title, so FindWindow
could resolve to the wrong window and the focus-observer timer stole foreground
from the wrong session.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

### Task 7: 全量构建 + 手动验证矩阵

- [x] **Step 1: Dragablz 全 TFM 构建**

Run: `dotnet build Dragablz/Dragablz/Dragablz.csproj`
Expected: net6/8/9/10/11-windows 与 net48 全部 Build succeeded

- [x] **Step 2: 主仓构建**

Run: `dotnet build Ui/Ui.csproj`
Expected: Build succeeded

- [ ] **Step 3: 运行应用，按 spec 验证矩阵手动验证**

场景清单（详见 spec「测试与验证」）：
1. 多 tab 窗口内重排：tab 条内小幅拖动（含向下偏 10–30px）**不**触发 detach
2. 拖 tab 朝内容区 >80px / 横向出界 >40px：正常 detach
3. detach 出的新窗口出现在鼠标处，尺寸同源窗口；**不再**闪现屏幕中心/记忆位置；源窗口最大化时新窗口**不**最大化
4. detach 后拖回原窗口 tab 条（含其下方约 40px）：吸回合并
5. 窗口 A 的 tab 拖到窗口 B：两跳式（先 popout 再吸入 B），判定区为 B 的 tab 条 ± 扩展区
6. detach 后在新窗口对该会话按 F11 全屏：分辨率按**新窗口所在屏**计算（多屏验证）
7. detach 后两窗口间切换 tab / 键盘输入：焦点无错乱（RDP 与 SSH/Integrate 各验一次）
8. 混合 DPI 双屏（如 100%+150%）：detach/合并落点与修复前无差异（P5 为行为不变的防御性修正）
9. 单 tab 窗口拖 tab 头：仍是移动窗口（P8 保持现状，非缺陷）

- [ ] **Step 4: 如有问题，回溯对应任务修复并追加 commit；全部通过后向维护者汇报验证结果**
