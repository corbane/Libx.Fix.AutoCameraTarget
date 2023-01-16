
#define WIN32

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.ComponentModel;

#if RHP
namespace Libx.Fix.AutoCameraTarget.Ui.Native;
#endif


#if RHP
// TODO: Convert NativeKeys to EF.Keys
// file
#endif
static class NativeKeys
{
    public const int SHIFT   = 0x10;
    public const int CONTROL = 0x11;
    public const int MENU    = 0x12; // ALT
    public const int CAPITAL = 0x14;
    public const int SPACE   = 0x20;
}


static class UnsafeNativeMethods
{
    #region Global Hook

    public delegate IntPtr HookProc ( int nCode, IntPtr wParam, IntPtr lParam );

    [DllImport ("user32.dll")]
    public  static extern IntPtr SetWindowsHookEx (int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport ("user32.dll")]
    [return: MarshalAs (UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx (IntPtr hhk);

    [DllImport ("user32.dll")]
    public static extern IntPtr CallNextHookEx (IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport ("kernel32.dll")]
    public static extern IntPtr GetModuleHandle (string lpModuleName);

    [DllImport ("user32.dll")]
    public static extern IntPtr GetForegroundWindow ();

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern int GetWindowThreadProcessId(IntPtr  hWnd, out int ProcessId);

    // Ne fonctionne plus.
    // https://discourse.mcneel.com/t/get-the-drop-down-list-handle-of-the-command-line/74629/3
    //
    // [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    // public static extern int GetWindowTextLength(IntPtr hWnd);
    // 
    // [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    // public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    // 
    // public static string? GetWindowText (IntPtr hwnd)
    // {
    //     try { 
    //         StringBuilder sb = new StringBuilder(UnsafeNativeMethods.GetWindowTextLength (hwnd) + 1);
    //         UnsafeNativeMethods.GetWindowText (hwnd, sb, sb.Capacity);
    //         return sb.ToString ();
    //     } catch {
    //         return null;
    //     }
    // }

    // https://discourse.mcneel.com/t/finalize-the-mousecallback-class/148057/3
    // Finalement `EF.Keyboard.Modifiers == EF.Keys.Alt` fait le job.
    // `RhinoApp.CommandWindowCaptureEnabled` annule `RhinoApp.WriteLine` mais pas les entrées du clavier.

    #endregion


    #region Keyboard Hook

    [DllImport ("user32.dll", CharSet = CharSet.Auto)]
    private static extern short GetKeyState(int keyCode);

    [DllImport("user32.dll")]
    static extern void keybd_event (byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    #endregion


    #region Keyboard Helpers

    public static bool AltIsDown ()
    {
        return GetKeyState (NativeKeys.MENU) < 0;
    }

    public static bool CtrlIsDown ()
    {
        return GetKeyState (NativeKeys.CONTROL) < 0;
    }

    public static bool ShiftIsDown ()
    {
        return GetKeyState (NativeKeys.SHIFT) < 0;
    }

    public static bool CapsLockIsDown ()
    {
        return GetKeyState (NativeKeys.CAPITAL) < 0;
    }

    public static bool CapsLockIsActive ()
    {
        // https://stackoverflow.com/questions/577411/how-can-i-find-the-state-of-numlock-capslock-and-scrolllock-in-net
        return (((ushort)GetKeyState(0x14)) & 0xffff) != 0;
    }

    public static void ToggleCapsLock ()
    {
        const int KEYEVENTF_EXTENDEDKEY = 0x1;
        const int KEYEVENTF_KEYUP = 0x2;

        // https://stackoverflow.com/questions/13623245/how-do-i-turn-off-the-caps-lock-key
        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-keybd_event
        keybd_event (NativeKeys.CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
        keybd_event (NativeKeys.CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
    }

    #endregion


    [DllImport("user32.dll")]
    public static extern int ShowCursor(bool bShow);
}