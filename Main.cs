#define WIN32
/*/
    I could have made this plugin portable on MacOS:
    - If it was possible to make the command line not intercept keyboard input.
    - if I could with API hide/show mouse cursor in a view.
/*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using SD = System.Drawing;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RH = Rhino;
using ON = Rhino.Geometry;
using RP = Rhino.PlugIns;
using RC = Rhino.Commands;
using RD = Rhino.Display;
using RO = Rhino.DocObjects;
using RI = Rhino.Input.Custom;
using RR = Rhino.Runtime;
using RUI = Rhino.UI;
using RhinoDoc = Rhino.RhinoDoc;
using RhinoApp = Rhino.RhinoApp;
using RhinoViewSettings = Rhino.ApplicationSettings.ViewSettings;
using System.Security.Cryptography;


/*/

███    ███  █████  ██ ███    ██ 
████  ████ ██   ██ ██ ████   ██ 
██ ████ ██ ███████ ██ ██ ██  ██ 
██  ██  ██ ██   ██ ██ ██  ██ ██ 
██      ██ ██   ██ ██ ██   ████ 
 
/*/


#if RHP


[assembly: System.Runtime.InteropServices.Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: RP.PlugInDescription (RP.DescriptionType.Country, "France")]
[assembly: RP.PlugInDescription (RP.DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: RP.PlugInDescription (RP.DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]
[assembly: RP.PlugInDescription (RP.DescriptionType.Icon, "Libx.Fix.AutoCameraTarget.ico.RotateArround.ico")]


namespace Libx.Fix.AutoCameraTarget;


public class EntryPoint : RP.PlugIn
{
    public override RP.PlugInLoadTime LoadTime => RP.PlugInLoadTime.AtStartup; // for Intersector.AttachEvents ()

    protected override RP.LoadReturnCode OnLoad (ref string errorMessage)
    {
        Intersector.AttachEvents ();
        Main.Instance.LoadOptions (Settings);
        return base.OnLoad (ref errorMessage);
    }

    protected override void OnShutdown ()
    {
        Main.Instance.SaveOptions (Settings);
        base.OnShutdown ();
    }
}


public class AutoCameraTargetCommand : RC.Command
{
    public static string Name => "ToggleAutoCameraTarget";
    public override string EnglishName => Name;

    protected override RC.Result RunCommand (RhinoDoc doc, RC.RunMode mode)
    {
        return Main.Instance.RunToggleCommand (doc);
    }

}


#endif // RHP


class Main
{
    static Main? g_instance;
    public static Main Instance => g_instance ??= new ();

    public NavigationOptions Options;

    readonly RMBListener _mouse;
    readonly CameraController _camera;
    readonly IntersectionData _data;

    Main ()
    {
        Options = new NavigationOptions ();
        _data = new (Options);
        _camera = new (Options, _data);
        _mouse = new (_camera);

        Options.PropertyChanged += _OnSettingsChanged;
    }

    public RC.Result RunToggleCommand (RhinoDoc doc)
    {
        var go = new RI.GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");

        var active  = new RI.OptionToggle (false, "No", "Yes") { CurrentValue = Options.Active };
        var options = new RI.OptionToggle (false, "No", "Yes") { CurrentValue = false };

        for (;;)
        {
            go.ClearCommandOptions ();
            go.AddOptionToggle ("active", ref active);
            go.AddOptionToggle ("settings", ref options);

            var ret = go.Get ();
            if (ret == Rhino.Input.GetResult.Option)
            {
                if (options.CurrentValue) {
                    ShowOptions ();
                    return RC.Result.Success;
                }
                else {
                    Options.Active = active.CurrentValue;
                    return RC.Result.Success;
                }
            }

            return ret == Rhino.Input.GetResult.Cancel
                 ? RC.Result.Cancel
                 : RC.Result.Success;

        }
    }

    public void ShowOptions () { new NavigationForm (Options).Show (); }

    public void LoadOptions (RH.PersistentSettings settings) { Options.Load (settings); }

    public void SaveOptions (RH.PersistentSettings settings) { Options.Save (settings); }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Options.Active):
            _mouse.Enabled = Options.Active;
            break;
        }
    }
}


/*/

 ██████  ██████  ████████ ██  ██████  ███    ██ ███████ 
██    ██ ██   ██    ██    ██ ██    ██ ████   ██ ██      
██    ██ ██████     ██    ██ ██    ██ ██ ██  ██ ███████ 
██    ██ ██         ██    ██ ██    ██ ██  ██ ██      ██ 
 ██████  ██         ██    ██  ██████  ██   ████ ███████ 

/*/


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


/*/

██ ███    ██ ████████ ███████ ██████  ███████ ███████  ██████ ████████ ██  ██████  ███    ██ 
██ ████   ██    ██    ██      ██   ██ ██      ██      ██         ██    ██ ██    ██ ████   ██ 
██ ██ ██  ██    ██    █████   ██████  ███████ █████   ██         ██    ██ ██    ██ ██ ██  ██ 
██ ██  ██ ██    ██    ██      ██   ██      ██ ██      ██         ██    ██ ██    ██ ██  ██ ██ 
██ ██   ████    ██    ███████ ██   ██ ███████ ███████  ██████    ██    ██  ██████  ██   ████ 

