using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using VoiceInkWin.Interop;
using VoiceInkWin.ViewModels;

namespace VoiceInkWin.Views;

public partial class RecordingOverlayWindow : Window
{
    private readonly RecordingOverlayViewModel _viewModel;
    private readonly DispatcherTimer _renderTimer;
    private readonly Rectangle[] _bars = new Rectangle[12];
    private float[] _targetHeights = new float[12];
    private float[] _currentHeights = new float[12];

    public RecordingOverlayWindow(RecordingOverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // Position at top-center of primary screen
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2;
        Top = 30;

        // Create waveform bars
        double barWidth = 12;
        double gap = 4;
        double totalWidth = 12 * barWidth + 11 * gap;
        double startX = (WaveformCanvas.ActualWidth > 0 ? WaveformCanvas.ActualWidth : 160) / 2 - totalWidth / 2;

        for (int i = 0; i < 12; i++)
        {
            _bars[i] = new Rectangle
            {
                Width = barWidth,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(85, 85, 255),
                    Color.FromRgb(170, 85, 255),
                    90)
            };
            System.Windows.Controls.Canvas.SetLeft(_bars[i], startX + i * (barWidth + gap));
            System.Windows.Controls.Canvas.SetBottom(_bars[i], 0);
            WaveformCanvas.Children.Add(_bars[i]);
        }

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
        _renderTimer.Start();
    }

    public void StopAnimation()
    {
        _renderTimer.Stop();
        // Reset bars
        for (int i = 0; i < 12; i++)
        {
            _bars[i].Height = 4;
            _currentHeights[i] = 0;
        }
    }

    private void RenderTick(object? sender, EventArgs e)
    {
        _viewModel.UpdateBands();
        var bands = _viewModel.FrequencyBands;

        for (int i = 0; i < 12 && i < bands.Length; i++)
        {
            _targetHeights[i] = bands[i] * 28 + 2; // min 2px, max 30px
            // Smooth interpolation
            _currentHeights[i] += (_targetHeights[i] - _currentHeights[i]) * 0.3f;
            _bars[i].Height = Math.Max(2, _currentHeights[i]);
        }
    }
}
