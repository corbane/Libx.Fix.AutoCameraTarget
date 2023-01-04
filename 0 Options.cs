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
namespace Libx.Fix.AutoCameraTarget;
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


public interface IOptions : INotifyPropertyChanged
{
    int DataVersion { get; }
    T Copy <T> () where T : new();
    void Apply (object data);
    bool Validate ();
}


public abstract class Settings : IOptions
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



public class NavigationSettings : Settings, INavigationSettings
{
    const string TT_MOD = "Modifier key. \n'None' for no modifier key, 'Disable' to disable this feature.";
    const string TT_MOD_PV = "Modifier key in plan views. \n'None' for no modifier key, 'Disable' to disable this feature.";

    bool _active  = false;
    bool _activeP = false;
    int  _delay   = 150;
    bool _marker  = false;
    
    [Option (Tooltip = "Enable plug-in in non plan views.")]
    public bool Active { get => _active; set { Set (ref _active, value); } }

    [Option (Tooltip = "Enable plug-in in plan views.")]
    public bool ActiveInPlanView { get => _activeP; set { Set (ref _activeP, value); } }

    [Option (Tooltip = "Adds a pause when a modifier key is released to avoid transforming the view when you stop navigating.",
             Min = 0, Max = 1000, Increment = 1)]
    public int DelayBetweenModes { get => _delay; set { Set (ref _delay, value); } }

    [Option (Tooltip = "Show camera pivot point.")]
    public bool Marker { get => _marker; set { Set (ref _marker, value); } }


    #region Visual Debug

    bool _debug;

    [Option (Tooltip = "Display visual information for debugging or understanding the intersection process.")]
    public bool Debug { get => _debug;  set { Set (ref _debug, value);  } }

    #if DEBUG
    [Exclude]
    public bool ShowCamera { get => _showcam;  set { Set (ref _showcam, value);  } }
    bool _showcam;
    #endif

    #endregion


    // TODO:
    // ??? Pan plan parallel views with Ctrl+Shift+RMB ???
    // ??? Auto adjust camera target after Pan and Zoom ???
    // ??? Gumbal Rotate view around Gumball ???
    // public bool AlwaysPanParallelViews => RhinoViewSettings.AlwaysPanParallelViews;


    #region Pan

    NavigationModifier _pmod = NavigationModifier.Shift;
    NavigationModifier _pmodP = NavigationModifier.Disabled;
    
    [Option (Tooltip = TT_MOD)]
    public NavigationModifier PanModifier { get => _pmod;  set { Set (ref _pmod, value);  } }

    [Option (Tooltip = TT_MOD_PV)]
    public NavigationModifier PanModifierInPlanView { get => _pmodP;  set { Set (ref _pmodP, value);  } }

    #endregion


    #region Turn

    NavigationModifier _rmod = NavigationModifier.None;
    NavigationModifier _rmodP = NavigationModifier.Shift;

    [Option (Tooltip = TT_MOD)]
    public NavigationModifier RotateModifier { get => _rmod;  set { Set (ref _rmod, value);  } }

    [Option (Tooltip = TT_MOD_PV)]
    public NavigationModifier RotateModifierInPlanView { get => _rmodP;  set { Set (ref _rmodP, value);  } }

    #endregion


    #region Zoom

    NavigationModifier _zmod = NavigationModifier.Ctrl;
    NavigationModifier _zmodP = NavigationModifier.Disabled;
    bool   _zinv;
    double _zforce = 4;

    [Option (Tooltip = TT_MOD)]
    public NavigationModifier ZoomModifier { get => _zmod;  set { Set (ref _zmod, value);  } }

    [Option (Tooltip = TT_MOD_PV)]
    public NavigationModifier ZoomModifierInPlanView { get => _zmodP;  set { Set (ref _zmodP, value);  } }

    // ??? Zoom > Reverse action ???
    [Option (Tooltip = "Reverse zoom direction. \nShould be set with 'Rhino Options > View > Zoom > Reverse action'.")]
    public bool ReverseZoom { get => _zinv;  set { Set (ref _zinv, value);  } }

