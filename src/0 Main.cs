﻿/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Text;
using SD = System.Drawing;

using EF = Eto.Forms;
using ED = Eto.Drawing;

using RH = Rhino;
using RP = Rhino.PlugIns;
using RC = Rhino.Commands;
using RI = Rhino.Input.Custom;
using RhinoDoc = Rhino.RhinoDoc;


#if RHP

using Libx.Fix.AutoCameraTarget.Config;
using Libx.Fix.AutoCameraTarget.Ui;
using Libx.Fix.AutoCameraTarget.Sync;
using Libx.Fix.AutoCameraTarget.Intersection;
using Libx.Fix.AutoCameraTarget.Views;


[assembly: Guid("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: RP.PlugInDescription(RP.DescriptionType.Country, "France")]
[assembly: RP.PlugInDescription(RP.DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: RP.PlugInDescription(RP.DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]
[assembly: RP.PlugInDescription(RP.DescriptionType.Icon, "Libx.Fix.AutoCameraTarget.ico.RotateArround.ico")]


namespace Libx.Fix.AutoCameraTarget;


#endif // RHP

public class RhinoPlugIn : RP.PlugIn
{
    #nullable disable
    public static RhinoPlugIn Instance { get; private set; }
    #nullable enable

    public RhinoPlugIn () 
    {
        Instance = this;
    }


    public override RP.PlugInLoadTime LoadTime => RP.PlugInLoadTime.AtStartup;

    protected override RP.LoadReturnCode OnLoad(ref string errorMessage)
    {
        Main.Instance.LoadOptions(Settings);
        RH.RhinoApp.Idle += _OnIdle; // When the plugin is loaded, the toolbar is not.
        
        return base.OnLoad(ref errorMessage);
    }

    protected override void OnShutdown()
    {
        // RH.RhinoApp.Closing is called after OnShutdown
        // RH.RhinoApp.WriteLine ("Rhino Shutdown");
        Main.Instance.SaveOptions(Settings);
        base.OnShutdown();
    }

    // https://developer.rhino3d.com/guides/rhinocommon/create-deploy-plugin-toolbar/

    void _OnIdle(object o, EventArgs e)
    {
        RH.RhinoApp.Idle -= _OnIdle;
        _LoadRUI();
    }

    void _LoadRUI()
    {
        var path =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
            $@"\McNeel\Rhinoceros\{RH.RhinoApp.ExeVersion}.0\UI\Plug-ins\{Assembly.GetName().Name}.rui";

        if (File.Exists(path + ".rui_bak") == false)
        {
            try
            {
                RH.RhinoApp.ToolbarFiles.Open(path).GetGroup(0).Visible = true;
            }
            catch { /**/ }
        }
    }

}


public class AutoCameraTargetCommand : RC.Command
{
    public static string Name => "ToggleAutoCameraTarget";
    public override string EnglishName => Name;

    protected override RC.Result RunCommand(RhinoDoc doc, RC.RunMode mode)
    {
        return Main.Instance.RunToggleCommand(doc);
    }
}


public class Main
{
    static Main? g_instance;
    public static Main Instance => g_instance ??= new();

    public NavigationSettings Settings { get; }
    public ViewportMenuSettings ViewportMenuSettings { get; }

    readonly Navigator _navigator;

    readonly NavigationMenu _vpmenu;

    Main()
    {
        Settings = new ();
        var data = new IntersectionData (Settings);
        _navigator = new (Settings, new Controller (Settings, data));

        Settings.PropertyChanged += _OnSettingsChanged;

        ViewportMenuSettings = new ();
        _vpmenu = new (ViewportMenuSettings);
    }

    public RC.Result RunToggleCommand (RhinoDoc doc)
    {
        var go = new RI.GetOption();
        go.SetCommandPrompt("Toggle auto camera target");
        var optactive = go.AddOption("toggle");
        var optsettings = go.AddOption("settings");
        #if DEBUG
        var optcache = go.AddOption("cache");
        #endif

        var active = Settings.Active || Settings.ActiveInPlanView;

        var ret = go.Get();
        if (ret == RH.Input.GetResult.Option)
        {
            var optindex = go.OptionIndex();
            if (optindex == optactive)
            {
                Settings.Active = !active;
                Settings.ActiveInPlanView = !active;
                return RC.Result.Success;
            }
            else if (optindex == optsettings)
            {
                ShowNavigationSettings();
                return RC.Result.Success;
            }
            #if DEBUG
            if (optindex == optcache)
            {
                Cache.ShowDebugForm();
                return RC.Result.Success;
            }
            #endif
        }

        return ret == RH.Input.GetResult.Cancel
                ? RC.Result.Cancel
                : RC.Result.Success;
    }

    public void ShowNavigationSettings ()
    {
        var form = new NavigationSettingsForm (Settings);
        form.Show();
    }

    public void LoadOptions (RH.PersistentSettings settings)
    {
        RH.PersistentSettings chunk;

        if((settings.TryGetChild (Settings.GetType ().FullName, out chunk)))
            Settings.Load (chunk);
            
        if((settings.TryGetChild (_vpmenu.Settings.GetType ().FullName, out chunk)))
            _vpmenu.Settings.Load (chunk);
    }

    public void SaveOptions (RH.PersistentSettings settings)
    {
        RH.PersistentSettings chunk;

        chunk = settings.AddChild (Settings.GetType ().FullName);
        Settings.Save (chunk);

        chunk = settings.AddChild (_vpmenu.Settings.GetType ().FullName);
        _vpmenu.Settings.Save (chunk);
    }

    public static void RestoreWindow (EF.Window win)
    {
        if (RhinoPlugIn.Instance.Settings.TryGetChild (win.GetType ().FullName, out var chunk) == false)
            return;

        if (chunk.TryGetPoint (nameof (win.Location), out var location))
            win.Location = new (location.X, location.Y);

        if (chunk.TryGetSize  (nameof (win.Size), out var size)) 
            win.Size = new (size.Width, size.Height);
    }

    public static void SaveWindow (EF.Window win)
    {
        var chunk = RhinoPlugIn.Instance.Settings.AddChild (win.GetType ().FullName);
        chunk.SetPoint (nameof (win.Location), new SD.Point (win.Location.X, win.Location.Y));
        chunk.SetSize  (nameof (win.Size), new SD.Size (win.Size.Width, win.Size.Height));
    }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof (Settings.Active):
            case nameof (Settings.ActiveInPlanView):

                // IntersectionConduit.Hide();
                Intersector.HideDebugConduit ();
                LiveCamera.HideDebugConduit();
                VirtualCursor.Hide();
                Cursor.ShowCursor();

                if (Settings.Active || Settings.ActiveInPlanView)
                {
                    _vpmenu.Start ();
                    _navigator.Enabled = true;
                    Cache.Start();
                }
                else
                {
                    _vpmenu.Stop ();
                    _navigator.Enabled = false;
                    Cache.Stop();
                }
                break;
        }
    }
}


public class NavigationSettings : Settings, IControllerSettings, IIntersectionSettings, ICameraSettings, INavigationSettings
{
    // public override Guid SettingsGuidKey => new Guid("{E255B528-60E3-4FD4-BE4E-00DD640F8FDE}");

    const string TT_MOD = "Modifier key. \n'None' for no modifier key, 'Disable' to disable this feature.";
    const string TT_MOD_PV = "Modifier key in plan views. \n'None' for no modifier key, 'Disable' to disable this feature.";

    bool _active = false;
    bool _activeP = false;
    int _delay = 150;
    bool _marker = true;

    [Option(Tooltip = "Enable plug-in in non plan views.")]
    public bool Active { get => _active; set { Set(ref _active, value); } }

    [Option(Tooltip = "Enable plug-in in plan views.")]
    public bool ActiveInPlanView { get => _activeP; set { Set(ref _activeP, value); } }

    [Option(Tooltip = "Adds a pause when a modifier key is released to avoid transforming the view when you stop navigating.",
             Min = 0, Max = 1000, Increment = 1)]
    public int DelayBetweenModes { get => _delay; set { Set(ref _delay, value); } }

    [Option(Tooltip = "Show camera pivot point.")]
    public bool Marker { get => _marker; set { Set(ref _marker, value); } }


    #region Visual Debug

    bool _debug = true;

    [Option(Tooltip = "Display visual information for debugging or understanding the intersection process.")]
    public bool Debug { get => _debug; set { Set(ref _debug, value); } }

    [Exclude]
    public bool ShowCamera { get => _showcam; set { Set(ref _showcam, value); } }
    bool _showcam;

    #endregion


    // TODO:
    // ??? Pan plan parallel views with Ctrl+Shift+RMB ???
    // ??? Auto adjust camera target after Pan and Zoom ???
    // ??? Gumbal Rotate view around Gumball ???
    // public bool AlwaysPanParallelViews => RhinoViewSettings.AlwaysPanParallelViews;


    #region Pan

    NavigationModifier _pmod = NavigationModifier.Shift;
    NavigationModifier _pmodP = NavigationModifier.Disabled;

    [Option(Tooltip = TT_MOD)]
    public NavigationModifier PanModifier { get => _pmod; set { Set(ref _pmod, value); } }

    [Option(Tooltip = TT_MOD_PV)]
    public NavigationModifier PanModifierInPlanView { get => _pmodP; set { Set(ref _pmodP, value); } }

    #endregion


    #region Turn

    NavigationModifier _rmod = NavigationModifier.None;
    NavigationModifier _rmodP = NavigationModifier.Shift;

    [Option(Tooltip = TT_MOD)]
    public NavigationModifier RotateModifier { get => _rmod; set { Set(ref _rmod, value); } }

    [Option(Tooltip = TT_MOD_PV)]
    public NavigationModifier RotateModifierInPlanView { get => _rmodP; set { Set(ref _rmodP, value); } }

    #endregion


    #region Zoom

    NavigationModifier _zmod = NavigationModifier.Ctrl;
    NavigationModifier _zmodP = NavigationModifier.Disabled;
    bool _zinv;
    double _zforce = 4;

    [Option(Tooltip = TT_MOD)]
    public NavigationModifier ZoomModifier { get => _zmod; set { Set(ref _zmod, value); } }

    [Option(Tooltip = TT_MOD_PV)]
    public NavigationModifier ZoomModifierInPlanView { get => _zmodP; set { Set(ref _zmodP, value); } }

    // ??? Zoom > Reverse action ???
    [Option(Tooltip = "Reverse zoom direction. \nShould be set with 'Rhino Options > View > Zoom > Reverse action'.")]
    public bool ReverseZoom { get => _zinv; set { Set(ref _zinv, value); } }

    [Option(Min = 0.1, Max = 10, Increment = 0.1)]
    public double ZoomForce { get => _zforce; set { Set(ref _zforce, value); } }

    #endregion


    #region Presets

    NavigationModifier _xmod = NavigationModifier.Alt;
    NavigationModifier _xmodP = NavigationModifier.Alt;
    int _sangle = 4;
    double _sforce = 1;
    bool _scplane = false;

    [Option(Tooltip = TT_MOD)]
    public NavigationModifier PresetsModifier { get => _xmod; set { Set(ref _xmod, value); } }

    [Option(Tooltip = TT_MOD_PV)]
    public NavigationModifier PresetsModifierInPlanView { get => _xmodP; set { Set(ref _xmodP, value); } }

    [Option(Tooltip = "Number of preset positions in 360°.", Min = 1, Max = 60, Increment = 1)]
    public int PresetSteps { get => _sangle; set { Set(ref _sangle, value); } }

    [Option(Min = 0.1, Max = 10, Increment = 0.1)]
    public double PresetForce { get => _sforce; set { Set(ref _sforce, value); } }

    [Option(Tooltip = "Automatically align the CPlane (Front, Right, Top) to the view.")]
    public bool PresetsAlignCPlane { get => _scplane; set { Set(ref _scplane, value); } }

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

    static readonly Type NavigationOptionsType = typeof(NavigationSettings);

    string[] g_modnames = new[] {
        nameof (PanModifier),
        nameof (RotateModifier),
        nameof (ZoomModifier),
        nameof (PresetsModifier)
    };

    string[] g_modnamesP = new[] {
        nameof (PanModifierInPlanView),
        nameof (RotateModifierInPlanView),
        nameof (ZoomModifierInPlanView),
        nameof (PresetsModifierInPlanView)
    };

    public override bool Validate()
    {
        bool r = true;
        bool v;
        var t = GetType ();
        
        v = Active;
        OptionAttribute.Get (t, nameof (PanModifier)).Enabled = v;
        OptionAttribute.Get (t, nameof (RotateModifier)).Enabled = v;
        OptionAttribute.Get (t, nameof (ZoomModifier)).Enabled = v;
        OptionAttribute.Get (t, nameof (PresetsModifier)).Enabled = v;
        
        v = ActiveInPlanView;
        OptionAttribute.Get (t, nameof (PanModifierInPlanView)).Enabled = v;
        OptionAttribute.Get (t, nameof (RotateModifierInPlanView)).Enabled = v;
        OptionAttribute.Get (t, nameof (ZoomModifierInPlanView)).Enabled = v;
        OptionAttribute.Get (t, nameof (PresetsModifierInPlanView)).Enabled = v;

        v = Active || ActiveInPlanView;
        OptionAttribute.Get (t, nameof (ReverseZoom)).Enabled = v;
        OptionAttribute.Get (t, nameof (ZoomForce)).Enabled = v;
        OptionAttribute.Get (t, nameof (PresetSteps)).Enabled = v;
        OptionAttribute.Get (t, nameof (PresetForce)).Enabled = v;
        OptionAttribute.Get (t, nameof (PresetsAlignCPlane)).Enabled = v;

        r &= _Validate(g_modnames);
        r &= _Validate(g_modnamesP);
        return r;

        bool _Validate(string[] names)
        {
            OptionAttribute a, b;
            NavigationModifier vA, vB;
            var c = names.Length;
            var ok = true;

            for (var i = 0; i < c; i++)
            {
                a = OptionAttribute.Get(NavigationOptionsType, names[i]);
                a.Valid = true;
                vA = a.GetValue<NavigationModifier>(this);
                if (vA == NavigationModifier.Disabled) continue;

                for (var j = 0; j < c; j++)
                {
                    b = OptionAttribute.Get(NavigationOptionsType, names[j]);
                    vB = b.GetValue<NavigationModifier>(this);
                    if (vB == NavigationModifier.Disabled) continue;

                    if (i != j && vA == vB)
                    {
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

    public NavigationMode GetAssociatedMode(NavigationModifier modifier, bool inPlanView)
    {
        if (inPlanView)
        {
            if (_pmodP == modifier) return NavigationMode.Pan;
            if (_zmodP == modifier) return NavigationMode.Zoom;
            if (_rmodP == modifier) return NavigationMode.Rotate;
            if (_xmodP == modifier) return NavigationMode.Presets;
        }
        else
        {
            if (_pmod == modifier) return NavigationMode.Pan;
            if (_zmod == modifier) return NavigationMode.Zoom;
            if (_rmod == modifier) return NavigationMode.Rotate;
            if (_xmod == modifier) return NavigationMode.Presets;
        }
        return NavigationMode.Unknown;
    }

    #endregion
}


public class NavigationSettingsLayout : SettingsLayout <NavigationSettings>
{
    readonly EF.Control _pmod, _pmodP;
    readonly EF.Control _rmod, _rmodP;
    readonly EF.Control _zmod, _zmodP, _zinv, _zforce;
    readonly EF.Control _xmod, _xmodP, _xsteps, _xforce, _xcplane;

    public NavigationSettingsLayout (NavigationSettings options) : base (options)
    {
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
            Row ("Active", HBox(active, activeP)),
            Row ("Delay", delay)
        );

        Section (
            "Pan",
            Row ("Modifier", HBox(_pmod, _pmodP))
        );

        Section (
            "Rotation",
            Row ("Modifier", HBox(_rmod, _rmodP))
        );

        Section (
            "Zoom",
            Row ("Modifier", HBox(_zmod, _zmodP)),
            Row ("Force", _zforce),
            Row ("Reverse", _zinv)
        );

        Section(
            "Presets",
            Row ("Modifier", HBox(_xmod, _xmodP)),
            Row ("Steps", _xsteps),
            Row ("Sensitivity", _xforce),
            Row ("Align CPlane", _xcplane)
        );

        Section(
            "Advanced",
            Row ("Marker", marker),
            Row ("Debug", debug)
            #if DEBUG
            , Row ("Show camera", showcam)
            #endif
        );
    }

    protected override void UpdateControls (string? propertyChanged)
    {
        DBG.Log (propertyChanged ?? "Initialization");

        base.UpdateControls (propertyChanged);
        switch (propertyChanged)
        {
            case null:
                // _InitControlTags();
                _UpdateActive();
                _UpdateActiveInPlanView();
                _UpdateActiveShared();
                // _UpdateModifiers();
                break;

            case nameof(Settings.Active):
                _UpdateActive ();
                _UpdateActiveShared ();
                break;

            case nameof(Settings.ActiveInPlanView):
                _UpdateActiveInPlanView ();
                _UpdateActiveShared ();
                break;

            default:
                // _UpdateModifiers ();
                break;
        }
        
    }

    void _UpdateActive ()
    {
        // var v = Settings.Active;
        // _pmod.Enabled = v;
        // _rmod.Enabled = v;
        // _zmod.Enabled = v;
        // _xmod.Enabled = v;
    }

    void _UpdateActiveInPlanView ()
    {
        // var v = Settings.ActiveInPlanView;
        // _pmodP.Enabled = v;
        // _rmodP.Enabled = v;
        // _zmodP.Enabled = v;
        // _xmodP.Enabled = v;
    }

    void _UpdateActiveShared ()
    {
        // var v = Settings.Active || Settings.ActiveInPlanView;
        // _zinv.Enabled = v;
        // _zforce.Enabled = v;
        // _xmod.Enabled = v;
        // _xmodP.Enabled = v;
        // _xsteps.Enabled = v;
        // _xforce.Enabled = v;
        // _xcplane.Enabled = v;
    }

    // void _InitControlTags ()
    // {
    //     foreach (var (c, a) in PropertyToControls.Values)
    //     {
    //         c.Tag = c.BackgroundColor;
    //     }
    // }

    // void _UpdateModifiers()
    // {
    //     Settings.Validate();
    //     foreach (var (c, a) in PropertyToControls.Values)
    //     {
    //         c.BackgroundColor = a.Valid
    //                         ? (ED.Color)c.Tag
    //                         : ED.Colors.IndianRed;
    //     }
    // }
}


class NavigationSettingsForm : FloatingForm
{
    public NavigationSettingsForm (NavigationSettings settings)
    {
        Title = "Navigation settings";

        var control = new NavigationSettingsLayout(settings);
        control.ButtonOk.Click += delegate { Close(); };
        control.ButtonCancel.Click += delegate { Close(); };

        Content = control;
    }
}