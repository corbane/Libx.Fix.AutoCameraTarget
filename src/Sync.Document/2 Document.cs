/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


#if CSX
#load "../Sync/1 Idle.cs"
#endif


using System;
using System.Runtime.CompilerServices;

using RH = Rhino;
using RC = Rhino.Commands;
using RO = Rhino.DocObjects;
using RhinoDoc = Rhino.RhinoDoc;


#if RHP
namespace Libx.Fix.AutoCameraTarget.Sync;
#endif


public static class DocumentObserver
{
    const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;

    public delegate void OnBeginDocumentHandler ();
    public delegate void OnEndDocumentHandler (RhinoDoc? doc);
    public delegate void OnAppendHandler (RO.RhinoObject obj);
    public delegate void OnRemoveHandler (Guid guid);

    public static event OnBeginDocumentHandler? OnBeginDocument;
    public static event OnEndDocumentHandler? OnEndDocument;
    public static event OnAppendHandler? OnAppendObject;
    public static event OnRemoveHandler? OnRemoveObject;

    static RhinoDoc? _topdoc;

    // public DocumentObserver(
    //     Action? onBeginDocument,
    //     Action<RhinoDoc?>? onEndDocument,
    //     Action<RO.RhinoObject>? onAppendObject,
    //     Action<Guid>? onRemoveObject
    // )
    // {
    //     OnBeginDocument = onBeginDocument;
    //     OnEndDocument = onEndDocument;
    //     Append = onAppendObject;
    //     Remove = onRemoveObject;
    //     _eventgroup = new(_OnEndIdle);
    // }
    static DocumentObserver ()
    {
        _eventgroup = new(_OnEndIdle);
        AttachEvents ();
    }


    #region Rhino Events
    /*/ Event stack:
         
        By default, with blank document.
        - OnNewDocument

        On dbl click or drag:
        - [OnCloseDocument] if application is open
        - OnLayerTableEvent*?
        - OnBeginOpenDocument
        - OnAddRhinoObject*?
        - OnEndOpenDocument

        On dbl click or drag on a worksession:
        - ...normal file
        - then per file in worksession:
          - OnBeginOpenDocument
          - OnLayerTableEvent*?
          - OnAddRhinoObject*?
          - OnEndOpenDocument
          - OnEndOpenDocumentInitialViewUpdate

        !!! I don't know how to execute an action after loading an RWS file. !!!
        NOTE: OnActiveDocumentChanged is different on Mac and PC
              OnActiveDocumentChanged is called before the linked files are loaded.
    /*/

    static bool _isattached = false;

    public static void AttachEvents()
    {
        if (_isattached) return;
        _isattached = true;

        RhinoDoc.CloseDocument     += _OnCloseDocument;
        RhinoDoc.BeginOpenDocument += _OnBeginOpenDocument;
        RhinoDoc.EndOpenDocument   += _OnEndOpenDocument;

        RhinoDoc.AddRhinoObject               += _OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject            += _OnDeleteRhinoObject;
        RhinoDoc.ModifyObjectAttributes       += _OnModifyObjectAttributes;
        RhinoDoc.UndeleteRhinoObject          += _OnUndeleteRhinoObject;
        RhinoDoc.LayerTableEvent              += _OnLayerTableEvent;
        RhinoDoc.InstanceDefinitionTableEvent += _OnInstanceDefinitionTableEvent;
    }

    public static void DetachEvents()
    {
        RhinoDoc.CloseDocument     -= _OnCloseDocument;
        RhinoDoc.BeginOpenDocument -= _OnBeginOpenDocument;
        RhinoDoc.EndOpenDocument   -= _OnEndOpenDocument;

        RhinoDoc.AddRhinoObject               -= _OnAddRhinoObject;
        RhinoDoc.DeleteRhinoObject            -= _OnDeleteRhinoObject;
        RhinoDoc.ModifyObjectAttributes       -= _OnModifyObjectAttributes;
        RhinoDoc.UndeleteRhinoObject          -= _OnUndeleteRhinoObject;
        RhinoDoc.LayerTableEvent              -= _OnLayerTableEvent;
        RhinoDoc.InstanceDefinitionTableEvent -= _OnInstanceDefinitionTableEvent;

        _isattached = false;
    }

    #endregion


    #region Rhino Document Events

    static readonly IdleQueueIncrement _eventgroup;

    static void _OnEndIdle()
    {
        OnEndDocument?.Invoke(_topdoc);
        _topdoc = null;
    }

    /// <summary>
    ///     When creating a new document OR opening a file. </summary>
    static void _OnCloseDocument(object sender, RH.DocumentEventArgs e)
    {
        DBG.Log ();

        OnBeginDocument?.Invoke();
        _eventgroup.Reset();
    }

    /// <summary>
    ///     At the start of loading files or referenced files </summary>
    static void _OnBeginOpenDocument(object sender, RH.DocumentOpenEventArgs e)
    {
        DBG.Log ();

        _LockTableEvents();
        if (_eventgroup.IsStarted == false)
            _topdoc = e.Document;
        _eventgroup.Increment();
    }

    /// <summary>
    ///     At the end of loading files or referenced files </summary>
    static void _OnEndOpenDocument(object sender, RH.DocumentOpenEventArgs e)
    {
        DBG.Log ();

        _UnLockTableEvents();
    }

    #endregion


    #region Rhino Table Events

