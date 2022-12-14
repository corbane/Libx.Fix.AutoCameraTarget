
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SD = System.Drawing;

using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.PlugIns;
using Rhino.UI;
using RI = Rhino.Input.Custom;
using RC = Rhino.Commands;


[assembly: Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: PlugInDescription (DescriptionType.Country, "France")]
[assembly: PlugInDescription (DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: PlugInDescription (DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]


namespace Libx.Fix.AutoCameraTarget;


public class Main : PlugIn
{
    #nullable disable
    public static Main Instance { get; private set; }
    #nullable enable
    public Main () { Instance = this; }

    static Stopwatch? sw;
    static MouseListener g_listener { get; } = new ();
    static IntersectionConduit g_conduit { get; } = new ();
    internal static MouseIntersector g_intersector { get; } = new ();

    public static bool Active
    {
        get => g_listener.Enabled;
        set
        {
            g_listener.Enabled = value;
            if (value == false) g_conduit.Enabled = false;
        }
    }
    public static bool Marker = true;
    public static bool Debug = true;

    internal static RC.Result RunToggleCommand (RhinoDoc doc)
    {
        var go = new RI.GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");

        var active = new RI.OptionToggle (false, "No", "Yes");
        var marker = new RI.OptionToggle (false, "No", "Yes");
        var debug = new RI.OptionToggle (false, "No", "Yes");

        active.CurrentValue = Active;
        marker.CurrentValue = Marker;
        debug.CurrentValue = Debug;

        for (; ; )
        {
            go.ClearCommandOptions ();
            go.AddOptionToggle ("active", ref active);
            if (Active) go.AddOptionToggle ("marker", ref marker);
            if (Active && Marker) go.AddOptionToggle ("debug", ref debug);

            var ret = go.Get ();
            if (ret == Rhino.Input.GetResult.Option)
            {
                Active = active.CurrentValue;

                marker.CurrentValue = active.CurrentValue && marker.CurrentValue;
                if (marker.CurrentValue != Marker)
                {
                    Marker = marker.CurrentValue;
                    doc.Views.Redraw ();
                }

                Debug = marker.CurrentValue && debug.CurrentValue;

                continue;
            }

            return ret == Rhino.Input.GetResult.Cancel
                 ? RC.Result.Cancel
                 : RC.Result.Success;

        }
    }

    internal static void StartRotation (RhinoViewport vp, SD.Point viewportPoint)
    {
        if (Debug)
        {
            sw ??= new Stopwatch ();
            sw.Restart ();
        }

        if (g_intersector.UpdatePoint (vp, viewportPoint))
            vp.SetCameraTarget (g_intersector.Point, updateCameraLocation: false);

        if (Debug)
        {
            RhinoApp.WriteLine ("Get Point " + sw!.ElapsedMilliseconds + "ms for " + g_intersector.ObjectCount + " object(s).");
            sw.Stop ();
        }
        if (Marker)
        {
            g_conduit.ViewportFilter = vp.Id;
            g_conduit.Enabled = true;
        }
    }

    internal static void StopRotation ()
    {
        g_conduit.Enabled = false;
    }

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
        return Main.RunToggleCommand (doc);
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


class MouseIntersector
{
    #region State

    public RhinoViewport? ActiveViewport;

    /// <summary>
    /// Nombre d'objet calculer sous la souris.
    /// </summary>
    public uint ObjectCount;

    /// <summary>
    /// The line under the mouse pointer to the far plane Frustum
    /// </summary>
    public Ray3d Rayline;

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

    public IntersectionStatus IntersectionStatus;

    readonly List<Mesh> _usedmeshes = new ();

    readonly Rhino.DocObjects.ObjectEnumeratorSettings _enumeratorSettings = new ()
    {
        VisibleFilter = true,
        HiddenObjects = false,
        DeletedObjects = false,
        IncludeGrips = false,
        LockedObjects = true,
        ActiveObjects = true,  // File objects
        ReferenceObjects = true,  // Imported/Linked Objects
        // ViewportFilter - Set in UpdatePoint,
        // NormalObjects  - Not understand, probably unnecessary since HiddenObjects, LockedObjects are defined.
        // IdefObjects    - Not understand, if true, blocked objects and references are excluded.
    };

    // public int Complexity;

    #endregion


    static Ray3d _GetMouseRay (RhinoViewport vp)
    {
        var mp = MouseCursor.Location;
        var pt = vp.ScreenToClient (new SD.Point ((int)mp.X, (int)mp.Y));
        vp.GetFrustumLine (pt.X, pt.Y, out var line);

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new Ray3d (line.To, line.From - line.To);
    }

    public bool UpdatePoint (RhinoViewport vp, SD.Point viewportPoint)
    {
        var ray = _GetMouseRay (vp);
        ActiveViewport = vp;
        Rayline = ray;
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
        BoundingBox bbox;
        BoundingBox activebbox = BoundingBox.Unset;
        BoundingBox visiblebbox = BoundingBox.Unset;
        double t;
        double tbboxmin = 1.1;
        var arg = new Rhino.DocObjects.RhinoObject[1];

        if (vp.ParentView.Document.Objects.Count < _usedmeshes.Capacity)
            _usedmeshes.Capacity = vp.ParentView.Document.Objects.Count;

        _enumeratorSettings.ViewportFilter = vp;
        foreach (var obj in vp.ParentView.Document.Objects.GetObjectList (_enumeratorSettings))
        {
            // if (obj.Visible == false) continue;
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
            total++;

            // `obj.GetMeshes(MeshType.Default)` does not return meshes for block instances.
            // GetRenderMeshes has a different behavior with SubDs
            // https://discourse.mcneel.com/t/rhinoobject-getrendermeshes-bug/151953
            arg[0] = obj;
            _usedmeshes.AddRange (from oref in Rhino.DocObjects.RhinoObject.GetRenderMeshes (arg, true, false)
                                  let m = oref.Mesh ()
                                  where m != null
                                  select m);
        }

        ObjectCount = total;

        // Complexity = _usedmeshes.Sum (m => m.Faces.Count);
        // La methode utilisant une image pour détecter les intersections
        // est plus rapide que `Intersection.MeshRay` à partir de 1 millions de faces (sur mon ordinateur).
        // complexity 65_000 faces, gain 33% par rapport à `IdConduit.PickIndex`
        // complexity 1_500_000 faces, gain 30%/40% par rapport à `Parallel.For (Intersection.MeshRay)`
        // complexity 5_900_000 faces, gain 30%/40% par rapport à `Parallel.For (Intersection.MeshRay)`
#if USE_BITMAP
        // test the intersections with the meshes.
        var mindex = _usedmeshes.Count > 0
                    ? IdConduit.PickIndex (vp.ParentView, _usedmeshes, viewportPoint)
                    : -1;

        // test the intersections with the meshes.
        if (mindex > -1 && mindex < _usedmeshes.Count)
        {
            t = Rhino.Geometry.Intersect.Intersection.MeshRay (_usedmeshes[mindex], ray);
            _usedmeshes.Clear ();
            IntersectionStatus = IntersectionStatus.OnMesh;
            Point = ray.PointAt (t);
            return true;
        }
        _usedmeshes.Clear ();
#else
        // test the intersections with the meshes.
        double tmin = 1.1;
        System.Threading.Tasks.Parallel.For (0, _usedmeshes.Count, (int i) => {
            var t = Rhino.Geometry.Intersect.Intersection.MeshRay (_usedmeshes[i], ray);
            // Je ne suis pas certain que ce soit une bonne chose d'assigner dans les thread cette variable partagé.
            if (t > 0 && t < tmin) tmin = t;
        });
        _usedmeshes.Clear ();

        // Has an intersection with a mesh been found ?
        if (tmin != 1.1)
        {
            IntersectionStatus = IntersectionStatus.OnMesh;
            Point = ray.PointAt (tmin);
            return true;
        }
#endif


        // Has an intersection with a bounding box been found ?
        if (activebbox.Min != Point3d.Unset)
        {
            IntersectionStatus = IntersectionStatus.OnBBox;
            ActiveBBox = activebbox;
            Point = ray.PointAt (tbboxmin);
            return true;
        }

        // Is there at least one object visible ?
        if (visiblebbox.Min == Point3d.Unset)
        {
            IntersectionStatus = IntersectionStatus.None;
            Point = Point3d.Unset;
            return false;
        }

        // the ray line intersects the global bounding box ?
        t = _RayBoxIntersectionCenter (rayPos, rayInvDir, visiblebbox);
        if (t >= 0)
        {
            IntersectionStatus = IntersectionStatus.OnVisibleBBox;
            VisibleBBox = visiblebbox;
            Point = ray.PointAt (t);
            return true;
        }

        // The mouse is outside any bounding boxes and there are objects visible on the screen.
        else
        {
            IntersectionStatus = IntersectionStatus.Outside;
            vp.GetFrustumFarPlane (out var plane);
            plane.Origin = visiblebbox.Center;
            FrustumFrontPlane = plane;
            VisibleBBox = visiblebbox;
            Point = ray.PointAt (intersectPlane (plane, ray));
            return true;
        }

        static double intersectPlane (Plane plane, Ray3d ray)
        {
            return Vector3d.Multiply (plane.Origin - ray.Position, plane.Normal) / Vector3d.Multiply (plane.Normal, ray.Direction);
        }
    }


    #region Ray-AABB

    /// <summary>
    /// same as _RayBoxIntersection but returns the midpoint of the ray line trimmed by the bounding box
    /// </summary>
    static double _RayBoxIntersectionCenter (double[] r_origin, double[] r_dir_inv, BoundingBox b)
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
    static double _RayBoxIntersection (double[] r_origin, double[] r_dir_inv, BoundingBox b)
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

    #endregion
}


class MouseListener : MouseCallback
{
    protected override void OnMouseDown (MouseCallbackEventArgs e)
    {
        if (e.MouseButton != MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown || e.View.ActiveViewport.IsPlanView) return;

        Main.StartRotation (e.View.ActiveViewport, e.ViewportPoint);
    }

    protected override void OnEndMouseUp (MouseCallbackEventArgs e)
    {
        Main.StopRotation ();
    }
}


class IntersectionConduit : DisplayConduit
{
    public Guid ViewportFilter { get; set; }
    // public bool InRotation { get; set; } = false;

    protected override void CalculateBoundingBox (CalculateBoundingBoxEventArgs e)
    {
        e.BoundingBox.Union (Main.g_intersector.Rayline.Position);
        e.BoundingBox.Union (Main.g_intersector.Rayline.PointAt (1));
        e.IncludeBoundingBox (Main.g_intersector.VisibleBBox);
    }

    protected override void DrawForeground (DrawEventArgs e)
    {
        // if (InRotation == false || e.Viewport.Id != ViewportFilter) return;
        e.Display.DrawPoint (Main.g_intersector.Point, PointStyle.RoundActivePoint, 3, SD.Color.Black);

        if (Main.Debug == false) return;

        // e.Display.DrawLine (Main.Intersector.Rayline, SD.Color.BlueViolet);
        e.Display.DrawLine (
            Main.g_intersector.Rayline.Position,
            Main.g_intersector.Rayline.Position + Main.g_intersector.Rayline.Direction,
            SD.Color.BlueViolet
        );

        switch (Main.g_intersector.IntersectionStatus)
        {
        // case IntersectionStatus.None: break;
        // case IntersectionStatus.OnMesh: break;
        case IntersectionStatus.Outside:
            e.Display.DrawBox (Main.g_intersector.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            e.Display.DrawConstructionPlane (new Rhino.DocObjects.ConstructionPlane
            {
                Plane = Main.g_intersector.FrustumFrontPlane,
                ShowGrid = false
            });
            break;
        case IntersectionStatus.OnBBox:
            e.Display.DrawBox (Main.g_intersector.ActiveBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;
        case IntersectionStatus.OnVisibleBBox:
            e.Display.DrawBox (Main.g_intersector.VisibleBBox, SD.Color.FromArgb (125, 255, 0, 0));
            break;
        }
    }
}


#if USE_BITMAP

class IdConduit : DisplayConduit
{
    static IdConduit? g_instance;
    static DisplayModeDescription? g_viewmode;

    public static void Dispose ()
    {
        if (g_instance == null) return;
        g_instance.Enabled = false;
        g_instance = null;
        g_viewmode = null;
    }

    public static int PickIndex (RhinoView view, IEnumerable <Mesh> meshes, SD.Point viewportPoint)
    {
        if (g_instance == null) 
            g_instance = new ();

        if (g_viewmode == null)
        {
            g_viewmode = DisplayModeDescription.FindByName ("IdConduit_BlankMode"); // IdConduit_BlankMode
            if (g_viewmode == null) {
                g_viewmode = DisplayModeDescription.GetDisplayMode (
                    DisplayModeDescription.ImportFromFile (
                        System.IO.Path.Combine (typeof (IdConduit).Assembly.Location, "viewmode.ini")
                    )
                );
            }
        }

        g_instance.Meshes = meshes;
        g_instance.Enabled = true;
        var bitmap = view.CaptureToBitmap (g_viewmode);
        g_instance.Enabled = false;

        var index = unchecked (bitmap.GetPixel (viewportPoint.X, viewportPoint.Y).ToArgb () & 0xFFFFFF);
        return index == 0xFFFFFF ? -1 : index-1;
    }

    public IEnumerable <Mesh>? Meshes;

    protected override void DrawOverlay(DrawEventArgs e)
    {
        if (Meshes == null) return;

        // Argb(255, 0, 0, 0) to Argb(255, 255, 255, 255)
        // (int)0xFF000000 to (int)0xFFFFFFFF

        var i = 1;
        foreach (var m in Meshes)
        {
            var c = unchecked(0xFF000000 | i);
            e.Display.DrawMeshShaded (m, new DisplayMaterial (SD.Color.FromArgb ((int)c)));
            i++;
        }
    }
}

#endif

