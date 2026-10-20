using System.Runtime.InteropServices;

namespace KeyDeck.Platform.Linux.Native;

#pragma warning disable CS8981 // The type name 'pollfd' only contains lower-cased ascii characters

[StructLayout(LayoutKind.Sequential)]
public struct input_event
{
    public IntPtr time_sec;
    public IntPtr time_usec;
    public ushort type;
    public ushort code;
    public int value;
}

[StructLayout(LayoutKind.Sequential)]
public struct input_id
{
    public ushort bustype;
    public ushort vendor;
    public ushort product;
    public ushort version;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct uinput_setup
{
    public input_id id;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string name;
    public uint ff_effects_max;
}

[StructLayout(LayoutKind.Sequential)]
public struct pollfd
{
    public int fd;
    public short events;
    public short revents;
}

/// <summary>
/// P/Invoke definitions for Linux libc system calls (evdev, uinput, POSIX I/O).
/// </summary>
public static unsafe class NativeLibc
{
    private const string Libc = "libc";

    // Open flags
    public const int O_RDONLY    = 0x0000;
    public const int O_WRONLY    = 0x0001;
    public const int O_RDWR      = 0x0002;
    public const int O_NONBLOCK  = 0x0800;
    public const int O_CLOEXEC   = 0x80000;

    // Poll events
    public const short POLLIN  = 0x0001;
    public const short POLLPRI = 0x0002;
    public const short POLLOUT = 0x0004;
    public const short POLLERR = 0x0008;
    public const short POLLHUP = 0x0010;

    [DllImport(Libc, SetLastError = true)]
    public static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags, int mode = 0);

    [DllImport(Libc, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint read(int fd, void* buf, nuint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern nint write(int fd, void* buf, nuint count);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, int arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, ulong arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, void* arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int ioctl(int fd, ulong request, ref uinput_setup arg);

    [DllImport(Libc, SetLastError = true)]
    public static extern int poll(pollfd* fds, uint nfds, int timeout);
}
