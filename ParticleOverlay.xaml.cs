using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClaudeNotifier;

public partial class ParticleOverlay : Window, IOverlayWindow
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly ClaudeEvent _evt;

    public ParticleOverlay(ClaudeEvent evt)
    {
        InitializeComponent();
        _evt = evt;

        // Tool name as title; full message as subtitle
        var tool = evt.ToolName ?? "Tool";
        TitleText.Text = $"Permission needed for {tool}";
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
        Left = area.Right - Width - 24;
        Top = area.Bottom - Height - 24;

        Root.IsHitTestVisible = true;
        Root.MouseLeftButtonUp += OnClick;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // ----- Card entrance: fade + slide-in from right -----
        var cardFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Root.BeginAnimation(OpacityProperty, cardFade);

        var slide = new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(520))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slide);

        // ----- Entrance halo flourish: 2 expanding rings, fade out -----
        AnimateHalo(HaloRing, HaloScale, 0, 1500, fromScale: 0.4, toScale: 1.9, peakOpacity: 0.85);
        AnimateHalo(HaloRing2, HaloScale2, 220, 1700, fromScale: 0.4, toScale: 2.2, peakOpacity: 0.6);

        // ----- Orb breathing pulse -----
        var orbScale = new DoubleAnimation(1.0, 1.07, TimeSpan.FromMilliseconds(1400))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        OrbScale.BeginAnimation(ScaleTransform.ScaleXProperty, orbScale);
        OrbScale.BeginAnimation(ScaleTransform.ScaleYProperty, orbScale);

        // ----- Repeating ripple rings (stagger) -----
        StartRipple(Ripple1, Ripple1Scale, 0);
        StartRipple(Ripple2, Ripple2Scale, 900);

        // ----- Text stagger fade-up -----
        StaggerIn(EyebrowRow, 180);
        StaggerIn(TitleText, 280);
        StaggerIn(MessageText, 380);
        StaggerIn(TabPill, 460);
    }

    private void AnimateHalo(UIElement target, ScaleTransform scale, int delayMs, int durMs,
                              double fromScale, double toScale, double peakOpacity)
    {
        var dur = TimeSpan.FromMilliseconds(durMs);
        var sb = new Storyboard();

        var sx = new DoubleAnimation(fromScale, toScale, dur)
        { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(sx, target);
        Storyboard.SetTargetProperty(sx, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        // Use direct binding on scale instead — simpler:
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(fromScale, toScale, dur)
            { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(fromScale, toScale, dur)
            { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

        // Opacity: 0 → peak → 0
        var fadeIn = new DoubleAnimation(0, peakOpacity, TimeSpan.FromMilliseconds(durMs / 3))
        { BeginTime = TimeSpan.FromMilliseconds(delayMs) };
        var fadeOut = new DoubleAnimation(peakOpacity, 0, TimeSpan.FromMilliseconds(2 * durMs / 3))
        { BeginTime = TimeSpan.FromMilliseconds(delayMs + durMs / 3) };
        target.BeginAnimation(OpacityProperty, fadeIn);
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs + durMs / 3) };
        t.Tick += (s, ev) => { t.Stop(); target.BeginAnimation(OpacityProperty, fadeOut); };
        t.Start();
    }

    private void StartRipple(UIElement ring, ScaleTransform scale, int delayMs)
    {
        var sx = new DoubleAnimation(0.55, 1.4, TimeSpan.FromMilliseconds(1800))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, sx);

        var op = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(1800)
        };
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.7, KeyTime.FromPercent(0.15)));
        op.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));
        ring.BeginAnimation(OpacityProperty, op);
    }

    private void StaggerIn(FrameworkElement el, int delayMs)
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420))
        { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        el.BeginAnimation(OpacityProperty, fade);

        var tt = new TranslateTransform();
        el.RenderTransform = tt;
        var slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(520))
        { BeginTime = TimeSpan.FromMilliseconds(delayMs), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        tt.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (App.Config.ClickFocusesTab) UiaTabFocus.Focus(_evt);
        BeginDismiss();
    }

    public void BeginDismiss()
    {
        // Slide out right + fade
        var slide = new DoubleAnimation(0, 80, TimeSpan.FromMilliseconds(320))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slide);

        var fade = new DoubleAnimation(Root.Opacity, 0, TimeSpan.FromMilliseconds(320));
        fade.Completed += (s, e) => Close();
        Root.BeginAnimation(OpacityProperty, fade);
    }
}
