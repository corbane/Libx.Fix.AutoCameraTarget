/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Reflection;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RH = Rhino;
using RUI = Rhino.UI;
using RhinoViewSettings = Rhino.ApplicationSettings.ViewSettings;


#if RHP

using Libx.Fix.AutoCameraTarget.Views;

namespace Libx.Fix.AutoCameraTarget.Config;

#endif


public class OptionAttribute : Attribute
{
    public string? Tooltip;

    public double Min = 0;
    public double Max = 100;
    public double Increment = 1;

    internal bool Valid = true;

    static Dictionary <(IntPtr Ptr, string PropertyNname), OptionAttribute> g_cache = new ();

    public static OptionAttribute Get (Type t, string propname)
    {
        var k = (t.TypeHandle.Value, propname);

        if (g_cache.TryGetValue (k, out var a)) return a;
        
        var p = t.GetProperty (propname);
        a = p.GetCustomAttribute <OptionAttribute> (inherit: true)
            ?? new OptionAttribute ();
        a._propinfo = p;

        return g_cache[k] = a;
    }

    #nullable disable // set by Get()
    PropertyInfo _propinfo;
    #nullable enable

    public object GetValue (object obj)
    {
        return _propinfo.GetValue (obj);
    }

    public T GetValue <T> (object obj)
    {
        return (T)_propinfo.GetValue (obj);
    }
}


public class Exclude : Attribute { }


public interface ISettings : INotifyPropertyChanged
{
    int DataVersion { get; }
    T Copy <T> () where T : new();
    void Apply (object data);
    bool Validate ();
}


public abstract class Settings : ISettings
{
    #region Event

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion


    #region Helpers

    [Exclude]
    public int DataVersion { get; private set; }

    protected void Set <T> (ref T member, T value, [CallerMemberName] string? propertyName = null)
    {
        if (object.Equals (member, value)) return;
        member = value;
        DataVersion++;
        Emit (propertyName);
    }

    public T Copy <T> () where T : new()
    {
        var data = new T ();

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        foreach (var p in props)
            p.SetValue (data, p.GetValue (this));

        return data;
    }

    public void Apply (object data)
    {
        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        foreach (var p in props)
            p.SetValue (this, p.GetValue (data));
    }
    
    public void Save (RH.PersistentSettings settings)
    {
        var t_bool   = typeof (bool);
        var t_int    = typeof (int);
        var t_double = typeof (double);
        var t_modkey = typeof (NavigationModifier); // !!! NavigationModifier here ! Is not a good implementation !!!

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        foreach (var p in props)
        {
            var t = p.PropertyType;

            if (t == t_modkey)
                settings.SetEnumValue <NavigationModifier> (p.Name, (NavigationModifier)p.GetValue (this));

            else if (t == t_bool)
                settings.SetBool (p.Name, (bool)p.GetValue (this));
            
            else if (t == t_int)
                settings.SetInteger (p.Name, (int)p.GetValue (this));
            
            else if (t == t_double)
                settings.SetDouble (p.Name, (double)p.GetValue (this));
        }
    }

    public void Load (RH.PersistentSettings settings)
    {

        var t_bool   = typeof (bool);
        var t_int    = typeof (int);
        var t_double = typeof (double);
        var t_modkey = typeof (NavigationModifier); // !!! NavigationModifier here ! Is not a good implementation !!!

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        var keys = settings.Keys;
        foreach (var p in props)
        {
            if (keys.Contains (p.Name) == false) continue;

            var t = p.PropertyType;

            if (t == t_modkey)
                p.SetValue (this, settings.GetEnumValue <NavigationModifier> (p.Name));

            else if (t == t_bool)
                p.SetValue (this, settings.GetBool (p.Name));
            
            else if (t == t_int)
                p.SetValue (this, settings.GetInteger (p.Name));
            
            else if (t == t_double)
                p.SetValue (this, settings.GetDouble (p.Name));
        }
    }

    #endregion


    #region Errors

    [Exclude]
    public List <string> Errors {get; } = new ();

    public void ClearErrors ()
    {
        Errors.Clear ();
        Emit (nameof (Errors));
    }
    public void AddError (string message)
    {
        Errors.Add (message);
        Emit (nameof (Errors));
    }

    #endregion


    public abstract bool Validate ();
}


