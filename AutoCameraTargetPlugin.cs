using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.PlugIns;
using Rhino.UI;
using System;
using System.Diagnostics;
using System.Linq;
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
    private Stopwatch? sw;
    public Line line = new Line ();
    public Point3d targetPoint = Point3d.Origin;

    protected override void OnMouseDown (MouseCallbackEventArgs e)
    {
        if (e.MouseButton != MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown) return;
        
        // RhinoApp.WriteLine (">>> Mouse down ");

        AutoCameraTargetPlugin.Conduit.ViewportFilter = e.View.ActiveViewportID;
        AutoCameraTargetPlugin.Conduit.InRotation = true;

        sw ??= new Stopwatch ();
        sw.Restart ();
        targetPoint = _GetCenter (e.View.Document, _GetMouseRay ());
        RhinoApp.WriteLine ("Get Point "+sw.ElapsedMilliseconds+"ms");
        sw.Stop();

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

        BoundingBox bbox;
        double d;
        double min = 1.1;
        double gMin = 1.1;
        Mesh[] meshes;
        foreach (var obj in doc.Objects)
        {
            if (obj.Visible == false) continue;

            bbox = obj.Geometry.GetBoundingBox (accurate: false);
            if (bbox.IsValid == false) continue;

            d = RayBoxIntersection (rayPos, rayInvDir, bbox);
            if (d < 0) continue;

            // `obj.GetMeshes(MeshType.Default)` does not return meshes for block instances.
            meshes = (from oref in Rhino.DocObjects.RhinoObject.GetRenderMeshes (new Rhino.DocObjects.RhinoObject[] { obj }, true, false)
                      let m = oref.Mesh ()
                      where m != null
                      select m).ToArray ();

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

    /**
     * Cette fonction est un peu plus rapide que `Mesh.CreateFromBox (bbox, 1, 1, 1);`
     */
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

    private double RayBoxIntersection (double[] r_origin, double[] r_dir_inv, BoundingBox b)
    // https://tavianator.com/2011/ray_box.html
    // https://tavianator.com/2015/ray_box_nan.html
    {
        double t;
        double t1 = (b.Min.X - r_origin[0]) * r_dir_inv[0];
        double t2 = (b.Max.X - r_origin[0]) * r_dir_inv[0];

        double tmin = t1 < t2 ? t1 : t2; // min (t1, t2);
        double tmax = t1 > t2 ? t1 : t2; // max (t1, t2);


        t1 = (b.Min.Y - r_origin[1]) * r_dir_inv[1];
        t2 = (b.Max.Y - r_origin[1]) * r_dir_inv[1];
        if (t1 > t2)
        {
            t = t2;
            t2 = t1;
            t1 = t;
        }

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));


        t1 = (b.Min.Z - r_origin[2]) * r_dir_inv[2];
        t2 = (b.Max.Z - r_origin[2]) * r_dir_inv[2];
        if (t1 > t2)
        {
            t = t2;
            t2 = t1;
            t1 = t;
        }

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));

        //return tmin; // tmax == tmin in case of flat box
        if (tmax >= (tmin > 0 ? tmin : 0)) // tmax > max (tmin, 0.0);
        {
            return tmin < 0 ? tmax : tmin;
            // return tmin;
        }
        else
        {
            return -1;
        }
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
        #if DEBUG
        e.Display.DrawLine (AutoCameraTargetPlugin.Listener.line, SD.Color.BlueViolet);
        #endif
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