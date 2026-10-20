using KeyDeck.Core.Models;

namespace KeyDeck.Platform.Linux.Native;

/// <summary>
/// Constants from Linux kernel headers &lt;linux/input.h&gt; and &lt;linux/input-event-codes.h&gt;.
/// </summary>
public static class LinuxInputCodes
{
    // ── Event types (EV_*) ──────────────────────────────────────────────────
    public const ushort EV_SYN = 0x00;
    public const ushort EV_KEY = 0x01;
    public const ushort EV_REL = 0x02;
    public const ushort EV_ABS = 0x03;
    public const ushort EV_MSC = 0x04;
    public const ushort EV_SW  = 0x05;
    public const ushort EV_LED = 0x11;
    public const ushort EV_SND = 0x12;
    public const ushort EV_REP = 0x14;
    public const ushort EV_FF  = 0x15;
    public const ushort EV_PWR = 0x16;
    public const ushort EV_FF_STATUS = 0x17;

    // ── Synchronization events ──────────────────────────────────────────────
    public const ushort SYN_REPORT    = 0;
    public const ushort SYN_CONFIG    = 1;
    public const ushort SYN_MT_REPORT = 2;
    public const ushort SYN_DROPPED   = 3;

    // ── Key press/release state values ──────────────────────────────────────
    public const int KEY_RELEASE = 0;
    public const int KEY_PRESS   = 1;
    public const int KEY_REPEAT  = 2;

    // ── LEDs ────────────────────────────────────────────────────────────────
    public const ushort LED_NUML    = 0x00;
    public const ushort LED_CAPSL   = 0x01;
    public const ushort LED_SCROLLL = 0x02;

    // ── Numpad Key Codes (KEY_KP*) ──────────────────────────────────────────
    public const ushort KEY_KP7         = 71;
    public const ushort KEY_KP8         = 72;
    public const ushort KEY_KP9         = 73;
    public const ushort KEY_KPMINUS     = 74;
    public const ushort KEY_KP4         = 75;
    public const ushort KEY_KP5         = 76;
    public const ushort KEY_KP6         = 77;
    public const ushort KEY_KPPLUS      = 78;
    public const ushort KEY_KP1         = 79;
    public const ushort KEY_KP2         = 80;
    public const ushort KEY_KP3         = 81;
    public const ushort KEY_KP0         = 82;
    public const ushort KEY_KPDOT       = 83;
    public const ushort KEY_KPENTER     = 96;
    public const ushort KEY_KPSLASH     = 98;
    public const ushort KEY_KPASTERISK  = 55;
    public const ushort KEY_NUMLOCK     = 69;
    public const ushort KEY_KPEQUAL     = 117;
    public const ushort KEY_KPCOMMA     = 121;

    // ── Modifier & Navigation Keys ──────────────────────────────────────────
    public const ushort KEY_ESC        = 1;
    public const ushort KEY_1          = 2;
    public const ushort KEY_2          = 3;
    public const ushort KEY_3          = 4;
    public const ushort KEY_4          = 5;
    public const ushort KEY_5          = 6;
    public const ushort KEY_6          = 7;
    public const ushort KEY_7          = 8;
    public const ushort KEY_8          = 9;
    public const ushort KEY_9          = 10;
    public const ushort KEY_0          = 11;
    public const ushort KEY_MINUS      = 12;
    public const ushort KEY_EQUAL      = 13;
    public const ushort KEY_BACKSPACE  = 14;
    public const ushort KEY_TAB        = 15;
    public const ushort KEY_Q          = 16;
    public const ushort KEY_W          = 17;
    public const ushort KEY_E          = 18;
    public const ushort KEY_R          = 19;
    public const ushort KEY_T          = 20;
    public const ushort KEY_Y          = 21;
    public const ushort KEY_U          = 22;
    public const ushort KEY_I          = 23;
    public const ushort KEY_O          = 24;
    public const ushort KEY_P          = 25;
    public const ushort KEY_ENTER      = 28;
    public const ushort KEY_LEFTCTRL   = 29;
    public const ushort KEY_A          = 30;
    public const ushort KEY_S          = 31;
    public const ushort KEY_D          = 32;
    public const ushort KEY_F          = 33;
    public const ushort KEY_G          = 34;
    public const ushort KEY_H          = 35;
    public const ushort KEY_J          = 36;
    public const ushort KEY_K          = 37;
    public const ushort KEY_L          = 38;
    public const ushort KEY_LEFTSHIFT  = 42;
    public const ushort KEY_Z          = 44;
    public const ushort KEY_X          = 45;
    public const ushort KEY_C          = 46;
    public const ushort KEY_V          = 47;
    public const ushort KEY_B          = 48;
    public const ushort KEY_N          = 49;
    public const ushort KEY_M          = 50;
    public const ushort KEY_RIGHTSHIFT = 54;
    public const ushort KEY_LEFTALT    = 56;
    public const ushort KEY_SPACE      = 57;
    public const ushort KEY_CAPSLOCK   = 58;
    public const ushort KEY_F1         = 59;
    public const ushort KEY_F2         = 60;
    public const ushort KEY_F3         = 61;
    public const ushort KEY_F4         = 62;
    public const ushort KEY_F5         = 63;
    public const ushort KEY_F6         = 64;
    public const ushort KEY_F7         = 65;
    public const ushort KEY_F8         = 66;
    public const ushort KEY_F9         = 67;
    public const ushort KEY_F10        = 68;
    public const ushort KEY_SCROLLLOCK = 70;
    public const ushort KEY_F11        = 87;
    public const ushort KEY_F12        = 88;
    public const ushort KEY_RIGHTCTRL  = 97;
    public const ushort KEY_RIGHTALT   = 100;
    public const ushort KEY_HOME       = 102;
    public const ushort KEY_UP         = 103;
    public const ushort KEY_PAGEUP     = 104;
    public const ushort KEY_LEFT       = 105;
    public const ushort KEY_RIGHT      = 106;
    public const ushort KEY_END        = 107;
    public const ushort KEY_DOWN       = 108;
    public const ushort KEY_PAGEDOWN   = 109;
    public const ushort KEY_INSERT     = 110;
    public const ushort KEY_DELETE     = 111;
    public const ushort KEY_LEFTMETA   = 125; // Super / Windows key
    public const ushort KEY_RIGHTMETA  = 126;

