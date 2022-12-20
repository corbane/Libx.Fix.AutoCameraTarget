#define WIN32

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using SD = System.Drawing;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using Rhino.Geometry;
using RP = Rhino.PlugIns;
using RC = Rhino.Commands;
using RD = Rhino.Display;
using RI = Rhino.Input.Custom;
using RUI = Rhino.UI;
using RhinoDoc = Rhino.RhinoDoc;
using RhinoApp = Rhino.RhinoApp;


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
[assembly: RP.PlugInDescription (RP.DescriptionType.Icon, "Libx.Fix.AutoCameraTarget.EmbeddedResources.RotateArround.ico")]


namespace Libx.Fix.AutoCameraTarget;


public class EntryPoint : RP.PlugIn { /**/ }


public class AutoCameraTargetCommand : RC.Command
{
    public static string Name => "ToggleAutoCameraTarget";
    public override string EnglishName => Name;

    protected override RC.Result RunCommand (RhinoDoc doc, RC.RunMode mode)
    {
        return Main.Instance.RunToggleCommand (doc);
    }

}


static class Resources
{
    static Stream _Get (string ressource) => typeof (Resources).Assembly.GetManifestResourceStream (ressource);
    static string _rscpath = "Libx.Fix.AutoCameraTarget.EmbeddedResources.";
    
    static SD.Bitmap? _hand;
    static SD.Bitmap? _zoom;
    static SD.Bitmap? _rotatation;

    public static SD.Bitmap HandBitmap            => _hand ??= new SD.Bitmap (_Get (_rscpath + "Hand.png"));
    public static SD.Bitmap MagnifyingGlassBitmap => _zoom ??= new SD.Bitmap (_Get (_rscpath + "MagnifyingGlass.png"));
    public static SD.Bitmap RotationBitmap        => _rotatation ??= new SD.Bitmap (_Get (_rscpath + "Rotation.png"));
}

#else

static class Resources
{
    static string _ressourceDiectory = @"E:\Projet\Rhino\Libx\Libx.Fix.AutoCameraTarget\EmbeddedResources";
    // static string _ressourceDiectory = Path.Combine (Path.GetDirectoryName (typeof (Resources).Assembly.Location), "..", "EmbeddedResources");
    
    static SD.Bitmap? _hand;
    static SD.Bitmap? _zoom;
    static SD.Bitmap? _rotatation;

    public static SD.Bitmap HandBitmap            => _hand ??= new (Path.Combine (_ressourceDiectory, "Hand.png"));
    public static SD.Bitmap MagnifyingGlassBitmap => _zoom ??= new (Path.Combine (_ressourceDiectory, "MagnifyingGlass.png"));
    public static SD.Bitmap RotationBitmap        => _rotatation ??= new (Path.Combine (_ressourceDiectory, "Rotation.png"));
}

#endif


public class Main
{
    static Main? g_instance;
    public static Main Instance => g_instance ??= new ();

    NavigationListener _listener;
    NavigationController _navigator;

    Main ()
    {
        _navigator = new ();
        _listener = new (_navigator);
    }

    public bool Active
    {
        get => _listener.Enabled;
        set { _listener.Enabled = value; }
    }

    public bool Marker
    {
        get => _navigator.Data.ShowMarker;
        set { _navigator.Data.ShowMarker = value; }
    }

    public bool Debug
    {
        get => _navigator.Data.Debug;
        set { _navigator.Data.Debug = value; }
    }

    public RC.Result RunToggleCommand (RhinoDoc doc)
    {
        var go = new RI.GetOption ();
        go.SetCommandPrompt ("Toggle auto camera target");

        var active = new RI.OptionToggle (false, "No", "Yes") { CurrentValue = Active };
        var marker = new RI.OptionToggle (false, "No", "Yes") { CurrentValue = Marker };
        var debug = new RI.OptionToggle (false, "No", "Yes") { CurrentValue = Debug };

        for (; ; )
        {
            go.ClearCommandOptions ();
            go.AddOptionToggle ("active", ref active);
            if (active.CurrentValue) go.AddOptionToggle ("marker", ref marker);
            if (active.CurrentValue && Marker) go.AddOptionToggle ("debug", ref debug);

            var ret = go.Get ();
            if (ret == Rhino.Input.GetResult.Option)
            {
                marker.CurrentValue = active.CurrentValue && marker.CurrentValue;
                if (marker.CurrentValue != Marker)
                {
                    Marker = marker.CurrentValue;
                    doc.Views.Redraw ();
                }

                Debug = marker.CurrentValue && debug.CurrentValue;

                continue;
            }

            Active = active.CurrentValue;

            return ret == Rhino.Input.GetResult.Cancel
                 ? RC.Result.Cancel
                 : RC.Result.Success;

        }
    }
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