/*/


enum IntersectionStatus
{
    /// <summary>
    /// There is nothing visible in the viewport.
    /// </summary>
    None,

    /// <summary>
    /// The line is outside any visible element and its bounding boxes.
    /// </summary>
    Outside,

    /// <summary>
    /// There is an intersection with a mesh.
    /// </summary>
    OnMesh,

    /// <summary>
    /// There is an intersection with a bounding box.
    /// </summary>
    OnBBox,

    /// <summary>
    /// There is an intersection with the bounding box of all visible elements.
    /// </summary>
    OnVisibleBBox,
}


class IntersectionData
{
    #nullable disable
    public RD.RhinoViewport Viewport;
    #nullable enable

    public SD.Point ViewportPoint;

    public IIntersectionOptions Options { get; }

    public IntersectionData (IIntersectionOptions options)
    {
        Options = options;
    }

    /*/ Computed properties /*/


    /// <summary>
    /// Type of intersection found.
    /// </summary>
    public IntersectionStatus Status;

    /// <summary>
    /// The line under the mouse pointer to the far plane Frustum
    /// </summary>
    public ON.Ray3d Rayline;

    /// <summary>
    /// Plan in front of the camera at the center of VisibleBBox.
    /// </summary>
    public ON.Plane FrustumFrontPlane;


    /// <summary>
    /// Number of objects under the mouse.
    /// </summary>
    public uint ObjectCount;

    #if DEBUG
    public int Complexity;
    #endif

    /// <summary>
    /// The bounding box of the element closest to the camera.
    /// </summary>
    public ON.BoundingBox ActiveBBox;

    /// <summary>
    /// Box containing the objects visible in the viewport.
    /// </summary>
    public ON.BoundingBox VisibleBBox;

    public ON.BoundingBox InfoBBox;

    /// <summary>
    /// The target point of the camera
    /// </summary>
    public ON.Point3d TargetPoint;
}


static class Intersector
{
    #region Cache
    /*/
        Event stack:

        - _OnCloseDocument or _OnNewDocument

        - then per file in worksession:
          - _OnBeginOpenDocument
          - _OnAddRhinoObject*...
          - _OnEndOpenDocument
          - _OnEndOpenDocumentInitialViewUpdate
          - _OnActiveDocumentChanged or not

        !!! Except _OnNewDocument, no event is called when the application starts without opening an existing file. !!!
    /*/

    static Dictionary<Guid, (RO.RhinoObject Obj, ON.BoundingBox BBox)> _bboxcache = new ();

    public static void AttachEvents ()
    {
        RhinoDoc.CloseDocument                    += _OnCloseDocument;
        RhinoDoc.NewDocument                      += _OnNewDocument;
        RhinoDoc.AddRhinoObject                   += _OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject                += _OnDeleteRhinoObject;
    }

    private static void _OnCloseDocument (object sender, RH.DocumentEventArgs e)
    {
        RhinoApp.WriteLine (nameof (_OnCloseDocument));
        _bboxcache.Clear ();
    }

    private static void _OnNewDocument (object sender, RH.DocumentEventArgs e)
    {
        RhinoApp.WriteLine (nameof (_OnNewDocument));
        _bboxcache.Clear ();
    }

    static void _OnDeleteRhinoObject (object sender, RO.RhinoObjectEventArgs e)
    {
        // RhinoApp.WriteLine (nameof (_OnDeleteRhinoObject));
        _bboxcache.Remove (e.ObjectId);
    }

    static void _OnAddRhinoObject (object sender, RO.RhinoObjectEventArgs e)
    {
        // RhinoApp.WriteLine (nameof (_OnAddRhinoObject));
        // var bbox = e.TheObject.Geometry.GetBoundingBox (accurate: false);
        // if (bbox.IsValid == false) return;
        _bboxcache[e.ObjectId] = (
            e.TheObject,
            e.TheObject.Geometry.GetBoundingBox (accurate: false)
        );
    }

    #endregion

    readonly static List<ON.Mesh> _usedMeshes = new ();

    static ON.Ray3d _GetMouseRay (RD.RhinoViewport vp)
    {
        var mp = RUI.MouseCursor.Location;
        var p = vp.ScreenToClient (new SD.Point ((int)mp.X, (int)mp.Y));
		vp.GetScreenPort(out var pl, out var _, out var _, out var pt, out var _, out var _);
        vp.GetFrustumLine (p.X - pl, p.Y - pt, out var line);

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new ON.Ray3d (line.To, line.From - line.To);
    }
    
    public static void Compute (IntersectionData data, ON.Point3d? defaultTargetPoint = null)
    {
        var ray = _GetMouseRay (data.Viewport);
        data.Rayline = ray;
        var rayPos = new double[]
        {
            ray.Position.X,
            ray.Position.Y,
            ray.Position.Z
        };
        var rayInvDir = new double[]
        {
            ray.Direction.X == 0 ? double.NegativeInfinity : 1/ray.Direction.X,
            ray.Direction.Y == 0 ? double.NegativeInfinity : 1/ray.Direction.Y,
            ray.Direction.Z == 0 ? double.NegativeInfinity : 1/ray.Direction.Z
        };

        uint total = 0;
        ON.BoundingBox activebbox = ON.BoundingBox.Unset;
        ON.BoundingBox visiblebbox = ON.BoundingBox.Unset;
        double t;
        double tbboxmin = 1.1;
        var arg = new RO.RhinoObject[1];

        #if DEBUG
        if (data.Viewport.ParentView.Document.Objects.Count != _bboxcache.Count)
            RhinoApp.WriteLine (
                "Document.Objects.Count != _bboxcache.Count"+
                "\n  count: "+ data.Viewport.ParentView.Document.Objects.Count+
                "\n  cache: "+_bboxcache.Count
            );
        #endif

        if (data.Viewport.ParentView.Document.Objects.Count < _usedMeshes.Capacity)
            _usedMeshes.Capacity = data.Viewport.ParentView.Document.Objects.Count;

        foreach (var (obj, bbox) in _bboxcache.Values)
        {
            if (bbox.IsValid == false) continue;
        
            t = _RayBoxIntersection (rayPos, rayInvDir, bbox);
            if (t < 0)
            {
                if (data.Viewport.IsVisible (bbox))
                    visiblebbox.Union (bbox);
                continue;
            }
            // If its bounding box is closest to the camera.
            else if (t < tbboxmin)
            {
                tbboxmin = t;
                activebbox = bbox;
            }
            total++;
        
            // `obj.GetMeshes(MeshType.Default)` does not return meshes for block instances.
            // GetRenderMeshes has a different behavior with SubDs
            // https://discourse.mcneel.com/t/rhinoobject-getrendermeshes-bug/151953
            arg[0] = obj;
            _usedMeshes.AddRange (from oref in RO.RhinoObject.GetRenderMeshes (arg, true, false)
                                  let m = oref.Mesh ()
                                  where m != null
                                  select m);
        }

        data.ObjectCount = total;

        #if DEBUG
        data.Complexity = _usedMeshes.Sum (m => m.Faces.Count);
        #endif

        data.InfoBBox = new ON.BoundingBox (ray.Position, ray.Position + ray.Direction);
        if (visiblebbox.IsValid) data.InfoBBox.Union (visiblebbox);

        // test the intersections with the meshes.
        double tmin = double.MaxValue;
        System.Threading.Tasks.Parallel.For (0, _usedMeshes.Count, (int i) =>
        {
            var t = ON.Intersect.Intersection.MeshRay (_usedMeshes[i], ray);
            if (t > 0 && t < tmin) tmin = t; // Is it a good thing to define a shared variable here?
        });
        _usedMeshes.Clear ();

        // Has an intersection with a mesh been found ?
        if (tmin != double.MaxValue)
        {
            data.Status = IntersectionStatus.OnMesh;
            data.TargetPoint = ray.PointAt (tmin);
            return;
        }

        // Has an intersection with a bounding box been found ?
        if (activebbox.IsValid)
        {
            data.Status = IntersectionStatus.OnBBox;
            data.ActiveBBox = activebbox;
            t = _RayBoxIntersectionCenter (rayPos, rayInvDir, activebbox);
            data.TargetPoint = ray.PointAt (t); //(tbboxmin);
            return;
        }

        // Is there at least one object visible ?
        if (visiblebbox.IsValid == false)
        {
            data.Status = IntersectionStatus.None;
            data.TargetPoint = defaultTargetPoint ?? ON.Point3d.Unset;
            return;
        }

        // the ray line intersects the global bounding box ?
        t = _RayBoxIntersectionCenter (rayPos, rayInvDir, visiblebbox);
        if (t >= 0)
        {
            data.Status = IntersectionStatus.OnVisibleBBox;
            data.VisibleBBox = visiblebbox;
            data.TargetPoint = ray.PointAt (t);
            return;
        }

        // The mouse is outside any bounding boxes and there are objects visible on the screen.
        else
        {
            data.Viewport.GetFrustumFarPlane (out var plane);
            data.Status = IntersectionStatus.Outside;
            plane.Origin = visiblebbox.Center;
            data.FrustumFrontPlane = plane;
            data.VisibleBBox = visiblebbox;
            data.TargetPoint = ray.PointAt (intersectPlane (plane, ray));
            return;
        }

        static double intersectPlane (ON.Plane plane, ON.Ray3d ray)
        {
            return ON.Vector3d.Multiply (plane.Origin - ray.Position, plane.Normal) / ON.Vector3d.Multiply (plane.Normal, ray.Direction);
        }
    }

    #region Ray-AABB

    /// <summary>
    /// same as _RayBoxIntersection but returns the midpoint of the ray line trimmed by the bounding box
    /// </summary>
    static double _RayBoxIntersectionCenter (double[] r_origin, double[] r_dir_inv, ON.BoundingBox b)
    {
        double t;

        // X
        double t1 = (b.Min.X - r_origin[0]) * r_dir_inv[0];
        double t2 = (b.Max.X - r_origin[0]) * r_dir_inv[0];

        double tmin = t1 < t2 ? t1 : t2; // min (t1, t2);
        double tmax = t1 > t2 ? t1 : t2; // max (t1, t2);

        // Y
        t1 = (b.Min.Y - r_origin[1]) * r_dir_inv[1];
        t2 = (b.Max.Y - r_origin[1]) * r_dir_inv[1];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));

        // Z
        t1 = (b.Min.Z - r_origin[2]) * r_dir_inv[2];
        t2 = (b.Max.Z - r_origin[2]) * r_dir_inv[2];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));

        // tmin >  tmax  the ray is outside the box
        // tmin == tmax  the box is flat
        // tmin <  tmax  the ray is inside the box
        if (tmax < (tmin > 0 ? tmin : 0)) // tmax < max (tmin, 0.0);
            return -1;
        return (tmin + tmax) * 0.5;
    }

    /// <summary>
    /// Ray-AABB (Axis Aligned Bounding Box) intersection.
    /// <br/> <see href="https://tavianator.com/2011/ray_box.html"/>
    /// <br/> <see href="https://tavianator.com/2015/ray_box_nan.html"/>
    /// </summary>
    static double _RayBoxIntersection (double[] r_origin, double[] r_dir_inv, ON.BoundingBox b)
    {
        double t;

        // X
        double t1 = (b.Min.X - r_origin[0]) * r_dir_inv[0];
        double t2 = (b.Max.X - r_origin[0]) * r_dir_inv[0];

        double tmin = t1 < t2 ? t1 : t2; // min (t1, t2);
        double tmax = t1 > t2 ? t1 : t2; // max (t1, t2);

        // Y
        t1 = (b.Min.Y - r_origin[1]) * r_dir_inv[1];
        t2 = (b.Max.Y - r_origin[1]) * r_dir_inv[1];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));

        // Z
        t1 = (b.Min.Z - r_origin[2]) * r_dir_inv[2];
        t2 = (b.Max.Z - r_origin[2]) * r_dir_inv[2];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));

        // tmin >  tmax  the ray is outside the box
        // tmin == tmax  the box is flat
        // tmin <  tmax  the ray is inside the box
        if (tmax < (tmin > 0 ? tmin : 0)) // tmax < max (tmin, 0.0);
            return -1;
        return tmin < 0 ? tmax : tmin;
    }

    #endregion

    #region Infos / Debug

    static System.Diagnostics.Stopwatch? _sw;

    public static void StartPerformenceLog ()
    {
        _sw ??= new ();
        _sw.Restart ();
    }

    public static void StopPerformenceLog (IntersectionData? data)
    {
        if (_sw != null) _sw.Stop ();
        if (data == null) return;

        #if DEBUG
        RhinoApp.WriteLine ("Get Point " + _sw!.ElapsedMilliseconds + "ms for " + data.ObjectCount + " object(s). complexity: " + data.Complexity);
        #else
        RhinoApp.WriteLine ("Get Point " + _sw!.ElapsedMilliseconds + "ms for " + data.ObjectCount + " object(s).");
        #endif
    }

    #endregion
}


class IntersectionConduit : RD.DisplayConduit
{
    static IntersectionConduit? g_instance;

    public static void Show (IntersectionData data)
    {
        g_instance ??= new (data);
        g_instance.Enabled = true;
    }

    public static void Hide ()
    {
        if (g_instance != null)
            g_instance.Enabled = false;
    }

    readonly IntersectionData _data;

    IntersectionConduit (IntersectionData data)
    {
        _data = data;
        SpaceFilter = RO.ActiveSpace.ModelSpace;
    }

    protected override void CalculateBoundingBox (RD.CalculateBoundingBoxEventArgs e)
    {
        e.BoundingBox.Union (_data.InfoBBox);
    }

    protected override void DrawForeground (RD.DrawEventArgs e)
    {
        if (e.Viewport.Id != _data.Viewport.Id) return;

        var G = e.Display;

        G.DrawPoint (_data.TargetPoint, RD.PointStyle.RoundActivePoint, 3, SD.Color.Black);

        if (_data.Options.Debug == false) return;

        G.DrawLine (
            _data.Rayline.Position,
            _data.Rayline.Position + _data.Rayline.Direction,
            SD.Color.BlueViolet
        );

        switch (_data.Status)
        {
        case IntersectionStatus.Outside:

            G.DrawBox (_data.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            G.DrawConstructionPlane (new Rhino.DocObjects.ConstructionPlane
            {
                Plane = _data.FrustumFrontPlane,
                ShowGrid = false
            });
            break;

        case IntersectionStatus.OnBBox:

            G.DrawBox (_data.ActiveBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;

        case IntersectionStatus.OnVisibleBBox:

            G.DrawBox (_data.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;
        }
    }
}


/*/

