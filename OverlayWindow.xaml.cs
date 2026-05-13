using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ClaudeNotifier;

public partial class OverlayWindow : Window, IOverlayWindow
{
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int GWL_EXSTYLE = -20;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly ClaudeEvent _evt;

    public OverlayWindow(ClaudeEvent evt)
    {
        InitializeComponent();
        _evt = evt;
        MessageText.Text = evt.Message;
        var tag = !string.IsNullOrEmpty(evt.WtSession) && evt.WtSession.Length >= 8
            ? evt.WtSession.Substring(0, 8) : "?";
        TabText.Text = $"tab {tag}  ·  {evt.Cwd}";

        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            // Keep click-through OFF for the border (we want click-to-focus on the bubble),
            // but stay no-activate + tool-window so we don't steal focus when shown.
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        };

        // Position bottom-right primary screen
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Bottom - Height - 24;

        // Re-enable hit-test on the visible card so click works
        Root.IsHitTestVisible = true;
        Root.MouseLeftButtonUp += OnClickBubble;

        Loaded += (s, e) =>
        {
            var sb = (Storyboard)Resources["EnterStoryboard"];
            sb.Begin();
        };
    }

    private void OnClickBubble(object sender, MouseButtonEventArgs e)
    {
        if (App.Config.ClickFocusesTab)
        {
            UiaTabFocus.Focus(_evt);
        }
        BeginDismiss();
    }

    public void BeginDismiss()
    {
        var sb = (Storyboard)Resources["ExitStoryboard"];
        sb.Begin();
    }

    private void ExitStoryboard_Completed(object? sender, EventArgs e)
    {
        Close();
    }
}