    /// <summary>
    ///     Flag to disable object and layer listeners. </summary>
    /// <remarks>
    ///     For a 3dm or rws file, the active file and referenced files receive `[Begin|End]OpenDocument` events.
    ///     Each time Rhino loads a file, layer and object listeners are disabled by this flag.
    ///     Cache entries are added at the end of each document load. </remarks>
    static bool _locktableevents = false;

    [MethodImpl(INLINE)] static void _LockTableEvents() { _locktableevents = true; }

    [MethodImpl(INLINE)] static void _UnLockTableEvents() { _locktableevents = false; }

    /// <summary>
    ///     On creation OR modification (deletion then creation) OR loading (disabled by `_locktableevents`) of objects </summary>
    static void _OnAddRhinoObject(object sender, RO.RhinoObjectEventArgs e)
    {
        if (_locktableevents || e.TheObject.IsInstanceDefinitionGeometry) return;

        DBG.Log (e.TheObject.ObjectType);

        if (e.TheObject.Visible) OnAppendObject?.Invoke(e.TheObject);
    }

    static void _OnInstanceDefinitionTableEvent(object sender, RO.Tables.InstanceDefinitionTableEventArgs e)
    {
        if (_locktableevents) return;

        DBG.Log (e.InstanceDefinitionIndex);

        foreach (var r in e.NewState.GetReferences(wheretoLook: 1 /*Top level and nested in doc*/))
            if (r.Visible) OnAppendObject?.Invoke(r);
    }

    /// <summary>
    ///     On deletion OR modification (deletion then creation) of objects. </summary>
    static void _OnDeleteRhinoObject(object sender, RO.RhinoObjectEventArgs e)
    {
        // Currently `_locktableevents` is only used during the loading process
        // if (_locktableevents) return;

        DBG.Log (e.TheObject.ObjectType);

        OnRemoveObject?.Invoke(e.ObjectId);
    }

    /// <summary>
    ///     On undo, `_OnAddRhinoObject` is not called but this callback instead. </summary>
    static void _OnUndeleteRhinoObject(object sender, RO.RhinoObjectEventArgs e)
    {
        // Currently `_locktableevents` is only used during the loading process
        // if (_locktableevents) return;

        DBG.Log ();

        if (e.TheObject.Visible) OnAppendObject?.Invoke(e.TheObject);
    }

    /// <summary>
    ///     Listen to object visibility. </summary>
    static void _OnModifyObjectAttributes(object sender, RO.RhinoModifyObjectAttributesEventArgs e)
    {
        if (_locktableevents) return;

        DBG.Log (nameof (_OnModifyObjectAttributes)+" "+e.RhinoObject.ObjectType);

        if (e.OldAttributes.Visible != e.NewAttributes.Visible)
        {
            // !!! see Cache.cs header !!! 
            if (_IsCommand("UnlockSelected")) return;

            if (e.NewAttributes.Visible) OnAppendObject?.Invoke(e.RhinoObject);
            else OnRemoveObject?.Invoke(e.RhinoObject.Id);
        }
        else if (e.OldAttributes.LayerIndex != e.NewAttributes.LayerIndex)
        {
            if (e.Document.Layers[e.NewAttributes.LayerIndex].IsVisible)
            {
                if (e.Document.Layers[e.OldAttributes.LayerIndex].IsVisible == false)
                    OnAppendObject?.Invoke(e.RhinoObject);
            }
            else
            {
                OnRemoveObject?.Invoke(e.RhinoObject.Id);
            }
        }
    }

    /// <summary>
    ///     Listen to object visibility. </summary>
    /// <remarks>
    ///     Called when layer state changes (Hide/Show/Enabled/...) or loaded (disabled by `_locktableevents`) </remarks>
    static void _OnLayerTableEvent(object sender, RO.Tables.LayerTableEventArgs e)
    {
        if (_locktableevents) return;

        DBG.Log ();

        if (e.OldState != null && e.OldState.IsVisible == e.NewState.IsVisible) return;

        // delete layer                     OK. _OnDeleteRhinoObject is called
        // undo deleted layer               OK. _OnUndeleteRhinoObject is called
        // show layer with hidden parent    OK. ??? I don't know why it works
        // show layer with hidden children  OK. ??? Maybe `Objects.FindByLayer` or/and `RhinoObject.Visible` does the job.

        if (e.NewState.IsVisible) _AppendLayer(e.Document, e.NewState);
        else _RemoveLayer(e.Document, e.NewState);
    }

    #endregion


    #region Helpers

    static void _AppendLayer(RhinoDoc doc, RO.Layer layer)
    {
        // `Objects.FindByLayer` returns the objects of the layer and descendent visible layers.
        foreach (var obj in doc.Objects.FindByLayer(layer))
            if (obj.Visible) OnAppendObject?.Invoke(obj);
    }

    static void _RemoveLayer(RhinoDoc doc, RO.Layer layer)
    {
        // `Objects.FindByLayer` returns the objects of the layer and descendent visible layers.
        foreach (var obj in doc.Objects.FindByLayer(layer))
            OnRemoveObject?.Invoke(obj.Id);
    }

    static bool _IsCommand(string name)
    {
        if (RC.Command.InCommand())
        {
            var guids = RC.Command.GetCommandStack();
            if (guids.Length > 0)
            {
                return name == RC.Command.LookupCommandName(guids[0], englishName: true);
            }
        }
        return false;
    }

    #endregion
}
