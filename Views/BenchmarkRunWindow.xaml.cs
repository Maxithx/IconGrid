using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IconGrid.Views
{
    /// <summary>
    /// Live window shown while a synthetic copy benchmark runs (100/300/500/1000 MB).
    /// Streams the BenchmarkRunner.exe stdout, logs into the ListBox, updates the
    /// progress bar and exposes a Stop button. Designed to work with the exact
    /// production copy pipeline (UsbCopyEngine.CopyPathsAsync).
    /// </summary>
    public partial class BenchmarkRunWindow : Window
    {
        private Process? _process;

        public BenchmarkRunWindow(string exePath, string[] args)
        {
            InitializeComponent();
            Owner = System.Windows.Application.Current?.MainWindow;
            DataContext = System.Windows.Application.Current?.MainWindow?.DataContext;
            Start(exePath, args);
        }

        private void Start(string exePath, string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                foreach (var a in args)
                {
                    psi.ArgumentList.Add(a);
                }

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) AppendLog(e.Data);
                };
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) AppendLog($"ERR {e.Data}");
                };
                _process.Exited += (_, _) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AppendLog("=== Benchmark færdig ===");
                        Stop_Click(this, new RoutedEventArgs());
                    }));
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // Initial log.
                AppendLog($"Starter benchmark: {Path.GetFileName(exePath)} {string.Join(' ', args)}");
                SetStatus("Kører...");
                SetProgress(0);
            }
            catch (Exception ex)
            {
                AppendLog($"Kunne ikke starte benchmark: {ex.Message}");
                SetStatus("Fejl");
            }
        }

        private void AppendLog(string line)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                if (LogBox.Items.Count > 500)
                {
                    LogBox.Items.RemoveAt(0);
                }
                LogBox.ScrollIntoView(LogBox.Items[^1]);
                ParseProgress(line);
            }));
        }

        private void ParseProgress(string line)
        {
            // BenchmarkRunner prints "x MB left" and "DONE ... avg_mib_s=" lines.
            // Heuristic: a line containing "MB left" indicates progress.
            var idx = line.IndexOf("MB left", StringComparison.Ordinal);
            if (idx >= 0)
            {
                SetStatus(line.Trim());
            }
        }

        private void SetStatus(string text) => StatusText.Text = text;
        private void SetProgress(double value) => ProgressBar.Value = value;

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // Best effort.
            }
            finally
            {
                if (LogBox.Items.Count > 0 && (LogBox.Items[^1]?.ToString()?.Contains("færdig") ?? false))
                {
                    Close_Click(sender, e);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // Best effort.
            }
            Close();
        }
    }
}