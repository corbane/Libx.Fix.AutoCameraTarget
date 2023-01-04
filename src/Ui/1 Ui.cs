/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RH = Rhino;
using RUI = Rhino.UI;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Ui;
#endif


public class FloatingForm : EF.Form
{
    public FloatingForm ()
    {
        Owner = RUI.RhinoEtoApp.MainWindow;
        MovableByWindowBackground = true;
    }
    
    #if RHP

    protected override void OnLoadComplete (EventArgs e)
    {
        base.OnLoadComplete(e);
		RUI.EtoExtensions.LocalizeAndRestore (this);
    }

    #else

    protected override void OnLoadComplete (EventArgs e)
    {
        base.OnLoadComplete (e);
        Visible = false;
    }

    protected override void OnShown (EventArgs e)
    {
        base.OnShown (e);
        Visible = true;

        var pos = RUI.RhinoEtoApp.MainWindow.Location;
        var size = RUI.RhinoEtoApp.MainWindow.Size;

        base.Location = new ED.Point(
            (int)(pos.X + size.Width*0.5 - Width*0.5),
            (int)(pos.Y + size.Height*0.5 - Height*0.5)
        );
    }

    #endif
}


/// <summary>
///     Utility methods that are unreliable and specific to this project. </summary>
static class EtoHelpers
{
    public static EF.StackLayout Divider (string label)
    {
        return new EF.StackLayout {
            Orientation = EF.Orientation.Horizontal,
            Spacing = 8,
            Items = {
                new EF.StackLayoutItem (new EF.Label { Text = label }, EF.VerticalAlignment.Stretch, expand: false),
                new EF.StackLayoutItem (new RUI.Controls.Divider (), EF.VerticalAlignment.Stretch, expand: true)
            }
        };
    }
    
    public static EF.Expander Expander (string label, params EF.Control?[] items)
    {
        var stack = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
        };
        foreach (var c in items) stack.Items.Add (c);
        return new EF.Expander 
        {
            Header = EtoHelpers.Divider (label),
            Expanded = false,
            Content = stack
        };
    }
}


#if false
class TEST : EF.Drawable
{
    NavigationOptions _opt;

    readonly int _padding = 5;

    readonly int _fontsize;
    readonly int _lineheight;
    readonly ED.Font _font;
    readonly ED.Color _fontcolor;

    readonly ED.Pen _strokecolor;

    public TEST (NavigationOptions options)
    {
        _opt = options;

        _fontsize = 12;
        _lineheight = _fontsize + 3;
        _font = new ED.Font (ED.FontFamilies.Sans, _fontsize);
        _fontcolor = ED.Colors.Black;

        _strokecolor = ED.Pens.Black;
    }

    struct Box
    {
        public int X, Y, W, H;
        public bool Contains (ED.PointF point)
        {
            return X <= point.X && point.X <= X+W
                && Y <= point.Y && point.Y <= Y+H;
        }
    }
    ED.Rectangle ClipRectangle;
    Box _btnrect = new ();

    protected override void OnPaint (EF.PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = e.ClipRectangle;
        ClipRectangle.X = (int)r.X;
        ClipRectangle.Y = (int)r.Y;
        ClipRectangle.Width = (int)r.Width;
        ClipRectangle.Height = (int)r.Height;
        var x = 10;
        var y = 10;

        var s = g.MeasureString (_font, "Paint");
        _btnrect.X = x;
        _btnrect.Y = y; 
        _btnrect.W = (int)s.Width+_padding*2;
        _btnrect.H = (int)s.Height+_padding*2;
        g.DrawRectangle (_strokecolor, x, y, _btnrect.W, _btnrect.H);
        g.DrawText (_font, _fontcolor, x+_padding, y+_padding, "Paint");
        y += _btnrect.H+_padding;

        g.DrawText (_font, _fontcolor, x, y, "Pan");
        y += _fontsize + _padding;;

        g.DrawText (_font, _fontcolor, x, y, "Zoom");
        y += _fontsize + _padding;;

        g.DrawText (_font, _fontcolor, x, y, "Rotate");
        y += _fontsize + _padding;;

        base.OnPaint (e);
    }

    protected override void OnMouseDown (EF.MouseEventArgs e)
    {
        if (_btnrect.Contains (e.Location))
        {
            Update (ClipRectangle);
        }
        base.OnMouseDown (e);
    }
}
#endif
