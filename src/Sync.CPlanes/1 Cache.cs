
#if CSX
#load "../Sync.Document/0.csx"
#endif

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SD = System.Drawing;

using RH = Rhino;
using ON = Rhino.Geometry;
using RO = Rhino.DocObjects;
using RD = Rhino.Display;
using RUI = Rhino.UI;
using RhinoApp = Rhino.RhinoApp;
using RhinoDoc = Rhino.RhinoDoc;
using RO_CPlane = Rhino.DocObjects.ConstructionPlane;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Sync;
#endif


public static partial class RhinoHelpers
{
    public static bool EqualCPlanes (RO_CPlane? A, RO_CPlane? B)
    {
        if (A == null || B == null)
            return A == null && B == null;

        return A.Name == B.Name
            && A.GridLineCount == B.GridLineCount
            && A.GridSpacing == B.GridSpacing
            && A.Plane == B.Plane;
    }
}


public static class ArchiPlane
{
    public static ON.Plane WorldXY => ON.Plane.WorldXY;
    public static ON.Plane WorldInvertedXY => new ON.Plane (ON.Point3d.Origin, ON.Vector3d.XAxis, -ON.Vector3d.YAxis);
    public static ON.Plane WorldXZ => new ON.Plane (ON.Point3d.Origin, ON.Vector3d.XAxis, ON.Vector3d.ZAxis);
    public static ON.Plane WorldInvertedXZ => new ON.Plane (ON.Point3d.Origin, -ON.Vector3d.XAxis, ON.Vector3d.ZAxis);
    public static ON.Plane WorldYZ => ON.Plane.WorldYZ;
    public static ON.Plane WorldInvertedYZ => new ON.Plane (ON.Point3d.Origin, -ON.Vector3d.YAxis, ON.Vector3d.ZAxis);
}


public class ArchiCPlane
{
    public static RO_CPlane WorldXY         => _GetCPlane (ArchiPlane.WorldXY);
    public static RO_CPlane WorldInvertedXY => _GetCPlane (ArchiPlane.WorldInvertedXY);
    public static RO_CPlane WorldXZ         => _GetCPlane (ArchiPlane.WorldXZ);
    public static RO_CPlane WorldInvertedXZ => _GetCPlane (ArchiPlane.WorldInvertedXZ);
    public static RO_CPlane WorldYZ         => _GetCPlane (ArchiPlane.WorldYZ);
    public static RO_CPlane WorldInvertedYZ => _GetCPlane (ArchiPlane.WorldInvertedYZ);

    static RO_CPlane _GetCPlane (ON.Plane plane)
    {
        var o = RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport.GetConstructionPlane ();
        var n = new RO_CPlane ();

        var props = from p in typeof (RO_CPlane).GetProperties (BindingFlags.Instance | BindingFlags.Public)
                    where p.SetMethod != null
                    select p;

        foreach (var p in props)
            p.SetValue (n, p.GetValue (o));
        
        n.Plane = plane;

        return n;
    }
}


/*/
    Ce n'est pas un vrai cache car foreach (var cp in _doc.NamedConstructionPlanes) recréer un objet CPlane a chaque fois
    L'API n'as pas d'événement pour la table NamedConstructionPlanes

    Je ne peut pas synchroniser l'ordre des CPlanes:
    - `NamedConstructionPlaneTable.GetEnumerator` renvoie la liste dans l'ordre de création,
       l'ordre afficher dans le panneaux `Name CPlanes` Rhino n'est pas pris en compte.
    - L'objet `ConstructionPlane` n'a pas de propriété `Index` ou tout autre permettant de le connaître.
/*/


public class CGridItem
{
    public CGridItem (RO_CPlane cplane)
    {
        CPlane = cplane;
    }

    public RO_CPlane CPlane { get; }


    ON.Mesh? _preview;
    ON.Line _xline;
    ON.Line _yline;
    ON.Line _zline;
    ON.BoundingBox _bbox;

    static RD.DisplayMaterial _material = new (SD.Color.CornflowerBlue, 0.5);
    
