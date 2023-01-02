/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/


using System;
using System.Runtime.CompilerServices;
using SD = System.Drawing;

using ED = Eto.Drawing;

using RD = Rhino.Display;
using RUI = Rhino.UI;


#if RHP
namespace Libx.Fix.AutoCameraTarget;
#endif


/// <summary>
///     Wrapper class above <see cref="NavigationListener"/> </summary>
class RMBListener : RUI.MouseCallback
{
    NavigationListener _listener;

    public RMBListener (NavigationListener listener)
    {
        _listener = listener;
    }

    protected override void OnEndMouseDown (RUI.MouseCallbackEventArgs e)
    {
        if (_listener.CanRun (e))
            _listener.Start (e);
    }
}


class NavigationListener : RUI.MouseCallback
{
    const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;


    public RD.RhinoViewport Viewport { get; private set; }


    #nullable disable // Viewport
    public NavigationListener ()
    {
    } 
    #nullable enable


    #region Switches

    /// <summary>
    ///     Flag to cancel or not the MouseUp event.</summary>
    bool _started;
    
    /// <summary>
    ///     Flag blocking `OnMouseMove` event after cursor repositioning</summary>
    bool _lock;

    /// <summary>
    ///     Flag to temporarily escape the mouse move event. </summary>
    bool _pause;

    const int PAUSE_DELAY = 70;

    [MethodImpl(INLINE)] protected void StartPause ()
    {
        _pause = true;
        System.Threading.Tasks.Task.Delay (PAUSE_DELAY).ContinueWith ((_) => { _pause = false; });
    }

    #endregion


    #region Offset

    ED.Point _offset;

    [MethodImpl(INLINE)] ED.Point _GetOffset (SD.Point point)
    {
        _offset.X = point.X - Cursor.InitialCursorPosition.X;
        _offset.Y = point.Y - Cursor.InitialCursorPosition.Y;
        return _offset;
    }

    #endregion


    #region Modifiers

    readonly Action <ED.Point>?[] _actions = new Action <ED.Point> [Enum.GetNames (typeof(ModifierKey)).Length];

    readonly object?[] _tags = new object [Enum.GetNames (typeof(ModifierKey)).Length];

    /// <summary>
    ///     Define a callback function when moving the mouse.</summary>
    /// <param name="modifier">
    ///     One of the keys from <see cref="ModifierKey"/>
    ///     or <see cref="ModifierKey.None"/> if no modifier is required for this action.</param>
    /// <param name="action">
    ///     The action to execute.</param>
    /// <param name="tag">
    ///     Value sent to `OnActionChange` when modifier key and action change.</param>
    public void SetModifierCallback (ModifierKey modifier, Action <ED.Point>? action, object? tag)
    {
        _actions[(int)modifier] = action;
        _tags[(int)modifier] = tag;
    }

    /// <summary>
    ///     Returns the action associated with a modifier key. </summary>
    [MethodImpl(INLINE)] Action <ED.Point>? _GetAction (ModifierKey modifier)
    {
        return _actions[(int)modifier] ?? _actions[(int)ModifierKey.None];
    }

    /// <summary>
    ///     Returns the tag associated with an action. </summary>
    [MethodImpl(INLINE)] object? _GetActionTag (ModifierKey modifier)
    {
        return _tags[(int)modifier] ?? _tags[(int)ModifierKey.None];
    }

    ModifierKey _cmodifier;

    public ModifierKey ActiveModifier => _actions[(int)_cmodifier] != null ? _cmodifier : ModifierKey.None;

    [MethodImpl(INLINE)] void _SetActiveModifier (ModifierKey modifier)
    {
        _cmodifier = modifier;
    }

    [MethodImpl(INLINE)] ModifierKey _GetActiveModifier ()
    {
        return _cmodifier;
    }

    #endregion


    #region Sub classes methods

    /// <summary>
    ///     Called before activating the mouse listener to know whether to do it or not. </summary>
    public virtual bool CanRun (RUI.MouseCallbackEventArgs e) { return false; }

    /// <summary>
    ///     Called when the mouse first moves. </summary>
    protected virtual bool OnStartNavigation (RD.RhinoViewport viewport, SD.Point viewportPoint) { return true; }

    /// <summary>
    ///     Function called when the modifier key and its associated action change.
    ///     see <seealso cref="SetModifierCallback"/> </summary>
    /// <param name="previousTag">
    ///     Previously active action tag</param>
    /// <param name="currentTag">
    ///     New active action tag</param>
    protected virtual void OnActionChange (object? previousTag, object? currentTag) { /**/ }

    /// <summary>
    ///     Called when the right mouse button is up </summary>
    protected virtual void OnStopNavigation () { /**/ }

    #endregion


    internal void Start (RUI.MouseCallbackEventArgs e)
    {
        Viewport   = e.View.ActiveViewport;
        _started   = false;
        _lock      = false;
        Keyboard.MemorizeCapsLock ();
        Enabled    = true;
    }
    
    protected override void OnMouseMove (RUI.MouseCallbackEventArgs e)
    {
        // Waits for the mouse to move at least one pixel so as not to cancel the display of the contextual menu.
        if (_started == false) {
            _started = true;
                
            Enabled = OnStartNavigation (Viewport, e.ViewportPoint);
            if (Enabled) e.Cancel = true;
            else return;

            var mod = Keyboard.GetCurrentModifier ();

            // !!! Shouldn't be e.ViewportPoint but IntersectionData.ViewportPoint,
            //     because there may be an offset between the intersection point and the virtual cursor.
            //     but this class is not supposed to know `CameraController`
            // !!!
            Cursor.InitCursor (Viewport, e.ViewportPoint);
            Cursor.HideCursor ();

            _SetActiveModifier (mod);
            OnActionChange (null, _GetActionTag (mod));

            return;
        }

        // Is this function called after repositioning the cursor ?
        if (_lock) {
            e.Cancel = true;
            _lock = false;
            return;
        }

        // Is there a pause time between action changes ?
        if (_pause) {
            e.Cancel = true;
            _lock = true;
            Cursor.SetCursorPosition (Cursor.InitialCursorPosition);
            return;
        }

        var offset = _GetOffset (e.ViewportPoint);

        // Is there anything to move ?
        if (offset.X == 0 && offset.Y == 0) {
            e.Cancel = true;
            return;
        }

        // Is there a change of action ?
        var amodifier = _GetActiveModifier ();
        var cmodifier = Keyboard.GetCurrentModifier ();
        if (amodifier != cmodifier)
        {
            OnActionChange (_GetActionTag(amodifier), _GetActionTag(cmodifier));
            _SetActiveModifier (cmodifier);
            StartPause ();
        }

        // Is there a change of action ?
        var action = _GetAction (cmodifier);
        if (action != null) {
            e.Cancel = true;
            action (offset);
        }
    
        // e.Cancel = true; ??? pourquoi j'ai supprimé l'annulation ???
        _lock = true;
        Cursor.SetCursorPosition (Cursor.InitialCursorPosition);
    }

    protected override void OnMouseUp (RUI.MouseCallbackEventArgs e)
    {
        Keyboard.RestoreCapsLock ();
        Enabled = false;
        
        if (_started)
        {
            OnActionChange (_GetActionTag (_GetActiveModifier ()), null);

            e.Cancel = true;
            var pos = VirtualCursor.Position;
            Cursor.SetLimitedCursorPosition (pos.X, pos.Y);
            OnStopNavigation ();
        }
        
        Cursor.ShowCursor ();
    }
}