    [Option (Min = 0.1, Max = 10, Increment = 0.1)]
    public double ZoomForce { get => _zforce;  set { Set (ref _zforce, value);  } }

    #endregion


    #region Presets

    NavigationModifier _xmod  = NavigationModifier.Alt;
    NavigationModifier _xmodP = NavigationModifier.Alt;
    int    _sangle = 4;
    double _sforce = 1;
    bool   _scplane = false;

    [Option (Tooltip = TT_MOD)]
    public NavigationModifier PresetsModifier { get => _xmod;  set { Set (ref _xmod, value);  } }

    [Option (Tooltip = TT_MOD_PV)]
    public NavigationModifier PresetsModifierInPlanView { get => _xmodP;  set { Set (ref _xmodP, value);  } }

    [Option (Tooltip = "Number of preset positions in 360°.", Min = 1, Max = 60, Increment = 1)]
    public int PresetSteps { get => _sangle;  set { Set (ref _sangle, value);  } }

    [Option (Min = 0.1, Max = 10, Increment = 0.1)]
    public double PresetForce { get => _sforce;  set { Set (ref _sforce, value);  } }

    [Option (Tooltip = "Automatically align the CPlane (Front, Right, Top) to the view.")]
    public bool PresetsAlignCPlane { get => _scplane;  set { Set (ref _scplane, value);  } }

    // [Option (
    //     Tooltip = "Enable preset positions in plan views."
    // )]
    // public bool PresetsInPlanView { get => _sinplan;  set { Set (ref _sinplan, value);  } }
    // bool _sinplan = false;

    #endregion


    #region Validation

    // TODO:
    // Placer la validation dans une class, ce n'est vraiment pas utile de garder ces variables en mémoire
    // si la boite de controles n'est pas afficher (même si ce sont de petite variable).

    static readonly Type NavigationOptionsType = typeof (NavigationSettings);

    string[] g_modnames = new [] {
        nameof (PanModifier),
        nameof (RotateModifier),
        nameof (ZoomModifier),
        nameof (PresetsModifier)
    };

    string[] g_modnamesP = new [] {
        nameof (PanModifierInPlanView),
        nameof (RotateModifierInPlanView),
        nameof (ZoomModifierInPlanView),
        nameof (PresetsModifierInPlanView)
    };

    public override bool Validate ()
    {
        return _Validate (g_modnames)
            && _Validate (g_modnamesP);

        bool _Validate (string[] names)
        {
            OptionAttribute a, b;
            NavigationModifier vA, vB;
            var c = names.Length;
            var ok = true;

            for (var i = 0 ; i < c ; i++)
            {
                a = OptionAttribute.Get (NavigationOptionsType, names[i]);
                a.Valid = true;
                vA = a.GetValue <NavigationModifier> (this);
                if (vA == NavigationModifier.Disabled) continue;

                for (var j = 0 ; j < c ; j++)
                {
                    b = OptionAttribute.Get (NavigationOptionsType, names[j]);
                    vB = b.GetValue <NavigationModifier> (this);
                    if (vB == NavigationModifier.Disabled) continue;

                    if (i != j && vA == vB) {
                        a.Valid = ok = false;
                        break;
                    }
                }
            }

            return ok;
        }
    }

    #endregion


    #region Utility

    public NavigationMode GetAssociatedMode (NavigationModifier modifier, bool inPlanView)
    {
        if (inPlanView)
        {
            if(_pmodP == modifier) return NavigationMode.Pan;
            if(_zmodP == modifier) return NavigationMode.Zoom;
            if(_rmodP == modifier) return NavigationMode.Rotate;
            if(_xmodP == modifier) return NavigationMode.Presets;
        }
        else
        {
            if(_pmod == modifier) return NavigationMode.Pan;
            if(_zmod == modifier) return NavigationMode.Zoom;
            if(_rmod == modifier) return NavigationMode.Rotate;
            if(_xmod == modifier) return NavigationMode.Presets;
        }
        return NavigationMode.Unknown;
    }

    #endregion
}


