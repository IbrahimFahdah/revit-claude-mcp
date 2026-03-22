// NOTE: The window embedding feature in this file is unofficial and not affiliated with or endorsed by Anthropic.

using System;
using System.Diagnostics;
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
        static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_VISIBLE = 0x10000000;
        private const int SW_RESTORE = 9;


        private IntPtr _originalParent = IntPtr.Zero;

        private Process _claudeProc;
        private IntPtr _claudeHwnd = IntPtr.Zero;
        private bool _isEmbedded;

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

        public async Task AttachClaude()
        {
            // Check if Claude already running
            var existing = Process.GetProcessesByName("Claude");
            if (existing.Length > 0)
            {
                _claudeProc = existing[0];
            }
            else
            {
                var exePath = Environment.ExpandEnvironmentVariables(
                    @"%LOCALAPPDATA%\Claude\Claude.exe");
                _claudeProc = Process.Start(exePath);

                // Poll until Claude's window appears (up to 15s)
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);
                    _claudeProc.Refresh();
                    if (_claudeProc.MainWindowHandle != IntPtr.Zero) break;
                }
            }

            _claudeHwnd = _claudeProc.MainWindowHandle;
            if (_claudeHwnd == IntPtr.Zero)
            {
                _claudeHwnd = FindWindow(null, "Claude");
            }

            if (_claudeHwnd == IntPtr.Zero)
                throw new InvalidOperationException("Claude window not found.");

            _originalParent = GetParent(_claudeHwnd);
            var hostHandle = ((HwndSource)PresentationSource.FromVisual(ClaudeHost)).Handle;
            SetParent(_claudeHwnd, hostHandle);

            // Fit immediately
            MoveWindow(_claudeHwnd, 0, 0,
                (int)ClaudeHost.ActualWidth,
                (int)ClaudeHost.ActualHeight, true);

            _isEmbedded = true;
        }

        public void ReleaseClaude()
        {
            if (!_isEmbedded || _claudeHwnd == IntPtr.Zero) return;

            var targetParent = _originalParent != IntPtr.Zero
                ? _originalParent
                : GetDesktopWindow();

            SetParent(_claudeHwnd, targetParent);

            // Restore normal style
            var style = GetWindowLong(_claudeHwnd, GWL_STYLE);
            style |= WS_OVERLAPPEDWINDOW | WS_VISIBLE;
            SetWindowLong(_claudeHwnd, GWL_STYLE, style);

            // Restore position and focus
            ShowWindow(_claudeHwnd, SW_RESTORE);
            SetForegroundWindow(_claudeHwnd);

            _isEmbedded = false;
        }
    }
}