    public bool ShowMarker = false;
    public bool Debug;


    /*/ Cimputed properties /*/


    /// <summary>
    /// Type of intersection found.
    /// </summary>
    public IntersectionStatus Status;

    /// <summary>
    /// The line under the mouse pointer to the far plane Frustum
    /// </summary>
    public Ray3d Rayline;

    /// <summary>
    /// Plan in front of the camera at the center of VisibleBBox.
    /// </summary>
    public Plane FrustumFrontPlane;


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
    public BoundingBox ActiveBBox;

    /// <summary>
    /// /// Box containing the objects visible in the viewport.
    /// </summary>
    public BoundingBox VisibleBBox;

    public BoundingBox InfoBBox;

    /// <summary>
    /// The target point of the camera
    /// </summary>
    public Point3d TargetPoint;

    public Point3d GetCameraFrontTargetPoint ()
    { // Same as _data.GetFrustrumNearPoint (_data.ViewportPoint)

        Viewport.GetFrustumNearPlane (out var nearPlane);

        var shootLine = new Line(Viewport.CameraLocation, TargetPoint);
        Rhino.Geometry.Intersect.Intersection.LinePlane (shootLine, nearPlane, out var t);
        return Viewport.IsParallelProjection
                ? nearPlane.ClosestPoint (TargetPoint)
                : shootLine.PointAt (t);
    }

    public Point3d GetCameraFrontPoint (SD.Point clientPoint)
    {
        // Ca c'est le point 3d sur FrustumNearPlane
        var s2w = Viewport.GetTransform (Rhino.DocObjects.CoordinateSystem.Screen, Rhino.DocObjects.CoordinateSystem.World);
        var point = new Point3d (clientPoint.X, clientPoint.Y, 0);
        point.Transform (s2w);
        return point;
    }

    public Vector3d GetCameraFrontVector (SD.Point clientPoint)
    {
        return GetCameraFrontPoint (clientPoint) - GetCameraFrontPoint (ViewportPoint);
    }
}


static class Intersector
{
    readonly static Rhino.DocObjects.ObjectEnumeratorSettings _enumeratorSettings = new ()
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

    readonly static List<Mesh> _usedMeshes = new ();

    static System.Diagnostics.Stopwatch? _sw;

    static Ray3d _GetMouseRay (RD.RhinoViewport vp)
    {
        var mp = RUI.MouseCursor.Location;
        var pt = vp.ScreenToClient (new SD.Point ((int)mp.X, (int)mp.Y));
        vp.GetFrustumLine (pt.X, pt.Y, out var line);

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new Ray3d (line.To, line.From - line.To);
    }

    public static void Compute (IntersectionData data, Point3d? defaultTargetPoint = null)
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
        BoundingBox bbox;
        BoundingBox activebbox = BoundingBox.Unset;
        BoundingBox visiblebbox = BoundingBox.Unset;
        double t;
        double tbboxmin = 1.1;
        var arg = new Rhino.DocObjects.RhinoObject[1];

        if (data.Viewport.ParentView.Document.Objects.Count < _usedMeshes.Capacity)
            _usedMeshes.Capacity = data.Viewport.ParentView.Document.Objects.Count;

