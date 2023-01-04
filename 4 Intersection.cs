/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System.Collections.Generic;
using System.Linq;
using SD = System.Drawing;

using ON = Rhino.Geometry;
using RD = Rhino.Display;
using RO = Rhino.DocObjects;
using RhinoApp = Rhino.RhinoApp;


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


public partial interface INavigationSettings : IOptions
{
    public bool Marker { get; }
    public bool Debug { get; }
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


class IntersectionData
{
    #nullable disable
    public RD.RhinoViewport Viewport;
    #nullable enable

    public SD.Point ViewportPoint;

    public INavigationSettings Options { get; }

    public IntersectionData (INavigationSettings options)
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
    // TODO:
    // http://what-when-how.com/advanced-methods-in-computer-graphics/collision-detection-advanced-methods-in-computer-graphics-part-6/
    // https://discourse.mcneel.com/t/bvh-structure/152651/6

    #region Main Functions

    /// <summary>
    ///     Stores meshes whose bounding boxes collide with the mouse cursor. </summary>
    readonly static List<ON.Mesh> _usedMeshes = new ();

    /// <summary>
    ///     Gets the ray line under the mouse from the Frustum near plane to the far plane </summary>
    static ON.Ray3d _GetMouseRay (RD.RhinoViewport vp, SD.Point vpoint)
    {
		// vp.GetScreenPort(out var pl, out var _, out var _, out var pt, out var _, out var _);
        // vp.GetFrustumLine (vpoint.X - pl, vpoint.Y - pt, out var line);
        vp.GetFrustumLine (vpoint.X, vpoint.Y, out var line);

        // Visiblement GetFrustumLine retourne une line du point le plus loin au point le plus proche.
        return new ON.Ray3d (line.To, line.From - line.To);
    }
    
    /// <param name="data">
    ///     Intersection calculation data.
    ///     `Viewport` and `ViewportPoint` properties **MUST** be set, other properties will be filled by this function</param>
    /// <param name="defaultTargetPoint">
    ///     If no intersection is found, use this point as the target point</param>
    public static void Compute (IntersectionData data, ON.Point3d? defaultTargetPoint = null)
    {
        var ray = _GetMouseRay (data.Viewport, data.ViewportPoint);
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

        if (data.Viewport.ParentView.Document.Objects.Count < _usedMeshes.Capacity)
            _usedMeshes.Capacity = data.Viewport.ParentView.Document.Objects.Count;

        foreach (var (meshes, bbox) in Cache.Items)
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
        
            if (meshes != null)
                _usedMeshes.AddRange (meshes);
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

    #endregion

    #region Ray-AABB
    // peut être amélioré:
    // https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525

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

    protected override void DrawOverlay (RD.DrawEventArgs e)
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
