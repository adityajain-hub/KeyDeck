# KeyDeck for Linux — Architecture & Setup Guide

KeyDeck for Linux brings the exact same macro pad experience from Windows to Linux with **100% visual and behavioral parity**, running smoothly across **Wayland, X11, and TTY**.

---

## 🚀 Key Architectural Advantages

### 1. Universal Kernel-Level Key Interception (`evdev` + `uinput`)
On modern Linux (Ubuntu 22.04+, Fedora 38+, Arch, Debian), Wayland is the default display server. Unlike X11, Wayland intentionally isolates applications from reading global keystrokes.
- **KeyDeck solves this at the kernel level**: It reads physical keyboard events directly from `/dev/input/event*` using `evdev`.
- **Zero Latency Grab (`EVIOCGRAB`)**: When Macro Mode is active (Num Lock OFF), KeyDeck exclusively grabs numpad events from the keyboard device, preventing native keystrokes from leaking into your focused desktop application.
- **Synthetic Injection (`uinput`)**: Keystrokes, chords (`Ctrl+Shift+S`), and media controls (`VolumeUp`, `Play/Pause`) are synthesized through `/dev/uinput`, ensuring compatibility with games, Steam, terminals, browsers, and desktop apps.

### 2. High-Performance Avalonia UI
- Built on **Avalonia 11** with Skia rendering.
- Replicates the Windows WPF dark theme, 3-column layout, numpad grid, HUD window, and typography pixel-for-pixel.
- Consumes minimal memory (~35–45 MB RAM) with zero WebKit/Chromium bloat.

---

## 🛠️ Installation & Setup

### Prerequisites
Ensure your user account has permission to access input devices without running as `root`.

1. **Install udev rules**:
   ```bash
   sudo cp packaging/linux/99-keydeck.rules /etc/udev/rules.d/
   sudo udevadm control --reload-rules && sudo udevadm trigger
   ```

2. **Add user to `input` group**:
   ```bash
   sudo usermod -aG input $USER
   ```
   *Note: Log out and log back in for group changes to take effect.*

3. Alternatively, run the automated setup script:
   ```bash
   bash packaging/linux/setup-permissions.sh
   ```

---

## 🏗️ Building from Source

```bash
# Clone the repository
git clone https://github.com/adityajain-hub/KeyDeck.git
cd KeyDeck

# Restore and build the Linux application
dotnet build src/KeyDeck.UI.Avalonia/KeyDeck.UI.Avalonia.csproj

# Run KeyDeck
dotnet run --project src/KeyDeck.UI.Avalonia/KeyDeck.UI.Avalonia.csproj

# Or publish a standalone release package
bash packaging/linux/publish-linux.sh
```

---

## 📂 Configuration & Profiles

On Linux, KeyDeck adheres to the **XDG Base Directory Specification**:
- **Settings**: `~/.config/KeyDeck/settings.json`
- **Profiles**: `~/.config/KeyDeck/profiles/*.json`

All profiles exported or created on Windows are 100% interchangeable and compatible with Linux!
