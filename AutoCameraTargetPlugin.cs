using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.PlugIns;
using Rhino.UI;
using System;
using System.Runtime.InteropServices;
using SD = System.Drawing;


[assembly: Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]
[assembly: PlugInDescription (DescriptionType.Icon, "Libx.Fix.AutoCameraTarget.EmbeddedResources.RotateArround.ico")]

[assembly: PlugInDescription (DescriptionType.Address, "")]
[assembly: PlugInDescription (DescriptionType.Country, "France")]
[assembly: PlugInDescription (DescriptionType.Email, "contact@jmvrecq.fr")]
[assembly: PlugInDescription (DescriptionType.Phone, "")]
[assembly: PlugInDescription (DescriptionType.Fax, "")]
[assembly: PlugInDescription (DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: PlugInDescription (DescriptionType.UpdateUrl, "")]
[assembly: PlugInDescription (DescriptionType.WebSite, "jmvrecq.fr")]



namespace Libx.Fix.AutoCameraTarget;


public class AutoCameraTargetPlugin : PlugIn
{
    #nullable disable
    public static AutoCameraTargetPlugin Instance { get; private set; }
    internal static AutoCameraTargetListener Listener { get; private set; }
    internal static AutoCameraTargetConduit Conduit { get; private set; }
    #nullable enable

    public AutoCameraTargetPlugin ()
    {
        Instance = this;
        Listener = new AutoCameraTargetListener ();
        Conduit = new AutoCameraTargetConduit ();
    }
}


internal class AutoCameraTargetListener : MouseCallback
{
    public Line line = new Line ();
    public Point3d targetPoint = Point3d.Origin;

    protected override void OnMouseDown (MouseCallbackEventArgs e)
    {
        if (e.MouseButton != MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown) return;
        
        // RhinoApp.WriteLine (">>> Mouse down ");

        AutoCameraTargetPlugin.Conduit.ViewportFilter = e.View.ActiveViewportID;
        AutoCameraTargetPlugin.Conduit.InRotation = true;

        targetPoint = _GetCenter (e.View.Document, _GetMouseRay ());
        RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.SetCameraTarget (
            targetPoint,
            updateCameraLocation: false
        );
    }
    protected override void OnEndMouseUp (MouseCallbackEventArgs e)
    {
        AutoCameraTargetPlugin.Conduit.InRotation = false;
    }

    private Ray3d _GetMouseRay ()
    {
        var vp = RhinoDoc.ActiveDoc.Views.ActiveView;
        var mp = MouseCursor.Location;
        var pt = vp.ActiveViewport.ScreenToClient (new System.Drawing.Point ((int)mp.X, (int)mp.Y));
        vp.ActiveViewport.GetFrustumLine (pt.X, pt.Y, out line);

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new Ray3d (line.To, line.From - line.To);
    }

    private Point3d _GetCenter (RhinoDoc doc, Ray3d ray)
    {
        BoundingBox bbox;
        Mesh bmesh;
        double d;
        double min = 1.1;
        double gMin = 1.1;
        Mesh[] meshes;
        foreach (var obj in doc.Objects)
        {
            if (obj.Visible == false) continue;

            bbox = obj.Geometry.GetBoundingBox (accurate: false);
            if (bbox.IsValid == false) continue;

            // Does not work on plane objects.
            bmesh = Mesh.CreateFromBox (bbox, 1, 1, 1);
            // So...
            if (bmesh == null) bmesh = _GetBBoxMesh (bbox);
            // Here, bmesh.IsValid == false, it does not matter for the intersection.

            d = Rhino.Geometry.Intersect.Intersection.MeshRay (bmesh, ray);
            if (d < 0) continue;

            meshes = obj.GetMeshes (MeshType.Default);

            // If the object has no mesh, we memorize the intersection of the box
            if (meshes.Length == 0)
            {
                if (d < gMin) gMin = d;
                continue;
            }

            // Otherwise, test for intersections.
            foreach (var m in meshes)
            {
                d = Rhino.Geometry.Intersect.Intersection.MeshRay (m, ray);
                if (d < 0) continue;
                if (d < min) min = d;
            }
        }
        return (
            // Has an intersection with a mesh been found?
            min != 1.1 ? ray.PointAt (min)
            // Has an intersection with a bounding box been found?
            : gMin != 1.1 ? ray.PointAt (gMin)
            // Otherwise an arbitrary point is returned
            : ray.PointAt (0.5)
        );
    }

    Mesh _GetBBoxMesh (BoundingBox bbox)
    {

        Mesh mesh = new Mesh ();
        Point3d vertex  = bbox.PointAt (0.0, 0.0, 0.0);
        Point3d vertex2 = bbox.PointAt (1.0, 0.0, 0.0);
        Point3d vertex3 = bbox.PointAt (1.0, 1.0, 0.0);
        Point3d vertex4 = bbox.PointAt (0.0, 1.0, 0.0);
        Point3d vertex5 = bbox.PointAt (0.0, 0.0, 1.0);
        Point3d vertex6 = bbox.PointAt (1.0, 0.0, 1.0);
        Point3d vertex7 = bbox.PointAt (1.0, 1.0, 1.0);
        Point3d vertex8 = bbox.PointAt (0.0, 1.0, 1.0);
        mesh.Vertices.Add (vertex);
        mesh.Vertices.Add (vertex4);
        mesh.Vertices.Add (vertex3);
        mesh.Vertices.Add (vertex2);
        mesh.Faces.AddFace (0, 1, 2, 3);
        mesh.Vertices.Add (vertex4);
        mesh.Vertices.Add (vertex);
        mesh.Vertices.Add (vertex5);
        mesh.Vertices.Add (vertex8);
        mesh.Faces.AddFace (4, 5, 6, 7);
        mesh.Vertices.Add (vertex);
        mesh.Vertices.Add (vertex2);
        mesh.Vertices.Add (vertex6);
        mesh.Vertices.Add (vertex5);
        mesh.Faces.AddFace (8, 9, 10, 11);
        mesh.Vertices.Add (vertex2);
        mesh.Vertices.Add (vertex3);
        mesh.Vertices.Add (vertex7);
        mesh.Vertices.Add (vertex6);
        mesh.Faces.AddFace (12, 13, 14, 15);
        mesh.Vertices.Add (vertex3);
        mesh.Vertices.Add (vertex4);
        mesh.Vertices.Add (vertex8);
        mesh.Vertices.Add (vertex7);
        mesh.Faces.AddFace (16, 17, 18, 19);
        mesh.Vertices.Add (vertex5);
        mesh.Vertices.Add (vertex6);
        mesh.Vertices.Add (vertex7);
        mesh.Vertices.Add (vertex8);
        mesh.Faces.AddFace (20, 21, 22, 23);
        mesh.Normals.ComputeNormals ();
        return mesh;
    }
}


class AutoCameraTargetConduit : DisplayConduit
{
    public Guid ViewportFilter { get; set; }
    public bool InRotation { get; set; } = false;

    protected override void DrawForeground (DrawEventArgs e)
    {
        if (InRotation == false || e.Viewport.Id != ViewportFilter) return;
        e.Display.DrawPoint (AutoCameraTargetPlugin.Listener.targetPoint, PointStyle.RoundActivePoint, 3, SD.Color.Black);
        // Dbg
        // e.Display.DrawLine (AutoCameraTargetPlugin.Listener.line, SD.Color.BlueViolet);
    }
}


public class AutoCameraTargetCommand : Command
{
    #nullable disable
    public static AutoCameraTargetCommand Instance { get; private set; }
    #nullable enable

    public AutoCameraTargetCommand ()
    {
        Instance = this;
    }

    public override string EnglishName => "ToggleAutoCameraTarget";

    protected override Result RunCommand (RhinoDoc doc, RunMode mode)
    {
        var go = new GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");
        var active = new OptionToggle (false, "No", "Yes");
        var marker = new OptionToggle (false, "No", "Yes");
        active.CurrentValue = AutoCameraTargetPlugin.Listener.Enabled;
        marker.CurrentValue = AutoCameraTargetPlugin.Conduit.Enabled;

        for (;;)
        {
            go.ClearCommandOptions();
            go.AddOptionToggle ("active", ref active);
            go.AddOptionToggle ("marker", ref marker);
            var ret = go.Get ();
            if (ret == Rhino.Input.GetResult.Option)
            {
                AutoCameraTargetPlugin.Listener.Enabled = active.CurrentValue;
                
                marker.CurrentValue = active.CurrentValue && marker.CurrentValue;
                if (marker.CurrentValue != AutoCameraTargetPlugin.Conduit.Enabled) {
                    AutoCameraTargetPlugin.Conduit.Enabled = marker.CurrentValue;
                    doc.Views.Redraw ();
                }

                continue;
            }
            
            return ret == Rhino.Input.GetResult.Cancel
                 ? Result.Cancel
                 : Result.Success;

        }

        // AutoCameraTargetPlugin.Listener.Enabled = AutoCameraTargetPlugin.Conduit.Enabled = !AutoCameraTargetPlugin.Listener.Enabled;
        // return Result.Success;
    }
}