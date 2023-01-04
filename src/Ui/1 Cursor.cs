/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


// To make this plugin compatible with MacOS it is necessary to implement the functions surrounded by this macro.
// ~~The Rhino API has no method to show or hide the cursor~~.
// *It seems to be possible to use `System.Windows.Forms.Cursor` https://discourse.mcneel.com/t/hide-cursor/93850/17
#define WIN32


using System.Runtime.InteropServices;
using System.IO;
using SD = System.Drawing;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RD = Rhino.Display;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Ui;
#endif


/// <summary>
///     Utility class to manage the system cursor. </summary>
public static class Cursor
{
    #if WIN32

    [DllImport("user32.dll")]
    static extern int ShowCursor(bool bShow);

    static int _ccount = int.MaxValue;
    public static void HideCursor ()
    {
        while (_ccount >= 0) _ccount = ShowCursor (false);
        return;
    }

    public static void ShowCursor ()
    {
        while (_ccount > 0) _ccount = ShowCursor (false);
        while (_ccount < 1) _ccount = ShowCursor (true);
        return;
    }
    
    #endif
    
    /// <summary>
    ///     Viewport point when the mouse button is down. </summary>
    static ED.Point _initiaCursorPos;

    static SD.Rectangle _clientArea;


    public static ED.Point InitialCursorPosition => _initiaCursorPos;

    public static void InitCursor (RD.RhinoViewport viewport, SD.Point position)
    {
        _initiaCursorPos = new (position.X, position.Y);
        _clientArea = viewport.ParentView.ScreenRectangle;
    }

    public static void SetCursorPosition (ED.Point pos)
    {
        EF.Mouse.Position = new (_clientArea.X + pos.X, _clientArea.Y + pos.Y);
    }

    public static void SetLimitedCursorPosition (int X, int Y)
    {
        X = X < 0 ? 0 : X > _clientArea.Width ? _clientArea.Width : X;
        Y = Y < 0 ? 0 : Y > _clientArea.Height ? _clientArea.Height : Y;
        EF.Mouse.Position = new (_clientArea.X + X, _clientArea.Y + Y);
    }
}


public enum VirtualCursorIcon { Hand, Glass, Pivot, Axis, None }


/// <summary>
///     Class to draw a replacement cursor in the viewport. </summary>
public class VirtualCursor : RD.DisplayConduit
{
    static VirtualCursor? g_instance;

    public static void Init (ED.Point initialPosition)
    {
        _initiapos = new (initialPosition);
        _offset = new (0, 0);
    }

    public static void Show (VirtualCursorIcon type)
    {
        Icon = type;
        g_instance ??= new ();
        g_instance.Enabled = true;
    }

    public static void Hide ()
    {
        if (g_instance != null)
            g_instance.Enabled = false;
    }

    #region Ressources

    #if RHP
    const string _rscpath = "Libx.Fix.AutoCameraTarget.ico.";
    static Stream _GetStream (string path) => typeof (VirtualCursor).Assembly.GetManifestResourceStream (path);
    static SD.Bitmap _Get (string filename) => new (_GetStream (_rscpath + filename));
    #else
    static string _ressourceDiectory = @"E:\Projet\Rhino\Libx\Libx.Fix.AutoCameraTarget\ico";
    static SD.Bitmap _Get (string filename) => new (Path.Combine (_ressourceDiectory, filename));
    #endif

    static VirtualCursorIcon Icon;

    // Actuellement les images **DOIVENT** avoir une taille de 20x20px
    static readonly RD.DisplayBitmap _tIco = new (_Get ("Hand.png"));
    static readonly RD.DisplayBitmap _zIco = new (_Get ("MagnifyingGlass.png"));
    static readonly RD.DisplayBitmap _rIco = new (_Get ("Rotation.png"));
    static readonly RD.DisplayBitmap _xIco = new (_Get ("PredefinedOrientations.png"));

    #endregion

    #region Position

    static ED.Point _initiapos;
    static ED.Point _offset;

    public static ED.Point Position => new (_initiapos.X + _offset.X, _initiapos.Y + _offset.Y);

    public static void GrowPosition (ED.Point point)
    {
        _offset.X += point.X;
        _offset.Y += point.Y;
    }

    #endregion

    // DrawOverlay ne dessine pas au dessus des objets sélectionnés et du Gumball.
    protected override void DrawOverlay (RD.DrawEventArgs e)
    {
        var pos = Position;
        switch (Icon)
        {
        case VirtualCursorIcon.Glass : e.Display.DrawBitmap (_zIco, pos.X-10, pos.Y-10); break;
        case VirtualCursorIcon.Hand  : e.Display.DrawBitmap (_tIco, pos.X-10, pos.Y-10); break;
        case VirtualCursorIcon.Pivot : e.Display.DrawBitmap (_rIco, pos.X-10, pos.Y-10); break;
        case VirtualCursorIcon.Axis  : e.Display.DrawBitmap (_xIco, pos.X-10, pos.Y-10); break;
        }
    }
}