public class SettingsLayout <Opt> : EF.StackLayout where Opt : ISettings, new ()
{
    public const int SCRW = 14;
    public const int PAD = 8;
    public const int LINEH = 22;
    public const int CTRLW = 140;


    readonly Type _datatype;
    public readonly Opt Data;
    public Opt Copy { get; private set; }

    readonly EF.Scrollable _wrap;
    readonly EF.StackLayout _body;
    readonly EF.StackLayout _foot;
    readonly EF.Button _bok;
    readonly EF.Button _bno;


    public SettingsLayout (Opt options)
    {
        Orientation                = EF.Orientation.Vertical;
        HorizontalContentAlignment = EF.HorizontalAlignment.Stretch;
        VerticalContentAlignment   = EF.VerticalAlignment.Stretch;
        MinimumSize                = new (CTRLW + PAD*3 + SCRW, LINEH);

        Data        = options;
        Copy        = Data.Copy <Opt> ();
        DataContext = options;
        _datatype   = options.GetType ();
        
        _bno = new EF.Button { Text = "Cancel" };
        _bno.Click += delegate { RestoreOptions (); /*Close ();*/ };

        _bok = new EF.Button { Text = "Ok" };
        _bok.Click += delegate { ApplyOptions (); /*Close ();*/ };

        _body = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment = EF.VerticalAlignment.Stretch,
        };

        _wrap = new EF.Scrollable
        {
            Border = EF.BorderType.None,
            Padding = new (PAD, PAD, PAD + SCRW, 0 + SCRW),
            ExpandContentHeight = true,
            ExpandContentWidth  = false,
            Content = _body
        };