██   ██ ███████ ██    ██ ██████   ██████   █████  ██████  ██████  
██  ██  ██       ██  ██  ██   ██ ██    ██ ██   ██ ██   ██ ██   ██ 
█████   █████     ████   ██████  ██    ██ ███████ ██████  ██   ██ 
██  ██  ██         ██    ██   ██ ██    ██ ██   ██ ██   ██ ██   ██ 
██   ██ ███████    ██    ██████   ██████  ██   ██ ██   ██ ██████  

/*/


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


/*/

 ██████ ██    ██ ██████  ███████  ██████  ██████  
██      ██    ██ ██   ██ ██      ██    ██ ██   ██ 
██      ██    ██ ██████  ███████ ██    ██ ██████  
██      ██    ██ ██   ██      ██ ██    ██ ██   ██ 
 ██████  ██████  ██   ██ ███████  ██████  ██   ██ 

/*/


static class Cursor
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
    /// Viewport point when the mouse button is down.
    /// </summary>
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


enum VirtualCursorIcon { Hand, Glass, Pivot, None }


class VirtualCursor : RD.DisplayConduit
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

    // DrawForeground ne dessine pas au dessus des objets sélectionnés et du Gumball.
    protected override void DrawForeground(RD.DrawEventArgs e)
    {
        var pos = Position;
        switch (Icon)
        {
        case VirtualCursorIcon.Glass : e.Display.DrawBitmap (_zIco, pos.X-10, pos.Y-10); break;
        case VirtualCursorIcon.Hand  : e.Display.DrawBitmap (_tIco, pos.X-10, pos.Y-10); break;
        case VirtualCursorIcon.Pivot : e.Display.DrawBitmap (_rIco, pos.X-10, pos.Y-10); break;
        }
    }
}


/*/