    public ON.BoundingBox BoundingBox 
    {
        get {
            if (_preview == null) _CreateRenderObject ();
            return _bbox;
        }
    }

    ON.Mesh _CreateMeshPreview (RO_CPlane cplane)
    {
        DBG.Log ();

        return ON.Mesh.CreateFromPlane (
            cplane.Plane,
            xInterval: new (cplane.GridSpacing*-cplane.GridLineCount, cplane.GridSpacing*cplane.GridLineCount),
            yInterval: new (cplane.GridSpacing*-cplane.GridLineCount, cplane.GridSpacing*cplane.GridLineCount),
            xCount: 2,
            yCount: 2
        );
    }

    ON.Line _CreateVectorPreview (ON.Vector3d vector)
    {
        return new ON.Line (CPlane.Plane.Origin, vector*CPlane.GridLineCount*CPlane.GridSpacing);
    }

    void _CreateRenderObject ()
    {
        _preview = _CreateMeshPreview (CPlane);
        _xline   = _CreateVectorPreview (CPlane.Plane.XAxis);
        _yline   = _CreateVectorPreview (CPlane.Plane.YAxis);
        _zline   = _CreateVectorPreview (CPlane.Plane.ZAxis);

        var p = CPlane.Plane;
        var l = CPlane.GridLineCount*CPlane.GridSpacing;
        var v = p.XAxis+p.YAxis+p.ZAxis;

        _bbox = new ON.BoundingBox (
            new ON.Point3d (p.Origin + v * l),
            new ON.Point3d (p.Origin + v * -l)
        );
    }

    public void Draw (RD.DrawEventArgs e)
    {
        if (_preview == null) _CreateRenderObject ();
        e.Display.DrawMeshShaded (_preview!, _material);
        e.Display.DrawArrow (_xline, SD.Color.Red);
        e.Display.DrawArrow (_yline, SD.Color.Green);
        //e.Display.DrawArrow (_zline, SD.Color.Blue);
    }


    public static bool Equals (CGridItem? A, CGridItem? B)
    {
        if (A == null || B == null) return A == null & B == null;
        return RhinoHelpers.EqualCPlanes (A.CPlane, B.CPlane);
    }
}


public class CGrid
{

    public CGrid (RO_CPlane cplane)
    {
        XY = new (cplane);
    }

    public void Clear (RO_CPlane cplane)
    {
        XY     = new (cplane);
        _xz    = null;
        _yz    = null;
        _invXY = null;
        _invYZ = null;
        _invXZ = null;
    }

    public string Name => XY.CPlane.Name;
    
    public int GridLineCount;
    public double GridSpacing;
    public int ThickLineFrequency;

    /// <summary>
    ///     The original construction plane. </summary>
    public CGridItem XY { get; private set; }

    /// <summary>
    ///     Plane rotated 180° around the X axis (So the normal of the plane is reversed). </summary>
    public CGridItem InvertedXY => _invXY ??= _CreateInvertedXY ();

    /// <summary>
    ///     Plane rotated 90° around the Y axis (So the X axis becomes the normal of the plane). </summary>
    public CGridItem YZ => _yz ??= _CreateYZ ();

    public CGridItem InvertedYZ => _invYZ ??= _CreateInvertedYZ ();

    /// <summary>
    ///     Plane rotated 90° around the X axis (So the Y axis becomes the normal of the plane). </summary>
    public CGridItem XZ => _xz ??= _CreateXZ ();

    public CGridItem InvertedXZ => _invXZ ??= _CreateInvertedXZ ();
    

    CGridItem? _invXY;
    CGridItem? _xz;
    CGridItem? _yz;
    CGridItem? _invYZ;
    CGridItem? _invXZ;

    CGridItem _CreateInvertedXY () {
        var plane = XY.CPlane.Plane;
        return _DuplicateConstructionPlane (new ON.Plane (plane.Origin, plane.XAxis, -plane.YAxis));
    }

