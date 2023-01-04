/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


#if DEBUG
// #define DEBUG_EVENTS
#endif


using System;
using System.Collections.Generic;

using RhinoApp = Rhino.RhinoApp;


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


/// <summary>
///     A queue to execute a callback function on Rhino's idle event. </summary>
/// <typeparam name="T">
///     Data sent to <see cref="ProcessCallback"/>. </typeparam>
public class IdleQueue<T>
{
    /// <param name="callback">
    ///     Callback function to run on Rhino idle events. </param>
    /// <param name="endCallback">
    ///     Callback function to execute when there is no more data in the queue. </param>
    public IdleQueue (ProcessCallback callback, Action? endCallback = null)
    {
        _process = callback;
        _callback = endCallback;
    }

    /// <summary>
    ///     Callback function to process queued data. </summary>
    /// <param name="arg">
    ///     Data to be processed. </param>
    public delegate void ProcessCallback (T arg);

    readonly ProcessCallback _process;

    readonly Action? _callback;

    readonly Queue<T> _queue = new ();

    // Indicates that the Idle event is already attached.
    bool _attached = false;

    #if DEBUG
    int _processcount;
    #endif

    /// <summary>
    ///     Delete all queue data. </summary>
    public void Clear () { _queue.Clear (); }

    /// <summary>
    ///     Add data to the queue. </summary>
    public void Enqueue (T obj)
    {
        if (obj == null)
            return;

        _queue.Enqueue (obj);

        if (_attached) return;

        #if DEBUG
         RhinoApp.WriteLine ("Start IdleQueue");
        _processcount = 0;
        #endif

        RhinoApp.Idle += _OnRhinoIdle;
        _attached = true;
    }

    void _OnRhinoIdle (object sender, EventArgs e)
    {
        if (_queue.Count > 0)
        {
            #if DEBUG
            _processcount++;
            #endif

            var item = _queue.Dequeue ();
            if (item != null) RunProcess (item);
        }
        else
        {
            #if DEBUG
            RhinoApp.WriteLine ("Number of calls in idle processes: "+_processcount);
            #endif

            RhinoApp.Idle -= _OnRhinoIdle;
            _attached = false;
            
            _callback?.Invoke ();
        }
    }

    void RunProcess (T item)
    {
        try
        {
            _process (item);
        }
        catch (Exception e)
        {
            #if DEBUG
            RhinoApp.WriteLine (e.Message);
            #endif
        }
    }
}


public class IdleRhinoEventGroup
{
    uint _rheventgroupcount;
    Action _callback;

    public IdleRhinoEventGroup (Action callback) { _callback = callback; }

    public bool IsStarted => _rheventgroupcount > 0;

    public void Reset ()
    {
        RhinoApp.Idle -= _OnRhinoIdle;
        _rheventgroupcount = 0;
    }

    public void Increment ()
    {
        if (_rheventgroupcount == 0)
            RhinoApp.Idle += _OnRhinoIdle;
        _rheventgroupcount++;
    }

    void _OnRhinoIdle (object sender, EventArgs e)
    {
        if (_rheventgroupcount == 0) {
            RhinoApp.Idle -= _OnRhinoIdle;
            _callback ();
        }
        else _rheventgroupcount--;
    }
}


#if false
public static class Views
{
    static RhinoDoc? _doc;
    static RD.RhinoViewport? _viewport;

    public static void Start ()
    {
        RhinoApp.Idle += _OnIdle;
    }

    public static void Stop ()
    {
        RhinoApp.Idle -= _OnIdle;
    }

    public static void Redraw (RD.RhinoViewport viewport)
    {
        _viewport = viewport;
    }

    public static void Redraw (RhinoDoc doc)
    {
        _doc = doc;
    }

    static void _OnIdle (object sender, EventArgs e)
    {
        if (_doc != null)
        {
            _doc.Views.Redraw ();
            _doc = null;
            _viewport = null;
        }
        else if (_viewport != null) 
        {
            _viewport.ParentView.Redraw ();
            _viewport = null;
        }
    }
}
#endif
