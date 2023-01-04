/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
 
    Optimizing intersection computation is important with managed code-side storage of object information.
    But it has a memory and delay cost when creating, deleting or modifying objects.
    Moving 1000 objects at once will regenerate 1000 meshes for the intersection calculation.
    So to limit the impact, the meshes of the objects are extracted during the Idle events.

    With this, obtaining calculation data is imperceptible during navigation.
    but it is possible to see a latency when opening a file. Depending on the number of objects in the file,
    it is possible that for several seconds the calculation of the intersections is done only with the bounding boxes
    (while waiting for the meshes to be generated)

    To limit this initial latency and preserve memory, the cache is synchronized only with visible objects
    and the new cache item is created each time an object becomes visible.

    Again, this solution (Synchronize cache only with visible objects) brings another problem:
    There are commands that can temporarily change the structure of the document and then restore it.
    This is the case of `UnlockSelected` but a command from a third-party plugin could have the same behavior.
    To my knowledge, there is no possibility in the API to indicate that a modification is temporary (so to know if it is).
    If 1000 objects are temporarily modified and then restored, the plugin will unnecessarily recreate 1000 meshes.

    In the case of `UnlockSelected`,
    I disabled synchronization and therefore it is possible to find an intersection on a hidden object during this command.
    My choice between Sync cache only with visible objects or not is purely arbitrary and based on my use of Rhino.
    I can spend many hours with the same hidden objects and never use a command like `UnlockSelected`.
    So I made the choice to preserve the memory in a big file.
