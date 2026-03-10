using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using VoiceInkWin.Interop;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class RecordingOverlayWindow : Window
{
    private readonly RecordingOverlayViewModel _viewModel;
    private readonly DispatcherTimer _renderTimer;
    private readonly Rectangle[] _bars;
    private float[] _currentHeights;

    private const int BarCount = 24;
    private const double BarWidth = 3;
    private const double BarGap = 2.5;
    private const double CanvasHeight = 44;

    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;

    public RecordingOverlayWindow(RecordingOverlayViewModel viewModel)
    {
        ShowActivated = false; // Prevent overlay from stealing focus from target app
        InitializeComponent();
        _viewModel = viewModel;

        _bars = new Rectangle[BarCount];
        _currentHeights = new float[BarCount];

        // Position at top-center of primary screen
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2;
        Top = 30;

        // Create waveform bars — white, thin, rounded
        double totalWidth = BarCount * BarWidth + (BarCount - 1) * BarGap;
        double canvasWidth = Width - 40; // account for margin
        double startX = (canvasWidth - totalWidth) / 2;

        for (int i = 0; i < BarCount; i++)
        {
            _bars[i] = new Rectangle
            {
                Width = BarWidth,
                Height = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = Brushes.White
            };
            System.Windows.Controls.Canvas.SetLeft(_bars[i], startX + i * (BarWidth + BarGap));
            System.Windows.Controls.Canvas.SetTop(_bars[i], CanvasHeight / 2 - 1.5);
            WaveformCanvas.Children.Add(_bars[i]);
        }

        // Build fade-in storyboard: 0 → 1 opacity, 200ms
        _fadeInStoryboard = new Storyboard();
        var fadeInAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeInAnim, this);
        Storyboard.SetTargetProperty(fadeInAnim, new PropertyPath(OpacityProperty));
        _fadeInStoryboard.Children.Add(fadeInAnim);

        // Build fade-out storyboard: 1 → 0 opacity, 150ms
        _fadeOutStoryboard = new Storyboard();
        var fadeOutAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOutAnim, this);
        Storyboard.SetTargetProperty(fadeOutAnim, new PropertyPath(OpacityProperty));
        _fadeOutStoryboard.Children.Add(fadeOutAnim);
        _fadeOutStoryboard.Completed += (_, _) => Hide();

        // 30fps render timer
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _renderTimer.Tick += RenderTick;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make click-through
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW);
    }

    public void StartAnimation()
    {
        // Reset bars
        for (int i = 0; i < BarCount; i++)
        {
            _currentHeights[i] = 3;
            _bars[i].Height = 3;
            System.Windows.Controls.Canvas.SetTop(_bars[i], CanvasHeight / 2 - 1.5);
        }

        Show();
        _fadeOutStoryboard.Stop(this);
        _fadeInStoryboard.Begin(this, true);
        _renderTimer.Start();
    }

    public void StopAnimation()
    {
        _renderTimer.Stop();
        _fadeInStoryboard.Stop(this);
        _fadeOutStoryboard.Begin(this, true);
    }

    private void RenderTick(object? sender, EventArgs e)
    {
        _viewModel.UpdateBands();
        var bands = _viewModel.FrequencyBands;
        double centerY = CanvasHeight / 2;

        for (int i = 0; i < BarCount; i++)
        {
            // Map bar index to band index (bands may have fewer entries)
            int bandIndex = i < bands.Length ? i : bands.Length - 1;
            float target = bands.Length > 0 ? bands[bandIndex] * 34 + 3 : 3; // min 3px, max ~37px

            // Attack/decay smoothing: fast attack, slower decay
            float diff = target - _currentHeights[i];
            float factor = diff > 0 ? 0.8f : 0.7f;
            _currentHeights[i] += diff * factor;

            double height = Math.Max(3, _currentHeights[i]);
            _bars[i].Height = height;

            // Vertically center each bar
            System.Windows.Controls.Canvas.SetTop(_bars[i], centerY - height / 2);
        }
    }
}
