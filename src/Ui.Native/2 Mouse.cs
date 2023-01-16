/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)

    Les fonctionnalités 
    The Rhino API does not allow full control of keyboard events.
    To make this plugin compatible with MacOS it is necessary to implement the functions surrounded by the macro WIN32.
/*/

#if CSX
#load "./1 UnsafeNativeMethods.cs"
#load "../1 Dbg.cs"
#endif

#define WIN32


using System;
using System.Runtime.InteropServices;
using System.Diagnostics;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Ui.Native;
#endif



#if RHP
//file
#endif
enum WindowsMouseHookType
{
    Keyboard = 2,
    Mouse = 7,
    LowLevelKeyboard = 13,
    LowLevelMouse = 14
}


[StructLayout(LayoutKind.Sequential)]
#if RHP
file
#endif
readonly struct MSLLHOOKSTRUCT
{
    // POINT
    public readonly int X;
    public readonly int Y;
    public readonly uint mouseData;
    public readonly uint flags;
    public readonly uint time;
    public readonly IntPtr dwExtraInfo;
}


#if RHP
file
#endif
static class WM
{
    public const int MOUSEMOVE = 0x0200;
    public const int LBUTTONDOWN = 0x0201;
    public const int LBUTTONUP = 0x0202;
    public const int LBUTTONDBLCLK = 0x0203;
    public const int RBUTTONDOWN = 0x0204;
    public const int RBUTTONUP = 0x0205;
    public const int MBUTTONDOWN = 0x0207;
    public const int MBUTTONUP = 0x0208;
    public const int MOUSEWHEEL = 0x020A;
}


public enum MouseEventType : byte
{
    None,
    Down,
    Up,
    DblClick,
    Move,
    Whell,
}


[Flags]
public enum MouseButtons : byte
{
    None = 0,
    Right = 0b_0001,
    Left = 0b_0010,
    Middle = 0b_0100,
    Wheel = 0b_1000,
}


public interface IMouseMessage
{
    MouseEventType Type { get; }
    MouseButtons Button { get; }
    uint Time { get; }
    int ScreenX { get; }
    int ScreenY { get; }
}


public delegate bool MouseEventHandler(IMouseMessage message);


#if RHP
file
#endif
class MouseEventMessage : IMouseMessage
{
    internal MouseEventMessage(int pcode, MSLLHOOKSTRUCT hstruct)
    {
        m_struct = hstruct;
        m_pcode = pcode;
    }

    readonly int m_pcode;
    readonly MSLLHOOKSTRUCT m_struct;

    public MouseEventType Type => m_pcode switch
    {
        WM.LBUTTONDOWN => MouseEventType.Down,
        WM.LBUTTONUP => MouseEventType.Up,
        WM.LBUTTONDBLCLK => MouseEventType.DblClick,
        WM.RBUTTONDOWN => MouseEventType.Down,
        WM.RBUTTONUP => MouseEventType.Up,
        WM.MBUTTONDOWN => MouseEventType.Down,
        WM.MBUTTONUP => MouseEventType.Up,
        WM.MOUSEMOVE => MouseEventType.Move,
        //WM.MOUSEWHEEL    => MouseEventType.Down,
        _ => MouseEventType.None,
    };

    public MouseButtons Button => m_pcode switch
    {
        WM.LBUTTONDOWN => MouseButtons.Left,
        WM.LBUTTONUP => MouseButtons.Left,
        WM.LBUTTONDBLCLK => MouseButtons.Left,
        WM.RBUTTONDOWN => MouseButtons.Right,
        WM.RBUTTONUP => MouseButtons.Right,
        WM.MBUTTONDOWN => MouseButtons.Middle,
        WM.MBUTTONUP => MouseButtons.Middle,
        WM.MOUSEWHEEL => MouseButtons.Wheel,
        _ => MouseButtons.None,
    };

    public uint Time => m_struct.time;

    public int ScreenX => m_struct.X;
    public int ScreenY => m_struct.Y;
}


#if RHP
file
#endif
class MouseHook
{
    IntPtr _hook = IntPtr.Zero;
    int _pid = -1;
    IntPtr _hwnd = IntPtr.Zero;
    MouseEventHandler? _callback;


    public bool IsEnabled => _hook != IntPtr.Zero;


    #region Constraint

    int[]? _typefilter;

    void _SetTypeFilter(MouseEventType[] types)
    {
        var count = types.Length;
        var length = 0;
        for (var i = 0; i < count; i++)
        {
            switch (types[i])
            {
                case MouseEventType.Down: length += 3; break;
                case MouseEventType.Up: length += 3; break;
                case MouseEventType.Move: length += 1; break;
                case MouseEventType.DblClick: length += 1; break;
                    //TODO: MOUSEWHEEL
            }
        }

        _typefilter = null;

        if (length == 0)
            return;

        _typefilter = new int[length];

        var j = 0;
        for (var i = 0; i < count; i++)
        {
            switch (types[i])
            {
                case MouseEventType.Down:
                    _typefilter[j++] = WM.LBUTTONDOWN;
                    _typefilter[j++] = WM.RBUTTONDOWN;
                    _typefilter[j++] = WM.MBUTTONDOWN;
                    break;
                case MouseEventType.Up:
                    _typefilter[j++] = WM.LBUTTONUP;
                    _typefilter[j++] = WM.RBUTTONUP;
                    _typefilter[j++] = WM.MBUTTONUP;
                    break;
                case MouseEventType.Move:
                    _typefilter[j++] = WM.MOUSEMOVE;
                    break;
                case MouseEventType.DblClick:
                    _typefilter[j++] = WM.LBUTTONDBLCLK;
                    break;
                    //TODO: MOUSEWHEEL
            }
        }
    }

    public void StartOnAnyWindowsOf(IntPtr hWnd, MouseEventHandler callback, MouseEventType[] types)
    {
        _hwnd = IntPtr.Zero;
        UnsafeNativeMethods.GetWindowThreadProcessId(hWnd, out _pid);

        _SetTypeFilter(types);
        if (_Start(callback))
        {
            DBG.Log("Mouse hook OnAnySubWindows configured");
        }
    }

    public void StartOnWindow(IntPtr hWnd, MouseEventHandler callback, MouseEventType[] types)
    {
        _pid = -1;
        _hwnd = hWnd;

        _SetTypeFilter(types);
        if (_Start(callback))
        {
            DBG.Log("Mouse hook OnOneWindow configured");
        }
    }

    public void StartOnAllSystem(MouseEventHandler callback, MouseEventType[] types)
    {

        _SetTypeFilter(types);
        if (_Start(callback))
        {
            DBG.Log("Mouse hook OnAllSystem configured");
        }
    }

    #endregion


    #region Start/Stop

    UnsafeNativeMethods.HookProc? _hookproc;

    bool _Start (MouseEventHandler callback)
    {
        if (_hook != IntPtr.Zero)
            throw new Exception("The keyboard hook is already installed");

        if (callback == null)
            return false;

        DBG.Log("Mouse hook attached");

        _hookproc = new(_LowLevelProc);
        _hook = _AttachToSystem((int)WindowsMouseHookType.LowLevelMouse, _hookproc);
        _callback = callback;

        return true;
    }

    public void Stop ()
    {
        if (_hook == IntPtr.Zero)
            return;

        DBG.Log("Mouse hook detached");

        UnsafeNativeMethods.UnhookWindowsHookEx(_hook);
        _hookproc = null;
        _hook = IntPtr.Zero;
        _callback = null;
    }

    static IntPtr _AttachToSystem (int hookType, UnsafeNativeMethods.HookProc proc)
    {
        return UnsafeNativeMethods.SetWindowsHookEx(hookType, proc, Process.GetCurrentProcess().MainModule.BaseAddress, 0);
    }

    #endregion


    #region Proc

    public IntPtr _NextHook (int nCode, IntPtr wParam, IntPtr lParam) => UnsafeNativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);

    IntPtr _LowLevelProc (int nCode, IntPtr wParam, IntPtr lParam)
    {
        IntPtr CallNext () => _NextHook(nCode, wParam, lParam);

        if (nCode < 0)
            return CallNext ();

        if (_callback == null)
            return CallNext ();

        if (_typefilter != null)
        {
            var t = (int)wParam;

            foreach (var f in _typefilter)
                if (f == t) { t = 0; break; }

            if (t > 0)
                return CallNext();
        }

        if (_hwnd != IntPtr.Zero)
        {
            // Only window
            if (_hwnd != UnsafeNativeMethods.GetForegroundWindow ())
                return CallNext ();
        }
        else if (_pid != -1)
        {
            // Any windows
            var hWnd = UnsafeNativeMethods.GetForegroundWindow ();
            UnsafeNativeMethods.GetWindowThreadProcessId (hWnd, out var pid);
            if (pid != _pid)
                return CallNext ();
        }

        // DBG.Log ("LL Keyboard event captured");

        try
        {
            var msg = (MSLLHOOKSTRUCT)Marshal.PtrToStructure (lParam, typeof(MSLLHOOKSTRUCT));
            var evt = new MouseEventMessage ((int)wParam, msg);
            return _callback(evt) ? (IntPtr)1 : CallNext ();
        }
        catch (Exception e)
        {
            DBG.Fail (e);
            return CallNext ();
        }
    }

    #endregion
}


public interface IMouseObserver
{
    bool IsEnabled { get; }
    void StartOnAnyWindowsOf(IntPtr hWnd, MouseEventHandler callback, params MouseEventType[] types);
    void StartOnAllSystem(MouseEventHandler callback, params MouseEventType[] types);
    void Stop();
}


#if RHP
file
#endif
class MouseObserver : IMouseObserver
{
    MouseHook? _mhook;

    public bool IsEnabled => (_mhook?.IsEnabled) ?? false;

    public void StartOnAnyWindowsOf(IntPtr hWnd, MouseEventHandler callback, params MouseEventType[] types)
    {
        if (_mhook == null)
            _mhook = new();
        else if (_mhook.IsEnabled)
            return;

        _mhook.StartOnAnyWindowsOf(hWnd, callback, types);
    }

    public void StartOnAllSystem(MouseEventHandler callback, params MouseEventType[] types)
    {
        if (_mhook == null)
            _mhook = new();
        else if (_mhook.IsEnabled)
            return;

        _mhook.StartOnAllSystem(callback, types);
    }

    public void Stop()
    {
        if (_mhook == null)
            return;

        if (_mhook.IsEnabled == false)
            return;

        _mhook.Stop();
    }
}


class Mouse
{
    public static IMouseObserver CreateObserver ()
    {
        return new MouseObserver ();
    }
}