    // ── Media & Volume Keys ─────────────────────────────────────────────────
    public const ushort KEY_MUTE        = 113;
    public const ushort KEY_VOLUMEDOWN  = 114;
    public const ushort KEY_VOLUMEUP    = 115;
    public const ushort KEY_POWER       = 116;
    public const ushort KEY_PAUSE       = 119;
    public const ushort KEY_STOPCD      = 166;
    public const ushort KEY_PREVIOUSSONG = 165;
    public const ushort KEY_NEXTSONG    = 163;
    public const ushort KEY_PLAYPAUSE   = 164;

    // ── ioctl requests ──────────────────────────────────────────────────────
    // _IOW('E', 0x90, int) -> 0x40044590 (EVIOCGRAB)
    public const ulong EVIOCGRAB = 0x40044590;

    // uinput ioctls (_IOW('U', ...))
    public const ulong UI_SET_EVBIT   = 0x40045564;
    public const ulong UI_SET_KEYBIT  = 0x40045565;
    public const ulong UI_SET_RELBIT  = 0x40045566;
    public const ulong UI_SET_ABSBIT  = 0x40045567;
    public const ulong UI_SET_LEDBIT  = 0x40045569;
    public const ulong UI_DEV_CREATE  = 0x5501;
    public const ulong UI_DEV_DESTROY = 0x5502;
    public const ulong UI_DEV_SETUP   = 0x405c5503;

    /// <summary>
    /// Attempts to map a Linux evdev keycode to a <see cref="NumpadKeyId"/>.
    /// </summary>
    public static bool TryMapToNumpadKey(ushort code, out NumpadKeyId keyId)
    {
        switch (code)
        {
            case KEY_KP0:
                keyId = NumpadKeyId.Numpad0;
                return true;
            case KEY_KP1:
                keyId = NumpadKeyId.Numpad1;
                return true;
            case KEY_KP2:
                keyId = NumpadKeyId.Numpad2;
                return true;
            case KEY_KP3:
                keyId = NumpadKeyId.Numpad3;
                return true;
            case KEY_KP4:
                keyId = NumpadKeyId.Numpad4;
                return true;
            case KEY_KP5:
                keyId = NumpadKeyId.Numpad5;
                return true;
            case KEY_KP6:
                keyId = NumpadKeyId.Numpad6;
                return true;
            case KEY_KP7:
                keyId = NumpadKeyId.Numpad7;
                return true;
            case KEY_KP8:
                keyId = NumpadKeyId.Numpad8;
                return true;
            case KEY_KP9:
                keyId = NumpadKeyId.Numpad9;
                return true;
            case KEY_KPDOT:
                keyId = NumpadKeyId.NumpadDecimal;
                return true;
            case KEY_KPPLUS:
                keyId = NumpadKeyId.NumpadAdd;
                return true;
            case KEY_KPMINUS:
                keyId = NumpadKeyId.NumpadSubtract;
                return true;
            case KEY_KPASTERISK:
                keyId = NumpadKeyId.NumpadMultiply;
                return true;
            case KEY_KPSLASH:
                keyId = NumpadKeyId.NumpadDivide;
                return true;
            case KEY_KPENTER:
                keyId = NumpadKeyId.NumpadEnter;
                return true;
            default:
                keyId = default;
                return false;
        }
    }

