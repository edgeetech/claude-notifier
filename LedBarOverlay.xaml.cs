using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace ClaudeNotifier;

public partial class LedBarOverlay : Window, IOverlayWindow
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly ClaudeEvent _evt;

    public LedBarOverlay(ClaudeEvent evt)
    {
        InitializeComponent();
        _evt = evt;

        MessageText.Text = evt.Message;
        var tag = !string.IsNullOrEmpty(evt.WtSession) && evt.WtSession.Length >= 8
            ? evt.WtSession.Substring(0, 8) : "?";
        TabText.Text = $"tab {tag}   ·   {evt.Cwd}";

        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        };

        var area = SystemParameters.WorkArea;
        Width = area.Width;
        Left = area.Left;
        Top = area.Top;

        Bar.MouseLeftButtonUp += OnClick;
        Card.MouseLeftButtonUp += OnClick;

        Loaded += (s, e) => ((Storyboard)Resources["EnterStoryboard"]).Begin();
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (App.Config.ClickFocusesTab) UiaTabFocus.Focus(_evt);
        BeginDismiss();
    }

    public void BeginDismiss() => ((Storyboard)Resources["ExitStoryboard"]).Begin();

    private void ExitStoryboard_Completed(object? sender, EventArgs e) => Close();
}
