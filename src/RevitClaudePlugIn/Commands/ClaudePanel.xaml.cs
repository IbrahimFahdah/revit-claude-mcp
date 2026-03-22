// NOTE: The window embedding feature in this file is unofficial and not affiliated with or endorsed by Anthropic.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;


namespace RevitClaudePlugIn.Commands
{
    public partial class ClaudePanel : UserControl
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int SW_RESTORE = 9;


        private Process _claudeProc;
        private IntPtr _claudeHwnd = IntPtr.Zero;
        private bool _isEmbedded;
        internal bool IsPanelVisible { get; set; }

        public ClaudePanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await AttachClaude();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Claude embedding error: " + ex.Message);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ReleaseClaude();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Claude release error: " + ex.Message);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isEmbedded && _claudeHwnd != IntPtr.Zero)
            {
                MoveWindow(_claudeHwnd, 0, 0,
                    (int)ClaudeHost.ActualWidth,
                    (int)ClaudeHost.ActualHeight, true);
            }
        }

        /// <summary>
        /// Launches Claude regardless of install type (MSIX Store or legacy Squirrel).
        /// Returns the started process, or null if launched via shell URI (MSIX).
        /// </summary>
        private static Process LaunchClaude()
        {
            // Legacy Squirrel install
            var legacyPath = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Claude\Claude.exe");
            if (File.Exists(legacyPath))
                return Process.Start(legacyPath);

            // MSIX (Windows Store) install — launch via explorer shell URI.
            var aumid = FindMsixAumid();
            if (aumid == null)
            {
                MessageBox.Show(
                    "Claude installation not found.\nPlease install Claude from https://claude.ai/download and try again.",
                    "Claude Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return null;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}"
            });
            return null;
        }

        /// <summary>
        /// Looks up the Claude MSIX App User Model ID (AUMID) from the registry.
        /// AUMID format: PackageFamilyName!AppId (e.g. Claude_pzs8sxrjxfjjc!claude)
        /// Returns null if Claude is not installed as an MSIX package.
        /// </summary>
        private static string FindMsixAumid()
        {
            const string registryPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            using var packagesKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath);
            if (packagesKey == null) return null;

            foreach (var subKeyName in packagesKey.GetSubKeyNames())
            {
                if (!subKeyName.StartsWith("Claude_", StringComparison.OrdinalIgnoreCase)) continue;

                // Derive PackageFamilyName from PackageFullName
                // "Claude_1.1.7714.0_x64__pzs8sxrjxfjjc" → "Claude_pzs8sxrjxfjjc"
                var parts = subKeyName.Split("__");
                if (parts.Length != 2) continue;
                var familyName = $"{parts[0].Split('_')[0]}_{parts[1]}";

                // The AppId is the first subkey directly under the package key
                // e.g. Claude_1.1.7714.0_x64__pzs8sxrjxfjjc\Claude → AppId = "Claude"
                using var packageKey = packagesKey.OpenSubKey(subKeyName);
                if (packageKey == null) continue;

                var subKeys = packageKey.GetSubKeyNames();
                if (subKeys.Length == 0) continue;

                return $"{familyName}!{subKeys[0]}";
            }
            return null;
        }

        public async Task AttachClaude()
        {
            if (_isEmbedded) return;

            // Check if Claude already running
            var existing = Process.GetProcessesByName("Claude");
            if (existing.Length > 0)
            {
                _claudeProc = existing[0];
            }
            else
            {
                _claudeProc = LaunchClaude();

                // Poll until Claude's window appears (up to 15s).
                // For MSIX launches _claudeProc is null — find by name first, then wait for window.
                for (var i = 0; i < 30; i++)
                {
                    await Task.Delay(500);

                    if (_claudeProc == null)
                    {
                        var found = Process.GetProcessesByName("Claude");
                        if (found.Length > 0) _claudeProc = found[0];
                    }

                    if (_claudeProc != null)
                    {
                        _claudeProc.Refresh();
                        if (_claudeProc.MainWindowHandle != IntPtr.Zero) break;
                    }
                }
            }

            if (_claudeProc == null)
            {
                MessageBox.Show("Claude did not start within the expected time.", "Claude Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _claudeProc.Refresh();
            _claudeHwnd = _claudeProc.MainWindowHandle;

            // Electron apps sometimes recreate the main window during startup — validate and fallback
            if (_claudeHwnd == IntPtr.Zero || !IsWindow(_claudeHwnd))
                _claudeHwnd = FindWindow(null, "Claude");

            if (_claudeHwnd == IntPtr.Zero)
            {
                MessageBox.Show("Claude window not found. Try opening Claude manually.", "Claude Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PresentationSource.FromVisual(ClaudeHost) is not HwndSource source)
            {
                MessageBox.Show("Claude panel is not yet rendered. Try reopening the panel.", "Claude Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetParent(_claudeHwnd, source.Handle);

            // Fit immediately
            MoveWindow(_claudeHwnd, 0, 0,
                (int)ClaudeHost.ActualWidth,
                (int)ClaudeHost.ActualHeight, true);

            _isEmbedded = true;
        }

        public async Task RestartClaude()
        {
            ReleaseClaude();

            // Kill all running Claude processes
            foreach (var p in Process.GetProcessesByName("Claude"))
            {
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }
            _claudeProc = null;
            _claudeHwnd = IntPtr.Zero;

            if (IsPanelVisible)
                await AttachClaude();
            else
                _claudeProc = LaunchClaude();
        }

        public void ReleaseClaude()
        {
            if (!_isEmbedded || _claudeHwnd == IntPtr.Zero) return;

            // NULL parent promotes the window back to a true top-level window
            SetParent(_claudeHwnd, IntPtr.Zero);

            // Remove WS_CHILD (set by SetParent) and restore normal overlapped window style
            var style = GetWindowLong(_claudeHwnd, GWL_STYLE);
            style &= ~WS_CHILD;
            style |= WS_OVERLAPPEDWINDOW | WS_VISIBLE;
            SetWindowLong(_claudeHwnd, GWL_STYLE, style);

            // Restore position and focus
            ShowWindow(_claudeHwnd, SW_RESTORE);
            SetForegroundWindow(_claudeHwnd);

            _isEmbedded = false;
        }
    }
}