███    ██  █████  ██    ██ ██  ██████   █████  ████████ ██  ██████  ███    ██ 
████   ██ ██   ██ ██    ██ ██ ██       ██   ██    ██    ██ ██    ██ ████   ██ 
██ ██  ██ ███████ ██    ██ ██ ██   ███ ███████    ██    ██ ██    ██ ██ ██  ██ 
██  ██ ██ ██   ██  ██  ██  ██ ██    ██ ██   ██    ██    ██ ██    ██ ██  ██ ██ 
██   ████ ██   ██   ████   ██  ██████  ██   ██    ██    ██  ██████  ██   ████ 

/*/


/// <summary>
///     Wrapper class above <see cref="NavigationListener"/> </summary>
class RMBListener : RUI.MouseCallback
{
    NavigationListener _listener;

    public RMBListener (NavigationListener listener)
    {
        _listener = listener;
    }

    protected override void OnEndMouseDown (RUI.MouseCallbackEventArgs e)
    {
        if (_listener.CanRun (e))
            _listener.Start (e);
    }
}


class NavigationListener : RUI.MouseCallback
{
    const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;
    const int PAUSE_DELAY = 70;


    public INavigationOptions Options { get; private set; }
    public RD.RhinoViewport Viewport { get; private set; }


    #nullable disable // Viewport
    public NavigationListener (INavigationOptions options)
    {
        Options = options;
    } 
    #nullable enable



    #region Switches

    /// <summary>
    ///     Flag to cancel or not the MouseUp event.</summary>
    bool _started;
    
    /// <summary>
    ///     Flag blocking `OnMouseMove` event after `EF.Mouse.Position=...`</summary>
    bool _lock;

    bool _pause;

    [MethodImpl(INLINE)]
    void _StartPause ()
    {
        _pause = true;
        System.Threading.Tasks.Task.Delay (PAUSE_DELAY).ContinueWith ((_) => { _pause = false; });
    }

    #endregion


    #region Offset

    ED.Point _offset;

    [MethodImpl(INLINE)]
    ED.Point _GetOffset (SD.Point point)
    {
        _offset.X = point.X - Cursor.InitialCursorPosition.X;
        _offset.Y = point.Y - Cursor.InitialCursorPosition.Y;
        return _offset;
    }

    #endregion


    #region Modifiers

    readonly Action <ED.Point>?[] _actions = new Action <ED.Point> [Enum.GetNames (typeof(ModifierKey)).Length];
    readonly object?[] _tags = new object [Enum.GetNames (typeof(ModifierKey)).Length];

    /// <summary>
    ///     Define a callback function when moving the mouse.</summary>
    /// <param name="modifier">
    ///     One of the keys from <see cref="ModifierKey"/>
    ///     or <see cref="ModifierKey.None"/> if no modifier is required for this action.</param>
    /// <param name="action">
    ///     The action to execute.</param>
    /// <param name="tag">
    ///     Value sent to `OnActionChange` when modifier key and action change.</param>
    public void SetModifierCallback (ModifierKey modifier, Action <ED.Point>? action, object? tag)
    {
        _actions[(int)modifier] = action;
        _tags[(int)modifier] = tag;
    }

    [MethodImpl(INLINE)] Action <ED.Point>? _GetAction (ModifierKey modifier)
    {
        return _actions[(int)modifier] ?? _actions[(int)ModifierKey.None];
    }

    [MethodImpl(INLINE)] object? _GetActionTag (ModifierKey modifier)
    {
        return _tags[(int)modifier] ?? _tags[(int)ModifierKey.None];
    }

    ModifierKey _cmodifier;

    public ModifierKey ActiveModifier => _actions[(int)_cmodifier] != null ? _cmodifier : ModifierKey.None;

    [MethodImpl(INLINE)] void _SetActiveModifier (ModifierKey modifier)
    {
        _cmodifier = modifier;
    }

    [MethodImpl(INLINE)] ModifierKey _GetActiveModifier ()
    {
        return _cmodifier;
    }

    #endregion


    public virtual bool CanRun (RUI.MouseCallbackEventArgs e) { return false; }

    protected virtual bool OnStartNavigation (RD.RhinoViewport viewport, SD.Point viewportPoint) { return true; }

    /// <summary>
    ///     Function called when the modifier key and its associated action change.
    ///     see <seealso cref="SetModifierCallback"/> </summary>
    /// <param name="previousTag">
    ///     Previously active action tag</param>
    /// <param name="currentTag">
    ///     New active action tag</param>
    protected virtual void OnActionChange (object? previousTag, object? currentTag) { /**/ }

    protected virtual void OnStopNavigation () { /**/ }


    internal void Start (RUI.MouseCallbackEventArgs e)
    {
        Viewport   = e.View.ActiveViewport;
        _started   = false;
        _lock      = false;
        Keyboard.MemorizeCapsLock ();
        Enabled    = true;
    }
    
    protected override void OnMouseMove (RUI.MouseCallbackEventArgs e)
    {
        // Attend que la souris se déplace d'au moins un pixel
        // pour ne pas annuler l'affichage du menu contextuel.
        if (_started == false) {
            _started = true;
                
            Enabled = OnStartNavigation (Viewport, e.ViewportPoint);
            if (Enabled) e.Cancel = true;
            else return;

            var mod = Keyboard.GetCurrentModifier ();

            // !!! Ne devrais pas être e.ViewportPoint mais IntersectionData.ViewportPoint,
            //     car il peut y avoir un décalage entre le point d'intersection et le curseur virtuel.
            //     Mais les décalages sont minimes et visibles surtout en mode débogage.
            // !!!
            Cursor.InitCursor (Viewport, e.ViewportPoint);
            Cursor.HideCursor ();

            _SetActiveModifier (mod);
            OnActionChange (null, _GetActionTag (mod));

            return;
        }

        // Cette fonction est-elle appelée après le repositionnement du curseur ?
        if (_lock) {
            e.Cancel = true;
            _lock = false;
            return;
        }

        // Y a-t-il un temps de pause entre le changement d'actions ?
        if (_pause) {
            e.Cancel = true;
            _lock = true;
            Cursor.SetCursorPosition (Cursor.InitialCursorPosition);
            return;
        }

        var offset = _GetOffset (e.ViewportPoint);

        // Y a-t'il quelque chose à déplacer ?
        if (offset.X == 0 && offset.Y == 0) {
            e.Cancel = true;
            return;
        }

        // Y a-t'il un changement d'action ?
        var amodifier = _GetActiveModifier ();
        var cmodifier = Keyboard.GetCurrentModifier ();
        if (amodifier != cmodifier)
        {
            OnActionChange (_GetActionTag(amodifier), _GetActionTag(cmodifier));
            _SetActiveModifier (cmodifier);
            _StartPause ();
        }

        // Y a-t'il une action à effectuer ?
        var action = _GetAction (cmodifier);
        if (action != null) {
            e.Cancel = true;
            action (offset);
        }
    
        // e.Cancel = true; ??? pourquoi j'ai supprimé l'annulation ???
        _lock = true;
        Cursor.SetCursorPosition (Cursor.InitialCursorPosition);
    }

    protected override void OnMouseUp (RUI.MouseCallbackEventArgs e)
    {
        Keyboard.RestoreCapsLock ();
        Enabled = false;
        
        if (_started)
        {
            OnActionChange (_GetActionTag (_GetActiveModifier ()), null);

            e.Cancel = true;
            var pos = VirtualCursor.Position;
            Cursor.SetLimitedCursorPosition (pos.X, pos.Y);
            OnStopNavigation ();
        }
        
        Cursor.ShowCursor ();
    }
}


