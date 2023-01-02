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
namespace Libx.Fix.AutoCameraTarget;
#endif


public static class Ui
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
            Header = Ui.Divider (label),
            Expanded = false,
            Content = stack
        };
    }

    public static EF.CheckBox CheckBox (object data, string property, string? text = null)
    {
        var c = new EF.CheckBox { Text = text };
        c.CheckedBinding.Bind (data, property);
        return c;
    }
}
