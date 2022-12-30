/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/

#if DEBUG
#define DEBUG_CACHE
#endif

using System;
using System.Linq;
using System.Collections.Generic;

using RhinoApp = Rhino.RhinoApp;
using RH = Rhino;

using ON = Rhino.Geometry;
using RO = Rhino.DocObjects;
using RhinoDoc = Rhino.RhinoDoc;


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


/*/
    Optimizing intersection computation is important with managed code-side storage of object information.
    But this has a cost when creating, deleting or modifying objects.
    Moving 1000 objects at once will take longer than Rhino's default behavior.
    So to limit the impact, the meshes of the objects are extracted (and if necessary created) during the Idle event.
/*/


public class IdleQueue<T>
{
    public IdleQueue (ProcessCallback callback) { _process = callback; }

    public delegate void ProcessCallback (T arg);
    public ProcessCallback _process;

    readonly Queue<T> _queue = new ();

    public void Clear () { _queue.Clear (); }

    // Indicate new data in the queue.
    bool _update = false;

    // Indicates that the Idle event is already attached.
    bool _attached = false;

    public void Enqueue (T obj)
    {
        if (obj == null)
            return;

        _queue.Enqueue (obj);

        _update = true;
        if (_attached == false)
            RhinoApp.Idle += _OnRhinoIdle;
    }

    void _OnRhinoIdle (object sender, EventArgs e)
    {
        if (_update)
        {
            var item = _queue.Dequeue ();
            _update = _queue.Count > 0;
            if (item != null) _RunProcess (item);
        }
        else
        {
            RhinoApp.Idle -= _OnRhinoIdle;
            _attached = false;
        }
    }

    void _RunProcess (T item)
    {
        try
        {
            _process (item);
        }
        catch (Exception e)
        {
            #if DEBUG
            RhinoApp.WriteLine (e.Message);
            // RhinoLinkService.Logger?.LogError (e.Message);
            #endif
        }
    }
}


static class Cache
{
    #region Data

    static readonly Dictionary<Guid, CacheItem> _cache = new ();

    static readonly ON.Mesh[] EMPTY_MESH_ARRAY = Array.Empty <ON.Mesh> ();

    public struct CacheItem
    {
        public ON.Mesh[] Meshes;
        public ON.BoundingBox BBox;
        public bool IsVisible;
        public bool IsValid;

        public CacheItem (RO.RhinoObject obj)
        {
            var bbox = obj.Geometry.GetBoundingBox (accurate: false);
            Meshes    = EMPTY_MESH_ARRAY;
            BBox      = bbox;
            IsValid   = false; // exclude this object from intersections
            IsVisible = obj.Attributes.Visible;
        }

        public void Deconstruct (out ON.Mesh[] meshes, out ON.BoundingBox bBox, out bool isVisible, out bool isValid)
        {
            meshes    = Meshes;
            bBox      = BBox;
            isVisible = IsVisible;
            isValid   = IsValid;
        }
    }

    public static ICollection <CacheItem> Items => _cache.Values;

    public static void Clear ()
    {
        _idlemesher.Clear ();
        _cache.Clear ();
    }

    public static void Append (RO.RhinoObject obj)
    {
        _cache[obj.Id] = new CacheItem (obj);
        _idlemesher.Enqueue (new (obj.Document, obj.Id));
    }

    public static void Remove (Guid objectId)
    {
        Remove (objectId);
    }

    public static void SetIntersectionMeshes (Guid objectId, ON.Mesh[] meshes)
    {
        var item = _cache[objectId];
        item.Meshes = meshes;
        item.IsValid = item.BBox.IsValid;
        _cache[objectId] = item;
    }

    public static void SetVisibility (Guid objectId, bool visible)
    {
        var item = _cache[objectId];
        item.IsVisible = visible;
        _cache[objectId] = item;
    }

    #endregion

    #region Idle Processor

    static IdleQueue <ProcessArg> _idlemesher = new (_OnProcess);

    readonly struct ProcessArg
    {
        public readonly RhinoDoc Doc;
        public readonly Guid ObjectId;

        public ProcessArg (RhinoDoc doc, Guid objectId) {
            Doc = doc; ObjectId = objectId;
        }
    }