/*/

 ██████  █████  ███    ███ ███████ ██████   █████  
██      ██   ██ ████  ████ ██      ██   ██ ██   ██ 
██      ███████ ██ ████ ██ █████   ██████  ███████ 
██      ██   ██ ██  ██  ██ ██      ██   ██ ██   ██ 
 ██████ ██   ██ ██      ██ ███████ ██   ██ ██   ██ 

/*/


class Camera
{
    #nullable disable
    RD.RhinoViewport _vp;
    RO.ViewportInfo _vpinfo;
    #nullable enable

    ON.Transform _m = ON.Transform.Identity;
    ON.Interval _initialSizeX;
    ON.Interval _initialSizeY;

    ON.Point3d _target;

    /// <summary> Offset along camera X axis. </summary>
    public double PanX;
    /// <summary> Offset along camera Y (Up) axis </summary>
    public double PanY;
    /// <summary> Offset along camera Z (Direction) axis </summary>
    public double PanZ;

    /// <summary> Rotation of the camera around the X axis.
    /// This rotation is performed before the Z rotation. </summary>
    public double RotX;
    /// <summary> Rotation of the camera around the Z axis.
    /// This rotation is performed before the X rotation. </summary>
    public double RotZ;

    /// <summary> Global X position of all transformations </summary>
    public double PosX;
    /// <summary> Global Y position of all transformations </summary>
    public double PosY;
    /// <summary> Global Z position of all transformations </summary>
    public double PosZ;

