# KeyDeck Architecture & System Design Documentation

KeyDeck is an ultra-low-latency, zero-cost macro pad and productivity overlay application for Windows 10/11 built on **.NET 8** and **WPF**. It intercepts physical numpad keystrokes when Num Lock is toggled OFF (or in Always-On mode) and executes rich configurable actions without requiring dedicated macro hardware.

---

## 1. Architectural Layers & Separation of Concerns

KeyDeck adheres to **Clean Architecture** principles, strictly separating User Interface controls, reactive state orchestration, domain business rules, and low-level Win32 platform services.

```
┌────────────────────────────────────────────────────────────────────────┐
│                               UI LAYER                                 │
│  MainWindow  •  HudWindow  •  NumpadGrid  •  ActionEditor  •  Themes   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Binds to & Observes
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        REACTIVE STATE LAYER                            │
│           AppStateService (IAppStateService, AppStateChanged)          │
└───────────────┬───────────────────────────────────┬────────────────────┘
                │ Manages                           │ Dispatches to
                ▼                                   ▼
┌───────────────────────────────────┐   ┌────────────────────────────────┐
│            CORE DOMAIN            │   │      INFRASTRUCTURE SERVICES   │
│  ProfileManager (IProfileManager) │   │  EnvironmentService (IEnv)     │
│  KeyAction & Profile Models       │   │  ConfigService (Atomic I/O)    │
│  ActionExecutor (Async Execution) │   │  TrayIconService (Taskbar)     │
│  KeyInterceptor (IKeyInterceptor) │   │  StartupService (Registry Run) │
└─────────────────┬─────────────────┘   └────────────────────────────────┘
                  │ Intercepts
                  ▼
┌────────────────────────────────────────────────────────────────────────┐
│                         PLATFORM WIN32 LAYER                           │
│  KeyboardHook (WH_KEYBOARD_LL) • ForegroundAppMonitor (SetWinEventHook)│
│  NumLockMonitor (GetKeyState)  • NativeMethods (SendInput / P/Invoke) │
└────────────────────────────────────────────────────────────────────────┘
```

### Directory Structure
```
KeyDeck/
├── docs/
│   └── ARCHITECTURE.md                  # Comprehensive architectural guide
├── src/
│   └── KeyDeck/
│       ├── Core/
│       │   ├── Abstractions/            # Decoupled interface contracts
│       │   │   ├── IAppStateService.cs
│       │   │   ├── IConfigService.cs
│       │   │   ├── IEnvironmentService.cs
│       │   │   ├── IForegroundAppMonitor.cs
│       │   │   ├── IKeyInterceptor.cs
│       │   │   ├── INumLockMonitor.cs
│       │   │   └── IProfileManager.cs
│       │   ├── Models/                  # Pure domain entities (no UI dependencies)
│       │   │   ├── AppSettings.cs
│       │   │   ├── Enums.cs
│       │   │   ├── KeyAction.cs
│       │   │   └── Profile.cs
│       │   ├── State/                   # Central reactive state engine
│       │   │   └── AppStateService.cs
│       │   ├── ActionExecutor.cs        # High-performance async action dispatcher
│       │   ├── ForegroundAppMonitor.cs  # OS-level active window tracker
│       │   ├── IconExtractor.cs         # Win32 icon extraction & caching
│       │   ├── KeyInterceptor.cs        # High-frequency low-level hook filter
│       │   ├── NumLockMonitor.cs        # Hardware LED & key state monitor
│       │   └── ProfileManager.cs        # Profile lifecycle & process matching
│       ├── Infrastructure/
│       │   ├── Services/
│       │   │   └── EnvironmentService.cs# OS environment & process abstraction
│       │   ├── ConfigService.cs         # Atomic JSON persistence & migration
│       │   ├── HashHelper.cs            # Cryptographic & color hashing
│       │   ├── HotkeyRecorder.cs        # Live keystroke capture utility
│       │   ├── StartupService.cs        # Windows Registry autorun manager
│       │   └── TrayIconService.cs       # Reactive notify icon & context menu
│       ├── Platform/
│       │   └── Win32/                   # Unmanaged Win32 interop & structures
│       │       ├── KeyboardHook.cs
│       │       ├── NativeMethods.cs
│       │       └── WindowHelper.cs
│       ├── UI/
│       │   ├── Controls/                # Reusable modular WPF controls
│       │   │   ├── ActionEditor.xaml(.cs)
│       │   │   ├── NumpadGrid.xaml(.cs)
│       │   │   ├── NumpadKeyCell.xaml(.cs)
│       │   │   └── ProfileListPanel.xaml(.cs)
│       │   ├── Converters/              # Value converters for XAML bindings
│       │   │   └── ActionTypeToDisplayNameConverter.cs
│       │   └── HudOverlay/              # Semi-transparent draggable HUD overlay
│       │       └── HudWindow.xaml(.cs)
│       ├── Resources/
│       │   ├── Themes/                  # Dark & Light ResourceDictionaries
│       │   │   ├── DarkTheme.xaml
│       │   │   └── LightTheme.xaml
│       │   └── default-profile.json     # Embedded baseline profiles
│       ├── App.xaml(.cs)                # Composition root & lifecycle
│       └── MainWindow.xaml(.cs)         # Main desktop shell window
└── tests/
    └── KeyDeck.Tests/                   # Standalone zero-dependency test suite
        ├── Framework/                   # Custom assertions & test harness
        │   ├── Assert.cs
        │   └── TestRunner.cs
        ├── Suites/                      # Automated test suites
        │   ├── ActionExecutorTests.cs
        │   ├── AppStateServiceTests.cs
        │   ├── ConfigServiceTests.cs
        │   ├── HookAndInterceptorTests.cs
        │   ├── ModelTests.cs
        │   └── ProfileManagerTests.cs
        └── Program.cs                   # Test runner entry point
```