    /// <summary>
    /// Translates a Windows Virtual Key code (used in saved KeyDeck profiles) to a Linux evdev key code.
    /// </summary>
    public static ushort MapWinVkToLinuxKey(int winVk)
    {
        return winVk switch
        {
            0x10 => KEY_LEFTSHIFT,   // VK_SHIFT
            0x11 => KEY_LEFTCTRL,    // VK_CONTROL
            0x12 => KEY_LEFTALT,     // VK_MENU (Alt)
            0x5B => KEY_LEFTMETA,    // VK_LWIN
            0x5C => KEY_RIGHTMETA,   // VK_RWIN
            0x1B => KEY_ESC,
            0x09 => KEY_TAB,
            0x0D => KEY_ENTER,
            0x20 => KEY_SPACE,
            0x08 => KEY_BACKSPACE,
            0x2E => KEY_DELETE,
            0x2D => KEY_INSERT,
            0x24 => KEY_HOME,
            0x23 => KEY_END,
            0x21 => KEY_PAGEUP,
            0x22 => KEY_PAGEDOWN,
            0x25 => KEY_LEFT,
            0x26 => KEY_UP,
            0x27 => KEY_RIGHT,
            0x28 => KEY_DOWN,
            0x70 => KEY_F1,
            0x71 => KEY_F2,
            0x72 => KEY_F3,
            0x73 => KEY_F4,
            0x74 => KEY_F5,
            0x75 => KEY_F6,
            0x76 => KEY_F7,
            0x77 => KEY_F8,
            0x78 => KEY_F9,
            0x79 => KEY_F10,
            0x7A => KEY_F11,
            0x7B => KEY_F12,
            0xAD => KEY_MUTE,
            0xAE => KEY_VOLUMEDOWN,
            0xAF => KEY_VOLUMEUP,
            0xB0 => KEY_NEXTSONG,
            0xB1 => KEY_PREVIOUSSONG,
            0xB2 => KEY_STOPCD,
            0xB3 => KEY_PLAYPAUSE,
            // Letters A-Z (0x41 - 0x5A)
            >= 0x41 and <= 0x5A => (ushort)(winVk switch
            {
                0x41 => KEY_A, 0x42 => KEY_B, 0x43 => KEY_C, 0x44 => KEY_D,
                0x45 => KEY_E, 0x46 => KEY_F, 0x47 => KEY_G, 0x48 => KEY_H,
                0x49 => KEY_I, 0x4A => KEY_J, 0x4B => KEY_K, 0x4C => KEY_L,
                0x4D => KEY_M, 0x4E => KEY_N, 0x4F => KEY_O, 0x50 => KEY_P,
                0x51 => KEY_Q, 0x52 => KEY_R, 0x53 => KEY_S, 0x54 => KEY_T,
                0x55 => KEY_U, 0x56 => KEY_V, 0x57 => KEY_W, 0x58 => KEY_X,
                0x59 => KEY_Y, 0x5A => KEY_Z, _ => 0
            }),
            // Digits 0-9 (0x30 - 0x39)
            >= 0x30 and <= 0x39 => (ushort)(winVk switch
            {
                0x30 => KEY_0, 0x31 => KEY_1, 0x32 => KEY_2, 0x33 => KEY_3,
                0x34 => KEY_4, 0x35 => KEY_5, 0x36 => KEY_6, 0x37 => KEY_7,
                0x38 => KEY_8, 0x39 => KEY_9, _ => 0
            }),
            // Numpad 0-9 (0x60 - 0x69)
            0x60 => KEY_KP0, 0x61 => KEY_KP1, 0x62 => KEY_KP2, 0x63 => KEY_KP3,
            0x64 => KEY_KP4, 0x65 => KEY_KP5, 0x66 => KEY_KP6, 0x67 => KEY_KP7,
            0x68 => KEY_KP8, 0x69 => KEY_KP9,
            0x6A => KEY_KPASTERISK, 0x6B => KEY_KPPLUS, 0x6D => KEY_KPMINUS,
            0x6E => KEY_KPDOT, 0x6F => KEY_KPSLASH,
            _ => 0
        };
    }
}