    CGridItem _CreateYZ () {
        var plane = XY.CPlane.Plane;
        return _DuplicateConstructionPlane (new ON.Plane (plane.Origin, plane.YAxis, plane.ZAxis));
    }

    CGridItem _CreateInvertedYZ () {
        var plane = XY.CPlane.Plane;
        return _DuplicateConstructionPlane (new ON.Plane (plane.Origin, -plane.YAxis, plane.ZAxis));
    }

    CGridItem _CreateXZ () {
        var plane = XY.CPlane.Plane;
        return _DuplicateConstructionPlane (new ON.Plane (plane.Origin, plane.XAxis, plane.ZAxis));
    }

    CGridItem _CreateInvertedXZ () {
        var plane = XY.CPlane.Plane;
        return _DuplicateConstructionPlane (new ON.Plane (plane.Origin, -plane.XAxis, plane.ZAxis));
    }

    CGridItem _DuplicateConstructionPlane (ON.Plane plane)
    {
        return new (new RO.ConstructionPlane {
            Plane = plane,
            GridLineCount = XY.CPlane.GridLineCount,
            GridSpacing = XY.CPlane.GridSpacing,
        });
    }
}


public class CPlaneRegistry : INotifyPropertyChanged, IDisposable
{
    Dictionary <string, CGrid> _cplaneregistry = new ();


    public CPlaneRegistry ()
    {
        DBG.CTOR ();

        DocumentObserver.OnBeginDocument += _OnBeginDocument;
        DocumentObserver.OnEndDocument   += _onEndDocument;
    }

    ~CPlaneRegistry ()
    {
        DBG.CTOR ();
        
        Dispose ();
    }
    
    public void Dispose ()
    {
        DBG.CTOR ();
        
        StopSync ();
        DocumentObserver.OnBeginDocument -= _OnBeginDocument;
        DocumentObserver.OnEndDocument   -= _onEndDocument;
    }


    bool _prevstarted;

    void _OnBeginDocument ()
    {
        DBG.CTOR ();

        _prevstarted = _started;
        StopSync ();
    }
    
    void _onEndDocument (RhinoDoc? doc)
    {
        DBG.CTOR ();
        
        if (_prevstarted)
            StartSync ();
    }


    bool _started;

    public void StartSync ()
    {
        if (_started) return;

        DBG.CTOR ();

        _started = true;
        RhinoApp.Idle += _OnIdle;
    }

    public void StopSync ()
    {
        DBG.CTOR ();

        RhinoApp.Idle -= _OnIdle;
        _started = false;
    }

    void _OnIdle (object sender, EventArgs e)
    {
        Update ();
    }


    public IEnumerable <CGrid> Entries => from cp in _cplaneregistry.Values select cp;

    public void Update ()
    {
        var doc = RhinoDoc.ActiveDoc;

        CGrid item;
        bool changes = false;

        // After the loop, this contains the name of the CPlanes that don't exist in the document.
        var regnames = _cplaneregistry.Keys.ToList ();
        
        // Update or create registry entries for each CPlane in the document.
        foreach (var cp in doc.NamedConstructionPlanes)
        {
            if (_cplaneregistry.TryGetValue (cp.Name, out item))
            {
                regnames.Remove (cp.Name);
                if (RhinoHelpers.EqualCPlanes (item.XY.CPlane, cp) == false)
                {
                    DBG.Log ("Update CPlane", cp.Name);
                
                    changes = true;
                    item.Clear (cp);
                }
            }
            else
            {
                DBG.Log ("Create new CPlane", cp.Name);
                
                changes = true;
                _cplaneregistry[cp.Name] = item = new (cp);
            }
        }

        // Removes deleted CPlanes from the registry.
        foreach (var n in regnames)
        {
            DBG.Log ("Remove CPlane", n);
                
            changes = true;
            _cplaneregistry.Remove (n);
        }

        if (changes)
            Emit (nameof (Entries));
    }


    #region INotifyPropertyChanged

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    public virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        DBG.DATA (memberName);

        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}

