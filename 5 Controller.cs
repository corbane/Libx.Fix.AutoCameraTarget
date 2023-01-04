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


public enum NavigationMode { Pan, Rotate, Zoom, Presets, Unknown }

public enum NavigationModifier
{
    Ctrl,
    Shift,
    Alt,
    Capital,
    None,
    Disabled
}

public static class ModifierExtensions
{
    // The only difference between the navigation modifier and the keyboard modifier
    // is that the navigation modifier has a "Disabled" field.

    public static KeyboardModifier ToKeyboardModifier (this NavigationModifier modifier)
    {
        return modifier switch
        {
            NavigationModifier.Alt     => KeyboardModifier.Alt,
            NavigationModifier.Capital => KeyboardModifier.Capital,
            NavigationModifier.Ctrl    => KeyboardModifier.Ctrl,
            NavigationModifier.Shift   => KeyboardModifier.Shift,
            NavigationModifier.None    => KeyboardModifier.None,
            _                          => KeyboardModifier.None,
        };
    }

    public static NavigationModifier ToNavigationModifier (this KeyboardModifier modifier)
    {
        return modifier switch
        {
            KeyboardModifier.Alt     => NavigationModifier.Alt,
            KeyboardModifier.Capital => NavigationModifier.Capital,
            KeyboardModifier.Ctrl    => NavigationModifier.Ctrl,
            KeyboardModifier.Shift   => NavigationModifier.Shift,
            KeyboardModifier.None    => NavigationModifier.None,
            _                   => NavigationModifier.None,
        };
    }
}


public partial interface INavigationSettings : IOptions
{
    bool Active { get; }
    bool ActiveInPlanView { get; }

    NavigationMode GetAssociatedMode (NavigationModifier modifier, bool inPlanView);

    NavigationModifier RotateModifier { get; }
    NavigationModifier RotateModifierInPlanView { get; }

    NavigationModifier PanModifier { get; }
    NavigationModifier PanModifierInPlanView { get; }

    NavigationModifier ZoomModifier { get; }
    NavigationModifier ZoomModifierInPlanView { get; }
    bool ReverseZoom { get; }
    double ZoomForce { get; }

    NavigationModifier PresetsModifier { get; }
    NavigationModifier PresetsModifierInPlanView { get; }
    double PresetForce { get; }
    int PresetSteps { get; }
    bool PresetsAlignCPlane { get; }
}

class CameraController : NavigationListener 
{
    RhinoDoc _doc;

    public readonly IntersectionData Data;

    readonly Camera _cam;



    # nullable disable // _doc
    public CameraController (NavigationSettings options, IntersectionData data) : base (options)
    {
        Data = data;
        _cam = new ();
    }
    #nullable enable


    #region Start / Stop

    public override bool CanRun (RUI.MouseCallbackEventArgs e)
    {
        // Handle right mouse button only.
        // TODO: preserve/override CTRL+SHIFT
        if (e.MouseButton != RUI.MouseButton.Right || e.CtrlKeyDown && e.ShiftKeyDown)
            return false;

        var cmod = Keyboard.GetCurrentModifier ();

        // If there is no association with the current modifier key (or none), return false.
        if (e.View.ActiveViewport.IsPlanView)
        {
            if (Settings.ActiveInPlanView == false ||
                Settings.GetAssociatedMode (cmod.ToNavigationModifier (), inPlanView: true) == NavigationMode.Unknown
            ) return false;
        }
        else
        {
            if (Settings.Active == false ||
                Settings.GetAssociatedMode (cmod.ToNavigationModifier (), inPlanView: false) == NavigationMode.Unknown
            ) return false;
        }
        
        // TODO: Rotate around Gumball options/RhinoSettings?
        if (cmod == KeyboardModifier.None &&
            e.View.Document.Objects.GetSelectedObjects (includeLights: true, includeGrips: true).Count () > 0
        ) return false;

        return true;
    }