/*/


using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using SD = System.Drawing;

using EF = Eto.Forms;
using ED = Eto.Drawing;

using RH = Rhino;
using ON = Rhino.Geometry;
using RO = Rhino.DocObjects;
using RD = Rhino.Display;
using RUI = Rhino.UI;
using RhinoApp = Rhino.RhinoApp;
using RhinoDoc = Rhino.RhinoDoc;


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


#if DEBUG

public class CacheOptions : Settings
{
    public bool VisualDebugEnabled => _displaymeshes || _displaybbox;

    bool _displaymeshes;
    public bool DebugDisplayMeshes { get => _displaymeshes; set { Set (ref _displaymeshes, value); } }

    bool _displaybbox;
    public bool DebugDisplayBBox { get => _displaybbox; set { Set (ref _displaybbox, value); } }

    public override bool Validate()
    {
        return true;
    }
}

#endif


public static class Cache
{
    static Cache ()
    {
        #if DEBUG
        _ResetStates ();
        _initOptions ();
        #endif
    }


    #region Options
    #if DEBUG

    public static CacheOptions Options = new ();

    static void _initOptions ()
    {
        Options.PropertyChanged += _OnOptionChanges;
    }

    static void _OnOptionChanges (object sender, PropertyChangedEventArgs e)
    {
        if (Options.VisualDebugEnabled) CacheConduit.Show (Options);
        else CacheConduit.Hide ();
    }

    #endif
    #endregion


    #region Start/Stop

    static DocumentObserver _listener = new DocumentObserver (
        onBeginDocument : _Clear,
        onEndDocument   : _AppendDocument,
        onAppendObject  : _Append,
        onRemoveObject  : _Remove
    );

    public static void Start ()
    {
        #if DEBUG
        RhinoApp.WriteLine ("Cache started");
        #endif

        _AppendDocument (RhinoDoc.ActiveDoc);
        _listener.AttachEvents ();
    }

    public static void Stop ()
    {
        #if DEBUG
        RhinoApp.WriteLine ("Cache stoped");
        _ClearDebugChanges ();
        #endif

        _listener.DetachEvents ();
        _Clear ();
    }

    #endregion


    #region Data

    static readonly Dictionary<Guid, CacheItem> _cache = new ();

    public struct CacheItem
    {
        #if DEBUG
        public Guid Id;
        public RO.ObjectType ObjectType;
        #endif

        public ON.Mesh[]? Meshes;
        public ON.BoundingBox BBox;

        public CacheItem (RO.RhinoObject obj)
        {
            #if DEBUG
            Id         = obj.Id;
            ObjectType = obj.ObjectType;
            #endif

            var bbox = obj.Geometry.GetBoundingBox (accurate: false);
            Meshes    = null;
            BBox      = bbox;
        }

        public void Reset (RO.RhinoObject obj)
        {
            #if DEBUG
            Id         = obj.Id;
            ObjectType = obj.ObjectType;
            #endif

            var bbox = obj.Geometry.GetBoundingBox (accurate: false);
            Meshes    = null;
            BBox      = bbox;
        }

        public void Deconstruct (out ON.Mesh[]? meshes, out ON.BoundingBox bBox)
        {
            meshes = Meshes;
            bBox = BBox;
        }
    }

    public static ICollection <CacheItem> Items => _cache.Values;

    public static int Count => _cache.Count;

    public static void _Clear ()
    {
        _idlemesher.Clear ();
        _cache.Clear ();
            
        #if DEBUG
        _ResetStates ();
        _DeferDebugChanges ();
        #endif
    }

    static readonly RO.ObjectEnumeratorSettings _enumopts = new () {
        DeletedObjects        = false,
        NormalObjects         = true,
        HiddenObjects         = true,
        LockedObjects         = true,
        IncludeLights         = true,
        ReferenceObjects      = true,
        ActiveObjects         = true,
        ObjectTypeFilter = RO.ObjectType.AnyObject,
        // ??? les textes? https://discourse.mcneel.com/t/phantoms/81374/2 ???
        IncludePhantoms       = true,
    };

    static void _AppendDocument (RhinoDoc? doc)
    {
        if (doc == null) return;
        RhinoApp.WriteLine ("Append document to cache: "+doc.Path);
        // !!!
        //
        // N'ayant pas trouvé de solution pour éxécuter un action aprés que les fichiers référencés d'un RWS soit chargés,
        // `AppendDocument` est appellé par `_OnEndOpenDocument`. Mais:
        //
        // - event.Document fait toujours reference au document actif.
        // 
        // - event.Document.Objects.Count augmente a chaque chargement de fichier référencé.
        //   Et `foreach (... in e.Document.Objects)` retourne uniquement les objects du fichier actif.
        // 
        // - `event.Document.Objects.GetObjectList` retourne les objets charger par le fichier qui a émit `_OnEndOpenDocument`,
        //   ainsi que tous les précédent objets des autres fichiers.
        //
        // - `RhinoDoc.OpenDocuments (includeHeadless: true)` Returns only the active document of the worksession.
        //
        // !!!
        foreach (var obj in doc.Objects.GetObjectList (_enumopts))
            if (obj.Visible && _cache.ContainsKey (obj.Id) == false) _Append (obj);
    }

    static void _Append (RO.RhinoObject obj)
    {
        // TODO: Add filter function
        if (obj is RO.DetailViewObject ||
            obj.Attributes.Space != RO.ActiveSpace.ModelSpace
        ) return;

        if (_cache.ContainsKey (obj.Id) == false)
        { 
            _cache[obj.Id] = new CacheItem (obj);

            #if DEBUG
            _IncrementState (obj.ObjectType);
            _DeferDebugChanges ();
            #endif
        }
        else
        {
            _cache[obj.Id].Reset (obj);
        }

        if (obj.IsMeshable (ON.MeshType.Render))
            _idlemesher.Enqueue (new (obj.Document, obj.Id));

    }

    static void _Remove (Guid objectId)
    {
        #if DEBUG
        _DecrementState (_cache[objectId].ObjectType);
        _DeferDebugChanges ();
        #endif
        
        _cache.Remove (objectId);
    }

    static void _SetIntersectionMeshes (Guid objectId, ON.Mesh[] meshes)
    {
        var item = _cache[objectId];
        item.Meshes = meshes;
        _cache[objectId] = item;
    }
    
    // TODO: Ability to set a custom CacheItem.
    // static void Append (ON.Mesh intersectionMesh);

    // TODO: Custom intersection mesh generation 
    // static void _GenerateIntersectionMeshes (RO.RhinoObject obj) { }

    #endregion


    #region Idle Processor

    static IdleQueue <ProcessArg> _idlemesher = new (_Process);

    readonly struct ProcessArg
    {
        public readonly RhinoDoc Doc;
        public readonly Guid ObjectId;

        public ProcessArg (RhinoDoc doc, Guid objectId) {
            Doc = doc;
            ObjectId = objectId;
        }
    }

    static void _Process (ProcessArg arg)
    {
        var obj = arg.Doc.Objects.FindId (arg.ObjectId);

        // Fool's guard, case where an object is deleted before the generation of its mesh.
        if (obj == null) {
            _Remove (arg.ObjectId);
            return;
        }

        _SetIntersectionMeshes(
            arg.ObjectId,
            // `obj.GetMeshes(MeshType.Default)` does not return meshes for block instances.
            // GetRenderMeshes has a different behavior with SubDs
            // https://discourse.mcneel.com/t/rhinoobject-getrendermeshes-bug/151953
            (from oref in RO.RhinoObject.GetRenderMeshes (new [] { obj }, true, false)
             let m = oref.Mesh ()
             where m != null
             select m).ToArray ()
        );
    }

    #endregion


    #if DEBUG

    #region Debug Form

    public static void ShowDebugForm ()
    {
        new EF.Form {
            Content = new CacheDebugControl (Options),
            Owner = RUI.RhinoEtoApp.MainWindow
        }.Show ();
    }

    #endregion


    #region Debug Events

    /*/
        Specific for working with RhinoDoc events.
        These functions only works because when loading a file or modifying/creating/deleting objects,
        Rhino leaves no downtime.
    /*/

    public delegate void OnCacheChangedHandler ();

    public static event OnCacheChangedHandler? OnCacheChanged;

    static IdleRhinoEventGroup _debugeventgroup = new (_EmitDebugChanges);

    static void _EmitDebugChanges () { OnCacheChanged?.Invoke (); }

    static void _DeferDebugChanges () { if (_debugeventgroup.IsStarted == false) _debugeventgroup.Increment (); }

    static void _ClearDebugChanges () { _debugeventgroup.Reset (); }

    #endregion


    #region Debug States

    /*/
        These functions count the number of objects by types
    /*/

    public static readonly Dictionary <RO.ObjectType, uint> States = new ();

    static void _ResetStates ()
    {
        foreach (RO.ObjectType t in Enum.GetValues (typeof (RO.ObjectType)))
            States[t] = 0;
    }

    static void _IncrementState (RO.ObjectType t)
    {
        States[t]++;
    }

    static void _DecrementState (RO.ObjectType t)
    {
        States[t]--;
    }

    #endregion

    #endif
}


#if DEBUG

class CacheDebugControl : EF.StackLayout
{
    CacheOptions _options;
    EF.Label _tarea;

    public CacheDebugControl (CacheOptions options)
    {
        DataContext = _options = options;

        Orientation = EF.Orientation.Vertical;
        VerticalContentAlignment = EF.VerticalAlignment.Stretch;
        HorizontalContentAlignment = EF.HorizontalAlignment.Stretch;
        Size = new (400, 400);

        _tarea = new () {
            Font = new ED.Font (ED.FontFamilies.Monospace, 12),
            VerticalAlignment = EF.VerticalAlignment.Top
        };
        Items.Add (new EF.StackLayoutItem (_tarea, expand: true));

        var chkmeshes = new EF.CheckBox { Text = "Show meshes" };
        chkmeshes.CheckedBinding.BindDataContext (nameof (options.DebugDisplayMeshes));

        var chkbbox = new EF.CheckBox { Text = "Show bounding box" };
        chkbbox.CheckedBinding.BindDataContext (nameof (options.DebugDisplayBBox));

        var expander = Ui.Expander ("Options", chkmeshes, chkbbox);
        Items.Add (new EF.StackLayoutItem (expander, expand: false));

        Cache.OnCacheChanged += _OnCacheChaged;
        UnLoad += (_, _) => { Cache.OnCacheChanged -= _OnCacheChaged; };

        _Update ();

    }

    private void _OnCacheChaged ()
    {
        _Update ();
    }

    void _Update ()
    {
        var sb = new StringBuilder ();
        uint c = 0;

        sb.AppendLine (" Count | Object type");
        sb.AppendLine (" ----- | -----------");
        foreach (RO.ObjectType t in Enum.GetValues (typeof (RO.ObjectType)))
        {
            if (Cache.States[t] > 0) { 
                sb.AppendLine (Cache.States[t].ToString ().PadLeft (6)+" | "+t);
                c += Cache.States[t];
            }
        }
        sb.AppendLine ("Total : "+c);
        sb.AppendLine ("Cache : "+Cache.Count.ToString ());
        // sb.AppendLine ();
        // sb.AppendLine ("If there is a difference between the `Total` of states and the `Cache`,");
        // sb.AppendLine ("Then multiple `Append` function calls are invoked for the same objects.");

        _tarea.Text = sb.ToString ();
    }
}


class CacheConduit : RD.DisplayConduit
{
    static CacheConduit? g_instance;
    
    /// <remarks>
    ///     Does not redraw views. </remarks>
    public static bool Active
    {
        get => (g_instance?.Enabled) ?? false;
    }

    /// <remarks>
    ///     Does not redraw views. </remarks>
    public static void Show (CacheOptions options)
    {
        if (options.DebugDisplayMeshes == false && options.DebugDisplayBBox == false)
            return;

        g_instance ??= new ();
        g_instance._options = options;
        g_instance.Enabled = true;
    }

    /// <remarks>
    ///     Does not redraw views. </remarks>
    public static void Hide ()
    {
        if (g_instance != null)
            g_instance.Enabled = false;
    }

    #nullable disable
    CacheOptions _options;
    CacheConduit () { /**/ }
    #nullable enable

    // https://discourse.mcneel.com/t/opengl-in-displayconduit/82076/44
    protected override void DrawOverlay (RD.DrawEventArgs e)
    {
        if (_options.DebugDisplayMeshes == _options.DebugDisplayBBox)
        {
            foreach (var (meshes, bbox) in Cache.Items)
            {
                e.Display.DrawBox (bbox, SD.Color.AntiqueWhite, 1);
                if (meshes != null) {
                    foreach (var m in meshes)
                        e.Display.DrawMeshWires (m, SD.Color.AntiqueWhite, 1);
                }
            }
        }
        else if (_options.DebugDisplayBBox)
        {
            foreach (var (_, bbox) in Cache.Items)
                e.Display.DrawBox (bbox, SD.Color.AntiqueWhite, 1);
        }
        else
        {
            foreach (var (meshes, bbox) in Cache.Items)
            {
                if (meshes != null) {
                    foreach (var m in meshes)
                        e.Display.DrawMeshWires (m, SD.Color.AntiqueWhite, 1);
                }
            }
        }
    }
}

#endif