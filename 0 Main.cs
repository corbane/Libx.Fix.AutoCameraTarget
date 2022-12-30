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
using System.ComponentModel;

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



/*/

███    ███  █████  ██ ███    ██ 
████  ████ ██   ██ ██ ████   ██ 
██ ████ ██ ███████ ██ ██ ██  ██ 
██  ██  ██ ██   ██ ██ ██  ██ ██ 
██      ██ ██   ██ ██ ██   ████ 
 
/*/


#if RHP


[assembly: Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

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
        Cache.AttachEvents ();
        // Intersector.AttachEvents ();
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
            IntersectionConduit.Hide ();
            CameraConduit.hide ();
            VirtualCursor.Hide ();
            Cursor.ShowCursor ();

            if (Options.Active) {
                Cache.AttachEvents ();
            } else {
                Cache.DetachEvents ();
                Cache.Clear ();
            }

            break;
        }
    }
}

