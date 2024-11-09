using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Diagnostics.CodeAnalysis;

[assembly: SupportedOSPlatform("windows")]

namespace ClipboardMonitor
{
    public class ClipboardMonitorForm : Form
    {
        private NotifyIcon? trayIcon;
        private string previousContent = "";
        private IntPtr nextClipboardViewer;
        private System.Threading.Timer? reconnectTimer;
        private System.Threading.Timer? hideNotificationTimer;

        // Windows API imports
        [DllImport("user32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [SuppressUnmanagedCodeSecurity]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NotifyIcon))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ContextMenuStrip))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ToolStripMenuItem))]
        public ClipboardMonitorForm()
        {
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            SetupTrayIcon();
            InitializeClipboardMonitoring();
            SetupSystemEventHandlers();
            
            // Setup reconnect timer (checks every 5 minutes)
            var fiveMinutes = TimeSpan.FromMinutes(5);
            reconnectTimer = new System.Threading.Timer(
                _ => ReconnectClipboardChain(),
                null,
                fiveMinutes,
                fiveMinutes
            );
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon();
            
            // Get the icon from the current executable
            Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (appIcon != null)
            {
                trayIcon.Icon = appIcon;
            }
            else
            {
                trayIcon.Icon = SystemIcons.Application;
            }

            var contextMenu = new ContextMenuStrip();
            
            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += (s, e) => ShowAboutNotification();
            contextMenu.Items.Add(aboutItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => Application.Exit();
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.Text = "Clipboard Monitor";
            trayIcon.Visible = true;
        }

        private void ShowAboutNotification()
        {
            if (trayIcon == null) return;
            
            // Cancel any pending hide operations
            hideNotificationTimer?.Dispose();
            
            trayIcon.BalloonTipTitle = "";
            trayIcon.BalloonTipText = "Created By Andrew Hutchinson";
            trayIcon.BalloonTipIcon = (ToolTipIcon)0;
            
            // Show the notification
            trayIcon.ShowBalloonTip(5000);

            // Set up timer to hide the notification after exactly 5 seconds
            hideNotificationTimer = new System.Threading.Timer(
                _ => 
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => HideNotification()));
                    }
                    else
                    {
                        HideNotification();
                    }
                },
                null,
                5000, // delay before hiding
                Timeout.Infinite // don't repeat
            );
        }

        private void InitializeClipboardMonitoring()
        {
            try
            {
                nextClipboardViewer = SetClipboardViewer(this.Handle);
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize clipboard monitoring: {ex.Message}");
                ReconnectClipboardChain();
            }
        }

        private void SetupSystemEventHandlers()
        {
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    Thread.Sleep(1000); // Give system time to stabilize
                    ReconnectClipboardChain();
                    break;
            }
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionUnlock:
                    Thread.Sleep(1000); // Give system time to stabilize
                    ReconnectClipboardChain();
                    break;
            }
        }

        private bool IsClipboardChainValid()
        {
            try
            {
                // Try to access the clipboard - this will throw an exception if the chain is broken
                if (Clipboard.ContainsText())
                {
                    _ = Clipboard.GetText();
                    return true;
                }
                return true; // Return true if we can't check text but no exception occurred
            }
            catch
            {
                return false;
            }
        }

        private void ReconnectClipboardChain()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ReconnectClipboardChain));
                return;
            }

            try
            {
                // Only reconnect if the chain is invalid
                if (!IsClipboardChainValid())
                {
                    // Remove from current chain
                    if (nextClipboardViewer != IntPtr.Zero)
                    {
                        ChangeClipboardChain(this.Handle, nextClipboardViewer);
                    }

                    // Rejoin the chain
                    nextClipboardViewer = SetClipboardViewer(this.Handle);
                    LogError("Clipboard chain was broken and has been restored");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to reconnect clipboard chain: {ex.Message}");
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    OnClipboardChanged();
                    // Pass the message to the next viewer
                    if (nextClipboardViewer != IntPtr.Zero)
                    {
                        SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    }
                    break;

                case WM_CHANGECBCHAIN:
                    if (m.WParam == nextClipboardViewer)
                    {
                        nextClipboardViewer = m.LParam;
                    }
                    else if (nextClipboardViewer != IntPtr.Zero)
                    {
                        SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    }
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string currentContent = Clipboard.GetText();
                    if (currentContent != previousContent && !string.IsNullOrWhiteSpace(currentContent))
                    {
                        previousContent = currentContent;
                        ShowNotification(currentContent);
                    }
                }
            }
            catch
            {
                // Handle clipboard access errors silently
            }
        }

        private void ShowNotification(string text)
        {
            if (trayIcon == null) return;
            
            // Cancel any pending hide operations
            hideNotificationTimer?.Dispose();
            
            string displayText = text.Length > 100 ? text.Substring(0, 100) + "..." : text;

            trayIcon.BalloonTipTitle = "";
            trayIcon.BalloonTipText = displayText;
            trayIcon.BalloonTipIcon = (ToolTipIcon)0;
            
            // Hide any existing balloon
            trayIcon.Visible = false;
            trayIcon.Visible = true;
            
            // Show the new notification
            trayIcon.ShowBalloonTip(2000);

            // Set up timer to hide the notification after exactly 2 seconds
            hideNotificationTimer = new System.Threading.Timer(
                _ => 
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => HideNotification()));
                    }
                    else
                    {
                        HideNotification();
                    }
                },
                null,
                2000, // delay before hiding
                Timeout.Infinite // don't repeat
            );
        }

        private void HideNotification()
        {
            if (trayIcon == null) return;
            
            // This effectively hides the balloon tip
            trayIcon.Visible = false;
            trayIcon.Visible = true;
        }

        private void LogError(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cleanup notification timer
            hideNotificationTimer?.Dispose();
            
            // Cleanup system event handlers
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

            // Remove from clipboard chain
            if (nextClipboardViewer != IntPtr.Zero)
            {
                ChangeClipboardChain(this.Handle, nextClipboardViewer);
            }

            // Dispose of timer
            reconnectTimer?.Dispose();

            // Cleanup tray icon
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            base.OnFormClosing(e);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ClipboardMonitorForm());
        }
    }
}
