/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;
using System.Linq;
using SD = System.Drawing;

using ED = Eto.Drawing;

using ON = Rhino.Geometry;
using RD = Rhino.Display;
using RO = Rhino.DocObjects;
using RUI = Rhino.UI;
using RhinoDoc = Rhino.RhinoDoc;
using RhinoApp = Rhino.RhinoApp;


#if RHP

using Libx.Fix.AutoCameraTarget.Config;

namespace Libx.Fix.AutoCameraTarget.Views;

#endif


public interface ICameraSettings : ISettings
{
    bool ShowCamera {  get; }
}


/// <summary>
/// Provides utility functions to manipulate the camera in view. <br/>
/// Before each use, this class must be initialized with the 'Init' function
/// and the initial camera **MUST** be aligned with the horizon (i.e. the `RhinoViewport.CameraX.Z == 0`).
/// the calculation of transformations is optimized in a single pass in <see cref="_PanTurnMoveZoom"/>
/// and corresponds to the transformations:
/// <code>
///     Scale (camera-target * Zoom)
///     * Translation (Pos[X|Y|Z])    // Pos[X|Y|Z] is the target point.
///     * Rotation (RotZ, ZAxis)
///     * Rotation (RotX, XAxis)
///     * Translation (Pan[X|Y|Z])
/// </code>
/// </summary>
public class Camera
{
    #nullable disable
    RD.RhinoViewport _vp;
    RO.ViewportInfo _vpinfo;
    #nullable enable

    ON.Transform _m = ON.Transform.Identity;
    ON.Interval _initialSizeX;
    ON.Interval _initialSizeY;

    ON.Point3d _target;

    /// <summary> Offset along camera X axis. </summary>
    public double PanX;
    /// <summary> Offset along camera Y (Up) axis </summary>
    public double PanY;
    /// <summary> Offset along camera Z (Direction) axis </summary>
    public double PanZ;

    /// <summary> Rotation of the camera around the X axis.
    /// This rotation is performed before the Z rotation. </summary>
    public double RotX;
    /// <summary> Rotation of the camera around the Z axis.
    /// This rotation is performed after the X rotation. </summary>
    public double RotZ;

    /// <summary> Global X position of all transformations </summary>
    public double PosX;
    /// <summary> Global Y position of all transformations </summary>
    public double PosY;
    /// <summary> Global Z position of all transformations </summary>
    public double PosZ;

    double _zoom;
    double _zoomfactor;

    // TODO: Must be better.
    public double Zoom
    {
        get => _zoom;
        set {
            if (_vp.IsParallelProjection) {
                _zoomfactor = value/300;
                if (_zoomfactor > -1) _zoom = value;
                else _zoomfactor = -1;
            } else {
                _zoom = value;
            }
        }
    }

    /// <summary>
    ///     Initialize the class before using it </summary>
    /// <param name="target">
    ///     The target point of the camera. </param>
    public void Init (RD.RhinoViewport viewport, ON.Point3d target)
    {
        _vp = viewport;
        _vpinfo = new (viewport);
        _target = new (target);
        _vp.SetCameraTarget (_target, updateCameraLocation: false);
        Reset ();
    }

    /// <summary>
    ///     Recalculate the transforms property of this class from the viewport. </summary>
    public void Reset ()
    {
        var plane = new ON.Plane (_vp.CameraLocation, _vp.CameraX, _vp.CameraY);
        
        var cosZ = Math.Acos (plane.ZAxis.Z);
        var rx = plane.YAxis.Z < 0 ? -cosZ : cosZ;

        var cosY = Math.Acos (plane.XAxis.X); 
        var rz = plane.XAxis.Y < 0 ? -cosY : cosY;

        var ntarget = new ON.Point3d (_target);
        var nplane  = new ON.Plane (plane);

        ntarget.Transform (_TurnXZ (0, -rz));
        ntarget.Transform (_TurnXZ (-rx, 0));
        nplane.Transform (_TurnXZ (0, -rz));
        nplane.Transform (_TurnXZ (-rx, 0));

        PanX = nplane.OriginX - ntarget.X;
        PanY = nplane.OriginY - ntarget.Y;
        PanZ = nplane.OriginZ - ntarget.Z;
        RotX = rx;
        RotZ = rz;
        PosX = _target.X;
        PosY = _target.Y;
        PosZ = _target.Z;

        Zoom = 0;

        _vpinfo.GetFrustum (out var left, out var right, out var bottom, out var top, out var near, out var far);
        _initialSizeX  = new ON.Interval (left, right);
        _initialSizeY  = new ON.Interval (top, bottom);
        
        static ON.Transform _TurnXZ (double rotX, double rotZ)
        {
            var t = ON.Transform.Identity;
            var cosX = Math.Cos(rotX);
            var sinX = Math.Sin(rotX);
            var cosZ = Math.Cos(rotZ);
            var sinZ = Math.Sin(rotZ);

            // vector X
            t.M00 = cosZ;
            t.M10 = sinZ;
            t.M20 = 0d;

            // vector Y
            t.M01 = cosX * -sinZ;
            t.M11 = cosX * cosZ;
            t.M21 = sinX;

            // vector Z
            t.M02 = sinX * sinZ;
            t.M12 = -sinX * cosZ;
            t.M22 = cosX;

            return t;
        }
    }