        _enumeratorSettings.ViewportFilter = data.Viewport;
        foreach (var obj in data.Viewport.ParentView.Document.Objects.GetObjectList (_enumeratorSettings))
        {
            bbox = obj.Geometry.GetBoundingBox (accurate: false);
            if (bbox.IsValid == false) continue;

            t = _RayBoxIntersection (rayPos, rayInvDir, bbox);
            if (t < 0)
            {
                if (data.Viewport.IsVisible (bbox)) visiblebbox.Union (bbox);
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
            _usedMeshes.AddRange (from oref in Rhino.DocObjects.RhinoObject.GetRenderMeshes (arg, true, false)
                                  let m = oref.Mesh ()
                                  where m != null
                                  select m);
        }

        data.ObjectCount = total;

#if DEBUG
        data.Complexity = _usedMeshes.Sum (m => m.Faces.Count);
#endif

        data.InfoBBox = new BoundingBox (ray.Position, ray.Position + ray.Direction);
        if (visiblebbox.IsValid) data.InfoBBox.Union (visiblebbox);

#if USE_BITMAP
        // test the intersections with the meshes.
        int mindex = _usedMeshes.Count == 0 ? -1 : IdConduit.PickIndex (data.Viewport, _usedMeshes, data.ViewportPoint);

        // Has an intersection with a mesh been found ?
        if (mindex < 0 || mindex >= _usedMeshes.Count)
            _usedMeshes.Clear ();
        else {
            t = Rhino.Geometry.Intersect.Intersection.MeshRay (_usedMeshes[mindex], ray);
            _usedMeshes.Clear ();
            data.Status = IntersectionStatus.OnMesh;
            data.TargetPoint = ray.PointAt (t);
        }
#else
        // test the intersections with the meshes.
        double tmin = double.MaxValue;
        System.Threading.Tasks.Parallel.For (0, _usedMeshes.Count, (int i) =>
        {
            var t = Rhino.Geometry.Intersect.Intersection.MeshRay (_usedMeshes[i], ray);
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
#endif

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
            data.TargetPoint = defaultTargetPoint ?? Point3d.Unset;
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
    static double _RayBoxIntersection (double[] r_origin, double[] r_dir_inv, BoundingBox b)
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


class InfoConduit : RD.DisplayConduit
{
    static InfoConduit? g_instance;

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

    InfoConduit (IntersectionData data)
    {
        _data = data;
        SpaceFilter = Rhino.DocObjects.ActiveSpace.ModelSpace;
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

        if (_data.Debug == false) return;

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


███    ██  █████  ██    ██ ██  ██████   █████  ████████ ██  ██████  ███    ██ 
████   ██ ██   ██ ██    ██ ██ ██       ██   ██    ██    ██ ██    ██ ████   ██ 
██ ██  ██ ███████ ██    ██ ██ ██   ███ ███████    ██    ██ ██    ██ ██ ██  ██ 
██  ██ ██ ██   ██  ██  ██  ██ ██    ██ ██   ██    ██    ██ ██    ██ ██  ██ ██ 
██   ████ ██   ██   ████   ██  ██████  ██   ██    ██    ██  ██████  ██   ████ 

/*/


struct Vec
{
    public int X;
    public int Y;

    public Vec (int x, int y) { X = x; Y = y; }
    public Vec (SD.Point point) { X = point.X; Y = point.Y; }

    public Vec Copy () { return new (X, Y); }

    public Vec Set (int value)      { X = value; Y = value; return this; }
    public Vec Set (int x, int y)   { X = x; Y = y; return this; }
    public Vec Set (Vec point)    { X = point.X; Y = point.Y; return this; }
    public Vec Set (SD.Point point) { X = point.X; Y = point.Y; return this; }

    public Vec Add (Vec point)    { X += point.X; Y += point.Y; return this; }
    public Vec Add (SD.Point point) { X += point.X; Y += point.Y; return this; }

    public Vec Substract (Vec point)    { X -= point.X; Y -= point.Y; return this; }
    public Vec Substract (SD.Point point) { X -= point.X; Y -= point.Y; return this; }

    public static Vec operator +(Vec a, Vec b) { return new Vec(a.X + b.X, a.Y + b.Y); }

    public bool Is (int value) {return X == 0 && Y == 0; }

    public override string ToString () { return $"[{X}, {Y}]"; }
}


enum NavigationMode
{
    Rotate,
    Translate,
    Zoom
}


class NavigationCallback
{
    public virtual bool OnStartNavigation (RD.RhinoViewport viewport, Vec viewportPoint) { return true; }
    public virtual bool OnNavigationModeChange (NavigationMode mode) { return true; }
    public virtual void OnRotation (Vec offset) { /**/ }
    public virtual void OnZoom (Vec offset) { /**/ }
    public virtual void OnTranslation (Vec offset) { /**/ }
    public virtual void OnStopNavigation () { /**/ }
}


/// <summary>
/// Wrapper class above <see cref="MouseListener"/>
/// </summary>
class NavigationListener : RUI.MouseCallback
{
    NavigationCallback _callbacks;
    MouseListener _listener;

    public NavigationListener (NavigationCallback callback)
    {
        _callbacks = callback;
        _listener = new MouseListener ();
    }

    protected override void OnEndMouseDown (RUI.MouseCallbackEventArgs e)
    {
        // if (e.MouseButton != RUI.MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown || e.View.ActiveViewport.IsPlanView) return;
        if (e.MouseButton != RUI.MouseButton.Right || e.View.ActiveViewport.IsPlanView) return;

        _listener.Start (e, _callbacks);
    }
}


static class NavigationCursor
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
    static Vec _initiaCursorPos;

    static Vec _vCursorOffset;

    static SD.Rectangle _clientArea;

    /// <summary>
    /// Cursor position with <see cref="NavigationMode.Translate"/> offsets.
    /// </summary>
    public static Vec VirtualCursorOffset => _vCursorOffset;

    public static Vec InitialCursorPosition => _initiaCursorPos;

    public static Vec VirtualCursorPosition => _initiaCursorPos + _vCursorOffset;

    public static void InitCursor (RD.RhinoViewport viewport, Vec position)
    {
        _initiaCursorPos = position.Copy ();
        _clientArea = viewport.ParentView.ScreenRectangle;
        _vCursorOffset = new (0,0);
    }

    public static void SetCursorPosition (Vec pos)
    {
        EF.Mouse.Position = new (_clientArea.X + pos.X, _clientArea.Y + pos.Y);
    }

    public static void SetLimitedCursorPosition (int X, int Y)
    {
        X = X < 0 ? 0 : X > _clientArea.Width ? _clientArea.Width : X;
        Y = Y < 0 ? 0 : Y > _clientArea.Height ? _clientArea.Height : Y;
        EF.Mouse.Position = new (_clientArea.X + X, _clientArea.Y + Y);
    }

    public static void GrowVirtualCursorPosition (SD.Point point)
    {
        _vCursorOffset.X += point.X - _initiaCursorPos.X;
        _vCursorOffset.Y += point.Y - _initiaCursorPos.Y;
    }
}


class MouseListener : RUI.MouseCallback
{
    const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;
    const int PAUSE_DELAY = 70;

    #nullable disable
    public MouseListener () { /*_callbacks & Viewport*/ } 
    #nullable enable

    NavigationCallback _callbacks;
    public RD.RhinoViewport Viewport { get; private set; }

    NavigationMode _mode;
    Vec _offset;

    public NavigationMode NavigationMode => _mode;

    #region Preview

    NavigationConduit? _conduit;

    [MethodImpl(INLINE)]
    void _StartPreview ()
    {
        (_conduit ??= new (this)).Enabled = true;
    }

    [MethodImpl(INLINE)]
    void _StopPreview()
    {
        if (_conduit != null) _conduit.Enabled = false;
    }

    #endregion

    #region Switches

    /// <summary>
    /// Flag to cancel or not the MouseUp event.
    /// </summary>
    bool _started;
    
    /// <summary>
    /// Flag blocking `OnMouseMove` event after `EF.Mouse.Position=...`
    /// </summary>
    bool _lock;

    bool _pause;

    void _StartPause ()
    {
        _pause = true;
        System.Threading.Tasks.Task.Delay (PAUSE_DELAY).ContinueWith ((_) => { _pause = false; });
    }

    #endregion

    System.Windows.Forms.Cursor _ccur;
    public void Start (RUI.MouseCallbackEventArgs e, NavigationCallback callbacks)
    {
        Viewport   = e.View.ActiveViewport;
        _callbacks = callbacks;
        _started   = false;
        _lock      = false;
        Enabled    = true;
    }

    protected override void OnMouseMove (RUI.MouseCallbackEventArgs e)
    {
        if (_started == false) {
            _started = true;
                
            var origin = new Vec (e.ViewportPoint);

            Enabled = _callbacks.OnStartNavigation (Viewport, origin);
            if (Enabled) e.Cancel = true;
            else return;

            _mode        = NavigationMode.Rotate;
            _offset.Set (0);

            NavigationCursor.InitCursor (Viewport, origin);
            NavigationCursor.HideCursor ();

            _StartPreview ();
            
            return;
        }

        e.Cancel = true;

        if (_lock) {
            _lock = false;
            return;
        }

        if (_pause) {
            _lock = true;
            NavigationCursor.SetCursorPosition (NavigationCursor.InitialCursorPosition);
            return;
        }

        if (NavigationCursor.InitialCursorPosition.Is (0)) {
            return;
        }

        _offset = _offset.Add (e.ViewportPoint).Substract (NavigationCursor.InitialCursorPosition);

        if (EF.Keyboard.Modifiers == EF.Keys.Alt)
        {
            RhinoApp.WriteLine ("ALT");
        }

        if (e.ShiftKeyDown) // Pan
        {
            if (_mode == NavigationMode.Translate)
            {
                _callbacks.OnTranslation (_offset);
                NavigationCursor.GrowVirtualCursorPosition (e.ViewportPoint);
            }
            else if (_callbacks.OnNavigationModeChange (_mode))
            {
                _mode = NavigationMode.Translate;
                _offset.Set (0);
                _StartPause ();
            }
        }
        else if (e.CtrlKeyDown) // Zoom
        {
            if (_mode == NavigationMode.Zoom)
            {
                _callbacks.OnZoom (_offset);
            }
            else if (_callbacks.OnNavigationModeChange (_mode))
            {
                _mode = NavigationMode.Zoom;
                _offset.Set (0);
                _StartPause ();
            }
        }
        else // Rotate
        {
            if (_mode == NavigationMode.Rotate)
            {
                _callbacks.OnRotation (_offset);
            }
            else if (_callbacks.OnNavigationModeChange (_mode))
            {
                _mode = NavigationMode.Rotate;
                _offset.Set (0);
                _StartPause ();
            }
        }

        _lock = true;
        NavigationCursor.SetCursorPosition (NavigationCursor.InitialCursorPosition);
    }

    protected override void OnMouseUp (RUI.MouseCallbackEventArgs e)
    {
        Enabled = false;
        _StopPreview ();
        
        if (_started)
        {
            e.Cancel = true;
            
            var pos = NavigationCursor.InitialCursorPosition + NavigationCursor.VirtualCursorOffset;
            NavigationCursor.SetLimitedCursorPosition (pos.X, pos.Y);
            _callbacks.OnStopNavigation ();

        }
        NavigationCursor.ShowCursor ();
    }
}


class NavigationConduit : RD.DisplayConduit
{
    MouseListener _listener;
    SD.Rectangle _clientRect;
    RD.DisplayBitmap _tIco;
    RD.DisplayBitmap _zIco;
    RD.DisplayBitmap _rIco;

    public NavigationConduit (MouseListener listener)
    {
        _listener = listener;
        _clientRect = listener.Viewport.ParentView.ScreenRectangle;
        _tIco = new (Resources.HandBitmap);
        _zIco = new (Resources.MagnifyingGlassBitmap);
        _rIco = new (Resources.RotationBitmap);
    }

    // Il n'est pas possible avec DisplayConduit de dessiner au dessus des objets sélectionnés et le Gumball.
    protected override void DrawForeground(RD.DrawEventArgs e)
    {
        var pos = NavigationCursor.VirtualCursorPosition;
        switch (_listener.NavigationMode)
        {
        case NavigationMode.Rotate    : e.Display.DrawBitmap (_rIco, pos.X-10, pos.Y-10); break;
        case NavigationMode.Translate : e.Display.DrawBitmap (_tIco, pos.X-10, pos.Y-10); break;
        case NavigationMode.Zoom      : e.Display.DrawBitmap (_zIco, pos.X-10, pos.Y-10); break;
        }
    }
}


class NavigationController : NavigationCallback
{
    public readonly IntersectionData Data;
    Rhino.DocObjects.ViewportInfo _vpInfo;
    Point3d _initialLocation;
    Vector3d _initialDirection;
    Vector3d _initiallUp;
    Vector3d _initialX;
    Interval _initialSizeX;
    Interval _initialSizeY;
    
    // Il n'est pas possible d'utiliser le cumul des décalages car en vue perspective,
    // entre deux mouvements XYZ, le zoom (et donc `_w2sScale`) a pus changer.
    Transform _cumulTransformation;
    double _cumulRotZ;
    double _cumulRotX;

    public double RotationZ;
    public double RotationX;
    public double TranslationX;
    public double TranslationY;
    public double ZoomFactor;
    double _w2sScale;
    
    #nullable disable //_vpInfo
    public NavigationController ()
    {
        Data = new ();
    }
    #nullable enable

    public override bool OnStartNavigation (RD.RhinoViewport viewport, Vec viewportPoint)
    {
        // Ne fonctionne actuellement pas avec une projection à deux point
        if (viewport.IsTwoPointPerspectiveProjection) return false;

        Data.Viewport = viewport;
        Data.ViewportPoint = new SD.Point (viewportPoint.X, viewportPoint.Y);

        _vpInfo = new (viewport);
        _vpInfo.GetFrustum (out var left, out var right, out var bottom, out var top, out var near, out var far);
        _initialSizeX     = new Interval (left, right);
        _initialSizeY     = new Interval (top, bottom);
        _initialLocation  = new Point3d (viewport.CameraLocation);
        _initialDirection = new Vector3d (viewport.CameraDirection);
        _initiallUp       = new Vector3d (viewport.CameraUp);
        _initialX         = new Vector3d (viewport.CameraX);
        
        _cumulTransformation = Transform.Identity;
        RotationZ    = _cumulRotX = 0;
        RotationX    = _cumulRotZ = 0;
        ZoomFactor   = 1;
        TranslationX = 0;
        TranslationY = 0;

        Data.Viewport.GetWorldToScreenScale (Data.TargetPoint, out _w2sScale);

        if (Data.Debug)
        {
            Intersector.StartPerformenceLog ();
            Intersector.Compute (Data, viewport.CameraTarget);
            Intersector.StopPerformenceLog (Data);
        }
        else
        {
            Intersector.Compute (Data, viewport.CameraTarget);
        }

        // if (Data.Status == IntersectionStatus.None) 
        //     return false;

        if (Data.ShowMarker) InfoConduit.Show (Data);
        return true;
    }

    public override bool OnNavigationModeChange (NavigationMode mode)
    {
        Data.Viewport.GetWorldToScreenScale (Data.TargetPoint, out _w2sScale);

        _cumulTransformation = GetPositionTransform ();

        TranslationX = 0;
        TranslationY = 0;

        ZoomFactor = 1;

        _cumulRotZ += RotationZ;
        _cumulRotX += RotationX;
        RotationZ = 0;
        RotationX = 0;
        
        _vpInfo.GetFrustum (out var left, out var right, out var bottom, out var top, out var near, out var far);
        _initialSizeX = new Interval (left, right);
        _initialSizeY = new Interval (top, bottom);

        return true;
    }

    public Transform GetRotationTransfom ()
    {
        return (
            Transform.Identity
            * Transform.Rotation (_cumulRotZ + RotationZ, Vector3d.ZAxis, Data.TargetPoint)
            * Transform.Rotation (_cumulRotX + RotationX, _initialX, Data.TargetPoint)
        );
    }

    public Transform GetPositionTransform ()
    {
        return (
            Transform.Identity
            * Transform.Translation (-(_initialX*(TranslationX)) + _initiallUp*(TranslationY))
            * Transform.Scale (Data.TargetPoint, ZoomFactor)
            * _cumulTransformation
        );
    }

    public void ApplyChanges ()
    {
        // RhinoApp.WriteLine (
        //     "tX " + TranslationX.ToString("F3") +", tY "+TranslationY.ToString("F3") + 
        //     ", rZ "+ RotationZ.ToString("F3") + ", rX " + RotationX.ToString("F3") + 
        //     ", z " + ZoomFactor.ToString ("F3")
        // );
        
        var pos = new Point3d (_initialLocation);
        var dir = new Vector3d (_initialDirection);
        var up = new Vector3d (_initiallUp);
        
        var t = GetRotationTransfom ();
        pos.Transform (GetPositionTransform ());
        pos.Transform (t);
        dir.Transform (t);
        up.Transform (t);

        var vp = Data.Viewport;

        _vpInfo.SetCameraDirection (dir);
        _vpInfo.SetCameraLocation (pos);
        _vpInfo.SetCameraUp (up);
        vp.SetCameraTarget (Data.TargetPoint, updateCameraLocation: false);

        if (_vpInfo.IsParallelProjection)
        {
            _vpInfo.SetFrustum (
                _initialSizeX.T0 * ZoomFactor,
                _initialSizeX.T1 * ZoomFactor,
                _initialSizeY.T1 * ZoomFactor,
                _initialSizeY.T0 * ZoomFactor,
                _vpInfo.FrustumNear,
                _vpInfo.FrustumFar
            );
        }
        
        vp.SetViewProjection (_vpInfo, updateTargetLocation: false);
        vp.ParentView.Redraw ();
    }

    public override void OnRotation (Vec offset)
    {
        RotationZ = -Math.PI * offset.X / 300;
        RotationX = -Math.PI * offset.Y / 300;
        ApplyChanges ();
        Data.Viewport.ParentView.Document.Views.Redraw ();
    }

    public override void OnZoom (Vec offset)
    {
        ZoomFactor =  1 + ((double)offset.Y) / 300;
        if (ZoomFactor < 0.001) ZoomFactor = 0.001;
        ApplyChanges ();
    }

    public override void OnTranslation (Vec offset)
    {
        TranslationX = offset.X / _w2sScale;
        TranslationY = offset.Y / _w2sScale;
        ApplyChanges ();
    }

    public override void OnStopNavigation ()
    {
        InfoConduit.Hide ();
        Data.Viewport.ParentView.Redraw ();
    }
}


/*/

 ██████  ██████  ███████  ██████  ██      ███████ ████████ ███████ 
██    ██ ██   ██ ██      ██    ██ ██      ██         ██    ██      
██    ██ ██████  ███████ ██    ██ ██      █████      ██    █████   
██    ██ ██   ██      ██ ██    ██ ██      ██         ██    ██      
 ██████  ██████  ███████  ██████  ███████ ███████    ██    ███████ 

/*/


#if USE_BITMAP

class IdConduit : RD.DisplayConduit
{
    static IdConduit? g_instance;
    static RD.DisplayModeDescription? g_viewmode;

    public static void Dispose ()
    {
        if (g_instance == null) return;
        g_instance.Enabled = false;
        g_instance = null;
        g_viewmode = null;
    }

    public static int PickIndex (RD.RhinoViewport viewport, IEnumerable <Mesh> meshes, SD.Point viewportPoint)
    {
        if (g_instance == null) 
            g_instance = new ();

        if (g_viewmode == null)
        {
            g_viewmode = RD.DisplayModeDescription.FindByName ("IdConduit_BlankMode"); // IdConduit_BlankMode
            if (g_viewmode == null) {
                g_viewmode = RD.DisplayModeDescription.GetDisplayMode (
                    RD.DisplayModeDescription.ImportFromFile (
                        System.IO.Path.Combine (typeof (IdConduit).Assembly.Location, "viewmode.ini")
                    )
                );
            }
        }

        g_instance.Meshes = meshes;
        g_instance.Enabled = true;
        var bitmap = viewport.ParentView.CaptureToBitmap (g_viewmode);
        g_instance.Enabled = false;

        var index = unchecked (bitmap.GetPixel (viewportPoint.X, viewportPoint.Y).ToArgb () & 0xFFFFFF);
        return index == 0xFFFFFF ? -1 : index-1;
    }

    public IEnumerable <Mesh>? Meshes;

    IdConduit () { SpaceFilter = Rhino.DocObjects.ActiveSpace.ModelSpace; }

    protected override void DrawOverlay(RD.DrawEventArgs e)
    {
        if (Meshes == null) return;

        // Argb(255, 0, 0, 0) to Argb(255, 255, 255, 255)
        // 0xFF000000 to 0xFFFFFFFF

        var i = 1;
        foreach (var m in Meshes)
        {
            var c = unchecked(0xFF000000 | i);
            e.Display.DrawMeshShaded (m, new RD.DisplayMaterial (SD.Color.FromArgb ((int)c)));
            i++;
        }
    }
}

#endif

#if false

class GetRhinoMouseOffset
// Obsoléte !
// Pas de chance, aprés avoir trouvé une solution pour optenir la décalage de la souris l'orsqu'elle peut passer d'un bord à l'autre,
// (ce qui est le comportement de la souris dans Rhino pendant une rotation)
// j'ai pu constater que l'appelle à `MouseCallbackEventArgs.Cancel = true` annulais cet effet.
// Mais c'était tellement ... que je garde le code.
{
    SD.Point _startPoint;
    SD.Point _lastPoint;
    SD.Point _offsetPoint;
    SD.Rectangle _viewportBounds;

    int _lastDirX;
    int _lastSideX;
    int _loopX;
    int _lastDirY;
    int _lastSideY;
    int _loopY;

    public void Start (RD.RhinoViewport viewport, SD.Point viewportPoint)
    {
        _startPoint = viewportPoint;
        _lastPoint.X = viewportPoint.X;
        _lastPoint.Y = viewportPoint.Y;
        _offsetPoint.X = 0;
        _offsetPoint.Y = 0;
        _viewportBounds = viewport.Bounds;

        _lastDirX = 0;
        _lastSideX = 1;
        _loopX = 0;
        _lastDirY = 0;
        _lastSideY = 1;
        _loopY = 0;
    }

    public void Compute (SD.Point currentPoint)
    {
        var currentSideX = currentPoint.X < _startPoint.X ? -1
                         : currentPoint.X > _startPoint.X ? 1
                         : 0;

        if (_lastDirX != _lastSideX)
        {
            _lastDirX = _lastPoint.X < currentPoint.X ? 1 : -1;
        }
        else if (currentSideX < 0 && _startPoint.X <= _lastPoint.X)
        {
            //RhinoApp.WriteLine ("flip to left");
            _loopX++;
            _lastDirX = 1;
        }
        else if (0 < currentSideX && _lastPoint.X <= _startPoint.X)
        {
            //RhinoApp.WriteLine ("flip to right");
            _loopX--;
            _lastDirX = -1;
        }
        else
        {
            _lastDirX = _lastPoint.X < currentPoint.X ? 1 : -1;
        }

        _offsetPoint.X = _viewportBounds.Width * _loopX + (currentPoint.X - _startPoint.X);
        _lastSideX = currentSideX;
        _lastPoint.X = currentPoint.X;
        //RhinoApp.WriteLine ($" side: {(_lastSideX < 0 ? "-1" : "+1")} dirX: {(_lastDirX < 0 ? "-1" : "+1")} loopX: {_loopX} offsetX: { _offsetPoint.X }");


        var currentSideY = currentPoint.Y < _startPoint.Y ? -1
                         : currentPoint.Y > _startPoint.Y ? 1
                         : 0;

        if (_lastDirY != _lastSideY)
        {
            _lastDirY = _lastPoint.Y < currentPoint.Y ? 1 : -1;
        }
        else if (currentSideY < 0 && _startPoint.Y <= _lastPoint.Y)
        {
            //RhinoApp.WriteLine ("flip to top");
            _loopY++;
            _lastDirY = 1;
        }
        else if (0 < currentSideY && _lastPoint.Y <= _startPoint.Y)
        {
            //RhinoApp.WriteLine ("flip to bottom");
            _loopY--;
            _lastDirY = -1;
        }
        else
        {
            _lastDirY = _lastPoint.Y < currentPoint.Y ? 1 : -1;
        }

        _offsetPoint.Y = _viewportBounds.Height * _loopY + (currentPoint.Y - _startPoint.Y);
        _lastSideY = currentSideY;
        _lastPoint.Y = currentPoint.Y;
        //RhinoApp.WriteLine ($" sideY: {(_lastSideY < 0 ? "-1" : "+1")} dirY: {(_lastDirY < 0 ? "-1" : "+1")} loopY: {_loopY} offsetY: { _offsetPoint.Y }");
    }
}

#endif

/*/
link:
- https://patorjk.com/software/taag/#p=display&f=ANSI%20Regular&t=camera
/*/