# Solution for Issue #951: Popup Window Causing Main Window to be Hidden

## Problem Description
When opening a file selection dialog from a popup window in 1Remote, after closing the dialog, the main window would move to the bottom layer (z-order) instead of staying on top. This created a poor user experience as users had to manually bring the main window back to focus.

## Root Cause
The issue was in the `Shawn.Utils.Wpf.FileSystem.SelectFileHelper` class, which provides helper methods for showing file dialogs (OpenFileDialog, SaveFileDialog). These methods were calling `ShowDialog()` without specifying an owner window parameter.

```csharp
// Old problematic code
if (dlg.ShowDialog() != true) return null;
```

When `ShowDialog()` is called without an owner parameter in WPF, the dialog doesn't establish a proper parent-child window relationship. This causes Windows' window manager to lose track of which window should be active after the dialog closes, especially when the dialog is opened from a popup or child window.

## Solution Approach
Since `Shawn.Utils` is an external git submodule that we don't control directly, we created a wrapper class in the main 1Remote repository that:

1. Provides the same API as the original `SelectFileHelper`
2. Adds support for an optional `owner` parameter
3. Implements automatic owner window detection when no owner is specified
4. Delegates to the native WPF dialogs with proper owner window set

## Implementation Details

### New Wrapper Class: `_1RM.Utils.SelectFileHelper`
Created at: `Ui/Utils/SelectFileHelper.cs`

This wrapper class provides:

#### 1. Owner Parameter Support
All methods now accept an optional `Window? owner` parameter:
```csharp
public static string? OpenFile(..., Window? owner = null)
public static string? SaveFile(..., Window? owner = null)
public static string[]? OpenFiles(..., Window? owner = null)
```

#### 2. Automatic Owner Detection
When `owner` is null, the `GetActiveWindow()` method finds the best window to use as owner:

```csharp
private static Window? GetActiveWindow()
{
    // 1. Try to get the currently active window
    var activeWindow = Application.Current.Windows.OfType<Window>()
        .FirstOrDefault(w => w.IsActive);
    if (activeWindow != null) return activeWindow;
    
    // 2. If no active window, get the topmost window
    var topmostWindow = Application.Current.Windows.OfType<Window>()
        .FirstOrDefault(w => w.Topmost);
    if (topmostWindow != null) return topmostWindow;
    
    // 3. Fallback to main window
    if (Application.Current.MainWindow != null && 
        Application.Current.MainWindow.IsLoaded)
        return Application.Current.MainWindow;
    
    // 4. Last resort: return the first loaded window
    return Application.Current.Windows.OfType<Window>()
        .FirstOrDefault(w => w.IsLoaded);
}
```

#### 3. Proper ShowDialog Calls
The wrapper now calls `ShowDialog(owner)` with the owner window:
```csharp
var ownerWindow = owner ?? GetActiveWindow();
if (ownerWindow != null && dlg.ShowDialog(ownerWindow) != true) return null;
else if (ownerWindow == null && dlg.ShowDialog() != true) return null;
```

### Code-Behind Updates
Updated 7 code-behind files (*.xaml.cs) to explicitly pass the owner window:

```csharp
// Example from SftpFormView.xaml.cs
var path = SelectFileHelper.OpenFile(
    filter: "ppk|*.*", 
    currentDirectoryForShowingRelativePath: Environment.CurrentDirectory,
    owner: Window.GetWindow(this)  // Explicitly pass the owner
);
```

Files updated:
- `Ui/View/Editor/Forms/SftpFormView.xaml.cs`
- `Ui/View/Editor/Forms/SshFormView.xaml.cs` (2 call sites)
- `Ui/View/Editor/Forms/SerialFormView.xaml.cs`
- `Ui/View/Editor/Forms/LocalAppFormView.xaml.cs`
- `Ui/View/Editor/PasswordPopupDialogView.xaml.cs`
- `Ui/Controls/LogoSelector.xaml.cs`
- `Ui/View/ErrorReport/ErrorReportWindow.xaml.cs`

### ViewModel Usage
ViewModels and other non-UI classes continue to work without modification. They call the method without the owner parameter, relying on automatic owner detection:

```csharp
// Example from ServerEditorPageViewModel.cs
var path = SelectFileHelper.OpenFile(
    title: "Select a script", 
    filter: $"script|*.bat;*.cmd;*.ps1;*.py|*|*.*"
);
// Owner is automatically detected via GetActiveWindow()
```

### Import Statement Updates
Updated 29 files to use the new wrapper (note: some files have multiple call sites, totaling 28 distinct call sites):

**Before:**
```csharp
using Shawn.Utils.Wpf.FileSystem;
```

**After:**
```csharp
using _1RM.Utils;
```

## Benefits

1. **Fixes the Z-Order Issue**: File dialogs now maintain proper parent-child relationships, preventing the main window from moving to the background
2. **Backward Compatible**: The owner parameter is optional with a default of null, so existing code works without modification
3. **No Breaking Changes**: All existing call sites continue to work
4. **Better Window Management**: Automatic owner detection improves window focus management throughout the application
5. **Maintainable**: Solution is contained within the 1Remote codebase, not dependent on external submodule changes

## Testing Scenarios

To verify the fix works correctly:

1. **Popup Window Scenario** (Original Issue #951):
   - Open a server editor in a popup window
   - Click a button that opens a file dialog (e.g., "Select private key")
   - Close the file dialog
   - **Expected**: The popup window remains active, main window stays in background
   - **Previous behavior**: Main window would move behind other windows

2. **Main Window Scenario**:
   - Open a file dialog from the main window
   - Close the dialog
   - **Expected**: Main window remains active

3. **Multiple Windows Scenario**:
   - Have multiple 1Remote windows open
   - Open a file dialog from one window
   - **Expected**: The correct window (the one that opened the dialog) remains active

## Technical Notes

- The solution reimplements the file dialog functionality with proper owner window handling, providing an alternative to the original `Shawn.Utils.Wpf.FileSystem.SelectFileHelper`
- The `OpenInExplorer` and `OpenInExplorerAndSelect` methods are pass-through to the original implementation
- Error handling is maintained (try-catch blocks)
- Null safety is preserved throughout

## Future Considerations

If/when the `Shawn.Utils` library is updated to support owner windows natively, we can:
1. Keep our wrapper for the enhanced automatic detection
2. Or remove our wrapper and update all call sites to use the native implementation

For now, the wrapper provides a clean, maintainable solution that's fully contained within the 1Remote repository.
