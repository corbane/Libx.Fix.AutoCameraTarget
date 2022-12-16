
using System;
using System.Collections.Generic;
using System.Linq;
using SD = System.Drawing;

using Rhino.Geometry;
using RP = Rhino.PlugIns;
using RC = Rhino.Commands;
using RD = Rhino.Display;
using RI = Rhino.Input.Custom;
using RUI = Rhino.UI;
using RhinoDoc = Rhino.RhinoDoc;
using RhinoApp = Rhino.RhinoApp;


#if RHP

[assembly: System.Runtime.InteropServices.Guid ("45d93b79-52d5-4ee8-bfba-ee4816bf0080")]

[assembly: RP.PlugInDescription (RP.DescriptionType.Country, "France")]
[assembly: RP.PlugInDescription (RP.DescriptionType.Organization, "Vrecq Jean-marie")]
[assembly: RP.PlugInDescription (RP.DescriptionType.WebSite, "https://github.com/corbane/Libx.Fix.AutoCameraTarget")]


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

#endif


interface IRotationCallback
{
    public bool OnStartRotation (RD.RhinoViewport viewport, SD.Point viewportPoint);
    public void OnRotation (int offsetX, int offsetY);
    public void OnStopRotation ();
}


/// <summary>
/// Wrapper class above <see cref="MouseListener"/>
/// </summary>
class MouseDownListener : RUI.MouseCallback
{
    IRotationCallback _callbacks;

    public MouseDownListener (IRotationCallback callback) { _callbacks = callback; }

    protected override void OnEndMouseDown (RUI.MouseCallbackEventArgs e)
    {
        if (e.MouseButton != RUI.MouseButton.Right || e.CtrlKeyDown || e.ShiftKeyDown || e.View.ActiveViewport.IsPlanView) return;

        MouseListener.Instance.Start (e.View.ActiveViewport, e.ViewportPoint, _callbacks);
    }
}


class MouseListener : RUI.MouseCallback
{
    static MouseListener? g_instance;
    public static MouseListener Instance => g_instance ??= new ();

    #nullable disable
    MouseListener () { /*private constructor*/ }
    #nullable enable

    /// <summary>
    /// Flag to cancel or not the MouseUp event.
    /// </summary>
    bool _inrotation;

    IRotationCallback _callbacks;
    RD.RhinoViewport _viewport;
    SD.Point _startPoint;

    public void Start (RD.RhinoViewport viewport, SD.Point viewportPoint, IRotationCallback callbacks)
    {
        _inrotation = false;
        _callbacks = callbacks;
        _viewport = viewport;
        _startPoint = viewportPoint;
        Enabled = true;
    }

    protected override void OnMouseMove (RUI.MouseCallbackEventArgs e)
    {
        if (e.View.ActiveViewportID != _viewport.Id) return;

        if (_inrotation == false)
        {
            _inrotation = true;
            Enabled = _callbacks.OnStartRotation (_viewport, _startPoint);
            if (Enabled) e.Cancel = true;
        }
        else
        {
            var offsetX = e.ViewportPoint.X - _startPoint.X;
            var offsetY = e.ViewportPoint.Y - _startPoint.Y;
            _callbacks.OnRotation (offsetX, offsetY);
            e.Cancel = true;
        }
    }

    protected override void OnMouseUp (RUI.MouseCallbackEventArgs e)
    {
        Enabled = false;
        if (_inrotation == false) return;
        _callbacks.OnStopRotation ();
        e.Cancel = true;
    }
}


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


public class Main : IRotationCallback
{
    static Main? g_instance;
    public static Main Instance => g_instance ??= new ();

    MouseDownListener _listener;

    Main ()
    {
        _listener = new (this);
        _data = new ();
    }

    public bool Active
    {
        get => _listener.Enabled;
        set { _listener.Enabled = value; }
    }
    public bool Marker { get; private set; } = true;
    public bool Debug
    {
        get => _data.Debug;
        set { _data.Debug = value; }
    }

    Plane _cameraPlane;
    IntersectionData _data;

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

    public bool OnStartRotation (RD.RhinoViewport viewport, SD.Point viewportPoint)
    {
        _data.Viewport = viewport;
        _data.ViewportPoint = viewportPoint;
        viewport.GetCameraFrame (out _cameraPlane);

        if (_data.Debug)
        {
            MouseIntersector.StartPerformenceLog ();
            MouseIntersector.UpdatePoint (ref _data);
            MouseIntersector.StopPerformenceLog (_data);
        }
        else
        {
            MouseIntersector.UpdatePoint (ref _data);
        }

        if (_data.Status == IntersectionStatus.None) 
            return false;

        viewport.SetCameraTarget (_data.TargetPoint, updateCameraLocation: false);
        if (Marker) InfoConduit.Show (_data);
        return true;
    }

    public void OnRotation (int offsetX, int offsetY)
    {
        var pos = new Point3d (_cameraPlane.Origin);
        var dir = new Vector3d (-_cameraPlane.ZAxis);
        var up = new Vector3d (_cameraPlane.YAxis);

        var t
            = Transform.Rotation (-Math.PI * offsetX / 400, Vector3d.ZAxis, _data.TargetPoint)
            * Transform.Rotation (-Math.PI * offsetY / 400, _cameraPlane.XAxis, _data.TargetPoint)
            ;

        pos.Transform (t);
        dir.Transform (t);
        up.Transform (t);

        _data.Viewport.SetCameraLocation (pos, true);
        _data.Viewport.SetCameraDirection (dir, true);
        _data.Viewport.CameraUp = up;
        _data.Viewport.ParentView.Redraw ();
    }

    public void OnStopRotation ()
    {
        InfoConduit.Hide ();
        _data.Viewport.ParentView.Redraw ();
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


class IntersectionData
{
#nullable disable
    public RD.RhinoViewport Viewport;
#nullable enable

    public SD.Point ViewportPoint;

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
}


static class MouseIntersector
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

    public static void UpdatePoint (ref IntersectionData data)
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
            data.TargetPoint = Point3d.Unset;
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