    double _zoom;
    double _zoomfactor;

    // TODO: Must be better.
    public double Zoom
    {
        get => _zoom;
        set {
            if (_vp.IsParallelProjection) {
                _zoomfactor = value/300;
                if (_zoomfactor > -1) _zoom = value;
                else _zoomfactor = -1;
            } else {
                _zoom = value;
            }
        }
    }

    public void Init (RD.RhinoViewport viewport, ON.Point3d target)
    {
        _vp = viewport;
        _vpinfo = new (viewport);
        _target = new (target);
        _vp.SetCameraTarget (_target, updateCameraLocation: false);
        ApplyCanges ();
    }

    public void ApplyCanges ()
    {
        var plane = new ON.Plane (_vp.CameraLocation, _vp.CameraX, _vp.CameraY);
        
        var cosZ = Math.Acos (plane.ZAxis.Z);
        var rx = plane.YAxis.Z < 0 ? -cosZ : cosZ;

        var cosY = Math.Acos (plane.XAxis.X); 
        var rz = plane.XAxis.Y < 0 ? -cosY : cosY;

        var ntarget = new ON.Point3d (_target);
        var nplane  = new ON.Plane (plane);

        ntarget.Transform (_TurnXZ (0, -rz));
        ntarget.Transform (_TurnXZ (-rx, 0));
        nplane.Transform (_TurnXZ (0, -rz));
        nplane.Transform (_TurnXZ (-rx, 0));

        PanX = nplane.OriginX - ntarget.X;
        PanY = nplane.OriginY - ntarget.Y;
        PanZ = nplane.OriginZ - ntarget.Z;
        RotX = rx;
        RotZ = rz;
        PosX = _target.X;
        PosY = _target.Y;
        PosZ = _target.Z;

        Zoom = 0;

        _vpinfo.GetFrustum (out var left, out var right, out var bottom, out var top, out var near, out var far);
        _initialSizeX  = new ON.Interval (left, right);
        _initialSizeY  = new ON.Interval (top, bottom);
        
        static ON.Transform _TurnXZ (double rotX, double rotZ)
        {
            var t = ON.Transform.Identity;
            var cosX = Math.Cos(rotX);
            var sinX = Math.Sin(rotX);
            var cosZ = Math.Cos(rotZ);
            var sinZ = Math.Sin(rotZ);

            // vector X
            t.M00 = cosZ;
            t.M10 = sinZ;
            t.M20 = 0d;

            // vector Y
            t.M01 = cosX * -sinZ;
            t.M11 = cosX * cosZ;
            t.M21 = sinX;

            // vector Z
            t.M02 = sinX * sinZ;
            t.M12 = -sinX * cosZ;
            t.M22 = cosX;

            return t;
        }
    }

    public void _PanTurnMoveZoom ()
    {
        var cosX = Math.Cos(RotX);
        var sinX = Math.Sin(RotX);
        var cosZ = Math.Cos(RotZ);
        var sinZ = Math.Sin(RotZ);
        
        // vector X
        _m.M00 = cosZ;
        _m.M10 = sinZ;
        _m.M20 = 0d;

        // vector Y
        _m.M01 = cosX * -sinZ;
        _m.M11 = cosX * cosZ;
        _m.M21 = sinX;

        // vector Z
        _m.M02 = sinX * sinZ;
        _m.M12 = -sinX * cosZ;
        _m.M22 = cosX;

        // origin       panX*vecX    panY*vecY    panZ*vecZ ;
        _m.M03 = PosX + PanX*_m.M00 + PanY*_m.M01 + PanZ*_m.M02;
        _m.M13 = PosY + PanX*_m.M10 + PanY*_m.M11 + PanZ*_m.M12;
        _m.M23 = PosZ + PanX*_m.M20 + PanY*_m.M21 + PanZ*_m.M22;

        // vector taget to origin
        var x = _m.M03 - PosX;
        var y = _m.M13 - PosY;
        var z = _m.M23 - PosZ;

        if (_vp.IsParallelProjection)
        {
            // + t2o * zoom factor
            _m.M03 += x * _zoomfactor;
            _m.M13 += y * _zoomfactor;
            _m.M23 += z * _zoomfactor;
        }
        else
        {
            var length = Math.Sqrt (x*x + y*y + z*z);
            // origin + (xyz for zoom=1) * zoom
            _m.M03 += x/length * _zoom;
            _m.M13 += y/length * _zoom;
            _m.M23 += z/length * _zoom;
        }

        // Perspectcive and global scale are not touch.
    }