class SettingsLayout <Opt> : EF.StackLayout where Opt : IOptions, new ()
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


    public EF.Button ButtonOk => _bok;
    
    public EF.Button ButtonCancel => _bno;

    public new EF.Control Content
    {
        get => _body;
        set { throw new NotImplementedException ("Cannot define the content"); }
    }


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


class NavigationSettingsLayout : SettingsLayout <NavigationSettings>
{
    readonly EF.Control _pmod, _pmodP;
    readonly EF.Control _rmod, _rmodP;
    readonly EF.Control _zmod, _zmodP, _zinv, _zforce;
    readonly EF.Control _xmod, _xmodP, _xsteps, _xforce, _xcplane;

    public NavigationSettingsLayout (NavigationSettings options) : base (options)
    {
        options.PropertyChanged += _OnDataChanged;

        var active  = GetControl (nameof (options.Active));
        var activeP = GetControl (nameof (options.ActiveInPlanView));
        var delay   = GetControl (nameof (options.DelayBetweenModes));
        var marker  = GetControl (nameof (options.Marker));
        var debug   = GetControl (nameof (options.Debug));
        #if DEBUG
        var showcam = GetControl (nameof (options.ShowCamera));
        #endif

        _pmod  = GetControl (nameof (options.PanModifier));
        _pmodP = GetControl (nameof (options.PanModifierInPlanView));

        _rmod  = GetControl (nameof (options.RotateModifier));
        _rmodP = GetControl (nameof (options.RotateModifierInPlanView));

        _zmod   = GetControl (nameof (options.ZoomModifier));
        _zmodP  = GetControl (nameof (options.ZoomModifierInPlanView));
        _zinv   = GetControl (nameof (options.ReverseZoom));
        _zforce = GetControl (nameof (options.ZoomForce));

        _xmod    = GetControl (nameof (options.PresetsModifier));
        _xmodP   = GetControl (nameof (options.PresetsModifierInPlanView));
        _xsteps  = GetControl (nameof (options.PresetSteps));
        _xforce  = GetControl (nameof (options.PresetForce));
        _xcplane = GetControl (nameof (options.PresetsAlignCPlane));

        //

        Section (
            "Global",
            Row ("Active", HBox (active, activeP)),
            Row ("Delay", delay)
        );

        Section (
            "Pan",
            Row ("Modifier", HBox (_pmod, _pmodP))
        );

        Section (
            "Rotation",
            Row ("Modifier", HBox (_rmod, _rmodP))
        );

        Section (
            "Zoom",
            Row ("Modifier", HBox (_zmod, _zmodP)),
            Row ("Force", _zforce),
            Row ("Reverse", _zinv)
        );

        Section (
            "Presets",
            Row ("Modifier", HBox (_xmod, _xmodP)),
            Row ("Steps", _xsteps),
            Row ("Sensitivity", _xforce),
            Row ("Align CPlane", _xcplane)
        );

        Section (
            "Advanced",
            Row ("Marker", marker),
            Row ("Debug", debug)
            #if DEBUG
            ,Row ("Show camera", showcam)
            #endif
        );

    }

    protected override void OnLoadComplete(EventArgs e)
    {
        base.OnLoadComplete(e);
        _InitControlTags ();
        _UpdateActive ();
        _UpdateActiveInPlanView ();
        _UpdateActiveShared ();
        _UpdateModifiers ();
    }

