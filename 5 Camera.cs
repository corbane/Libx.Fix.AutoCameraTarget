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
namespace Libx.Fix.AutoCameraTarget;
#endif


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
class Camera
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
class CameraConduit : RD.DisplayConduit
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
    
    protected override void DrawOverlay(RD.DrawEventArgs e)
    {
        if (_vp == null || _vp.Id == e.Viewport.Id) return;

        e.Display.DrawPoint (_vp.CameraLocation, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawPoint (_vp.CameraTarget, RD.PointStyle.Triangle, 10, SD.Color.BlueViolet);

        e.Display.DrawLine (new ON.Line (_vp.CameraLocation, _vp.CameraTarget), SD.Color.BlueViolet);

        var _vpinfo = new Rhino.DocObjects.ViewportInfo (_vp);

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


enum NavigationMode { Pan, Rotate, Zoom, Presets }


class CameraController : NavigationListener 
{
    int _optversion = -1;

    RhinoDoc _doc;

    public NavigationOptions Options { get; private set; }

    public readonly IntersectionData Data;

    readonly Camera _cam;

    bool _inplanview;


    # nullable disable // _doc
    public CameraController (NavigationOptions options, IntersectionData data)
    {
        Options = options;
        Data = data;
        _cam = new ();
    }
    #nullable enable


    #region Start / Stop

    public override bool CanRun (RUI.MouseCallbackEventArgs e)
    {
        if (e.MouseButton != RUI.MouseButton.Right || e.CtrlKeyDown && e.ShiftKeyDown)
            return false;

        if (e.View.ActiveViewport.IsPlanView)
        {
            if (Options.PresetsInPlanView == false) return false;
            if (Keyboard.GetCurrentModifier () != Options.PresetsModifier) return false;
        }

        // TODO:
        if (Keyboard.GetCurrentModifier () == ModifierKey.None &&
            e.View.Document.Objects.GetSelectedObjects (includeLights: true, includeGrips: true).Count () > 0
        ) return false;

        return true;
    }

    protected override bool OnStartNavigation (RD.RhinoViewport viewport, SD.Point viewportPoint)
    {
        Data.Viewport = viewport;
        Data.ViewportPoint = new SD.Point (viewportPoint.X, viewportPoint.Y);
        _doc = viewport.ParentView.Document;
        _inplanview = viewport.IsPlanView;

        // Calculate the point of intersection under the mouse cursor.
        // If there is nothing visible in the view, the intersection is placed on the camera position.
        if (Options.Debug) {
            Intersector.StartPerformenceLog ();
            Intersector.Compute (Data, viewport.CameraLocation);
            Intersector.StopPerformenceLog (Data);
        } else {
            Intersector.Compute (Data, viewport.CameraLocation);
        }

        // Initialize properties.
        _InitializePan ();
        _InitializeZoom ();
        _InitializePresets ();

        // Initialize the camera.
        _cam.Init (viewport,  Data.TargetPoint);

        // Define actions.
        if (Options.DataVersion != _optversion)
        {
            _UpdateActions ();
            _optversion = Options.DataVersion;
        }

        // Displays visual information.
        if (Options.Marker) IntersectionConduit.Show (Data);
        if (Options.ShowCamera) CameraConduit.Show (viewport);

        VirtualCursor.Init (new (viewportPoint.X, viewportPoint.Y));

        // TODO: Si la touche CapsLock est enfoncée avant le bouton de la souris.

        return true;
    }

    void _UpdateActions ()
    {
        foreach (ModifierKey n in Enum.GetValues (typeof (ModifierKey)))
            SetModifierCallback (Options.RotateModifier, null, null);

        SetModifierCallback (Options.RotateModifier, _OnRotation, NavigationMode.Rotate);
        SetModifierCallback (Options.PanModifier, _OnPan, NavigationMode.Pan);
        SetModifierCallback (Options.ZoomModifier, _OnZoom, NavigationMode.Zoom);
        SetModifierCallback (Options.PresetsModifier, _OnPresets, NavigationMode.Presets);
    }

    protected override void OnActionChange (object? previousTag, object? currentTag)
    {
        switch (previousTag)
        {
        case NavigationMode.Zoom: _InitializePan (); break;
        case NavigationMode.Presets: _StopPresetsNavigation (); break;
        }
        
        _cam.Reset ();
        if (currentTag != null)
            VirtualCursor.Show (_GetCursor ((NavigationMode)currentTag));

        switch (currentTag)
        {
        case NavigationMode.Presets: _StartPresetsNavigation (); break;
        }
    }

    protected override void OnStopNavigation ()
    {
        // TODO: Testez si la cible est devant la caméra.
        if (Data.Status != IntersectionStatus.None)
            Data.Viewport.SetCameraTarget (Data.TargetPoint, updateCameraLocation: false);
        IntersectionConduit.Hide ();
        CameraConduit.hide ();
        VirtualCursor.Hide ();

        // TODO: Si la touche CapsLock est enfoncée avant le bouton de la souris.
        //       Il n'est pas certain qu'elle ne le soit pas encore ici.

        _doc.Views.Redraw ();
    }

    VirtualCursorIcon _GetCursor (NavigationMode modifier)
    {
        return modifier switch 
        {
            NavigationMode.Pan     => VirtualCursorIcon.Hand,
            NavigationMode.Rotate  => VirtualCursorIcon.Pivot,
            NavigationMode.Zoom    => VirtualCursorIcon.Glass,
            NavigationMode.Presets => VirtualCursorIcon.Axis,
            _ => VirtualCursorIcon.None
        };
    }

    #endregion


    #region Turn

    void _OnRotation (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.RotX += -Math.PI*offset.Y/300;
        _cam.RotZ += -Math.PI*offset.X/300;
        _cam.ApplyTransforms ();
        // _doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Zoom

    double _zforce;
    double _zinv;

    void _InitializeZoom ()
    {
        _zinv = Options.ReverseZoom ? -1 : 1;
        _zforce = Options.ZoomForce;
    }

    void _OnZoom (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.Zoom += offset.Y * _zinv * _zforce;
        _cam.ApplyTransforms ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Pan

    double _w2sScale;

    void _InitializePan ()
    {
        Data.Viewport.GetWorldToScreenScale (Data.TargetPoint, out _w2sScale);
    }

    void _OnPan (ED.Point offset)
    {
        if (_inplanview) return;
        _cam.PanX += -offset.X/_w2sScale;
        _cam.PanY += offset.Y/_w2sScale;
        VirtualCursor.GrowPosition (offset);
        _cam.ApplyTransforms ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    #endregion


    #region Presets

    const double EPSILON = 1E-15;

    // Allez comprendre pourquoi, les CPlanes standard sont visibles dans le panneau CPlane mais pas dans l'API.
    static ON.Plane[] _defaultCPlanes = {
        new (ON.Point3d.Origin, ON.Vector3d.ZAxis),
        new (ON.Point3d.Origin, ON.Vector3d.YAxis),
        new (ON.Point3d.Origin, ON.Vector3d.XAxis),
    };

    /// <summary>
    ///     Offset accumulator. </summary>
    ED.Point _accu;

    int _scount;
    double _srad;
    double _sforce;

    void _InitializePresets ()
    {
        _sforce = Options.PresetForce;
        _scount = Options.PresetSteps;
        _srad = Math.PI*2 / _scount;
    }

    void _StartPresetsNavigation ()
    {
        _accu.X = _accu.Y = 0;

        _cam.RotX = Math.Round (_cam.RotX / _srad) % 8 * _srad;
        _cam.RotZ = Math.Round (_cam.RotZ / _srad) % 8 * _srad;

        _cam.ApplyTransforms ();
        _doc.Views.Redraw ();
    }

    void _OnPresets (ED.Point offset)
    {
        _accu.X += (int)(offset.X * _sforce);
        _accu.Y += (int)(offset.Y * _sforce);

        var x = _srad * (_accu.X / -200);
        var y = _srad * (_accu.Y / -200);
        if (x+y == 0) return;

        if (y != 0) { _cam.RotX += y; _accu.Y = 0; }
        if (x != 0) { _cam.RotZ += x; _accu.X = 0; }
        
        _cam.ApplyTransforms ();
        //_doc.Views.Redraw ();
        Data.Viewport.ParentView.Redraw ();
    }

    void _StopPresetsNavigation ()
    {
        if (Options.PresetsAlignCPlane == false) return;

        var bnormal = Viewport.CameraZ;
        bnormal.Unitize ();
        var inormal = -bnormal;

        foreach (var plane in _defaultCPlanes)
        {
            var cnormal = plane.Normal;
            cnormal.Unitize ();

            if (cnormal.EpsilonEquals (bnormal, EPSILON) == false &&
                cnormal.EpsilonEquals (inormal, EPSILON) == false
            ) continue;

            Viewport.SetConstructionPlane (plane);

            // TODO: Réaligner la caméra pour supprimer le bruit.
            // var cosZ = Math.Acos (plane.ZAxis.Z);
            // _cam.RotX = plane.YAxis.Z < 0 ? -cosZ : cosZ;
            // var cosY = Math.Acos (plane.XAxis.X);
            // _cam.RotZ = plane.XAxis.Y < 0 ? -cosY : cosY;
            // _cam.UpdateView ();
            // _doc.Views.Redraw ();
            return;
        }
        // foreach (var cplane in _doc.NamedConstructionPlanes)
        // {
        //     var cnormal = cplane.Plane.Normal;
        //     cnormal.Unitize ();
        // 
        //     if (cnormal.CompareTo (bnormal) == 0 == false &&
        //         cnormal.CompareTo (inormal) == 0 == false
        //     ) continue;
        // 
        //     Viewport.SetConstructionPlane (cplane);
        //     // TODO: Réaligner la caméra pour supprimer le bruit.
        //     return;
        // }
    }

    #endregion
}
