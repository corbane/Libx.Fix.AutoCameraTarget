/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;
using System.Linq;
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


interface IOptions : INotifyPropertyChanged
{
    public int DataVersion { get; }
}


interface INavigationOptions : IOptions
{
    public bool Debug { get; }
    public bool Marker { get; }
    public bool ShowCamera { get; }

    public ModifierKey PanModifier { get; }

    public ModifierKey RotateModifier { get; }

    public ModifierKey ZoomModifier { get; }
    public double ZoomForce { get; }
    public bool ReverseZoom { get; }

    public ModifierKey PresetsModifier { get; }
    public bool ParallelPresets { get; }
    public int PresetSteps { get; }
    public double PresetForce { get; }
    public bool PresetsInPlanView { get; }
    public bool PresetsAlignCPlane { get; }
}


interface IIntersectionOptions : IOptions
{
    public bool Marker { get; }
    public bool Debug { get; }
}


class NavigationOptions : IIntersectionOptions, INavigationOptions
{
    bool _active;
    public bool Active { get => _active; set { _Set (ref _active, value); } }

    bool _marker;
    public bool Marker { get => _marker; set { _Set (ref _marker, value); } }


    #region Visual Debug

    bool _debug;
    public bool Debug { get => _debug;  set { _Set (ref _debug, value);  } }

    bool _showcam;
    public bool ShowCamera { get => _showcam;  set { _Set (ref _showcam, value);  } }

    #endregion


    // TODO:
    // ??? Pan plan parallel views with Ctrl+Shift+RMB ???
    // ??? Auto adjust camera target after Pan and Zoom ???
    // ??? Gumbal Rotate view around Gumball ???
    public bool AlwaysPanParallelViews => RhinoViewSettings.AlwaysPanParallelViews;


    #region Pan

    ModifierKey _pmod = ModifierKey.Shift;
    public ModifierKey PanModifier { get => _pmod;  set { _Set (ref _pmod, value);  } }

    #endregion


    #region Turn

    ModifierKey _rmod = ModifierKey.None;
    public ModifierKey RotateModifier { get => _rmod;  set { _Set (ref _rmod, value);  } }

    #endregion


    #region Zoom

    ModifierKey _zmod = ModifierKey.Ctrl;
    public ModifierKey ZoomModifier { get => _zmod;  set { _Set (ref _zmod, value);  } }

    // ??? Zoom > Reverse action ???
    bool _zinv;
    public bool ReverseZoom { get => _zinv;  set { _Set (ref _zinv, value);  } }

    double _zforce = 4;
    public double ZoomForce { get => _zforce;  set { _Set (ref _zforce, value);  } }

    #endregion


    #region Presets

    ModifierKey _xmod = ModifierKey.Alt;
    public ModifierKey PresetsModifier { get => _xmod;  set { _Set (ref _xmod, value);  } }

    // TODO:
    bool _sparallel = true;
    public bool ParallelPresets { get => _sparallel;  set { _Set (ref _sparallel, value);  } }

    int _sangle = 4;
    public int PresetSteps { get => _sangle;  set { _Set (ref _sangle, value);  } }

    double _sforce = 1;
    public double PresetForce { get => _sforce;  set { _Set (ref _sforce, value);  } }

    bool _sinplan = false;
    public bool PresetsInPlanView { get => _sinplan;  set { _Set (ref _sinplan, value);  } }

    bool _scplane = false;
    public bool PresetsAlignCPlane { get => _scplane;  set { _Set (ref _scplane, value);  } }

    #endregion


    #region Event

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion


    #region Helpers

    class ExcludeAttribute : Attribute { }

    [Exclude]
    public int DataVersion { get; private set; }

    void _Set <T> (ref T member, T value, [CallerMemberName] string? propertyName = null)
    {
        if (object.Equals (member, value)) return;
        member = value;
        DataVersion++;
        Emit (propertyName);
    }

    public NavigationOptions Copy ()
    {
        var data = new NavigationOptions ();

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <ExcludeAttribute> () == null
                    select p;

        foreach (var p in props)
            p.SetValue (data, p.GetValue (this));

        return data;
    }

    public void Apply (NavigationOptions data)
    {
        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <ExcludeAttribute> () == null
                    select p;

        foreach (var p in props)
            p.SetValue (this, p.GetValue (data));
    }
    