    protected override bool OnStartNavigation (RD.RhinoViewport viewport, SD.Point viewportPoint)
    {
        Data.Viewport = viewport;
        Data.ViewportPoint = new SD.Point (viewportPoint.X, viewportPoint.Y);
        _doc = viewport.ParentView.Document;

        // Calculate the point of intersection under the mouse cursor.
        // If there is nothing visible in the view, the intersection is placed on the camera position.
        // TODO: Give an option for this.
        if (Settings.Debug) {
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
        _UpdateActions (viewport.IsPlanView);

        // Displays visual information.
        if (Settings.Marker) IntersectionConduit.Show (Data);
        if (Settings.ShowCamera) CameraConduit.Show (viewport);

        VirtualCursor.Init (new (viewportPoint.X, viewportPoint.Y));

        // TODO: Si la touche CapsLock est enfoncée avant le bouton de la souris.

        return true;
    }

    void _UpdateActions (bool _inplanview)
    {
        foreach (KeyboardModifier n in Enum.GetValues (typeof (KeyboardModifier)))
            SetModifierCallback (n, null, null);

        NavigationModifier  rmod;
        NavigationModifier  pmod;
        NavigationModifier  zmod;
        NavigationModifier  xmod;

        if (_inplanview)
        {
            rmod = Settings.RotateModifierInPlanView;
            pmod = Settings.PanModifierInPlanView;
            zmod = Settings.ZoomModifierInPlanView;
            xmod = Settings.PresetsModifierInPlanView;
        }
        else
        {
            rmod = Settings.RotateModifier;
            pmod = Settings.PanModifier;
            zmod = Settings.ZoomModifier;
            xmod = Settings.PresetsModifier;
        }

        if (rmod != NavigationModifier.Disabled) SetModifierCallback (rmod.ToKeyboardModifier (), _OnRotation, NavigationMode.Rotate);
        if (pmod != NavigationModifier.Disabled) SetModifierCallback (pmod.ToKeyboardModifier (), _OnPan, NavigationMode.Pan);
        if (zmod != NavigationModifier.Disabled) SetModifierCallback (zmod.ToKeyboardModifier (), _OnZoom, NavigationMode.Zoom);
        if (xmod != NavigationModifier.Disabled) SetModifierCallback (xmod.ToKeyboardModifier (), _OnPresets, NavigationMode.Presets);
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

        _Redraw ();
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
        _cam.RotX += -Math.PI*offset.Y/300;
        _cam.RotZ += -Math.PI*offset.X/300;
        _cam.ApplyTransforms ();
        _Redraw ();
    }

    #endregion


    #region Zoom

    double _zforce;
    double _zinv;

    void _InitializeZoom ()
    {
        _zinv = Settings.ReverseZoom ? -1 : 1;
        _zforce = Settings.ZoomForce;
    }

    void _OnZoom (ED.Point offset)
    {
        _cam.Zoom += offset.Y * _zinv * _zforce;
        _cam.ApplyTransforms ();
        _Redraw ();
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
        _cam.PanX += -offset.X/_w2sScale;
        _cam.PanY += offset.Y/_w2sScale;
        VirtualCursor.GrowPosition (offset);
        _cam.ApplyTransforms ();
        _Redraw ();
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
        _sforce = Settings.PresetForce;
        _scount = Settings.PresetSteps;
        _srad = Math.PI*2 / _scount;
    }

    void _StartPresetsNavigation ()
    {
        _accu.X = _accu.Y = 0;

        _cam.RotX = Math.Round (_cam.RotX / _srad) % 8 * _srad;
        _cam.RotZ = Math.Round (_cam.RotZ / _srad) % 8 * _srad;

        _cam.ApplyTransforms ();
        _Redraw ();
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
        _Redraw ();
    }

    void _StopPresetsNavigation ()
    {
        if (Settings.PresetsAlignCPlane == false) return;

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
            // _Redraw ();
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

    void _Redraw ()
    {
        if (Settings.ShowCamera) _doc.Views.Redraw ();
        else Data.Viewport.ParentView.Redraw ();
    }
}
