/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)

    The Rhino API does not allow full control of keyboard events.
    To make this plugin compatible with MacOS it is necessary to implement the functions surrounded by the macro WIN32.

    ETO has more possibilities.
    The `Eto.Forms.Keyboard` class has the `Modifiers` property and the `IsKeyLocked` method that can be used.
    But I couldn't find any solution to set keyboard states or cancel keyboard event.
/*/

#define WIN32


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;

using EF = Eto.Forms;

using RhinoDoc = Rhino.RhinoDoc;
using RhinoApp = Rhino.RhinoApp;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Ui.Native;
#endif


// TODO: Convert NativeKeys to EF.Keys
public delegate bool CallbackHandler (int nativeKey);



enum WindowsHookType
{
    Keyboard         = 2,
    Mouse            = 7,
    LowLevelKeyboard = 13,
    LowLevelMouse    = 14
}


[StructLayout (LayoutKind.Sequential)]
#if RHP
file
#endif
readonly struct KBDLLHOOKSTRUCT
{
    public readonly int    VkCode;
    public readonly int    ScanCode;
    public readonly int    Flags;
    public readonly int    Time;
    public readonly IntPtr dwExtraInfo;
}


class KeyboardHook
{
    int _rhinoPId;
    IntPtr _rhinoWH;
	IntPtr _hook = IntPtr.Zero;
    CallbackHandler? _onDown;
    CallbackHandler? _onUp;

	public bool IsEnabled => _hook != IntPtr.Zero;


    #region Start/Stop
    

    UnsafeNativeMethods.HookProc? _hookproc;

	public void Stop ()
	{
		if( _hook == IntPtr.Zero )
            return;
        
        #if DEBUG
        RhinoApp.WriteLine ("Keyboard hook detached");
        #endif

        // TODO: GetLastError
		UnsafeNativeMethods.UnhookWindowsHookEx (_hook);
		_rhinoWH  = IntPtr.Zero;
        _rhinoPId = -1;
		_onUp     = null;
		_onDown   = null;
        _hookproc = null;
		_hook     = IntPtr.Zero;
	}

	public void Start (CallbackHandler? onKeyDown, CallbackHandler? onKeyUp)
	{
		if (_hook != IntPtr.Zero)
            throw new Exception ("The keyboard hook is already installed");

		if (onKeyDown == null && onKeyUp == null)
            return;

        #if DEBUG
        RhinoApp.WriteLine ("Keyboard hook attached");
        #endif

		_onDown   = onKeyDown != null ? new (onKeyDown) : null;
		_onUp     = onKeyUp   != null ? new (onKeyUp) : null;
		_rhinoWH  = RhinoApp.MainWindowHandle ();
        _rhinoPId = System.Diagnostics.Process.GetCurrentProcess().Id; // can run only as Rhino plugin

        _hookproc = new (_LowLevelProc);
        _hook = _AttachToSystem ((int) WindowsHookType.LowLevelKeyboard, _hookproc);
	}

	static IntPtr _AttachToSystem (int hookType, UnsafeNativeMethods.HookProc proc)
	{
	    return UnsafeNativeMethods.SetWindowsHookEx (hookType, proc, Process.GetCurrentProcess().MainModule.BaseAddress, 0);
	}

    #endregion


    #region Proc

	public IntPtr _NextHook (int nCode, IntPtr wParam, IntPtr lParam) => UnsafeNativeMethods.CallNextHookEx (_hook, nCode, wParam, lParam);

    IntPtr _LowLevelProc  (int nCode, IntPtr wParam, IntPtr lParam)
    {
        IntPtr CallNext () => _NextHook (nCode, wParam, lParam);

        if (nCode < 0)
            return CallNext ();

        var hWnd = UnsafeNativeMethods.GetForegroundWindow ();
        UnsafeNativeMethods.GetWindowThreadProcessId (hWnd, out var pid);
        // Note:
        // var anyRhinoWindows = pid == _rhinoPId;
        // var onlyRhinoWindow = _rhinoWH == UnsafeNativeMethods.GetForegroundWindow ();
        //
        // if (_rhinoWH != UnsafeNativeMethods.GetForegroundWindow ())
        //     return CallNext ();
        //
        if (pid != _rhinoPId)
            return CallNext ();

        #if DEBUG
        // RhinoApp.WriteLine ("LL Keyboard event captured");
        #endif

        var infos = (KBDLLHOOKSTRUCT) Marshal.PtrToStructure (lParam, typeof (KBDLLHOOKSTRUCT));
        
        var msg = (int) wParam;
        const int Keydown    = 0x100;
        const int SysKeydown = 0x104;
        const int Keyup      = 0x101;
        const int SysKeyup   = 0x105;

        try
        {
            if (msg == Keydown || msg == SysKeydown )
                if (_onDown != null)
                    return _onDown (infos.VkCode) ? (IntPtr) 1 : CallNext ();

            if ( msg == Keyup || msg == SysKeyup )
                if (_onUp != null)
                    return _onUp (infos.VkCode) ? (IntPtr) 1 : CallNext ();
        }
        catch (System.Exception e)
        {
            DBG.Fail ("HOOK ERROR:\n", e);
        }

        return CallNext ();
    }

    #endregion
}



public interface IKeyboardObserver
{
    bool IsEnabled {  get; }
    void Start (CallbackHandler? onKeyDown, CallbackHandler? onKeyUp);
    void Stop ();
}


class KeyboardObserver : IKeyboardObserver
{
    static KeyboardHook? _khook;

    public bool IsEnabled => (_khook?.IsEnabled) ?? false;

    public void Start (CallbackHandler? onKeyDown, CallbackHandler? onKeyUp)
    {
        if (_khook == null)
            _khook = new ();

        else if ( _khook.IsEnabled )
            return;
        
        #if DEBUG
        RhinoApp.WriteLine ("Start navigation menu");
        #endif

        _khook.Start (onKeyDown, onKeyUp);
    }

    public void Stop ()
    {
        if (_khook == null)
            return;

        if ( _khook.IsEnabled == false )
            return;
        
        #if DEBUG
        RhinoApp.WriteLine ("Stop navigation menu");
        #endif

        _khook.Stop ();
    }
    
    // static List <CallbackHandler>? _downcallbacks;
    // static List <CallbackHandler>? _upcallbacks;
    // static bool _OnKeyDown (int nativeKey)
    // {
    //     var handled = false;
    //     foreach (var fn in _downcallbacks!) {
    //         handled |= (fn?.Invoke (nativeKey)) ?? false;
    //     }
    //     return handled;
    // }
}


/// <summary>
///     Keyboard key for navigation. </summary>
/// <remarks>
///     This enumeration includes the uppercase key, in the future this key will be used to block a transformation axis. </remarks>
public enum KeyboardModifier
{
    None,
    Alt,
    Ctrl,
    Shift,
    Capital,
}


public static class Keyboard
{
    static bool _capslock;

    public static void MemorizeCapsLock ()
    {
        _capslock = UnsafeNativeMethods.CapsLockIsActive ();
    }

    public static void RestoreCapsLock ()
    {
        if (_capslock != UnsafeNativeMethods.CapsLockIsActive ()) 
            UnsafeNativeMethods.ToggleCapsLock ();
    }
    
    public static KeyboardModifier GetCurrentModifier ()
    {
        return EF.Keyboard.Modifiers switch
        {
            EF.Keys.Control => KeyboardModifier.Ctrl,
            EF.Keys.Shift   => KeyboardModifier.Shift,
            EF.Keys.Alt     => KeyboardModifier.Alt,
            _ => UnsafeNativeMethods.CapsLockIsDown () ? KeyboardModifier.Capital : KeyboardModifier.None
        };
    }
    
    public static bool AltIsDown () => UnsafeNativeMethods.AltIsDown ();

    public static bool CtrlIsDown () => UnsafeNativeMethods.CtrlIsDown ();

    public static bool ShiftIsDown () => UnsafeNativeMethods.ShiftIsDown ();

    public static bool CapsLockIsDown () => UnsafeNativeMethods.CapsLockIsDown ();

    public static bool CapsLockIsActive () => UnsafeNativeMethods.CapsLockIsActive ();


    public static IKeyboardObserver CreateObserver () { return new KeyboardObserver (); }

    // static IKeyboardObserver? g_observer;
    // 
    // public static bool ObserverIsStarted => (g_observer?.IsEnabled) ?? false;
    // 
    // public static void StartObserver (CallbackHandler? onKeyDown, CallbackHandler? onKeyUp)
    // {
    //     g_observer ??= new KeyboardObserver ();
    //     g_observer.Start (onKeyDown, onKeyUp);
    // }
    // 
    // public static void StopObserver ()
    // {
    //     if (g_observer != null)
    //         g_observer.Stop ();
    // }
}