    public void Save (RH.PersistentSettings settings)
    {
        var t_bool   = typeof (bool);
        var t_int    = typeof (int);
        var t_double = typeof (double);
        var t_modkey = typeof (ModifierKey);

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <ExcludeAttribute> () == null
                    select p;

        foreach (var p in props)
        {
            var t = p.PropertyType;

            if (t == t_modkey)
                settings.SetEnumValue <ModifierKey> (p.Name, (ModifierKey)p.GetValue (this));

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
        var t_modkey = typeof (ModifierKey);

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <ExcludeAttribute> () == null
                    select p;

        var keys = settings.Keys;
        foreach (var p in props)
        {
            if (keys.Contains (p.Name) == false) continue;

            var t = p.PropertyType;

            if (t == t_modkey)
                p.SetValue (this, settings.GetEnumValue <ModifierKey> (p.Name));

            else if (t == t_bool)
                p.SetValue (this, settings.GetBool (p.Name));
            
            else if (t == t_int)
                p.SetValue (this, settings.GetInteger (p.Name));
            
            else if (t == t_double)
                p.SetValue (this, settings.GetDouble (p.Name));
        }
    }

    #endregion
}


class NavigationForm : EF.Form
{
    static ED.Size _spacing = new (8, 8);

    readonly NavigationOptions _data;
    readonly NavigationOptions _copy;
    EF.EnumDropDown <ModifierKey> _pmod;
    EF.EnumDropDown <ModifierKey> _rmod;
    EF.EnumDropDown <ModifierKey> _zmod;
    EF.EnumDropDown <ModifierKey> _xmod;

    bool _valid;
    EF.Button _bok;

    public NavigationForm (NavigationOptions options)
    {
        Title = "Navigation settings";
        Owner = RUI.RhinoEtoApp.MainWindow;
        MovableByWindowBackground = true;
        Padding = new (8, 8);

        _valid = true;
        _copy  = options.Copy ();
        _data  = options;
        _data.PropertyChanged += _OnDataChanged;

        DataContext = _data;
        

        var active = new EF.CheckBox ();
        active.CheckedBinding.BindDataContext (nameof (_data.Active));

        // Pan

        _pmod = new ();
        _pmod.SelectedValueBinding.BindDataContext (nameof (options.PanModifier));

        // Turn

        _rmod = new ();
        _rmod.SelectedValueBinding.BindDataContext (nameof (options.RotateModifier));
        
        // Zoom

        _zmod = new ();
        _zmod.SelectedValueBinding.BindDataContext (nameof (options.ZoomModifier));

        var _zinv = new EF.CheckBox ();
        _zinv.CheckedBinding.BindDataContext (nameof (options.ReverseZoom));
        
        var _zforce = new EF.NumericStepper { MinValue = 0.1, MaxValue = 4, Increment = 0.01, DecimalPlaces = 2 };
        _zforce.ValueBinding.BindDataContext (nameof (options.ZoomForce));

        // Presets

        _xmod = new ();
        _xmod.SelectedValueBinding.BindDataContext (nameof (options.PresetsModifier));

        var xsteps = new EF.NumericStepper { MinValue = 2 };
        xsteps.ValueBinding.BindDataContext (nameof (options.PresetSteps));

        var xinplan = new EF.CheckBox ();
        xinplan.CheckedBinding.BindDataContext (nameof (options.PresetsInPlanView));

        var xforce = new EF.NumericStepper { MinValue = 0.1, MaxValue = 4, Increment = 0.01, DecimalPlaces = 2 };
        xforce.ValueBinding.BindDataContext (nameof (options.PresetForce));

        var xcplane = new EF.CheckBox ();
        xcplane.CheckedBinding.BindDataContext (nameof (options.PresetsAlignCPlane));

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
                        _Divider ("Pan"),
                        _Section (
                            _Row ("Modifier", _pmod, null),
                            _Row ()
                        ),
                        _Divider ("Rotation"),
                        _Section (
                            _Row ("Modifier", _rmod, null),
                            _Row ()
                        ),
                        _Divider ("Zoom"),
                        _Section (
                            _Row ("Modifier", _zmod, null),
                            _Row ("Force", _zforce, null),
                            _Row ("Reverse", _zinv, null),
                            _Row ()
                        ),
                        _Divider ("Presets"),
                        _Section (
                            _Row ("Modifier", _xmod, null),
                            _Row ("Steps", xsteps, null),
                            _Row ("Sensitivity", xforce, null),
                            _Row ("Plan view", xinplan, null),
                            _Row ("Align CPlane", xcplane, null),
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
                // ExpandContentHeight = true,
                // ExpandContentWidth = true,
                Content = stack
            };
        }

        static EF.Expander _CreateAdvancedOptions ()
        {
            var marker = new EF.CheckBox ();
            marker.CheckedBinding.BindDataContext (nameof (_data.Marker));

            var debug  = new EF.CheckBox ();
            debug.CheckedBinding.BindDataContext (nameof (_data.Debug));
            
            var showcam  = new EF.CheckBox ();
            showcam.CheckedBinding.BindDataContext (nameof (_data.ShowCamera));

            var expander = new EF.Expander {
                Header = _Divider ("Advanced"),
                Expanded = false,
                Content = _Section (
                    _Row ("Marker", marker, null),
                    _Row ("Debug", debug, null),
                    _Row ("Show camera", showcam, null)
                )
            };

            return expander;
        }

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

        static EF.StackLayout _Divider (string label)
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

    #if RHP // Requires a plugin

    protected override void OnLoadComplete(EventArgs e)
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
