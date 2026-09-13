using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using IconGrid.Helpers;
using IconGrid.Helpers.Hardware;
using IconGrid.Helpers.Launcher;
using IconGrid.Helpers.Logging;
using IconGrid.Helpers.Settings;
using WinForms = System.Windows.Forms;
using IconGrid.Views;
using IconGrid.Views.Launcher;

namespace IconGrid;

public partial class App : System.Windows.Application
{
    private bool _isMonitorAgentMode;
    private bool _isFpsEtwProbeMode;
    private bool _isWindowsStartupLaunch;
    private SingleInstanceGuard? _singleInstanceGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        TrySetAppUserModelId();
        TryEnablePerMonitorDpiAwareness();
        WinForms.Application.SetHighDpiMode(WinForms.HighDpiMode.PerMonitorV2);
        base.OnStartup(e);

        // Ensure an already-oversized trace.log (from an older build) is trimmed
        // even before the first write of this session.
        AppTrace.EnforceSizeLimit();

        if (e.Args.Any(arg => string.Equals(arg, "--install-startup-task", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var configManager = new ConfigManager();
                var config = configManager.LoadConfig();
                var exePath = Environment.ProcessPath ?? string.Empty;
                var ok = StartupTaskManager.ApplyStartupMode(exePath, config.StartupLaunchMode, config.StartWithWindows);
                WriteTrace($"Startup task installer finished. Success={ok}, Mode={config.StartupLaunchMode}, Enabled={config.StartWithWindows}");
                Shutdown(ok ? 0 : -1);
            }
            catch (Exception ex)
            {
                ReportFatal(ex, "startup-task-installer");
                Shutdown(-1);
            }
            return;
        }

        _isWindowsStartupLaunch = e.Args.Any(arg => string.Equals(arg, "--startup-launch", StringComparison.OrdinalIgnoreCase));
        _isMonitorAgentMode = e.Args.Any(arg => string.Equals(arg, "--monitor-agent", StringComparison.OrdinalIgnoreCase));
        _isFpsEtwProbeMode = e.Args.Any(arg => string.Equals(arg, "--fps-etw-probe-agent", StringComparison.OrdinalIgnoreCase));

        WriteTrace("OnStartup begin");
        RegisterGlobalExceptionHandlers();

        try
        {
            if (_isMonitorAgentMode)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                WriteTrace($"Starting hardware monitor agent mode. Elevated={IsCurrentProcessElevated()}");
                var exitCode = HardwareMonitorAgent.Run(e.Args, WriteTrace);
                Shutdown(exitCode);
                return;
            }

            if (_isFpsEtwProbeMode)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                WriteTrace($"Starting elevated ETW probe mode. Elevated={IsCurrentProcessElevated()}");
                var exitCode = FpsEtwProbeAgent.Run(e.Args, WriteTrace);
                Shutdown(exitCode);
                return;
            }

            WriteTrace($"Starting launcher UI. Elevated={IsCurrentProcessElevated()}");

            // Single-instance guard. A second launch must NOT create a second
            // launcher: it could not take over the hardware-monitor agent mutex and
            // would inherit the previous session's stale FPS target (which showed
            // the gaming overlay with no game running). Instead, ask the running
            // instance to come to the foreground and exit this one.
            _singleInstanceGuard = new SingleInstanceGuard();
            if (!_singleInstanceGuard.TryAcquire())
            {
                WriteTrace("Another IconGrid launcher is already running. Activating it and exiting this instance.");
                SingleInstanceGuard.SignalExistingInstance();
                _singleInstanceGuard.Dispose();
                _singleInstanceGuard = null;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Shutdown(0);
                return;
            }

            _singleInstanceGuard.ListenForActivation(Dispatcher, ActivateRunningLauncher);

            if (_isWindowsStartupLaunch)
            {
                WriteTrace("Windows startup launch detected; monitor agent is expected to be started by its own scheduled task.");
            }
            else
            {
                HardwareMonitorTaskManager.StartAgent(() => Environment.ProcessPath ?? string.Empty, WriteTrace);
            }
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            if (IsCurrentProcessElevated())
            {
                WriteTrace("Launcher UI is running elevated; drag-and-drop from Explorer may be blocked by UIPI.");
                System.Windows.MessageBox.Show(
                    "IconGrid launcher UI is running with administrator privileges.\n\n" +
                    "The UI should normally run as a standard user. If drag-and-drop from Explorer does not work, " +
                    "start IconGrid without elevation.",
                    "IconGrid",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            WriteTrace("MainWindow shown");
        }
        catch (Exception ex)
        {
            ReportFatal(ex, "startup");
            Shutdown(-1);
        }
    }

    private void ActivateRunningLauncher()
    {
        try
        {
            if (MainWindow is IconGrid.Views.Launcher.MainWindow window)
            {
                window.ActivateFromSecondInstance();
            }
        }
        catch (Exception ex)
        {
            WriteTrace($"Failed to activate the running launcher instance: {ex.Message}");
        }
    }

    private void TrySetAppUserModelId()
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID("IconGrid.Desktop.1.0");
            }
            catch
            {
                // ignore
            }
        }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ReportFatal(args.Exception, "dispatcher");
            args.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                ReportFatal(ex, "appdomain");
            }
            else
            {
                ReportFatal(new Exception(args.ExceptionObject?.ToString() ?? "Unknown"), "appdomain");
            }

            Shutdown(-1);
        };
    }

    private void ReportFatal(Exception ex, string source)
    {
        WriteTrace($"Fatal from {source}: {ex}");

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = System.IO.Path.Combine(appData, "IconGrid");
            System.IO.Directory.CreateDirectory(folder);
            var logPath = System.IO.Path.Combine(folder, "error.log");
            var message = $"[{DateTime.Now:O}] Source={source}: {ex}{Environment.NewLine}";
            System.IO.File.AppendAllText(logPath, message);
        }
        catch
        {
            // ignore logging failures
        }

        if (_isMonitorAgentMode || _isFpsEtwProbeMode)
        {
            return;
        }

        System.Windows.MessageBox.Show(
            $"IconGrid hit a fatal error ({source}).{Environment.NewLine}{Environment.NewLine}{ex}",
            "IconGrid",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void TryEnablePerMonitorDpiAwareness()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorV2))
                return;

            // Fallback for older Windows 10 builds (Per-Monitor V1)
            SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
        }
        catch (DllNotFoundException)
        {
            // Older Windows builds; ignore
        }
        catch (EntryPointNotFoundException)
        {
            // API missing; ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enable per-monitor DPI awareness: {ex}");
        }
    }

    private static readonly IntPtr DpiAwarenessContextPerMonitorV2 = new IntPtr(-4);

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private enum ProcessDpiAwareness
    {
        ProcessDpiUnaware = 0,
        ProcessSystemDpiAware = 1,
        ProcessPerMonitorDpiAware = 2
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(ProcessDpiAwareness awareness);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    private void WriteTrace(string message)
    {
        // Delegated to the shared, size-capped trace writer.
        AppTrace.Write(message);
    }
}
