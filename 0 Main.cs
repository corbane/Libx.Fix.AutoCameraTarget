/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)

    NOTE:
    - The file `0 ...` can reference or be referenced by other files
    - The other files are hierarchical.
      File `2...` can reference a class in file `1...` but not in `3...`
/*/


using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Text;

using EF = Eto.Forms;

using RH = Rhino;
using RP = Rhino.PlugIns;
using RC = Rhino.Commands;
using RI = Rhino.Input.Custom;
using RhinoDoc = Rhino.RhinoDoc;
using Rhino.UI;


#if RHP


[assembly: Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: RP.PlugInDescription (RP.DescriptionType.Country, "France")]
[assembly: RP.PlugInDescription (RP.DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: RP.PlugInDescription (RP.DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]
[assembly: RP.PlugInDescription (RP.DescriptionType.Icon, "Libx.Fix.AutoCameraTarget.ico.RotateArround.ico")]


namespace Libx.Fix.AutoCameraTarget;


public class EntryPoint : RP.PlugIn
{
    public override RP.PlugInLoadTime LoadTime => RP.PlugInLoadTime.AtStartup;

    protected override RP.LoadReturnCode OnLoad (ref string errorMessage)
    {
        Main.Instance.LoadOptions (Settings);
        Rhino.RhinoApp.Idle += _OnIdle; // When the plugin is loaded, the toolbar is not...
        return base.OnLoad (ref errorMessage);
    }

    protected override void OnShutdown ()
    {
        Main.Instance.SaveOptions (Settings);
        base.OnShutdown ();
    }

    // https://developer.rhino3d.com/guides/rhinocommon/create-deploy-plugin-toolbar/
    // Toujours aussi simple.
    // Et l'exemple n'est même pas complet, "PlugInVersion" n'existe pas.

    void _OnIdle (object o, EventArgs e)
    {
        Rhino.RhinoApp.Idle -= _OnIdle;
        _LoadRUI();
    }

    void _LoadRUI ()
    {
        var path =
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
            $@"\McNeel\Rhinoceros\{Rhino.RhinoApp.ExeVersion}.0\UI\Plug-ins\{Assembly.GetName().Name}.rui";

        if (File.Exists(path+".rui_bak") == false)
        {
            try {
                Rhino.RhinoApp.ToolbarFiles.Open(path).GetGroup(0).Visible = true;
            }
            catch { /**/ }
        }
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


public class Main
{
    static Main? g_instance;
    public static Main Instance => g_instance ??= new ();

    public NavigationSettings Settings {  get; private set; }

    readonly RMBListener _mouse;
    readonly CameraController _camera;
    readonly IntersectionData _data;

    Main ()
    {
        Settings = new NavigationSettings ();
        _data = new (Settings);
        _camera = new (Settings, _data);
        _mouse = new (_camera);

        Settings.PropertyChanged += _OnSettingsChanged;
    }

    public RC.Result RunToggleCommand (RhinoDoc doc)
    {
        var go = new RI.GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");
        var optactive = go.AddOption ("toggle");
        var optsettings = go.AddOption ("settings");
        #if DEBUG
        var optcache = go.AddOption ("cache");
        #endif

        var ret = go.Get ();
        if (ret == Rhino.Input.GetResult.Option)
        {
            var optindex = go.OptionIndex ();
            if (optindex == optactive)
            {
                Settings.Active = !Settings.Active;
                return RC.Result.Success;
            }
            else if (optindex == optsettings)
            {
                ShowOptions ();
                return RC.Result.Success;
            }
            #if DEBUG
            if (optindex == optcache) {
                Cache.ShowDebugForm ();
                return RC.Result.Success;
            }
            #endif
        }

        return ret == Rhino.Input.GetResult.Cancel
                ? RC.Result.Cancel
                : RC.Result.Success;
    }

    public void ShowOptions ()
    {
        var control = new NavigationSettingsLayout (Settings);
        var form = new FloatingForm { Content = control, Title = "Navigation settings" };
        control.ButtonOk.Click += delegate { form.Close (); };
        control.ButtonCancel.Click += delegate { form.Close (); };
        form.Show ();
        
        // new NavigationForm (Options).Show ();
    }

    public void LoadOptions (RH.PersistentSettings settings) { Settings.Load (settings); }

    public void SaveOptions (RH.PersistentSettings settings) { Settings.Save (settings); }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Settings.Active):

            _mouse.Enabled = Settings.Active;
            IntersectionConduit.Hide ();
            CameraConduit.hide ();
            VirtualCursor.Hide ();
            Cursor.ShowCursor ();

            if (Settings.Active) {
                Cache.Start ();
            } else {
                Cache.Stop ();
            }

            break;
        }
    }
}