    void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Data.Active):
            _UpdateActive ();
            _UpdateActiveShared ();
            break;

        case nameof (Data.ActiveInPlanView):
            _UpdateActiveInPlanView ();
            _UpdateActiveShared ();
            break;

        default:
            _UpdateModifiers ();
            break;
        }
    }

    void _UpdateActive ()
    {
        var v = Data.Active;
        _pmod.Enabled = v;
        _rmod.Enabled = v;
        _zmod.Enabled = v;
        _xmod.Enabled = v;
    }

    void _UpdateActiveInPlanView ()
    {
        var v = Data.ActiveInPlanView;
        _pmodP.Enabled = v;
        _rmodP.Enabled = v;
        _zmodP.Enabled = v;
        _xmodP.Enabled = v;
    }

    void _UpdateActiveShared ()
    {
        var v = Data.Active || Data.ActiveInPlanView;
        _zinv.Enabled    = v;
        _zforce.Enabled  = v;
        _xmod.Enabled    = v;
        _xmodP.Enabled   = v;
        _xsteps.Enabled  = v;
        _xforce.Enabled  = v;
        _xcplane.Enabled = v;
    }

    void _InitControlTags ()
    {
        foreach (var (c, a) in PropertyToControls.Values)
        {
            c.Tag = c.BackgroundColor;
        }
    }

    void _UpdateModifiers ()
    {
        Data.Validate ();
        foreach (var (c, a) in PropertyToControls.Values)
        {
            c.BackgroundColor = a.Valid
                            ? (ED.Color)c.Tag
                            : ED.Colors.IndianRed;
        }
    }
}



#if false
class NavigationForm : EF.Form
{
    static ED.Size _spacing = new (8, 8);

    readonly NavigationOptions _data;
    readonly NavigationOptions _copy;
    readonly EF.EnumDropDown <NavigationModifier> _pmod;
    readonly EF.EnumDropDown <NavigationModifier> _rmod;
    readonly EF.EnumDropDown <NavigationModifier> _zmod;
    readonly EF.EnumDropDown <NavigationModifier> _xmod;

    bool _valid;
    readonly EF.Button _bok;

    public NavigationForm (NavigationOptions options)
    {
        Title = "Navigation settings";
        Owner = RUI.RhinoEtoApp.MainWindow;
        MovableByWindowBackground = true;
        Padding = new (8, 8);

        _valid = true;
        _copy  = options.Copy <NavigationOptions> ();
        _data  = options;
        _data.PropertyChanged += _OnDataChanged;

        DataContext = _data;
        
        var active = Ui.CheckBox (_data, nameof (_data.Active));

        _pmod = Ui.EnumDropDown <NavigationModifier> (_data, nameof (options.PanModifier));

        _rmod = Ui.EnumDropDown <NavigationModifier> (_data, nameof (options.RotateModifier));

        _zmod = Ui.EnumDropDown <NavigationModifier> (_data, nameof (options.ZoomModifier));
        var _zinv   = Ui.CheckBox (_data, nameof (_data.ReverseZoom));
        var _zforce = Ui.NumericStepper (_data, nameof (options.ZoomForce), min: 0.1, max: 10, decimalPlaces: 2);

        _xmod = Ui.EnumDropDown <NavigationModifier> (_data, nameof (options.PresetsModifier));
        var xsteps  = Ui.NumericStepper (_data, nameof (options.PresetSteps), min: 2, max: 360, decimalPlaces: 0 );
        // var xinplan = Ui.CheckBox (_data, nameof (_data.PresetsInPlanView));
        var xforce  = Ui.NumericStepper (_data, nameof (options.PresetForce), min: 0.1, max: 4, decimalPlaces: 2 );
        var xcplane = Ui.CheckBox (_data, nameof (_data.PresetsAlignCPlane));

        //

        var bcancel = new EF.Button { Text = "Cancel" };
        bcancel.Click += delegate { _data.Apply (_copy); Close (); };

        _bok = new EF.Button { Text = "Ok" };
        _bok.Click    += delegate { if (!_valid) _data.Apply (_copy); Close (); };

        Content = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment = EF.VerticalAlignment.Stretch,
            Items = {
                new EF.StackLayoutItem (
                    _VScrollable (
                        _Section (
                            _Row ("Active", active, null)
                        ),
                        Ui.Divider ("Pan"),
                        _Section (
                            _Row ("Modifier", null, _pmod),
                            _Row ()
                        ),
                        Ui.Divider ("Rotation"),
                        _Section (
                            _Row ("Modifier", null, _rmod),
                            _Row ()
                        ),
                        Ui.Divider ("Zoom"),
                        _Section (
                            _Row ("Modifier", null, _zmod),
                            _Row ("Force", null, _zforce),
                            _Row ("Reverse", null, _zinv),
                            _Row ()
                        ),
                        Ui.Divider ("Presets"),
                        _Section (
                            _Row ("Modifier", null, _xmod),
                            _Row ("Steps", null, xsteps),
                            _Row ("Sensitivity", null, xforce),
                            // _Row ("Plan view", null, xinplan),
                            _Row ("Align CPlane", null, xcplane),
                            _Row ()
                        ),
                        _CreateAdvancedOptions (),
                        null
                    ),
                    expand: true
                ),
                new EF.StackLayout {
                    Orientation = EF.Orientation.Horizontal,
                    Spacing = _spacing.Width,
                    Items = { _bok, null, bcancel }
                }
            }
        };