---

## 2. Centralized Reactive State Management

In previous versions, UI components, background services, and the system tray independently mutated `AppSettings` and notified one another via coupled events. 

KeyDeck v2.0 introduces `IAppStateService` as the **Single Source of Truth**:

```csharp
public interface IAppStateService
{
    AppSettings Settings { get; }
    bool IsMacroEnabled { get; }
    bool IsAutoProfileSwitchEnabled { get; }
    HudVisibilityMode HudMode { get; }
    double HudOpacity { get; }
    bool IsHudLocked { get; }
    AppTheme Theme { get; }
    Profile ActiveProfile { get; }

    event Action<AppStateChangedEventArgs>? StateChanged;
    event Action? ResetHudPositionRequested;

    void SetMacroEnabled(bool enabled);
    void SetAutoProfileSwitchEnabled(bool enabled);
    void SetHudMode(HudVisibilityMode mode);
    void SetHudOpacity(double opacity);
    void SetHudLocked(bool isLocked);
    void SetHudPosition(double left, double top);
    void SetTheme(AppTheme theme);
    void SetActiveProfile(string profileId);
    void RequestResetHudPosition();
}
```

### State Propagation Flow
1. **User Action / Background Event:** An action occurs in `MainWindow`, `HudWindow`, `TrayIconService`, or via `ForegroundAppMonitor`.
2. **State Mutation:** The caller invokes a method on `IAppStateService` (e.g., `SetHudOpacity(0.85)`).
3. **Synchronization & Clamping:** `AppStateService` clamps numerical inputs (e.g., opacity between 20% and 100%), persists changes via `IConfigService` atomically, and updates its in-memory snapshot.
4. **Reactive Broadcast:** `StateChanged` is raised with granular `AppStateProperty` metadata.
5. **UI Subscribers React:** All active subscribers (`MainWindow`, `HudWindow`, `TrayIconService`) automatically update their controls in sync without tight inter-component coupling.

---

## 3. Environment Variable & OS Abstraction

Hardcoded calls to `Environment.GetFolderPath`, `Process.GetCurrentProcess()`, and direct file path manipulations have been extracted into `IEnvironmentService`:

```csharp
public interface IEnvironmentService
{
    int CurrentProcessId { get; }
    string ExpandEnvironmentVariables(string name);
    string? GetEnvironmentVariable(string variable);
    string GetFolderPath(Environment.SpecialFolder folder);
    bool FileExists(string? path);
    bool DirectoryExists(string? path);
}
```

### Advantages:
- **Zero Host Leakage:** Test suites inject mocked environment services to test path expansion (e.g. `%APPDATA%`, `%ProgramFiles%`) in sterile, cross-platform environments.
- **Portability:** Prepares KeyDeck for non-Windows file-system structures and sandboxed deployment environments.

---

## 4. Fault Tolerance & Atomic Persistence

### Atomic File Write Protocol
To prevent file corruption during system power loss, OS crashes, or write contention, `ConfigService` employs atomic temp-file swapping:

