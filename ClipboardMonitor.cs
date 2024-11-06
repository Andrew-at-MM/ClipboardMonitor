using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;

namespace ClipboardMonitor
{
    public class ClipboardMonitorForm : Form
    {
        private NotifyIcon? trayIcon;  // Made nullable
        private string previousContent = "";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        public ClipboardMonitorForm()
        {
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            SetupTrayIcon();
            SetClipboardViewer(this.Handle);
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon();
            
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");  // Moved to local variable
            if (File.Exists(iconPath))
            {
                trayIcon.Icon = new Icon(iconPath);
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
            trayIcon.Text = "Clipboard Monitor (Right-click to exit)";
            trayIcon.Visible = true;
        }

        private void ShowAboutNotification()
        {
            if (trayIcon == null) return;  // Added null check
            
            trayIcon.BalloonTipTitle = "";
            trayIcon.BalloonTipText = "Created By Andrew Hutchinson";
            trayIcon.BalloonTipIcon = (ToolTipIcon)0;
            trayIcon.ShowBalloonTip(5000);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DRAWCLIPBOARD = 0x0308;

            if (m.Msg == WM_DRAWCLIPBOARD)
            {
                OnClipboardChanged();
            }

            base.WndProc(ref m);
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
            catch  // Removed unused ex variable
            {
                // Handle clipboard access errors silently
            }
        }

        private void ShowNotification(string text)
        {
            if (trayIcon == null) return;  // Added null check
            
            string displayText = text.Length > 100 ? text.Substring(0, 100) + "..." : text;

            trayIcon.BalloonTipTitle = "";
            trayIcon.BalloonTipText = displayText;
            trayIcon.BalloonTipIcon = (ToolTipIcon)0;
            
            // Hide any existing balloon
            trayIcon.Visible = false;
            trayIcon.Visible = true;
            
            trayIcon.ShowBalloonTip(2000);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (trayIcon != null)  // Added null check
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();  // Added proper disposal
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ClipboardMonitorForm());
        }
    }
}