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
using System.Reflection;
using System.Runtime.InteropServices;
using SD = System.Drawing;


[assembly: Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: PlugInDescription (DescriptionType.Country, "France")]
[assembly: PlugInDescription (DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: PlugInDescription (DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]


namespace Libx.Fix.AutoCameraTarget;


public class Main : PlugIn
{
    #nullable disable
    public static Main Instance { get; private set; }
    internal static MouseListener Listener { get; } = new ();
    internal static IntersectionConduit Conduit { get; } = new ();
    internal static VisualTarget VisualTarget { get; } = new ();
    #nullable enable

    public Main () { Instance = this; }
}


public class AutoCameraTargetCommand : Command
{
    #nullable disable
    public static AutoCameraTargetCommand Instance { get; private set; }
    #nullable enable

    public AutoCameraTargetCommand () { Instance = this; }

    public override string EnglishName => "ToggleAutoCameraTarget";

    protected override Result RunCommand (RhinoDoc doc, RunMode mode)
    {
        var go = new GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");

        var active = new OptionToggle (false, "No", "Yes");
        var marker = new OptionToggle (false, "No", "Yes");
        var debug  = new OptionToggle (false, "No", "Yes");

        active.CurrentValue = Main.Listener.Enabled;
        marker.CurrentValue = Main.Conduit.Enabled;
        debug.CurrentValue  = Main.VisualTarget.Debug;

        for (;;)
        {
            go.ClearCommandOptions();
            go.AddOptionToggle ("active", ref active);
            if (Main.Listener.Enabled)
                go.AddOptionToggle ("marker", ref marker);
            if (Main.Conduit.Enabled)
                go.AddOptionToggle ("debug", ref debug);

            var ret = go.Get ();
            if (ret == Rhino.Input.GetResult.Option)
            {
                RhinoApp.WriteLine ("Option clicked");
                Main.Listener.Enabled = active.CurrentValue;
                
                marker.CurrentValue = active.CurrentValue && marker.CurrentValue;
                if (marker.CurrentValue != Main.Conduit.Enabled) {
                    Main.Conduit.Enabled = marker.CurrentValue;
                    doc.Views.Redraw ();
                }

                Main.VisualTarget.Debug = marker.CurrentValue && debug.CurrentValue;

                continue;
            }
            
            return ret == Rhino.Input.GetResult.Cancel
                 ? Result.Cancel
                 : Result.Success;

        }
    }
}


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


class VisualTarget
{
    /// <summary>
    /// The line under the mouse pointer to the far plane Frustum
    /// </summary>
    public Line Rayline;

    /// <summary>
    /// The bounding box of the element closest to the camera.
    /// </summary>
    public BoundingBox ActiveBBox;

    /// <summary>
    /// Box containing the objects visible in the viewport.
    /// </summary>
    public BoundingBox VisibleBBox;

    /// <summary>
    /// Plan in front of the camera at the center of VisibleBBox.
    /// </summary>
    public Plane FrustumFrontPlane;

    /// <summary>
    /// The target point of the camera
    /// </summary>
    public Point3d Point;

    public bool Debug = false;

    public IntersectionStatus IntersectionStatus;

    Stopwatch? sw;
    public void StartPerformence () {
        sw ??= new Stopwatch ();
        sw.Restart ();
    }
    public void StopPerformence () {
        RhinoApp.WriteLine ("Get Point " + sw!.ElapsedMilliseconds + "ms");
        sw.Stop ();
    }
}


class MouseListener : MouseCallback
{
    protected override void OnMouseDown (MouseCallbackEventArgs e)
    {
        if (e.MouseButton != MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown) return;

        if (Main.VisualTarget.Debug)
            Main.VisualTarget.StartPerformence ();

        Main.Conduit.ViewportFilter = e.View.ActiveViewportID;
        Main.Conduit.InRotation = true;

        var target = _GetCenter (e.View.Document, _GetMouseRay ());
        if (target != Point3d.Unset)
        {
            Main.VisualTarget.Point = target;
            RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.SetCameraTarget (
                target,
                updateCameraLocation: false
            );
        }

        if (Main.VisualTarget.Debug)
            Main.VisualTarget.StopPerformence ();
    }

    protected override void OnEndMouseUp (MouseCallbackEventArgs e)
    {
        Main.Conduit.InRotation = false;
        e.View.Redraw ();
    }

    Ray3d _GetMouseRay ()
    {
        var vp = RhinoDoc.ActiveDoc.Views.ActiveView;
        var mp = MouseCursor.Location;
        var pt = vp.ActiveViewport.ScreenToClient (new SD.Point ((int)mp.X, (int)mp.Y));
        vp.ActiveViewport.GetFrustumLine (pt.X, pt.Y, out var line);

        Main.VisualTarget.Rayline = line;

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new Ray3d (line.To, line.From - line.To);
    }

    Point3d _GetCenter (RhinoDoc doc, Ray3d ray)
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
        BoundingBox activebbox = BoundingBox.Unset;
        BoundingBox visiblebbox = BoundingBox.Unset;
        double t;
        double tmin = 1.1;
        double tbboxmin = 1.1;
        Mesh[] meshes;
        var arg = new Rhino.DocObjects.RhinoObject[1];
        var vp = doc.Views.ActiveView.ActiveViewport;

        foreach (var obj in doc.Objects)
        {
            if (obj.Visible == false) continue;
            bbox = obj.Geometry.GetBoundingBox (accurate: false);
            if (bbox.IsValid == false) continue;

            t = _RayBoxIntersection (rayPos, rayInvDir, bbox);
            if (t < 0)
            {
                if (vp.IsVisible (bbox)) visiblebbox.Union (bbox);
                continue;
            }
            // If its bounding box is closest to the camera.
            else if (t < tbboxmin)
            {
                tbboxmin = t;
                activebbox = bbox;
            }

            // `obj.GetMeshes(MeshType.Default)` does not return meshes for block instances.
            // GetRenderMeshes has a different behavior with SubDs
            // https://discourse.mcneel.com/t/rhinoobject-getrendermeshes-bug/151953
            arg[0] = obj;
            meshes = (from oref in Rhino.DocObjects.RhinoObject.GetRenderMeshes (arg, true, false)
                      let m = oref.Mesh ()
                      where m != null
                      select m).ToArray ();

            // test the intersections with the meshes.
            foreach (var m in meshes)
            {
                t = Rhino.Geometry.Intersect.Intersection.MeshRay (m, ray);
                if (t < 0) continue;
                if (t < tmin) tmin = t;
            }
        }

        // Has an intersection with a mesh been found ?
        if (tmin != 1.1)
        {
            Main.VisualTarget.IntersectionStatus = IntersectionStatus.OnMesh;
            return ray.PointAt (tmin);
        }

        // Has an intersection with a bounding box been found ?
        if (activebbox.Min != Point3d.Unset)
        {
            Main.VisualTarget.IntersectionStatus = IntersectionStatus.OnBBox;
            Main.VisualTarget.ActiveBBox = activebbox;
            return ray.PointAt (tbboxmin);
        }

        // Is there at least one object visible ?
        if (visiblebbox.Min == Point3d.Unset)
        {
            Main.VisualTarget.IntersectionStatus = IntersectionStatus.None;
            return Point3d.Unset;
        }

        // the ray line intersects the global bounding box ?
        t = _RayBoxIntersectionCenter (rayPos, rayInvDir, visiblebbox);
        if (t >= 0)
        {
            Main.VisualTarget.IntersectionStatus = IntersectionStatus.OnVisibleBBox;
            Main.VisualTarget.VisibleBBox = visiblebbox;
            return ray.PointAt (t);
        }

        // The mouse is outside any bounding boxes and there are objects visible on the screen.
        else
        {
            Main.VisualTarget.IntersectionStatus = IntersectionStatus.Outside;
            vp.GetFrustumFarPlane (out var plane);
            plane.Origin = visiblebbox.Center;
            Main.VisualTarget.FrustumFrontPlane = plane;
            Main.VisualTarget.VisibleBBox = visiblebbox;
            return ray.PointAt (intersectPlane (plane, ray));
        }

        static double intersectPlane (Plane plane, Ray3d ray)
        {
            return Vector3d.Multiply (plane.Origin - ray.Position, plane.Normal) / Vector3d.Multiply (plane.Normal, ray.Direction);
        }
    }

    /// <summary>
    /// same as _RayBoxIntersection but returns the midpoint of the ray line trimmed by the bounding box
    /// </summary>
    double _RayBoxIntersectionCenter (double[] r_origin, double[] r_dir_inv, BoundingBox b)
    {
        double t;
        double t1 = (b.Min.X - r_origin[0]) * r_dir_inv[0];
        double t2 = (b.Max.X - r_origin[0]) * r_dir_inv[0];

        double tmin = t1 < t2 ? t1 : t2; // min (t1, t2);
        double tmax = t1 > t2 ? t1 : t2; // max (t1, t2);


        t1 = (b.Min.Y - r_origin[1]) * r_dir_inv[1];
        t2 = (b.Max.Y - r_origin[1]) * r_dir_inv[1];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));


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
    double _RayBoxIntersection (double[] r_origin, double[] r_dir_inv, BoundingBox b)
    {
        double t;
        double t1 = (b.Min.X - r_origin[0]) * r_dir_inv[0];
        double t2 = (b.Max.X - r_origin[0]) * r_dir_inv[0];

        double tmin = t1 < t2 ? t1 : t2; // min (t1, t2);
        double tmax = t1 > t2 ? t1 : t2; // max (t1, t2);


        t1 = (b.Min.Y - r_origin[1]) * r_dir_inv[1];
        t2 = (b.Max.Y - r_origin[1]) * r_dir_inv[1];
        if (t1 > t2) { t = t2; t2 = t1; t1 = t; } // t1 must be smaller than t2

        tmin = tmin > t1 ? tmin : t1; // max (tmin, min (t1, t2));
        tmax = tmax < t2 ? tmax : t2; // min (tmax, max (t1, t2));


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
}


class IntersectionConduit : DisplayConduit
{
    public Guid ViewportFilter { get; set; }
    public bool InRotation { get; set; } = false;

    protected override void CalculateBoundingBox (CalculateBoundingBoxEventArgs e)
    {
        e.IncludeBoundingBox (Main.VisualTarget.Rayline.BoundingBox);
    }

    protected override void DrawForeground (DrawEventArgs e)
    {
        if (InRotation == false || e.Viewport.Id != ViewportFilter) return;
        e.Display.DrawPoint (Main.VisualTarget.Point, PointStyle.RoundActivePoint, 3, SD.Color.Black);

        if (Main.VisualTarget.Debug == false) return;

        e.Display.DrawLine (Main.VisualTarget.Rayline, SD.Color.BlueViolet);

        switch (Main.VisualTarget.IntersectionStatus)
        {
        // case IntersectionStatus.None: break;
        // case IntersectionStatus.OnMesh: break;
        case IntersectionStatus.Outside:
            e.Display.DrawBox (Main.VisualTarget.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            e.Display.DrawConstructionPlane (new Rhino.DocObjects.ConstructionPlane {
                Plane = Main.VisualTarget.FrustumFrontPlane,
                ShowGrid = false
            });
            break;
        case IntersectionStatus.OnBBox:
            e.Display.DrawBox (Main.VisualTarget.ActiveBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;
        case IntersectionStatus.OnVisibleBBox:
            e.Display.DrawBox (Main.VisualTarget.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;
        }
    }
}
