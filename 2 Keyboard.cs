/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


// The Rhino API has no method to show or hide the cursor and does not allow full control of keyboard events.
// To make this plugin compatible with MacOS it is necessary to implement the functions surrounded by this macro.
#define WIN32


using System;
using System.Runtime.InteropServices;

using EF = Eto.Forms;

#if RHP

namespace Libx.Fix.AutoCameraTarget;

#endif



enum ModifierKey { Ctrl, Shift, Alt, Capital, None }


static class Keyboard
{
    static bool _capslock;

    #if WIN32
    // https://discourse.mcneel.com/t/finalize-the-mousecallback-class/148057/3
    // private static int VK_MENU = 0x12;
    // Finalement `EF.Keyboard.Modifiers == EF.Keys.Alt` fait le job.
    // `RhinoApp.CommandWindowCaptureEnabled` annule `RhinoApp.WriteLine` mais pas les entrées du clavier.

    const int VK_CAPITAL = 0x14;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern short GetKeyState(int keyCode);

    [DllImport("user32.dll")]
    static extern void keybd_event (byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static bool CapsLockIsDown ()
    {
        return GetKeyState (VK_CAPITAL) < 0;
    }

    public static bool CapsLockIsActive ()
    {
        // https://stackoverflow.com/questions/577411/how-can-i-find-the-state-of-numlock-capslock-and-scrolllock-in-net
        return (((ushort)GetKeyState(0x14)) & 0xffff) != 0;
    }

    public static void RestoreCapsLock ()
    {
        if (_capslock == CapsLockIsActive ()) return;
        
        const int KEYEVENTF_EXTENDEDKEY = 0x1;
        const int KEYEVENTF_KEYUP = 0x2;

        // https://stackoverflow.com/questions/13623245/how-do-i-turn-off-the-caps-lock-key
        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-keybd_event
        keybd_event (VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
        keybd_event (VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
    }
    
    #endif

    public static void MemorizeCapsLock ()
    {
        _capslock = CapsLockIsActive ();
    }

    public static ModifierKey GetCurrentModifier ()
    {
        return EF.Keyboard.Modifiers switch
        {
            EF.Keys.Control => ModifierKey.Ctrl,
            EF.Keys.Shift   => ModifierKey.Shift,
            EF.Keys.Alt     => ModifierKey.Alt,
            _ => Keyboard.CapsLockIsDown () ? ModifierKey.Capital : ModifierKey.None
        };
    }

}

