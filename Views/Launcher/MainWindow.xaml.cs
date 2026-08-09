using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Threading;
using IconGrid.Helpers;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Settings;
using IconGrid.Models;
using IconGrid.ViewModels;
using IconGrid.ViewModels.Launcher;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using RadioButton = System.Windows.Controls.RadioButton;
using Forms = System.Windows.Forms;

namespace IconGrid.Views.Launcher
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly PawnIoWarningController _pawnIoWarningController;
        private readonly MonitorPollingController _monitorPollingController;
        private readonly LauncherWindowInterop _windowInterop;
        private LauncherWindowModeController? _windowModeController;
        private DevOverlayController? _devOverlayController;
        private LauncherDragDropHelper? _dragDropHelper;
        private LauncherShortcutActions? _shortcutActions;
        private LayoutMenuController? _layoutMenuController;
        private bool _isAnimatingHeight = true;
        private readonly DispatcherTimer? _autoHideTimer;
        private static readonly string PowerShellPath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe");
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private readonly Dictionary<string, string> _windowsShortcutTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "kontrolpanel", "Control Panel" },
            { "papirkurv", "Recycle Bin" },
            { "stifinder", "File Explorer" },
            { "skrivebord", "Desktop" },
            { "dokumenter", "Documents" },
            { "billeder", "Pictures" },
            { "videoer", "Videos" },
            { "musik", "Music" },
            { "overf?rsler", "Downloads" },
            { "galleri", "Gallery" },
            { "onedrive", "OneDrive" },
            { "taskmanager", "Task Manager" },
            { "windows powershell", "Windows PowerShell" },
            { "windows update", "Windows Update" },
            { "windows security", "Windows Security" },
            { "this pc", "This PC" },
            { "linux", "Linux" }
        };
        private bool _skipSavingLocation;

        private double _lastLoggedLeft = double.NaN;
        private double _lastLoggedTop = double.NaN;
        private readonly FloatingIconController _floatingIconController = new();
        private readonly SettingsWindowCoordinator _settingsWindowCoordinator = new();
        private readonly GamingOverlayWindowCoordinator _gamingOverlayWindowCoordinator = new();

        public MainWindow()
        {
            InitializeComponent();
            var baseTitle = Title ?? string.Empty;

            _viewModel = new MainViewModel();
            _pawnIoWarningController = new PawnIoWarningController(_viewModel);
            _monitorPollingController = new MonitorPollingController(_viewModel);
            _windowInterop = new LauncherWindowInterop(this, _viewModel, baseTitle, LogTrace);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ThemeHelper.ThemeChanged += ThemeHelper_ThemeChanged;
            SizeChanged += (_, _) => Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Background);
            _shortcutActions = new LauncherShortcutActions(
                this,
                _viewModel,
                PowerShellPath,
                ShowInputBox,
                _windowsShortcutTranslations);
            _shortcutActions.LoadWindowsShortcuts();
            _layoutMenuController = new LayoutMenuController(
                this,
                _viewModel,
                () => _windowInterop.Handle,
                LogTrace,
                ShowGamingOverlay,
                RefreshLayoutCardSelection);

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _windowModeController = new LauncherWindowModeController(
                this,
                _viewModel,
                _floatingIconController,
                _autoHideTimer,
                _windowInterop.TrayIcon,
                _monitorPollingController.SetPollingEnabled);
            _devOverlayController = new DevOverlayController(this, _viewModel);
            _dragDropHelper = new LauncherDragDropHelper(_viewModel, IsOverLauncherTile);

            _viewModel.GameLaunched += OnGameLaunched;
            _viewModel.GameExited += OnGameExited;
            _gamingOverlayWindowCoordinator.OverlayClosed += OnGamingOverlayClosed;
            _monitorPollingController.Start();
            _pawnIoWarningController.Update();
            LogTrace($"MainWindow created. Elevated={IsCurrentProcessElevated()}");

            // Start in floating icon mode; final position is set on load
            Left = 0;
            Top = 0;
            ResizeMode = ResizeMode.NoResize;

            _windowInterop.InitializeTrayIcon(EnterFullMode, ExitApplication);
            _windowInterop.ApplyDynamicIcon();
            _devOverlayController.Initialize();
        }

        private bool IsOverLauncherTile(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is System.Windows.Controls.Button btn && btn.DataContext is LauncherItem)
                {
                    return true;
                }

                obj = VisualTreeHelper.GetParent(obj);
            }

            return false;
        }

        private void ThemeHelper_ThemeChanged(object? sender, ThemeSnapshot e)
        {
            // Ensure icon updates when Windows accent/theme changes while the app is running.
            Dispatcher.BeginInvoke(() => _ = _windowInterop.RefreshTaskbarIconAsync(), DispatcherPriority.Background);
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _windowInterop.Initialize();

            // Capture measured header height once layout is available.
            Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Loaded);
        }
        private void EnterFullMode()
        {
            _windowModeController?.EnterFullMode();
        }

        private void EnterFloatingMode()
        {
            _windowModeController?.EnterFloatingMode();
        }

        private void PositionFloatingIcon(bool preferSaved = true)
        {
            _windowModeController?.PositionFloatingIcon(preferSaved);
        }

        private void ClampWindowToWorkArea()
        {
            _windowModeController?.ClampWindowToWorkArea();
        }

        private void ClampFloatingIconToWorkArea()
        {
            _windowModeController?.ClampFloatingIconToWorkArea();
        }

        private void LogTrace(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = Path.Combine(appData, "IconGrid");
                Directory.CreateDirectory(folder);
                var logPath = Path.Combine(folder, "trace.log");
                var line = $"[{DateTime.Now:O}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // ignore logging issues
            }
        }

        private static bool IsCurrentProcessElevated()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.StartDirectlyInLauncher)
            {
                EnterFullMode();
            }
            else
            {
                EnterFloatingMode();
                PositionFloatingIcon();
            }
            Dispatcher.BeginInvoke(UpdateHeaderHeightFromVisuals, DispatcherPriority.Background);

        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _skipSavingLocation = false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            _skipSavingLocation = true;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // no-op; we no longer need to reset positions after WinKey
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.IsOverlayOpen), StringComparison.OrdinalIgnoreCase))
            {
                // If overlay is closing, we want to suppress the window height animation that would normally fire.
                if (!_viewModel.IsOverlayOpen)
                {
                    _isAnimatingHeight = false;
                }
            }

            if (string.Equals(e.PropertyName, nameof(MainViewModel.Language), StringComparison.OrdinalIgnoreCase))
            {
                _shortcutActions?.LoadWindowsShortcuts();
                _windowInterop.RefreshTrayIconMenuLabels();
            }

            // Handle Dynamic Layout when icon panel expands/collapses
            if (_viewModel.IsFullWindowVisible &&
                (string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredHeightEffective), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredWidth), StringComparison.OrdinalIgnoreCase)))
            {
                var durationMs = (_viewModel.EnableSlideUpAnimation && _isAnimatingHeight) ? _viewModel.WindowAnimationDurationMs : 0;
                var duration = TimeSpan.FromMilliseconds(durationMs);
                var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

                if (string.Equals(e.PropertyName, nameof(MainViewModel.WindowDesiredHeightEffective), StringComparison.OrdinalIgnoreCase))
                {
                    var heightAnim = new DoubleAnimation(_viewModel.WindowDesiredHeightEffective, duration) { EasingFunction = ease };
                    this.BeginAnimation(HeightProperty, heightAnim);
                }
                else // Width
                {
                    // Animate width change with a shorter duration
                    var widthAnim = new DoubleAnimation(_viewModel.WindowDesiredWidth, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
                    this.BeginAnimation(WidthProperty, widthAnim);
                }

                // Reset the flag so subsequent height changes are animated again.
                _isAnimatingHeight = true;
            }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.LayoutIconGridSlot), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.LayoutPreset), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.AccentBrush), StringComparison.OrdinalIgnoreCase))
        {
            // Keep the layout card highlighting in sync when preset or slot changes.
            Dispatcher.BeginInvoke(RefreshLayoutCardSelection, DispatcherPriority.Input);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.LayoutReserveIconGridSlot), StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(RefreshLayoutCardSelection, DispatcherPriority.Input);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.ShowDevOverlay), StringComparison.OrdinalIgnoreCase))
        {
            _devOverlayController?.UpdateVisibility();
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.LauncherHideMode), StringComparison.OrdinalIgnoreCase))
        {
            _windowModeController?.ApplyIdleHideMode();
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsSettingsWindowOpen), StringComparison.OrdinalIgnoreCase))
        {
            if (!_viewModel.IsSettingsWindowOpen)
            {
                // Settings window just closed — mouse is likely outside the launcher.
                // Restart the idle auto-hide timer so it can fire naturally.
                _windowModeController?.HandleMouseLeave();
            }
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.AccentBrush), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.IsLightTheme), StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(() => _ = _windowInterop.RefreshTaskbarIconAsync(), DispatcherPriority.Background);
        }
    }

    private void UpdateHeaderHeightFromVisuals()
    {
        try
        {
            var top = TopBarGrid?.ActualHeight ?? 0;
            var tabs = TabsBorder?.ActualHeight ?? 0;
            var measured = top + tabs;
            _viewModel.SetHeaderHeight(measured);
        }
        catch (Exception ex)
        {
            LogTrace($"UpdateHeaderHeightFromVisuals failed: {ex}");
        }
    }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsFullWindowVisible)
                return;

            ShowInTaskbar = true;
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.StartDirectlyInLauncher)
            {
                ExitApplication();
            }
            else
            {
                EnterFloatingMode();
            }
        }

        private void FloatingIconButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _floatingIconController.HandleMouseLeftButtonDown(this, _viewModel, e);
        }

        private void FloatingIconButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _floatingIconController.HandleMouseMove(this, _viewModel, e);
        }

        private void FloatingIconButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_floatingIconController.HandleMouseLeftButtonUp(_viewModel, e))
            {
                EnterFullMode();
            }
        }

        private void FloatingIconButton_Click(object sender, RoutedEventArgs e)
        {
            if (_floatingIconController.HandleClick())
                EnterFullMode();
        }

        private void FloatingIconExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            // If the launcher is idle-hidden, a click on the peek strip should show it
            // instead of starting a drag.
            if (_windowModeController?.IsIdleHideActive == true && _windowModeController.IsHidden)
            {
                _windowModeController.PeekStripClicked();
                e.Handled = true;
                return;
            }

            DragMove();

            // Clamp to work area after drag so the user can't pull the window
            // entirely off-screen. Multi-monitor drag bypasses clamping when
            // the AllowMultiMonitorDrag flag is set.
            if (!_viewModel.AllowMultiMonitorDrag)
            {
                _windowModeController?.ClampWindowToWorkArea();
            }
        }

        private void IconScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Ensure ScrollableHeight updates promptly when content size changes.
            if (sender is ScrollViewer sv)
            {
                sv.InvalidateMeasure();
            }
        }

        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleWindowDragOver(sender, e);
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleWindowDrop(sender, e);
        }

        private void IdleHideButton_Click(object sender, RoutedEventArgs e)
        {
            _windowModeController?.ToggleManualHide();
        }

        // WPF DragEventArgs (fully-qualified to avoid ambiguity with WinForms)
        private void ItemsControl_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleItemsControlDragOver(sender, e);
        }

        private void ItemsControl_Drop(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleItemsControlDrop(sender, e);
        }

        private void LauncherItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragDropHelper?.HandleLauncherItemPreviewMouseLeftButtonDown(sender, e);
        }

        private void LauncherItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _dragDropHelper?.HandleLauncherItemPreviewMouseMove(sender, e);
        }

        private void LauncherItem_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleLauncherItemDragOver(sender, e);
        }

        private void LauncherItem_Drop(object sender, System.Windows.DragEventArgs e)
        {
            _dragDropHelper?.HandleLauncherItemDrop(sender, e);
        }

        private void RenameMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.RenameItem(item);
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _windowModeController?.HandleMouseLeave();
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _windowModeController?.HandleMouseEnter();
        }

        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            _windowModeController?.HandleAutoHideTick();
        }

        private string ShowInputBox(string prompt, string title, string defaultValue)
        {
            // Midlertidig ? kan senere erstattes af en rigtig WPF-dialog
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);
        }

        private void RemoveItemMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.RemoveItem(item);
        }

        private void LauncherItemButton_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.DataContext is not LauncherItem item)
                return;

            e.Handled = true;
            _shortcutActions?.LaunchItem(item);
        }

        private void OpenItemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.OpenItem(item);
        }

        private void RunAsAdminMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.RunAsAdmin(item);
        }

        private void OpenLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.OpenLocation(item);
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.CopyPath(item);
        }

        private void ChangeIconMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.ChangeIcon(item, menuItem.Tag as string);
        }

        private void ResetIconMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            _shortcutActions?.ResetIcon(item);
        }

        private void AddShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _shortcutActions?.AddShortcut();
        }

        private void AddPowerShellCustomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _shortcutActions?.AddPowerShellCustom();
        }

        private void AddWindowsShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not LauncherShortcutActions.WindowsShortcutTemplate template)
                return;

            _shortcutActions?.AddWindowsShortcut(template);
        }

        private void AddPowerShellPresetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem mi || mi.Tag is not string args)
                return;

            var label = mi.Header?.ToString() ?? "PowerShell";
            _shortcutActions?.AddPowerShellPreset(args, label);
        }

        private void AddAllWindowsShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _shortcutActions?.AddAllWindowsShortcuts();
        }

        private void ContentArea_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (IsOverLauncherTile(e.OriginalSource as DependencyObject))
            {
                return;
            }

            var menu = new System.Windows.Controls.ContextMenu();
            DevInspector.SetMetadata(menu, "Icon grid context menu → Views/MainWindow.xaml (ContentArea_ContextMenuOpening)");

            string AddShortcutText() => LocalizationHelper.Get(_viewModel.Language, "AddShortcut");
            string AddPowerShellText() => LocalizationHelper.Get(_viewModel.Language, "AddPowerShellShortcut");
            string CustomText() => LocalizationHelper.Get(_viewModel.Language, "Custom");
            string AddWindowsText() => LocalizationHelper.Get(_viewModel.Language, "AddWindowsShortcut");
            string AddAllText() => LocalizationHelper.Get(_viewModel.Language, "AddAll");
            string ClearCategoryText() => LocalizationHelper.Get(_viewModel.Language, "ClearCategory");
            string ClearCategoryConfirmText() => LocalizationHelper.Get(_viewModel.Language, "ClearCategoryConfirm");

            var addShortcut = new System.Windows.Controls.MenuItem { Header = AddShortcutText() };
            DevInspector.SetMetadata(addShortcut, "Add shortcut menu item → Views/MainWindow.xaml (AddShortcutMenuItem_Click)");
            addShortcut.Click += AddShortcutMenuItem_Click;
            menu.Items.Add(addShortcut);

            var psMenu = new System.Windows.Controls.MenuItem { Header = AddPowerShellText() };
            DevInspector.SetMetadata(psMenu, "Add PowerShell submenu → Views/MainWindow.xaml (AddPowerShellCustomMenuItem_Click)");
            var customPs = new System.Windows.Controls.MenuItem { Header = CustomText() };
            DevInspector.SetMetadata(customPs, "Custom PowerShell menu item → Views/MainWindow.xaml (AddPowerShellCustomMenuItem_Click)");
            customPs.Click += AddPowerShellCustomMenuItem_Click;
            psMenu.Items.Add(customPs);

            menu.Items.Add(psMenu);
            menu.Items.Add(new Separator());

            if (_shortcutActions?.WindowsShortcuts.Any() == true)
            {
                var winMenu = new System.Windows.Controls.MenuItem { Header = AddWindowsText() };
                DevInspector.SetMetadata(winMenu, "Add Windows shortcuts submenu → Views/MainWindow.xaml (AddWindowsShortcutMenuItem_Click)");
                var addAll = new System.Windows.Controls.MenuItem { Header = AddAllText() };
                DevInspector.SetMetadata(addAll, "Add all Windows shortcuts menu item → Views/MainWindow.xaml (AddAllWindowsShortcutsMenuItem_Click)");
                addAll.Click += AddAllWindowsShortcutsMenuItem_Click;
                winMenu.Items.Add(addAll);
                winMenu.Items.Add(new Separator());

                foreach (var shortcut in _shortcutActions!.WindowsShortcuts)
                {
                    var item = new System.Windows.Controls.MenuItem
                    {
                        Header = shortcut.DisplayName,
                        Tag = shortcut
                    };
                    DevInspector.SetMetadata(item, $"Add Windows shortcut '{shortcut.DisplayName}' → Views/MainWindow.xaml (AddWindowsShortcutMenuItem_Click)");
                    item.Click += AddWindowsShortcutMenuItem_Click;
                    winMenu.Items.Add(item);
                }

                menu.Items.Add(winMenu);
            }

            menu.Items.Add(new Separator());

            var clearCategory = new System.Windows.Controls.MenuItem { Header = ClearCategoryText() };
            DevInspector.SetMetadata(clearCategory, "Clear category menu item → Views/MainWindow.xaml (Clear current category)");
            clearCategory.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_viewModel.SelectedTab))
                    return;

                var result = System.Windows.MessageBox.Show(
                    ClearCategoryConfirmText(),
                    ClearCategoryText(),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.ClearCurrentCategory();
                }
            };
            menu.Items.Add(clearCategory);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void ChangeIconFromPackMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.DataContext is not LauncherItem item)
                return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose icon from custom pack",
                Filter = "Icon and image files (*.ico;*.png;*.jpg)|*.ico;*.png;*.jpg|All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true,
                InitialDirectory = Directory.Exists(_viewModel.IconPackFolder)
                    ? _viewModel.IconPackFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dialog.ShowDialog(this) == true)
            {
                _viewModel.UpdateItemIcon(item, dialog.FileName, 0);
            }
        }

        private void MoreButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _settingsWindowCoordinator.Show(
                this,
                _viewModel,
                value => _skipSavingLocation = value,
                LogTrace);
        }

        private void ArrangeNowButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ArrangeWindowsFromPreset(_viewModel.LayoutPreset);
        }

        private void SaveLayoutAsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PromptAndSaveLayout();
        }

        private void SaveLayoutAsMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            PromptAndSaveLayout();
        }

        internal void PromptAndSaveLayout()
        {
            _layoutMenuController?.PromptAndSaveLayout();
        }

        private void CloseLayoutsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsLayoutsOpen = false;
        }

        private void CloseHelpButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsHelpOpen = false;
        }

        private void LayoutPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu == null) return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        private void LogoButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.IsSettingsOpen = false;
            _viewModel.IsLayoutsOpen = false;
            ArrangeWindowsFromPreset(_viewModel.LayoutPreset);
        }

        private void LayoutPresetMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _layoutMenuController?.LayoutPresetMenuItem_Click(sender, e);
        }

        private void LayoutSlotButton_Click(object sender, RoutedEventArgs e)
        {
            _layoutMenuController?.LayoutSlotButton_Click(sender, e);
        }

        private void LayoutLinkButton_Click(object sender, RoutedEventArgs e)
        {
            _layoutMenuController?.LayoutLinkButton_Click(sender, e);
        }

        private void LayoutContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu menu) return;
            _layoutMenuController?.PopulateLayoutMenu(menu.Items);
            _layoutMenuController?.UpdateLayoutMenuChecks(menu.Items);
        }

        private void LayoutsPageMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ContextMenu menu) return;
            _layoutMenuController?.PopulateLayoutMenu(menu.Items);
            _layoutMenuController?.UpdateLayoutMenuChecks(menu.Items);
        }

        private void RenameLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _layoutMenuController?.RenameLayoutMenuItem_Click(sender, e);
        }

        private void DeleteLayoutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _layoutMenuController?.DeleteLayoutMenuItem_Click(sender, e);
        }

        private void ShowGamingOverlay(GamingOverlayLayout layout)
        {
            _gamingOverlayWindowCoordinator.Show(this, _viewModel, layout);
        }

        private void OnGameLaunched()
        {
            // Apply the configured launcher behavior: 0=None, 1=AutoHide, 2=MinimizeToTaskbar.
            _windowModeController?.HideForGame(_viewModel.GameLauncherAutoBehavior);

            // Optionally show the gaming overlay at the chosen "Default position".
            if (_viewModel.AutoShowGamingOverlayOnGameStart)
            {
                ShowGamingOverlay(GamingOverlayLayout.Horizontal);
            }
        }

        private void OnGameExited()
        {
            // Optionally close the gaming overlay when the game exits.
            if (_viewModel.AutoCloseGamingOverlayOnGameEnd)
            {
                _gamingOverlayWindowCoordinator.Close();
            }
        }

        private void OnGamingOverlayClosed()
        {
            // Always clear the game-hide state so idle-hide/peek can work again.
            // When the overlay is closed (manually or automatically), the
            // _isGameHideActive flag must be reset — otherwise the proximity
            // timer returns immediately and the peek strip becomes dead.
            //
            // RestoreLauncherFromGame handles this safely: it clears the flag,
            // un-minimizes the window, slides to visible if hidden, and
            // re-applies the normal idle-hide mode so auto-hide/manual-hide
            // continue to work exactly as configured.
            _windowModeController?.RestoreLauncherFromGame();
        }

        private void RefreshLayoutCardSelection()
        {
            try
            {
                var accent = _viewModel.AccentBrush;
                var normal = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));
                var dim = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));

                if (LayoutCardsHost == null)
                    return;

                foreach (var child in LayoutCardsHost.Children)
                {
                    if (child is Border card)
                    {
                        foreach (var button in FindVisualChildren<System.Windows.Controls.Button>(card))
                        {
                            if (button.Tag is not string tag)
                                continue;

                            if (tag.Contains("Link"))
                            {
                                var linkParts = tag.Split('|');
                                if (linkParts.Length == 4 && int.TryParse(linkParts[2], out var la) && int.TryParse(linkParts[3], out var lb))
                                {
                                    var active = _viewModel.LayoutLinks.TryGetValue(_viewModel.LayoutPreset, out var link) && link.Length == 2 && link[0] == la && link[1] == lb;
                                    button.Background = active ? accent : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                                    button.BorderBrush = active ? accent : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
                                    if (button.Content is TextBlock tb)
                                    {
                                        tb.Foreground = active ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
                                    }
                                }
                            }
                                else
                                {
                                    if (!int.TryParse(tag, out var slot))
                                        continue;

                                    var slotEnabled = _viewModel.LayoutReserveIconGridSlot;
                                    button.IsEnabled = slotEnabled;
                                    button.Opacity = slotEnabled ? 1 : 0.6;

                                    // Highlight purely by selected slot so user clicks always show.
                                    var isMatch = slotEnabled && slot == _viewModel.LayoutIconGridSlot;

                                    // Use app accent for selected so it matches the logo color.
                                    var selectedBg = _viewModel.AccentBrush as System.Windows.Media.SolidColorBrush
                                                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
                                    var unselectedBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55)); // dark tile
                                    var unselectedBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 32, 48));

                                    button.Background = isMatch ? selectedBg : unselectedBg;
                                button.BorderBrush = isMatch ? selectedBg : unselectedBorder;
                                button.BorderThickness = isMatch ? new System.Windows.Thickness(2) : new System.Windows.Thickness(0);
                                button.Foreground = System.Windows.Media.Brushes.White;

                                        if (button.Content is TextBlock tb)
                                        {
                                            var showIg = slotEnabled && isMatch;
                                            tb.Text = showIg ? "IG" : $"{slot + 1}";
                                        }
                                    }
                                }
                            }
                }
            }
            catch
            {
                // best effort
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        internal void ArrangeWindowsFromPreset(string presetName)
        {
            _layoutMenuController?.ArrangeWindowsFromPreset(presetName);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _monitorPollingController.Stop();
            _autoHideTimer?.Stop();
            HardwareMonitorTaskManager.SignalCurrentAgentToStop(LogTrace);
            if (_viewModel != null)
            {
                _viewModel.GameLaunched -= OnGameLaunched;
                _viewModel.GameExited -= OnGameExited;
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.SystemMonitor.Dispose();
            }

            _gamingOverlayWindowCoordinator.OverlayClosed -= OnGamingOverlayClosed;
            _pawnIoWarningController.Close();
            _settingsWindowCoordinator.Close();
            _gamingOverlayWindowCoordinator.Close();
            ThemeHelper.ThemeChanged -= ThemeHelper_ThemeChanged;
            _windowInterop.Cleanup();
        }

        private void ExitApplication()
        {
            _monitorPollingController.Stop();
            _autoHideTimer?.Stop();
            HardwareMonitorTaskManager.SignalCurrentAgentToStop(LogTrace);
            _pawnIoWarningController.Close();
            _settingsWindowCoordinator.Close();
            _gamingOverlayWindowCoordinator.Close();
            _windowInterop.Cleanup();
            var application = System.Windows.Application.Current;
            var windows = application?.Windows;
            if (windows != null)
            {
                foreach (var window in windows.OfType<System.Windows.Window>().ToList())
                {
                    if (window != this)
                    {
                        window.Close();
                    }
                }
            }
            application?.Shutdown();
            System.Environment.Exit(0);
        }

        // Ensure clicking a tab sets SelectedTab on the VM and closes overlays.
        private void TabToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.DataContext is not string tabName)
                return;

            _viewModel.IsSettingsOpen = false;

            // If the animation feature is enabled, handle the toggle/expand logic.
            if (_viewModel.EnableSlideUpAnimation)
            {
                // If the same tab is clicked, toggle the panel's visibility.
                if (string.Equals(_viewModel.SelectedTab, tabName, StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.IsIconPanelExpanded = !_viewModel.IsIconPanelExpanded;
                }
                else
                {
                    // If a different tab is clicked, always expand the panel and switch tabs.
                    _viewModel.IsIconPanelExpanded = true;
                    _viewModel.SelectedTab = tabName;
                }
            }
            else
            {
                // Default behavior when animation is disabled: just switch tabs.
                _viewModel.IsIconPanelExpanded = true; // Ensure it's always expanded
                if (!string.Equals(_viewModel.SelectedTab, tabName, StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.SelectedTab = tabName;
                }
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            var moved = double.IsNaN(_lastLoggedLeft)
                        || Math.Abs(Left - _lastLoggedLeft) > 0.5
                        || Math.Abs(Top - _lastLoggedTop) > 0.5;
            if (moved)
            {
                LogTrace($"OnLocationChanged skip={_skipSavingLocation}, pos={Left:F1},{Top:F1}, size={Width:F1}x{Height:F1}, state={WindowState}");
                _lastLoggedLeft = Left;
                _lastLoggedTop = Top;
            }

            if (_viewModel.IsFullWindowVisible && WindowState == WindowState.Normal)
            {
                if (!_skipSavingLocation)
                {
                    _viewModel.SaveWindowPosition(Left, Top);
                }
            }
            else if (!_viewModel.IsFullWindowVisible && WindowState == WindowState.Normal)
            {
                _floatingIconController.HandleLocationChanged(this, _viewModel, _skipSavingLocation);
            }
        }
    }
}