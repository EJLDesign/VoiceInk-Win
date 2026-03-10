using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using VoiceInkWin.Helpers;
using VoiceInkWin.Models;
using VoiceInkWin.Services;
using VoiceInkWin.ViewModels;
using VoiceInkWin.Views;

namespace VoiceInkWin;

public partial class App : Application
{
    private SingleInstanceGuard? _singleInstance;
    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private MainViewModel? _mainViewModel;
    private RecordingOverlayWindow? _overlayWindow;
    private RecordingOverlayViewModel? _overlayViewModel;

    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceInk", "error.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += (_, args) =>
        {
            Log($"UI Exception: {args.Exception}");
            MessageBox.Show($"Error: {args.Exception.Message}\n\nSee {LogFile}", "VoiceInk Error");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log($"Fatal Exception: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log($"Task Exception: {args.Exception}");
            args.SetObserved();
        };

        try
        {
            Log("VoiceInk starting...");

            // Single-instance check
            _singleInstance = new SingleInstanceGuard();
            if (!_singleInstance.IsFirstInstance)
            {
                MessageBox.Show("VoiceInk is already running.", "VoiceInk", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Build DI container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Load settings
            var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
            settingsService.Load();
            Log("Settings loaded");

            // Get main view model
            _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            // Setup system tray
            SetupTrayIcon();
            Log("Tray icon ready");

            // Setup overlay
            _overlayViewModel = _serviceProvider.GetRequiredService<RecordingOverlayViewModel>();
            _overlayWindow = new RecordingOverlayWindow(_overlayViewModel);

            // Wire recording events to overlay — BeginInvoke to avoid blocking hook thread
            _mainViewModel.RecordingStarted += () => Dispatcher.BeginInvoke(() =>
            {
                Log("Recording started - showing overlay");
                _overlayWindow.StartAnimation();
            });

            _mainViewModel.RecordingStopped += () => Dispatcher.BeginInvoke(() =>
            {
                Log("Recording stopped - hiding overlay");
                _overlayWindow.StopAnimation();
            });

            _mainViewModel.StateChanged += () => Dispatcher.BeginInvoke(UpdateTrayIcon);

            // Initialize (load model, register hotkeys)
            _mainViewModel.Initialize();
            Log("Initialized. Status: " + _mainViewModel.StatusText);
        }
        catch (Exception ex)
        {
            Log($"Startup failed: {ex}");
            MessageBox.Show($"VoiceInk failed to start:\n\n{ex.Message}\n\nSee {LogFile}", "VoiceInk Error");
            Shutdown();
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Services (singletons)
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<AudioAnalysisService>();
        services.AddSingleton<TranscriptionService>();
        services.AddSingleton<CredentialService>();
        services.AddSingleton<AIPostProcessorService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<PasteService>();
        services.AddSingleton<ModelManagerService>();
        services.AddSingleton<ModeManager>();
        services.AddSingleton<HistoryService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<RecordingOverlayViewModel>();
        services.AddTransient<GeneralSettingsViewModel>();
        services.AddTransient<ModelSettingsViewModel>();
        services.AddTransient<AISettingsViewModel>();
        services.AddTransient<AdvancedSettingsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "VoiceInk - Voice Dictation",
            ContextMenu = BuildTrayMenu()
        };

        try
        {
            _trayIcon.Icon = CreateTrayIcon("idle");
        }
        catch (Exception ex)
        {
            Log($"Tray icon creation failed: {ex.Message}");
        }

        _trayIcon.TrayMouseDoubleClick += (_, _) => OpenSettings();
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        // Status header
        var statusItem = new MenuItem { Header = "VoiceInk - Ready", IsEnabled = false };
        menu.Items.Add(statusItem);
        menu.Items.Add(new Separator());

        // Start/Stop recording
        var recordItem = new MenuItem { Header = "Start Recording" };
        recordItem.Click += (_, _) =>
        {
            if (_mainViewModel!.IsRecording)
                _mainViewModel.StopAndTranscribe();
            else
                _mainViewModel.StartRecording();
        };
        menu.Items.Add(recordItem);
        menu.Items.Add(new Separator());

        // Mode submenu
        var modeMenu = new MenuItem { Header = "Mode" };
        menu.Items.Add(modeMenu);

        // History submenu
        var historyMenu = new MenuItem { Header = "History" };
        menu.Items.Add(historyMenu);

        menu.Items.Add(new Separator());

        // Settings
        var settingsItem = new MenuItem { Header = "Settings..." };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        // Quit
        var quitItem = new MenuItem { Header = "Quit VoiceInk" };
        quitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(quitItem);

        // Rebuild dynamic items each time menu opens
        menu.Opened += (_, _) =>
        {
            // Update status
            statusItem.Header = $"VoiceInk - {_mainViewModel!.StatusText}";

            // Update record item
            recordItem.Header = _mainViewModel.IsRecording ? "Stop Recording" : "Start Recording";

            // Rebuild modes
            modeMenu.Items.Clear();
            foreach (var mode in _mainViewModel.AllModes)
            {
                var modeItem = new MenuItem
                {
                    Header = mode.Name,
                    IsCheckable = true,
                    IsChecked = mode.Name == _mainViewModel.SelectedModeName
                };
                var modeName = mode.Name;
                modeItem.Click += (_, _) => _mainViewModel.SelectMode(modeName);
                modeMenu.Items.Add(modeItem);
            }

            // Rebuild history
            historyMenu.Items.Clear();
            if (_mainViewModel.HistoryEntries.Count == 0)
            {
                historyMenu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
            }
            else
            {
                foreach (var entry in _mainViewModel.HistoryEntries)
                {
                    var displayText = entry.Text.Length > 60
                        ? entry.Text[..57] + "..."
                        : entry.Text;
                    var histItem = new MenuItem { Header = $"[{entry.ModeName}] {displayText}" };
                    var capturedEntry = entry;
                    histItem.Click += (_, _) => _mainViewModel.CopyHistoryEntry(capturedEntry);
                    historyMenu.Items.Add(histItem);
                }
            }
        };

        return menu;
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon == null || _mainViewModel == null) return;

        string state = _mainViewModel.CurrentState switch
        {
            MainViewModel.AppState.Recording => "recording",
            MainViewModel.AppState.Transcribing => "transcribing",
            _ => "idle"
        };

        try
        {
            _trayIcon.Icon = CreateTrayIcon(state);
            _trayIcon.ToolTipText = $"VoiceInk - {_mainViewModel.StatusText}";
        }
        catch { }
    }

    private static Icon CreateTrayIcon(string state)
    {
        int size = 16;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var color = state switch
        {
            "recording" => Color.FromArgb(255, 68, 68),
            "transcribing" => Color.FromArgb(255, 170, 0),
            _ => Color.FromArgb(85, 85, 255)
        };

        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, size - 2, size - 2);

        if (state == "recording")
        {
            using var whiteBrush = new SolidBrush(Color.White);
            g.FillEllipse(whiteBrush, 5, 3, 6, 7);
            g.FillRectangle(whiteBrush, 7, 10, 2, 3);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    private void OpenSettings()
    {
        var settingsVm = _serviceProvider!.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow(settingsVm);
        window.ShowDialog();

        if (window.Saved)
        {
            _mainViewModel!.ReloadSettings();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("VoiceInk shutting down");
        _mainViewModel?.Dispose();
        _overlayWindow?.Close();
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