    static void _OnProcess (ProcessArg arg)
    {
        var obj = arg.Doc.Objects.FindId (arg.ObjectId);
        if (obj == null) {
            Remove (arg.ObjectId);
            return;
        }

        SetIntersectionMeshes(
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

    #region Rhino Events

    /*/
        Event stack:

        - _OnCloseDocument or _OnNewDocument

        - then per file in worksession:
          - _OnBeginOpenDocument
          - _OnAddRhinoObject*...
          - _OnEndOpenDocument
          - _OnEndOpenDocumentInitialViewUpdate
          - _OnActiveDocumentChanged or not

        !!! Except _OnNewDocument, no event is called when the application starts without opening an existing file. !!!
    /*/

    public static void AttachEvents ()
    {
        RhinoDoc.CloseDocument          += _OnCloseDocument;
        RhinoDoc.NewDocument            += _OnNewDocument;
        RhinoDoc.AddRhinoObject         += _OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject      += _OnDeleteRhinoObject;
        RhinoDoc.ModifyObjectAttributes += _OnModifyObjectAttributes;
        RhinoDoc.UndeleteRhinoObject    += _OnUndeleteRhinoObject;

        RhinoApp.Closing += _OnAppClosing;
    }

    public static void DetachEvents ()
    {
        RhinoDoc.CloseDocument          -= _OnCloseDocument;
        RhinoDoc.NewDocument            -= _OnNewDocument;
        RhinoDoc.AddRhinoObject         -= _OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject      -= _OnDeleteRhinoObject;
        RhinoDoc.ModifyObjectAttributes -= _OnModifyObjectAttributes;
        RhinoDoc.UndeleteRhinoObject    -= _OnUndeleteRhinoObject;
    }

    static void _OnAppClosing (object sender, EventArgs e)
    {
        DetachEvents ();
    }

    static void _OnCloseDocument (object sender, RH.DocumentEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnCloseDocument));
        #endif

        Clear ();
    }

    static void _OnNewDocument (object sender, RH.DocumentEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnNewDocument));
        #endif

        Clear ();
    }

    static void _OnDeleteRhinoObject (object sender, RO.RhinoObjectEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnDeleteRhinoObject));
        #endif

        Remove (e.ObjectId);
    }

    static void _OnAddRhinoObject (object sender, RO.RhinoObjectEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnAddRhinoObject));
        #endif

        Append (e.TheObject);
    }

    static void _OnUndeleteRhinoObject (object sender, RO.RhinoObjectEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnUndeleteRhinoObject));
        #endif

        Append (e.TheObject);
    }

    static void _OnModifyObjectAttributes (object sender, RO.RhinoModifyObjectAttributesEventArgs e)
    {
        #if DEBUG_CACHE
        RhinoApp.WriteLine (nameof (_OnModifyObjectAttributes));
        #endif

        SetVisibility (e.RhinoObject.Id, e.NewAttributes.Visible);
    }

    #endregion

    #region Debug

    static readonly RO.ObjectEnumeratorSettings _enumopts = new () {
            DeletedObjects        = false,
            // NormalObjects         = true,
            HiddenObjects         = true,
            LockedObjects         = true,
            // IncludeGrips          = false,
            // IncludeLights         = false,
            // SubObjectSelected     = false,
            // ActiveObjects         = true,
            // IdefObjects           = true,
            IncludePhantoms       = true,    // ???
            ReferenceObjects      = true,
            // SelectedObjectsFilter = true,
            // VisibleFilter         = true,
            ObjectTypeFilter = RO.ObjectType.AnyObject
    };

    public static void TestCacheObjects (RhinoDoc doc)
    {
        var count = doc.Objects.ObjectCount (_enumopts);
        RhinoApp.WriteLine ("Objects.Count: " + count);

        if (count != _cache.Count) RhinoApp.WriteLine (
            "Document.Objects.Count != _bboxcache.Count" +
            "\n  count: " + count + //doc.Objects.Count +
            "\n  cache: " + _cache.Count
        );
    }

    #endregion
}