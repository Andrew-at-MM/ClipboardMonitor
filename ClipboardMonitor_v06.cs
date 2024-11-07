private System.Threading.Timer? hideNotificationTimer;

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

private void HideNotification()
{
    if (trayIcon == null) return;
    
    // This effectively hides the balloon tip
    trayIcon.Visible = false;
    trayIcon.Visible = true;
}

protected override void OnFormClosing(FormClosingEventArgs e)
{
    // Cleanup notification timer
    hideNotificationTimer?.Dispose();
    
    // ... rest of your cleanup code ...
    SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

    if (nextClipboardViewer != IntPtr.Zero)
    {
        ChangeClipboardChain(this.Handle, nextClipboardViewer);
    }

    reconnectTimer?.Dispose();

    if (trayIcon != null)
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
    }

    base.OnFormClosing(e);
}