        static EF.TableLayout _Section (params EF.TableRow[] rows)
        {
            return new EF.TableLayout (rows) {
                Spacing = _spacing
            };
        }

        static EF.TableRow _Row (params EF.TableCell?[] cells)
        {
            var row =  new EF.TableRow (cells);
            row.Cells.Insert (0, new () { ScaleWidth = false });
            return row;
        }

        static EF.Scrollable _VScrollable (params EF.Control?[] controls)
        {
            var stack = new EF.StackLayout
            {
                Orientation = EF.Orientation.Vertical,
                HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
                Spacing = _spacing.Height,
            };
            foreach (var c in controls) stack.Items.Add (c);
            return new EF.Scrollable {
                Border = EF.BorderType.None,
                Content = stack
            };
        }

        EF.Expander _CreateAdvancedOptions ()
        {
            var marker  = Ui.CheckBox (_data, nameof (_data.Marker));
            var debug   = Ui.CheckBox (_data, nameof (_data.Debug));
            var showcam = Ui.CheckBox (_data, nameof (_data.ShowCamera));

            return Ui.Expander (
                "Advanced",
                _Section (
                    _Row ("Marker", marker, null),
                    _Row ("Debug", debug, null),
                    _Row ("Show camera", showcam, null)
                )
            );
        }

    }

    void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        _valid = true;

        if (_Test (_data.PanModifier, _data.RotateModifier, _data.ZoomModifier, _data.PresetsModifier))
            _pmod.BackgroundColor = ED.Colors.Transparent;
        else {
            _valid = false;
            _pmod.BackgroundColor = ED.Colors.Red;
        }

        if (_Test (_data.ZoomModifier, _data.RotateModifier, _data.PanModifier, _data.PresetsModifier))
            _zmod.BackgroundColor = ED.Colors.Transparent;
        else {
            _valid = false;
            _zmod.BackgroundColor = ED.Colors.Red;
        }

        if (_Test (_data.RotateModifier, _data.PanModifier, _data.ZoomModifier, _data.PresetsModifier))
            _rmod.BackgroundColor = ED.Colors.Transparent;
        else {
            _valid = false;
            _rmod.BackgroundColor = ED.Colors.Red;
        }

        if (_Test (_data.PresetsModifier, _data.RotateModifier, _data.ZoomModifier, _data.PanModifier))
            _xmod.BackgroundColor = ED.Colors.Transparent;
        else {
            _valid = false;
            _xmod.BackgroundColor = ED.Colors.Red;
        }

        _bok.Enabled = _valid;

        static bool _Test <T> (T val, params T[] values)
        {
            foreach (var v in values) if (object.Equals (val, v)) return false;
            return true;
        }
    }

    #if RHP // EtoExtensions.LocalizeAndRestore requires a plugin

    protected override void OnLoadComplete (EventArgs e)
    {
        base.OnLoadComplete(e);
		RUI.EtoExtensions.LocalizeAndRestore (this);
    }

    #else

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var screen = base.Screen ?? EF.Screen.PrimaryScreen;
        if (screen != null)
        {
            var bounds = screen.Bounds;
            base.Location = new ED.Point(
                (int)(bounds.Width * 0.5 - Width * 0.5),
                (int)(bounds.Height * 0.5 - Height * 0.5)
            );
        }
    }

    #endif
}
#endif