    public void UpdateView ()
    {
        _PanTurnMoveZoom ();

        // En vue parallele, la position de la camera et le Frustum n'est pas modifier.
        // ??? Uniquement dans cam.csx, J'igniore pourquoi mais définir le Frustum change la position de la camera. ???
        if (_vp.IsParallelProjection)
        {
            _vpinfo.SetFrustum (
                _initialSizeX.T0 * (1+_zoomfactor),
                _initialSizeX.T1 * (1+_zoomfactor),
                _initialSizeY.T1 * (1+_zoomfactor),
                _initialSizeY.T0 * (1+_zoomfactor),
                _vpinfo.FrustumNear,
                _vpinfo.FrustumFar
            );
        }

        var pos = ON.Point3d.Origin;
        var dir = new ON.Vector3d (0, 0, -1);
        var up  = ON.Vector3d.YAxis;
        
        pos.Transform (_m);
        dir.Transform (_m);
        up.Transform (_m);
        
        #if DEBUG
        if (_vpinfo.SetCameraDirection (dir) == false) RhinoApp.WriteLine ("SetCameraDirection == false");
        if (_vpinfo.SetCameraLocation (pos) == false) RhinoApp.WriteLine ("SetCameraLocation == false");
        if (_vpinfo.SetCameraUp (up) == false) RhinoApp.WriteLine ("SetCameraUp == false");
        #else
        _vpinfo.SetCameraDirection (dir);
        _vpinfo.SetCameraLocation (pos);
        _vpinfo.SetCameraUp (up);
        #endif

        #if false // DEBUG // ??? Renvoie false même si tout semble fonctionné ???
        if (_vp.SetViewProjection (_vpinfo, updateTargetLocation: true)) RhinoApp.WriteLine ("SetViewProjection == false");
        #else
        _vp.SetViewProjection (_vpinfo, updateTargetLocation: true);
        #endif

    }
}


class CameraConduit : RD.DisplayConduit
{
    static CameraConduit? g_instance;

    RD.RhinoViewport? _vp;
    // public ON.Surface? _g;

    public static void Show (RD.RhinoViewport viewport)
    {
        g_instance ??= new ();
        g_instance._vp = viewport;
        g_instance.Enabled = true;
    }

    public static void hide ()
    {
        if (g_instance != null)
            g_instance.Enabled = false;
    }
    
