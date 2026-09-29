using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MacShotThumbnail;

internal sealed class KeyboardShortcuts : IDisposable
{
    private sealed record Binding(Keys Area, Keys Full, bool Enabled, bool Suspended);
    private volatile Binding binding = new(Keys.PrintScreen, Keys.Control | Keys.PrintScreen, false, false);
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly IntPtr target;
    private readonly int captureMessage;
    private readonly HookProc callback;
    private readonly HashSet<Keys> held = [];
    private IntPtr hook;
    private IntPtr controlWindow;
    private long lastCapture;
    private Exception? startupError;
    private const int StopMessage = 0x8002;
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardEvent
    {
        public uint Key, ScanCode, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);

    public KeyboardShortcuts(IntPtr target, int captureMessage, Settings settings)
    {
        this.target = target;
        this.captureMessage = captureMessage;
        Update(settings, false);
        callback = HandleKeyboard;
        thread = new Thread(Run) { IsBackground = true, Name = "MacShot shortcuts" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        if (startupError != null) { Settings.Log(startupError); }
    }
    public void Update(Settings settings, bool suspended) => binding = new(settings.AreaShortcut, settings.FullShortcut, settings.Enabled, suspended);

    private void Run()
    {
        using var context = new ApplicationContext();
        var window = new ControlWindow(context);
        using var recovery = new System.Windows.Forms.Timer { Interval = 30_000 };
        try
        {
            window.CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
            controlWindow = window.Handle;
            RefreshHook();
            recovery.Tick += (_, _) =>
            {
                try { RefreshHook(); } catch (Exception error) { Settings.Log(error); }
            };
            recovery.Start();
        }
        catch (Exception error) { startupError = error; }
        finally { ready.Set(); }
        if (controlWindow == IntPtr.Zero) return;
        try { Application.Run(context); }
        finally
        {
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            window.DestroyHandle();
        }
    }
    private void RefreshHook()
    {
        // Windows can silently remove a timed-out hook; periodically renew it on its own message loop.
        IntPtr next = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (next == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr previous = hook;
        hook = next;
        if (previous != IntPtr.Zero) UnhookWindowsHookEx(previous);
    }
    private IntPtr HandleKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code < 0) return CallNextHookEx(hook, code, message, data);
        Keys key = (Keys)Marshal.PtrToStructure<KeyboardEvent>(data).Key;
        int kind = message.ToInt32();
        bool up = kind is 0x0101 or 0x0105;
        if (up && held.Remove(key)) return (IntPtr)1;
        var current = binding;
        if (!current.Enabled || current.Suspended) return CallNextHookEx(hook, code, message, data);
        Keys combination = key;
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) combination |= Keys.Shift;
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) combination |= Keys.Control;
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) combination |= Keys.Alt;
        bool windows = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (windows || (combination != current.Area && combination != current.Full)) return CallNextHookEx(hook, code, message, data);
        if ((!up && held.Add(key)) || (up && key == Keys.PrintScreen))
        {
            long now = Environment.TickCount64;
            if (now - lastCapture >= 400)
            {
                lastCapture = now;
                PostMessage(target, captureMessage, (IntPtr)(combination == current.Area ? 3 : 4), IntPtr.Zero);
            }
        }
        return (IntPtr)1;
    }
    public void Dispose()
    {
        if (controlWindow != IntPtr.Zero) PostMessage(controlWindow, StopMessage, IntPtr.Zero, IntPtr.Zero);
        thread.Join();
        ready.Dispose();
    }
    private sealed class ControlWindow(ApplicationContext context) : NativeWindow
    {
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == StopMessage) context.ExitThread();
            base.WndProc(ref message);
        }
    }
}
