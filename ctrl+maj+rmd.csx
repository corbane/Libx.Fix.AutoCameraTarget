#nullable enable
#r "C:\Program Files\Rhino 7\System\RhinoCommon.dll"
#r "C:\Program Files\Rhino 7\Plug-ins\Grasshopper\Grasshopper.dll"
#r "E:\Projet\Rhino\Libx\Out\net48\Libx.Script.Grasshopper.gha"
#r "C:\Program Files\Rhino 7\System\Rhino.UI.dll"
#load "./Main.cs"

using System;
using Libx.Task;
using Libx.Task.Grasshopper;

[Input] bool enabled;
if (enabled) 
{ 
    Main.Instance.RunToggleCommand (Rhino.RhinoDoc.ActiveDoc);
}
[OnUnLoad] void Dispose ()
{
    Rhino.RhinoApp.WriteLine("Unload event");
    Main.Instance.Active = false;
    InfoConduit.Hide ();
}