    protected override void DrawOverlay(RD.DrawEventArgs e)
    {
        if (_vp == null || _vp.Id == e.Viewport.Id) return;

        e.Display.DrawPoint (_vp.CameraLocation, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawPoint (_vp.CameraTarget, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawLine (new ON.Line (_vp.CameraLocation, _vp.CameraTarget), SD.Color.BlueViolet);

        var _vpinfo = new Rhino.DocObjects.ViewportInfo (_vp);

        var points = _vpinfo.GetFarPlaneCorners ();
        var rect = new ON.PolylineCurve (points);
        e.Display.DrawCurve (rect, SD.Color.BlueViolet);

        points = _vpinfo.GetNearPlaneCorners ();
        rect = new ON.PolylineCurve (points);
        e.Display.DrawCurve (rect, SD.Color.BlueViolet);

        // if (_g == null) return;
        // e.Display.DrawSurface (_g, SD.Color.Blue, 2);
    }
}


enum NavigationMode { Pan, Rotate, Zoom, Presets }


class CameraController : NavigationListener 
{
    int _optversion = -1;

    RhinoDoc _doc;

    public readonly IntersectionData Data;

    readonly Camera _cam;

    bool _inplanview;


    # nullable disable // _doc
    public CameraController (INavigationOptions options, IntersectionData data) : base (options)
    {
        Data = data;
        _cam = new ();
    }
    #nullable enable


    #region Start / Stop

    public override bool CanRun (RUI.MouseCallbackEventArgs e)
    {
        if (e.MouseButton != RUI.MouseButton.Right || e.CtrlKeyDown && e.ShiftKeyDown)
            return false;

        if (e.View.ActiveViewport.IsPlanView)
        {
            if (Options.PresetsInPlanView == false) return false;
            if (Keyboard.GetCurrentModifier () != Options.PresetsModifier) return false;
        }

        // TODO:
        if (e.View.Document.Objects.GetSelectedObjects (includeLights: true, includeGrips: true).Count () > 0)
            return false;

        return true;
    }

    protected override bool OnStartNavigation (RD.RhinoViewport viewport, SD.Point viewportPoint)
    {
        Data.Viewport = viewport;
        Data.ViewportPoint = new SD.Point (viewportPoint.X, viewportPoint.Y);
        _doc = viewport.ParentView.Document;
        _inplanview = viewport.IsPlanView;

        // Calcule le point d'intersection sous le curseur de la souris.
        // S'il n'y a rien de visible dans la vue l'intersection est placée sur la position de la caméra.
        if (Options.Debug) {
            Intersector.StartPerformenceLog ();
            Intersector.Compute (Data, viewport.CameraLocation);
            Intersector.StopPerformenceLog (Data);
        } else {
            Intersector.Compute (Data, viewport.CameraLocation);
        }

        // Initialise les propriétés de déplacements.
        _InitializePan ();

        // Initialise les propriétés du zoom.
        _InitializeZoom ();

        // Initialise les propriétés des rotations prédéfinis.
        _InitializePresets ();

        // Initialise la camera.
        _cam.Init (viewport,  Data.TargetPoint);

        // Définis les actions.
        if (Options.DataVersion != _optversion)
        {
            _UpdateActions ();
            _optversion = Options.DataVersion;
        }

        // Affiche les informations visuelles.
        if (Options.Marker) IntersectionConduit.Show (Data);
        if (Options.ShowCamera) CameraConduit.Show (viewport);

        VirtualCursor.Init (new (viewportPoint.X, viewportPoint.Y));

        // TODO: Si la touche CapsLock est enfoncée avant le bouton de la souris.

        return true;
    }

    void _UpdateActions ()
    {
        foreach (ModifierKey n in Enum.GetValues (typeof (ModifierKey)))
            SetModifierCallback (Options.RotateModifier, null, null);

        SetModifierCallback (Options.RotateModifier, _OnRotation, NavigationMode.Rotate);
        SetModifierCallback (Options.PanModifier, _OnPan, NavigationMode.Pan);
        SetModifierCallback (Options.ZoomModifier, _OnZoom, NavigationMode.Zoom);
        SetModifierCallback (Options.PresetsModifier, _OnPresets, NavigationMode.Presets);
    }

    protected override void OnActionChange (object? previousTag, object? currentTag)
    {
        switch (previousTag)
        {
        case NavigationMode.Zoom: _InitializePan (); break;
        case NavigationMode.Presets: _StopPresetsNavigation (); break;
        }
        
        _cam.ApplyCanges ();
        if (currentTag != null)
            VirtualCursor.Show (_GetCursor ((NavigationMode)currentTag));

        switch (currentTag)
        {
        case NavigationMode.Presets: _StartPresetsNavigation (); break;
        }
    }

    protected override void OnStopNavigation ()
    {
        // TODO: Testez si la cible est devant la caméra.
        if (Data.Status != IntersectionStatus.None)
            Data.Viewport.SetCameraTarget (Data.TargetPoint, updateCameraLocation: false);
        IntersectionConduit.Hide ();
        CameraConduit.hide ();
        VirtualCursor.Hide ();

        // TODO: Si la touche CapsLock est enfoncée avant le bouton de la souris.
        //       Il n'est pas certain qu'elle ne le soit pas encore ici.

        _doc.Views.Redraw ();
    }

    VirtualCursorIcon _GetCursor (NavigationMode modifier)
    {
        if (NavigationMode.Pan == modifier) return VirtualCursorIcon.Hand;
        if (NavigationMode.Rotate == modifier) return VirtualCursorIcon.Pivot;
        if (NavigationMode.Zoom == modifier) return VirtualCursorIcon.Glass;
        // TODO: PresetsModifier
        return VirtualCursorIcon.None;
    }

    #endregion


    #region Turn

    void _OnRotation (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.RotX += -Math.PI*offset.Y/300;
        _cam.RotZ += -Math.PI*offset.X/300;
        _cam.UpdateView ();
        // _doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Zoom

    double _zforce;
    double _zinv;

    void _InitializeZoom ()
    {
        _zinv = Options.ReverseZoom ? -1 : 1;
        _zforce = Options.ZoomForce;
    }

    void _OnZoom (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.Zoom += offset.Y * _zinv * _zforce;
        _cam.UpdateView ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Pan

    double _w2sScale;

    void _InitializePan ()
    {
        Data.Viewport.GetWorldToScreenScale (Data.TargetPoint, out _w2sScale);
    }

    void _OnPan (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.PanX += -offset.X/_w2sScale;
        _cam.PanY += offset.Y/_w2sScale;
        VirtualCursor.GrowPosition (offset);
        _cam.UpdateView ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Presets

    const double EPSILON = 1E-15;

    // Allez comprendre pourquoi, les CPlanes standard sont visibles dans le panneau CPlane mais pas dans l'API.
    static ON.Plane[] _defaultCPlanes = {
        new (ON.Point3d.Origin, ON.Vector3d.ZAxis),
        new (ON.Point3d.Origin, ON.Vector3d.YAxis),
        new (ON.Point3d.Origin, ON.Vector3d.XAxis),
    };

    // Accumulateur d'offsets.
    ED.Point _accu;

    int _scount;
    double _srad;
    double _sforce;

    void _InitializePresets ()
    {
        _sforce = Options.PresetForce;
        _scount = Options.PresetSteps;
        _srad = Math.PI*2 / _scount;
    }

    void _StartPresetsNavigation ()
    {
        _accu.X = _accu.Y = 0;

        _cam.RotX = Math.Round (_cam.RotX / _srad) % 8 * _srad;
        _cam.RotZ = Math.Round (_cam.RotZ / _srad) % 8 * _srad;

        _cam.UpdateView ();
        _doc.Views.Redraw ();
    }

    void _OnPresets (ED.Point offset)
    {
        _accu.X += (int)(offset.X * _sforce);
        _accu.Y += (int)(offset.Y * _sforce);

        var x = _srad * (_accu.X / -200);
        var y = _srad * (_accu.Y / -200);
        if (x+y == 0) return;

        if (y != 0) { _cam.RotX += y; _accu.Y = 0; }
        if (x != 0) { _cam.RotZ += x; _accu.X = 0; }
        
        _cam.UpdateView ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    void _StopPresetsNavigation ()
    {
        if (Options.PresetsAlignCPlane == false) return;

        var bnormal = Viewport.CameraZ;
        bnormal.Unitize ();
        var inormal = -bnormal;

        foreach (var plane in _defaultCPlanes)
        {
            var cnormal = plane.Normal;
            cnormal.Unitize ();

            if (cnormal.EpsilonEquals (bnormal, EPSILON) == false &&
                cnormal.EpsilonEquals (inormal, EPSILON) == false
            ) continue;

            Viewport.SetConstructionPlane (plane);
            // TODO: Réaligner la caméra pour supprimer le bruit.
            // var cosZ = Math.Acos (plane.ZAxis.Z);
            // _cam.RotX = plane.YAxis.Z < 0 ? -cosZ : cosZ;
            // var cosY = Math.Acos (plane.XAxis.X);
            // _cam.RotZ = plane.XAxis.Y < 0 ? -cosY : cosY;
            // // _cam.RotZ = Math.Asin (cnormal.X);
            // // _cam.RotX = Math.Acos (cnormal.Z);
            // _cam.UpdateView ();
            // _doc.Views.Redraw ();
            return;
        }
        foreach (var cplane in _doc.NamedConstructionPlanes)
        {
            var cnormal = cplane.Plane.Normal;
            cnormal.Unitize ();

            if (cnormal.CompareTo (bnormal) == 0 == false &&
                cnormal.CompareTo (inormal) == 0 == false
            ) continue;

            Viewport.SetConstructionPlane (cplane);
            // TODO: Réaligner la caméra pour supprimer le bruit.
            return;
        }
    }

    #endregion
}


/*/
ISSUE:
    Parfois l'événement de la souris n'est pas déclencher.
    Dans ces cas là le comportement par default de navigation deviens donc actif.
link:
- https://patorjk.com/software/taag/#p=display&f=ANSI%20Regular&t=camera
/*/