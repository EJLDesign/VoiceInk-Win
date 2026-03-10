# VoiceInk for Windows

A fast, local speech-to-text app for Windows. Press a hotkey, speak, and your transcribed text is pasted directly into any application. Powered by OpenAI's Whisper running entirely on-device — no internet required for transcription.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![WPF](https://img.shields.io/badge/UI-WPF-blue) ![Whisper](https://img.shields.io/badge/Engine-Whisper.net-green) ![Windows](https://img.shields.io/badge/OS-Windows%2011-0078D4)

## Features

### Speech-to-Text
- **On-device transcription** using Whisper — no cloud, no latency, no API costs
- **9 model sizes** from Tiny (78 MB) to Large V3 Turbo (1.6 GB) — trade speed for accuracy
- **Multi-language** support with optional translate-to-English
- **Vocabulary hints** to improve accuracy for domain-specific terms

### Recording Modes
- **Toggle** — press hotkey to start, press again to stop
- **Push-to-Talk** — hold hotkey to record, release to stop

### AI Post-Processing (Optional)
Optionally clean up or reformat transcriptions using AI:

| Mode | Description |
|------|-------------|
| Raw | No processing — direct transcription |
| Clean | Fix grammar, punctuation, remove filler words |
| Email | Format as a professional email |
| Slack | Casual, concise message formatting |
| Notes | Extract and organize into bullet points |
| Code Comment | Format as a technical code comment |
| Custom | Create your own modes with custom system prompts |

**Supported AI providers:**
- **Anthropic (Claude)** — cloud API
- **Ollama** — local LLMs, fully offline

### System Integration
- **System tray app** — runs quietly in the background
- **Global hotkey** — works from any application
- **Auto-paste** — transcribed text is pasted directly into the focused window
- **Launch at startup** — optional Windows startup registration
- **Recording overlay** — animated waveform visualization while recording

## Getting Started

### Prerequisites
- Windows 10/11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run
```bash
git clone https://github.com/EJLDesign/VoiceInk-Win.git
cd VoiceInk-Win/VoiceInkWin
dotnet run
```

### First-Time Setup
1. Right-click the system tray icon and open **Settings**
2. **General tab** — set your hotkey (e.g., Right Ctrl)
3. **Model tab** — download a Whisper model (start with Base English for a good balance)
4. **Advanced tab** — select your microphone
5. Close settings, open any text field (Notepad, browser, etc.), and press your hotkey

## Settings

| Tab | Options |
|-----|---------|
| **General** | Recording mode, hotkey, launch at login, sound notification |
| **Model** | Model download/delete, language, translate to English, vocabulary hints |
| **AI Processing** | Provider (None/Anthropic/Ollama), API keys, processing mode, custom modes |
| **Advanced** | Audio input device, silence threshold, max recording duration, export history |

## Data Storage

All data is stored locally in `%AppData%/VoiceInk/`:

| File | Purpose |
|------|---------|
| `settings.json` | App configuration |
| `Models/` | Downloaded Whisper models |
| `history.json` | Last 20 transcriptions |
| `custom-modes.json` | User-created AI modes |
| `error.log` | Diagnostic log |

API keys are stored securely in **Windows Credential Manager**.

## Architecture

- **MVVM** pattern with [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) source generators
- **Dependency injection** via Microsoft.Extensions.DependencyInjection
- **Whisper.net** for on-device transcription
- **NAudio** for audio capture
- **Win32 interop** for global hotkeys, clipboard, and paste simulation

## License

MIT

## Credits

Windows port of [VoiceInk](https://github.com/Beingpax/VoiceInk) (macOS, by Beingpax).