    /// <summary>
    ///     Apply transformations but not redraw the view. </summary>
    public void ApplyTransforms ()
    {
        _PanTurnMoveZoom ();

        // En vue parallele, la position de la camera et le Frustum n'est pas modifier.
        // ??? Uniquement dans cam.csx, J'igniore pourquoi mais définir le Frustum change la position de la camera. ???
        if (_vp.IsParallelProjection)
        {
            _vpinfo.SetFrustum (
                _initialSizeX.T0 * (1+_zoomfactor),
                _initialSizeX.T1 * (1+_zoomfactor),
                _initialSizeY.T1 * (1+_zoomfactor),
                _initialSizeY.T0 * (1+_zoomfactor),
                _vpinfo.FrustumNear,
                _vpinfo.FrustumFar
            );
        }

        var pos = ON.Point3d.Origin;
        var dir = new ON.Vector3d (0, 0, -1);
        var up  = ON.Vector3d.YAxis;
        
        pos.Transform (_m);
        dir.Transform (_m);
        up.Transform (_m);
        
        #if DEBUG
        if (_vpinfo.SetCameraDirection (dir) == false) RhinoApp.WriteLine ("SetCameraDirection == false");
        if (_vpinfo.SetCameraLocation (pos) == false) RhinoApp.WriteLine ("SetCameraLocation == false");
        if (_vpinfo.SetCameraUp (up) == false) RhinoApp.WriteLine ("SetCameraUp == false");
        #else
        _vpinfo.SetCameraDirection (dir);
        _vpinfo.SetCameraLocation (pos);
        _vpinfo.SetCameraUp (up);
        #endif

        #if false // DEBUG // ??? Renvoie false même si tout semble fonctionné ???
        if (_vp.SetViewProjection (_vpinfo, updateTargetLocation: true)) RhinoApp.WriteLine ("SetViewProjection == false");
        #else
        _vp.SetViewProjection (_vpinfo, updateTargetLocation: true);
        #endif

    }

    /// <summary>
    ///     Compute the transformation matrix. </summary>
    // TODO: Add rotation for walk mode
    void _PanTurnMoveZoom ()
    {
        var cosX = Math.Cos(RotX);
        var sinX = Math.Sin(RotX);
        var cosZ = Math.Cos(RotZ);
        var sinZ = Math.Sin(RotZ);

        // vector X
        _m.M00 = cosZ;
        _m.M10 = sinZ;
        _m.M20 = 0d;

        // vector Y
        _m.M01 = cosX * -sinZ;
        _m.M11 = cosX * cosZ;
        _m.M21 = sinX;

        // vector Z
        _m.M02 = sinX * sinZ;
        _m.M12 = -sinX * cosZ;
        _m.M22 = cosX;

        // origin       panX*vecX    panY*vecY    panZ*vecZ ;
        _m.M03 = PosX + PanX*_m.M00 + PanY*_m.M01 + PanZ*_m.M02;
        _m.M13 = PosY + PanX*_m.M10 + PanY*_m.M11 + PanZ*_m.M12;
        _m.M23 = PosZ + PanX*_m.M20 + PanY*_m.M21 + PanZ*_m.M22;

        // vector taget to origin
        var x = _m.M03 - PosX;
        var y = _m.M13 - PosY;
        var z = _m.M23 - PosZ;

        if (_vp.IsParallelProjection)
        {
            // + t2o * zoom factor
            _m.M03 += x * _zoomfactor;
            _m.M13 += y * _zoomfactor;
            _m.M23 += z * _zoomfactor;
        }
        else
        {
            var length = Math.Sqrt (x*x + y*y + z*z);
            // origin + (xyz for zoom=1) * zoom
            _m.M03 += x/length * _zoom;
            _m.M13 += y/length * _zoom;
            _m.M23 += z/length * _zoom;
        }

        // Perspectcive and global scale are not touch.
    }
}


/// <summary>
///     For visual debugging (show camera with Grasshopper or native objects, change camera). </summary>
public class CameraConduit : RD.DisplayConduit
{
    static CameraConduit? g_instance;

    RD.RhinoViewport? _vp;
    // public ON.Surface? _g;

    public static void Show (RD.RhinoViewport viewport)
    {
        g_instance ??= new ();
        g_instance._vp = viewport;
        g_instance.Enabled = true;
    }

    public static void hide ()
    {
        if (g_instance != null)
            g_instance.Enabled = false;
    }
    
    protected override void DrawOverlay (RD.DrawEventArgs e)
    {
        if (_vp == null || _vp.Id == e.Viewport.Id) return;

        e.Display.DrawPoint (_vp.CameraLocation, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawPoint (_vp.CameraTarget, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawLine (new ON.Line (_vp.CameraLocation, _vp.CameraTarget), SD.Color.BlueViolet);

        var _vpinfo = new RO.ViewportInfo (_vp);

        var points = _vpinfo.GetFarPlaneCorners ();
        var rect = new ON.PolylineCurve (points);
        e.Display.DrawCurve (rect, SD.Color.BlueViolet);

        points = _vpinfo.GetNearPlaneCorners ();
        rect = new ON.PolylineCurve (points);
        e.Display.DrawCurve (rect, SD.Color.BlueViolet);

        // if (_g == null) return;
        // e.Display.DrawSurface (_g, SD.Color.Blue, 2);
    }
}