```csharp
var tmpPath = destinationPath + ".tmp";
File.WriteAllText(tmpPath, content, Encoding.UTF8);

if (File.Exists(destinationPath))
{
    File.Replace(tmpPath, destinationPath, destinationPath + ".bak");
    try { File.Delete(destinationPath + ".bak"); } catch { /* non-critical */ }
}
else
{
    File.Move(tmpPath, destinationPath);
}
```

### Graceful Fallbacks & Auto-Seeding
- If `settings.json` is missing or corrupted, KeyDeck loads healthy defaults without throwing unhandled exceptions.
- If the `profiles/` directory is empty or corrupt, `ConfigService` automatically seeds default profiles (Productivity, Media & OBS, Developer).
- Corrupt individual profile files are skipped and logged, allowing remaining healthy profiles to load uninterrupted.

---

## 5. Scalable Action Dispatcher (`ActionExecutor`)

KeyDeck executes actions asynchronously using Windows `SendInput` and `Process.Start` / `ShellExecute`:

```
┌────────────────────────────────────────────────────────┐
│                   ActionExecutor                       │
└──────────────────────────┬─────────────────────────────┘
                           │
       ┌───────────────────┼───────────────────┐
       ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  Hotkey /    │    │  Text Paste  │    │ Multi-Action │
│  Media Keys  │    │  (Unicode    │    │ (Sequence    │
│ (SendInput)  │    │   Chunks)    │    │  + Delays)   │
└──────────────┘    └──────────────┘    └──────────────┘
```

### Key Execution Highlights
1. **Extended Virtual Keys:** Arrow keys, navigation clusters (`Insert`, `Delete`, `Home`, `End`), and right-hand modifiers (`RControlKey`, `RMenu`) automatically receive `KEYEVENTF_EXTENDEDKEY` flags.
2. **Unicode Text Injection:** Supports rapid text paste via `KEYEVENTF_UNICODE` in 64-character chunks with short async delays (5ms) to prevent target input buffer drops.
3. **Multi-Action Sequencing:** Supports chained composite macros with embedded customizable delay periods (`0ms` to `60,000ms`).
4. **Visual Key-Press Feedback:** Visual key-press firing pulses on both `NumpadGrid` and `HudWindow` are driven reactively via `KeyInterceptor.NumpadKeyPressed` and `NumpadGrid.KeyCellActivated`.

---

## 6. Standalone Zero-Dependency Test Suite

Located under `tests/KeyDeck.Tests/`, KeyDeck includes an offline test harness that runs without requiring external NuGet packages or active internet access:

### Custom Test Framework
- **`KeyDeck.Tests.Framework.Assert`:** Full-featured assertion library supporting `True`, `False`, `Equal`, `NotEqual`, `Null`, `NotNull`, `Contains`, `Throws<T>`, and `InRange` with execution counters.
- **`KeyDeck.Tests.Framework.TestRunner`:** High-speed reflection-based runner discovering `[TestClass]` and `[TestMethod]` attributes, featuring color-coded console output and microsecond-level timing.

### Test Coverage Summary (100% Pass Rate)
- **`ActionExecutorTests`:** Unicode chunking math, extended virtual key tables, profile switching delegation, and None action handling.
- **`AppStateServiceTests`:** Reactive `StateChanged` notifications, input clamping (opacity 20%-100%), profile synchronization, and HUD position reset requests.
- **`ConfigServiceTests`:** Atomic file persistence, corrupt JSON handling, profile CRUD, and default profile seeding.
- **`HookAndInterceptorTests`:** Macro interception states, Num Lock hardware transitions, temporary bypass, and volume key repeat filter predicates.
- **`ModelTests`:** Deep cloning of profiles and multi-action sequences, JSON serialization roundtripping, and default value boundaries.
- **`ProfileManagerTests`:** Profile CRUD operations, active profile switching, last profile deletion protection, linked process normalization, and multi-pattern executable matching.

---

## 7. Build and Verification Instructions

### Building KeyDeck & Test Suite
```powershell
dotnet build KeyDeck.sln -c Release --no-restore -m:1
```

### Running the Test Suite
```powershell
dotnet run --project tests/KeyDeck.Tests/KeyDeck.Tests.csproj -c Release --no-build
```

### Running the Application
```powershell
dotnet run --project src/KeyDeck/KeyDeck.csproj -c Release --no-build
```