        _foot = new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            Padding = new (PAD),
            Spacing = 8,
            Items = { null, _bok, _bno, null }
        };

        Items.Add (new EF.StackLayoutItem (_wrap, expand: true));
        Items.Add (new EF.StackLayoutItem (_foot, expand: false));
    }


    #region Controls

    public EF.Button ButtonOk => _bok;
    
    public EF.Button ButtonCancel => _bno;

    public new EF.Control Content
    {
        get => _body;
        set { throw new NotImplementedException ("Cannot define the content"); }
    }

    #endregion


    #region Events

    protected override void OnShown (EventArgs e)
    {
        base.OnShown (e);
        if (_wrap.Content != null)
            SizeChanged += _OnSizeChanged;
    }

    // La barre de défilement vertical pose des problémes visuels:
    // - Si les controles enfants peuvent être décaler, ils sont décaler l'orsque la barre s'affiche.
    // - Si les controles enfants ne peuvent pas être décaler, la barre est déssiner par dessus les controles enfants.
    // - Sous windows 11 cette barre de défilement est énorme, donc les bouton Up/Down d'une entrée numérique sont cachées.
    // - Eto n'offre pas de possibilité de réduire la largeur de la barre ou de la laisser toujours affiché.
    // L'idée ici est de toujours laisser un espae vide pour l'emplacemet de la barre de défilement.
    // Pour que cela fonctionne il faut que la propriété `ExpandContentWidth` du Scrollable soit sur false.
    protected void _OnSizeChanged (object sender, EventArgs e)
    {
        if (this.HasFocus == false) return;
        _body.Width = Width - (PAD*2 + SCRW);
    }

    protected override void OnUnLoad(EventArgs e)
    {
        if (Data.DataVersion != Copy.DataVersion)
            ApplyOptions ();
        base.OnUnLoad(e);
    }

    #endregion


    #region Options

    public virtual void RestoreOptions ()
    {
        Data.Apply (Copy);
    }

    public virtual void ApplyOptions ()
    {
        if (Data.Validate () == false) Data.Apply (Copy);
        else Copy = Data.Copy <Opt> ();
    }

    #endregion


    #region Reflection

    protected readonly Dictionary <string, (EF.Control Control, OptionAttribute Attribute)> PropertyToControls = new ();
    
    protected EF.Control GetControl (string propname)
    {
        if (PropertyToControls.ContainsKey (propname))
            return PropertyToControls[propname].Control;
            
        var a = OptionAttribute.Get (_datatype, propname);

        EF.Control c;
        switch (a.GetValue (Data))
        {
        default: throw new ArgumentException ("Unknow property type", nameof (propname));
        case bool               : c = _CheckBox (propname); break;
        case int                : c = _NumericStepper (propname); break;
        case double             : c = _NumericStepper (propname); break;
        case NavigationModifier : c = _EnumDropDown <NavigationModifier> (propname); break; // !!! NavigationModifier here ! Is not a good implementation !!!
        }

        PropertyToControls[propname] = (c, a);
        return c;
    }

    EF.StackLayout _CheckBox (string propname, string? text = null)
    {
        var a = OptionAttribute.Get (_datatype, propname);
        var c = new EF.CheckBox {
            Text   = text,
            Width  = LINEH,
            Height = LINEH,
        };

        c.CheckedBinding.Bind (Data, propname);
        if (string.IsNullOrWhiteSpace (a.Tooltip) == false) c.ToolTip = a.Tooltip;
        
        // Sinon la case capte la souris sur toute la largeur.
        return new EF.StackLayout { Items = { c } };
    }

    EF.EnumDropDown <T> _EnumDropDown <T> (string propname)
    {
        var a = OptionAttribute.Get (_datatype, propname);
        var c = new EF.EnumDropDown <T> {
            Width  = LINEH,
            Height = LINEH,
        };

        c.SelectedValueBinding.Bind (Data, propname);
        if (string.IsNullOrWhiteSpace (a.Tooltip) == false) c.ToolTip = a.Tooltip;

        return c;
    }

    EF.NumericStepper _NumericStepper (string propname)
    {
        var a = OptionAttribute.Get (_datatype, propname);
        var c = new EF.NumericStepper {
            Width  = CTRLW,
            Height = LINEH
        };

        c.MinValue      = a.Min;
        c.MaxValue      = a.Max;
        c.Increment     = a.Increment;
        c.DecimalPlaces = BitConverter.GetBytes(decimal.GetBits((decimal)a.Increment)[3])[2];

        c.ValueBinding.Bind (Data, propname);
        if (string.IsNullOrWhiteSpace (a.Tooltip) == false) c.ToolTip = a.Tooltip;

        return c;
    }

    #endregion


    #region Helpers

    protected void Section (string label, params EF.TableRow[] rows)
    {
        _body.Items.Add (_Divider (label));
        Section (rows);
    }

    protected void Section (params EF.TableRow[] rows)
    {
        var c = new EF.TableLayout { Spacing = new (PAD, PAD) };

        foreach (var r in rows) c.Rows.Add (r);

        // without this line (without the label) the column widths of a table with one row are not correctly calculed.
        c.Rows.Add (new EF.TableRow (null, new EF.Label { Width = CTRLW, Height = 0 }));

        _body.Items.Add (new EF.StackLayoutItem (c, EF.VerticalAlignment.Stretch));
    }

    protected EF.TableRow Row (string label, EF.Control? cell)
    {
        var row =  new EF.TableRow ();
        row.Cells.Add (new EF.TableCell (_Label (label, 3), scaleWidth: true));
        row.Cells.Add (new EF.TableCell (cell, scaleWidth: false));
        return row;
    }

    protected EF.StackLayout HBox (params EF.Control[] controls)
    {
        var hbox = new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            Width = CTRLW
        };
        var w = CTRLW / controls.Length;
        foreach (var c in controls) 
        {
            c.Width = w;
            hbox.Items.Add (new EF.StackLayoutItem (c, expand: true));
        }
        return hbox;
    }

    EF.StackLayout _Divider (string label)
    {
        return new EF.StackLayout {
            Orientation = EF.Orientation.Horizontal,
            Spacing = 8,
            Items = {
                new EF.StackLayoutItem (_Label ( label ), EF.VerticalAlignment.Stretch, expand: false),
                new EF.StackLayoutItem (new RUI.Controls.Divider {  }, EF.VerticalAlignment.Stretch, expand: true)
            }
        };
    }
    
    EF.Expander _Expander (string label, params EF.Control?[] items)
    {
        var stack = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
        };
        foreach (var c in items) stack.Items.Add (c);
        return new EF.Expander 
        {
            Header = _Divider (label),
            Expanded = false,
            Content = stack
        };
    }

    EF.Label _Label (string text, int indent = 0)
    {
        return new EF.Label {
            Text   = new string(' ', indent)+text,
            Height = LINEH,
            Wrap   = EF.WrapMode.None,
        };
    }

    #endregion
}

