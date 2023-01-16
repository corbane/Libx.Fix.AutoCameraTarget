/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/

#if CSX
#load "../Ui.Native/2 Mouse.cs"
#load "../Ui/1 Menu.cs"
#load "../Sync/1 Idle.cs"
#endif


using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SD = System.Drawing;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RH = Rhino;
using RD = Rhino.Display;
using RUI = Rhino.UI;
using RhinoDoc = Rhino.RhinoDoc;
using RhinoPaintColor = Rhino.ApplicationSettings.PaintColor;


#if RHP
using Libx.Fix.AutoCameraTarget.Config;
using Libx.Fix.AutoCameraTarget.Sync;
using Libx.Fix.AutoCameraTarget.Ui.Native;
namespace Libx.Fix.AutoCameraTarget.Ui;
#endif



// TODO: TO REMOVE
static class EtoHelpers
{
    public static EF.StackLayout Divider (string label)
    {
        return new EF.StackLayout {
            Orientation = EF.Orientation.Horizontal,
            Spacing = 8,
            Items = {
                new EF.StackLayoutItem (new EF.Label { Text = label }, EF.VerticalAlignment.Stretch, expand: false),
                new EF.StackLayoutItem (new RUI.Controls.Divider (), EF.VerticalAlignment.Stretch, expand: true)
            }
        };
    }
    
    public static EF.Expander Expander (string label, params EF.Control?[] items)
    {
        var stack = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
        };
        foreach (var c in items) stack.Items.Add (c);
        return new EF.Expander 
        {
            Header = EtoHelpers.Divider (label),
            Expanded = false,
            Content = stack
        };
    }
}

static class RhinoTheme
{
    public static ED.Color GetRhinoFactorColor (RhinoPaintColor id)
    {
        var c = RH.ApplicationSettings.AppearanceSettings.GetPaintColor (id);
        return ED.Color.FromArgb (c.ToArgb ());
    }

    public static ED.Color ControlBackground =  GetRhinoFactorColor (RhinoPaintColor.NormalStart);
    public static ED.Color OverControlBackground =  GetRhinoFactorColor (RhinoPaintColor.MouseOverControlStart);
    public static ED.Color EditBoxBackground =  GetRhinoFactorColor (RhinoPaintColor.EditBoxBackground);
    public static ED.Color TextEnabled =  GetRhinoFactorColor (RhinoPaintColor.TextEnabled);
    public static ED.Color TextDisabled =  GetRhinoFactorColor (RhinoPaintColor.TextDisabled);
    public static ED.Color ActiveCaption =  GetRhinoFactorColor (RhinoPaintColor.ActiveCaption);
    public static ED.Color InactiveCaption =  GetRhinoFactorColor (RhinoPaintColor.InactiveCaption);
    public static ED.Color ActiveViewportTitle =  GetRhinoFactorColor (RhinoPaintColor.ActiveViewportTitle);
    public static ED.Color InactiveViewportTitle =  GetRhinoFactorColor (RhinoPaintColor.InactiveViewportTitle);
}

static class LightTheme
{
    public static ED.Color OverTint = new ED.Color (ED.Colors.Black, 0.05f);

    public static ED.Color OverBackground = RhinoTheme.OverControlBackground;
    public static ED.Color HalfOverBackground = new ED.Color (RhinoTheme.OverControlBackground, 0.5f);
    public static ED.Color ActiveBackground = RhinoTheme.ActiveCaption;

    public static ED.Color OverInputBackground = ED.Colors.White;
    public static ED.Color ActiveInputBackground = ED.Colors.White;
    public static ED.Color InvalidInputBackground = ED.Colors.IndianRed;
    public static ED.Color InactiveInputBackground = ED.Colors.LightGrey;

    public static ED.Color ActiveInputText = ED.Colors.Black;
    public static ED.Color InactiveInputText = ED.Colors.DarkSlateGray;
    public static ED.Color DisabledInputText = ED.Colors.Gray;
}



/***   ██   ██ ███████ ██      ██████  ███████ ██████  ███████   ***/
/***   ██   ██ ██      ██      ██   ██ ██      ██   ██ ██        ***/
/***   ███████ █████   ██      ██████  █████   ██████  ███████   ***/
/***   ██   ██ ██      ██      ██      ██      ██   ██      ██   ***/
/***   ██   ██ ███████ ███████ ██      ███████ ██   ██ ███████   ***/


/*/
    This class is used to listen for mouse movements. It can be used to resize or move an Eto window.
    Eto windows already have this kind of functionality with `MovableByWindowBackground` and `Resizable` properties.
    However I encountered problems with `MovableByWindowBackground` on certain elements like `Label` included in a `Panel` of the `Tool` class.
    The Eto window could lose focus if moved too quickly and the mouse would start dragging a Rhino toolbar.
    Another problem with `MovableByWindowBackground` is that it cancels events like `OnMouseUp`.
    The `Resizable` property allows resizing the width and height of the window but there is no way to limit the resizing to a single axis, width or height.
    Using the `OnMouseDown` and `OnMouseUp` listeners to set a custom window size also causes problems when moving the mouse fast.
/*/

static class FormDrag
{
    static IMouseObserver? _msmove;

    static bool _started;

    public static bool InDrag => _started;

    static int _startX;
    static int _startY;

    static int _offsetX;
    static int _offsetY;

    const float OFFSET_EPSILON = 10;

    public static bool WindowMoved 
    {
        get {
            return Math.Abs (_offsetX) > OFFSET_EPSILON
                || Math.Abs (_offsetY) > OFFSET_EPSILON;
        }
    }

    /// <summary> Window moved. </summary>
    static EF.Window? _dragwindow;
    static ED.PointF _startwinpos;

    public delegate void DragHandler (int offsetX, int offsetY);
    public delegate void StopHandler ();
    static DragHandler? _callback;
    static StopHandler? _onstop;

    public static void Start (DragHandler? callback = null, StopHandler? onStop = null)
    {
        var pos = EF.Mouse.Position;
        _startX = (int)pos.X;
        _startY = (int)pos.Y;
        _callback = callback;
        _onstop = onStop;
        _Attach ();
    }

    public static void Start (EF.Window window, StopHandler? onStop = null)
    {
        var pos = EF.Mouse.Position;
        _startX = (int)pos.X;
        _startY = (int)pos.Y;
        _callback = null;
        _onstop = onStop;
        _Attach (window);
    }

    public static void Start (EF.MouseEventArgs e, DragHandler? callback = null, StopHandler? onStop = null)
    {
        var pos = e.Location;
        _startX = (int)pos.X;
        _startY = (int)pos.Y;
        _callback = callback;
        _onstop = onStop;
        _Attach ();
    }

    public static void Start (RUI.MouseCallbackEventArgs e, DragHandler? callback = null, StopHandler? onStop = null)
    {
        var pos = e.View.ClientToScreen (e.ViewportPoint);
        _startX = (int)pos.X;
        _startY = (int)pos.Y;
        _callback = callback;
        _onstop = onStop;
        _Attach ();
    }

    static void _Attach (EF.Window? window = null)
    {
        _started = true;

        _offsetX = 0;
        _offsetY = 0;

        _dragwindow = window;
        if (window != null)
            _startwinpos = new (window.Location);

        if (_msmove == null)
            _msmove = Mouse.CreateObserver ();
        if (_msmove.IsEnabled == false)
            _msmove.StartOnAllSystem (_OnMouseMoveUp, MouseEventType.Move, MouseEventType.Up);
    }

    public static void Stop ()
    {
        if (_msmove != null)
            _msmove.Stop ();
        _started = false;

        _onstop?.Invoke ();
        // _offsetX = _offsetY = 0;
    }

    static bool _OnMouseMoveUp (IMouseMessage message)
    {
        if (message.Type == MouseEventType.Up)
        {
            // DBG.Log (""+message.Type);
            Stop ();
        }
        else
        {
            _offsetX = message.ScreenX - _startX;
            _offsetY = message.ScreenY - _startY;
            // DBG.Log (""+_offsetX+" - "+_offsetY);

            if (_dragwindow != null)
            {
                _dragwindow.Location = new ED.Point (
                    (int)_startwinpos.X + _offsetX,
                    (int)_startwinpos.Y + _offsetY
                );
            }

            _callback?.Invoke (_offsetX, _offsetY);
        }
        return false;
    }
}



/***   ██████   █████  ████████  █████    ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██   ██ ███████    ██    ███████   ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██████  ██   ██    ██    ██   ██   ***/



public class ControlData : INotifyPropertyChanged
{
    int _minwidth = -1;
    int _maxwidth = -1;
    int _minheight = -1;
    int _maxheight = -1;
    
    bool _visible = true;
    bool _over = true;
    int _width = -1;
    int _height = -1;

    public int MinWidth  { get => _minwidth; set { Set (ref _minwidth, value); } }
    public int MaxWidth  { get => _maxwidth; set { Set (ref _maxwidth, value); } }
    public int MinHeight { get => _minheight; set { Set (ref _minheight, value); } }
    public int MaxHeight { get => _maxheight; set { Set (ref _maxheight, value); } }

    public bool IsOver     { get => _over; set { Set (ref _over, value); } }
    public bool IsVisible  { get => _visible; set { Set (ref _visible, value); } }
    public int Width       { get => _width; set { Set (ref _width, value); } }
    public int Height      { get => _height; set { Set (ref _height, value); } }


    #region Event

    public int DataVersion { get; private set; }

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected void Set <T> (ref T member, T value, [CallerMemberName] string? propertyName = null)
    {
        if (object.Equals (member, value)) return;
        if (value == null) {
            DBG.Fail ("value == null");
            return;
        }

        member = value;
        DataVersion++;
        Emit (propertyName);
    }

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}

public enum WindowInteractionMode { InScrolling, InDragging, IsSizing, None }

public class WindowData : ControlData
{
    WindowInteractionMode _mode = WindowInteractionMode.None;
    bool _visible = true;

    public WindowInteractionMode InteractionMode  { get => _mode; set { Set (ref _mode, value); } }
}



/***    ██████  ██████  ███    ██ ████████ ██████   ██████  ██        ***/
/***   ██      ██    ██ ████   ██    ██    ██   ██ ██    ██ ██        ***/
/***   ██      ██    ██ ██ ██  ██    ██    ██████  ██    ██ ██        ***/
/***   ██      ██    ██ ██  ██ ██    ██    ██   ██ ██    ██ ██        ***/
/***    ██████  ██████  ██   ████    ██    ██   ██  ██████  ███████   ***/


public interface IControl : INotifyPropertyChanged
{
    EF.Control Eto { get; }
}

public abstract class Control <T> : IControl where T : EF.Control, new()
{
    T? _control;

    public T Eto => _control ??= new T ();

    EF.Control IControl.Eto => Eto;


    #region Event

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected void Set <V> (ref V member, V value, [CallerMemberName] string? propertyName = null)
    {
        if (value == null || object.Equals (member, value)) return;
        member = value;
        Emit (propertyName);
    }

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}

public interface IActiveControl : IControl, INotifyPropertyChanged
{
    bool IsOver { get; set; }
    bool IsActive { get; set; }
}

public class ActiveControl <T> : Control <T> where T : EF.Control, new()
{
    bool _isover;
    bool _isactive;

    public bool IsActive
    {
        get => _isactive;
        set {
            if (_isactive == value) return;
            _isactive = value;
            _UpdateBackground ();
            Emit ();
        }
    }

    public bool IsOver
    {
        get => _isover;
        set {
            if (_isover == value) return;
            _isover = value;
            _UpdateBackground ();
            Emit ();
        }
    }
    
    void _UpdateBackground ()
    {
        if (_isactive)
            Eto.BackgroundColor = LightTheme.ActiveBackground;
        else if (_isover)
            Eto.BackgroundColor = LightTheme.OverBackground;
        else
            Eto.BackgroundColor = ED.Colors.Transparent;
    }
    
}

public interface IResizable : IControl
{
    bool AutoSize { get; set; }
    int Width { get; set; }
    int Height { get; set; }
    int MinWidth { get; set; }
    int MinHeight { get; set; }
    // int MaxWidth { get; set; }
    // int MaxHeight { get; set; }
}



/***    ██████  ██████  ███    ██ ████████  █████  ██ ███    ██ ███████ ██████    **/
/***   ██      ██    ██ ████   ██    ██    ██   ██ ██ ████   ██ ██      ██   ██   **/
/***   ██      ██    ██ ██ ██  ██    ██    ███████ ██ ██ ██  ██ █████   ██████    **/
/***   ██      ██    ██ ██  ██ ██    ██    ██   ██ ██ ██  ██ ██ ██      ██   ██   **/
/***    ██████  ██████  ██   ████    ██    ██   ██ ██ ██   ████ ███████ ██   ██   **/


public class VStack <T> : Control <EF.StackLayout> where T : IControl
{
    public VStack ()
    {
        Eto.Orientation = EF.Orientation.Vertical;
    }

    List <T> _children = new ();

    public int Count => _children.Count;

    public T this[int index] => _children[index];

    public void Append (T control)
    {
        _children.Add (control);
        Eto.Items.Add (new (control.Eto, EF.HorizontalAlignment.Stretch, expand: false));
    }

    public void Clear ()
    {
        Eto.Items.Clear ();
    }
}


public class VerticalListItem : EF.Panel, IActiveControl
{
    public EF.Control Eto => this;

    public VerticalListItem (EF.Control content)
    {
        Content = content;
    }

    bool _isover;
    bool _isactive;

    public bool IsActive
    {
        get => _isactive;
        set {
            if (_isactive == value) return;
            _isactive = false;
            _UpdateBackground ();
            Emit ();
        }
    }

    public bool IsOver
    {
        get => _isover;
        set {
            if (_isover == value) return;
            _isover = value;
            _UpdateBackground ();
            Emit ();
        }
    }

    void _UpdateBackground ()
    {
        if (_isactive)
            Eto.BackgroundColor = LightTheme.ActiveBackground;
        else if (_isover)
            Eto.BackgroundColor = LightTheme.OverBackground;
        else
            Eto.BackgroundColor = ED.Colors.Transparent;
    }
    
    #region Event

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected void Set <T> (ref T member, T value, [CallerMemberName] string? propertyName = null)
    {
        if (value == null || object.Equals (member, value)) return;
        member = value;
        Emit (propertyName);
    }

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}


public class VerticalList : EF.PixelLayout
{
    EF.PixelLayout _content;
    EF.StackLayout _stack;

    public VerticalList ()
    {
        _stack   = new ();
        _content = new EF.PixelLayout { };
        _content.Add (_stack, 0, 0);
        Add (_content, 0, 0);

        _idleonshow = new (_OnShown);
    }

    void _InitializeSizes ()
    {
        //_content.Width = Width;
        //_stack.Width = Width;

        //if (Height >= _maxheight) 
        //    _content.Height = Height;
    }

    IdleQueueIncrement _idleonshow;
    protected override void OnShown (EventArgs e)
    {
        if (_idleonshow.IsStarted == false)
            _idleonshow.Increment ();
        base.OnShown (e);
    }
    void _OnShown ()
    {
        DBG.Log ("#####################"+_stack.Height, TotalHeight);
    }
    

    #region Items

    public float ItemHeight { get; } = 22;
    public int ItemCount { get; private set; }

    public void Append (string text)
    {
        var line = new VerticalListItem (new EF.Label { Text = text, Height = 33 });
        _stack.Items.Add (new (line, EF.HorizontalAlignment.Stretch));
        ItemCount++;
        // ItemHeight = 33;
        // _maxheight += 33;
        _content.Height = (int)TotalHeight;
    }

    public void Append (Tool tool)
    {
        _stack.Items.Add (new (new VerticalListItem (tool), EF.HorizontalAlignment.Stretch));
        // ItemHeight = tool.Height;
        // _maxheight += tool.Height;
        //DBG.Log (_maxheight);
        //_content.Height = (int)_maxheight;
    }

    public void Clear ()
    {
        _stack.Items.Clear ();
        // _maxheight = -1;
    }

    #endregion

    public ED.Color OverBackground { get; set; } = LightTheme.OverTint;


    #region Scroll

    float TotalHeight => ItemHeight*ItemCount;

    float _scrolly;

    EF.Panel? _handle;

    void _InitializeHandle ()
    {
        if (_handle == null) {  
            _handle = new EF.Panel { Width = 6, Height = 32, BackgroundColor = ED.Colors.DarkGray };
            Add (_handle, Width-_handle.Width, 0);
        }
    }

    void _UpdateScroll ()
    {
        if (_handle == null) {
            DBG.Fail ("_handle == null");
            return;
        }
        
        _handle.Visible = true;

        var diff = TotalHeight-Height;

        // No scrollbar
        if (diff <= 0)
        {
            _scrolly=0;
            _handle.Visible = false;
        }
        // Scroll on top
        else if (_scrolly > 0)
        {
            _scrolly=0;
        }
        // scoll on bottom
        else if (_scrolly < -diff)
        {
            _scrolly = (int)-diff;
        }

        _content.Move (_stack, 0, (int)_scrolly);

        var hpos = (Height-_handle.Height)*(_scrolly/diff);
        _content.Move (_handle, Width-_handle.Width, -(int)hpos);
    }

    #endregion
    

    #region Drag

    bool _indrag;
    float _starty;
    float _dragafactor;

    void _StartDrag (float mouseY)
    {
        _starty      = _scrolly;
        _dragafactor = EF.Screen.PrimaryScreen.Bounds.Height/TotalHeight;
        _indrag      = true;

        FormDrag.Start (_OnDrag, _StopDrag);
    }
    
    void _StopDrag ()
    {
        _indrag = false;
    }
    
    void _OnDrag (int offsetX, int offsetY)
    {
        //_scrolly = (_starty-offsetY)*_dragafactor;
        _scrolly = _starty-offsetY;
        _UpdateScroll ();
    }

    #endregion


    #region Over

    int _overitem = -1;
    
    void SetOverItem (int index, bool value)
    {
        if (index < 0 || _stack.Items.Count <= index || _stack.Items[index].Control is not IActiveControl item)
            return;
        item.IsOver = value;
    }
    
    void _StopOver ()
    {
        SetOverItem (_overitem, false);
    }
    
    void _OnOver (float mouseY)
    {
        var index = (int)((mouseY + -_scrolly)/ItemHeight);
        if (index != _overitem)
        {
        DBG.Log (mouseY, _scrolly, ItemHeight, index);
            SetOverItem (_overitem, false);
            SetOverItem (index, true);
            _overitem = index;
        }
    }

    #endregion


    #region Form Events

    protected override void OnLoadComplete (EventArgs e)
    {
        _InitializeHandle ();
        base.OnLoadComplete (e);
    }
    
    protected override void OnSizeChanged (EventArgs e)
    {
        _InitializeSizes ();
        _UpdateScroll ();
        base.OnSizeChanged(e);
    }
    
    protected override void OnMouseEnter (EF.MouseEventArgs e)
    {
        BackgroundColor = OverBackground;
        base.OnMouseEnter (e);
    }
    
    protected override void OnMouseLeave (EF.MouseEventArgs e)
    {
        _StopOver ();
        BackgroundColor = ED.Colors.Transparent;
        base.OnMouseLeave (e);
    }
    
    protected override void OnMouseWheel (EF.MouseEventArgs e)
    {
        _scrolly += (int)e.Delta.Height * 20;
        _UpdateScroll ();
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown (EF.MouseEventArgs e)
    {
        _StopOver ();
        if (e.Buttons == EF.MouseButtons.Middle) {
            _StartDrag (e.Location.Y);
            e.Handled = true;
        }
        else base.OnMouseDown (e);
    }

    protected override void OnMouseMove (EF.MouseEventArgs e)
    {
        if (_indrag == false)
            _OnOver (e.Location.Y);
    }

    protected override void OnMouseUp (EF.MouseEventArgs e)
    {
        _StopDrag ();
        _OnOver (e.Location.Y);
        base.OnMouseUp (e);
    }

    #endregion
}



/*** ██       █████  ██    ██  ██████  ██    ██ ████████   ***/
/*** ██      ██   ██  ██  ██  ██    ██ ██    ██    ██      ***/
/*** ██      ███████   ████   ██    ██ ██    ██    ██      ***/
/*** ██      ██   ██    ██    ██    ██ ██    ██    ██      ***/
/*** ███████ ██   ██    ██     ██████   ██████     ██      ***/


public class VLayout : Control <EF.StackLayout>
{
    public EF.Panel _head { get; }
    public EF.Panel _body { get; }
    public EF.Panel _foot { get; }

    public VLayout ()
    {
        _head = new EF.Panel { };
        _body = new EF.Panel { };
        _foot = new EF.Panel { };

        var eto = Eto;
        eto.Orientation = EF.Orientation.Vertical;
        eto.Items.Add (new (_head, EF.HorizontalAlignment.Stretch, expand: false));
        eto.Items.Add (new (_body, EF.HorizontalAlignment.Stretch, expand: true));
        eto.Items.Add (new (_foot, EF.HorizontalAlignment.Stretch, expand: false));
    }

    public EF.Control? Head
    {
        get => _head.Content;
        set {
            if (_head.Content == value) return;
            _head.Content = value;
            //Emit ();
        }
    }

    public EF.Control? Body
    {
        get => _body.Content;
        set {
            if (_body.Content == value) return;
            _body.Content = value;
            //Emit ();
        }
    }

    public EF.Control? Foot
    {
        get => _foot.Content;
        set {
            if (_foot.Content == value) return;
            _foot.Content = value;
            //Emit ();
        }
    }
}


public class VHLayout : EF.StackLayout
{
    public EF.StackLayoutItem _head { get; }
    public EF.StackLayoutItem _side { get; }
    public EF.StackLayoutItem _body { get; }
    public EF.StackLayoutItem _foot { get; }

    public VHLayout ()
    {
        _side = new (null, EF.VerticalAlignment.Stretch, expand: false);
        _body = new (null, EF.VerticalAlignment.Stretch, expand: true);

        var stack = new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            Spacing = 8,
            Items = { _side, _body }
        };

        _head = new (null, EF.HorizontalAlignment.Stretch, expand: false);
        _foot = new (null, EF.HorizontalAlignment.Stretch, expand: false);

        Orientation = EF.Orientation.Vertical;
        Items.Add (_head);
        Items.Add (new (stack, EF.HorizontalAlignment.Stretch, expand: true));
        Items.Add (_foot);

        this.KeyDown += delegate { DBG.Log (); };
    }

    public EF.Control? Head
    {
        get => _head.Control;
        set => _head.Control = value;
    }

    public EF.Control? Side
    {
        get => _side.Control;
        set => _side.Control = value;
    }

    public EF.Control? Body
    {
        get => _body.Control;
        set => _body.Control = value;
    }

    public EF.Control? Foot
    {
        get => _foot.Control;
        set => _foot.Control = value;
    }
}



/***  ██     ██ ██ ███    ██ ██████   ██████  ██     ██   ***/
/***  ██     ██ ██ ████   ██ ██   ██ ██    ██ ██     ██   ***/
/***  ██  █  ██ ██ ██ ██  ██ ██   ██ ██    ██ ██  █  ██   ***/
/***  ██ ███ ██ ██ ██  ██ ██ ██   ██ ██    ██ ██ ███ ██   ***/
/***   ███ ███  ██ ██   ████ ██████   ██████   ███ ███    ***/


public interface IWindowData : INotifyPropertyChanged
{
    EF.Control? ActiveControl { get; }
    EF.Control? OverControl { get; }
}

public abstract class FloatingForm : EF.Form
{
    public FloatingForm ()
    {
        Owner = RUI.RhinoEtoApp.MainWindow;
        MovableByWindowBackground = true;
        WindowStyle = EF.WindowStyle.None;
        Visible = false;
    }

    #if RHP

    protected override void OnLoadComplete (EventArgs e)
    {
        Main.RestoreWindow (this);
        base.OnLoadComplete(e);
    }

    protected override void OnClosing (CancelEventArgs e)
    {
        Main.SaveWindow (this);
        base.OnClosing (e);
    }

    #else

    // static ED.Size _previousSize = new (-1, -1);
    // static ED.Point _previousLocation = new (-1, -1);

    protected override void OnPreLoad (EventArgs e)
    {
        base.SizeChanged += _OnSizeChanged;
        base.OnPreLoad (e);
    }
    
    void _OnSizeChanged (object sender, EventArgs e)
    {
        if (Loaded == false) return;

        // if (_previousSize.Width != -1) {
        //     Size = _previousSize;
        //     Location = _previousLocation;
        // }

        var pos = RUI.RhinoEtoApp.MainWindow.Location;
        var size = RUI.RhinoEtoApp.MainWindow.Size;

        base.Location = new ED.Point(
            (int)(pos.X + size.Width*0.5 - Width*0.5),
            (int)(pos.Y + size.Height*0.5 - Height*0.5)
        );
    }
    protected override void OnShown (EventArgs e)
    {
        base.SizeChanged -= _OnSizeChanged;
        base.OnShown (e);
    }

    protected override void OnClosed (EventArgs e)
    {
        DBG.Log ();

        // _previousSize = Size;
        // _previousLocation = Location;
        base.OnClosed (e);
    }

    #endif
}


public enum HandlerPosition { Right, Bottom, Corner }

public class Handle : ActiveControl <EF.Panel>
{
    IResizable _target { get; set; }

    HandlerPosition _position;

    public Handle (IResizable targetControl, HandlerPosition position)
    {
        _target = targetControl;
        _position = position;

        Size = 10;
        _SetCursor ();

        Eto.MouseEnter += _MouseEnter;
        Eto.MouseLeave += _MouseLeave;
        Eto.MouseDown += _OnMouseDown;
        Eto.MouseDoubleClick += _OnMouseDoubleClick;
    }

    void _SetCursor ()
    {
        switch (_position)
        {
        case HandlerPosition.Corner:

            Eto.Cursor = EF.Cursors.SizeBottomRight;
            break;

        case HandlerPosition.Right:

            Eto.Cursor = EF.Cursors.SizeRight;
            break;
            
        case HandlerPosition.Bottom:

            Eto.Cursor = EF.Cursors.SizeBottom;
            break;
        }
    }

    void _MouseEnter (object _, EF.MouseEventArgs e)
    {
        IsOver = true;
    }

    void _MouseLeave (object _, EF.MouseEventArgs e)
    {
        IsOver = false;
    }

    int _size = 10;
    public int Size
    {
        get => _size;
        set { Eto.Width = Eto.Height = _size = value; }
    }

    int _startW;
    int _startH;

    void _StartResize ()
    {
        if (_target == null) return;
        _startW = _target.Width;
        _startH = _target.Height;
        IsActive = true;
        FormDrag.Start (_OnResize, _OnStoptResize);
    }

    void _OnStoptResize ()
    {
        IsActive = false;
    }

    void _OnResize (int offsetX, int offsetY)
    {
        switch (_position)
        {
        case HandlerPosition.Corner:

            // _target!.Size = new (_startW + offsetX, _startH + offsetY);
            _target!.Width  = _startW + offsetX;
            _target!.Height = _startH + offsetY;
            break;

        case HandlerPosition.Right:

            _target!.Width = _startW + offsetX;
            break;
            
        case HandlerPosition.Bottom:

            _target!.Height = _startH + offsetY;
            break;
        }
    }

    void _OnAutoResize ()
    {
        switch (_position)
        {
        case HandlerPosition.Corner:

            _target.Width = -1;
            _target.Height = -1;
            _target.AutoSize = true;
            break;

        case HandlerPosition.Right:

            _target.Width = -1;
            _target.AutoSize = true;
            break;
            
        case HandlerPosition.Bottom:

            _target.Height = -1;
            _target.AutoSize = true;
            break;
        }
    }

    void _OnMouseDown (object sender, EF.MouseEventArgs e)
    {
        e.Handled = true;
        _StartResize ();
    }

    void _OnMouseDoubleClick (object sender, EF.MouseEventArgs e)
    {
        e.Handled = true;
        _OnAutoResize ();
    }
}

public class ResizableWindow : Control <EF.Form>, IResizable
{
    EF.TableCell _content;

    public ResizableWindow ()
    {
        Eto.Owner = RUI.RhinoEtoApp.MainWindow;

        var hdlR = new Handle (this, HandlerPosition.Right);
        var hdlB = new Handle (this, HandlerPosition.Bottom);
        var hdlC = new Handle (this, HandlerPosition.Corner);
        var fake = new EF.Panel { Width = hdlC.Size, Height = hdlC.Size };

        Eto.MinimumSize = new (hdlC.Size*2, hdlC.Size*2);

        _content = new EF.TableCell (null, scaleWidth: true);

        var table = new EF.TableLayout ();
        table.Rows.Add (
            new EF.TableRow (
                new EF.TableCell (fake, scaleWidth: false),
                new EF.TableCell (null, scaleWidth: true),
                new EF.TableCell (null, scaleWidth: false)
            ) { ScaleHeight = false }
        );
        table.Rows.Add (
            new EF.TableRow (
                new EF.TableCell (null, scaleWidth: false),
                _content,
                new EF.TableCell (hdlR.Eto, scaleWidth: false)
            ) { ScaleHeight = true }
        );
        table.Rows.Add (
            new EF.TableRow (
                new EF.TableCell (null, scaleWidth: false),
                new EF.TableCell (hdlB.Eto, scaleWidth: false),
                new EF.TableCell (hdlC.Eto, scaleWidth: false)
            ) { ScaleHeight = false }
        );

        Eto.Content = table;
    }

    public virtual EF.Control Content
    {
        get => _content.Control;
        set => _content.Control = value;
    }

    public virtual bool AutoSize
    {
        get => Eto.AutoSize;
        set {
            DBG.PROP ();
            
            Eto.AutoSize = value;
        }
    }
    
    public virtual int Width
    {
        get => Eto.Width;
        set {
            if (Eto.Width == value) return;
            
            DBG.PROP ();
            
            Eto.Width = value;
            Emit ();
        }
    }
    
    public virtual int Height
    {
        get => Eto.Height;
        set {
            if (Eto.Height == value) return;
            
            DBG.PROP ();
            
            Eto.Height = value;
            Emit ();
        }
    }

    public virtual int MinWidth
    {
        get => Eto.MinimumSize.Width;
        set {
            if (Eto.MinimumSize.Width == value) return;
            
            DBG.PROP ();
            
            Eto.MinimumSize = new (value, Eto.Height);
            Emit ();
        }
    }

    public virtual int MinHeight
    {
        get => Eto.MinimumSize.Height;
        set {
            if (Eto.MinimumSize.Height == value) return;
            
            DBG.PROP ();
            
            Eto.MinimumSize = new (Eto.Width, value);
            Emit ();
        }
    }

    // public virtual int MaxWidth
    // {
    //     get => Eto.MinimumSize.Width;
    //     set {
    //         if (Eto.MinimumSize.Width == value) return;
    //         Eto.MinimumSize = new (value, Eto.Height);
    //         Emit ();
    //     }
    // }

    // public virtual int MaxHeight
    // {
    //     get => Eto.MinimumSize.Height;
    //     set {
    //         if (Eto.MinimumSize.Height == value) return;
    //         Eto.MinimumSize = new (Eto.Width, value);
    //         Emit ();
    //     }
    // }

    public virtual bool IsVisible 
    {
        get => Eto.Visible;
        set {
            if (Eto.Visible == value) return;

            DBG.PROP ();

            Eto.Visible = value;
            Emit ();
        }
    }


    public virtual void Dispose ()
    {
        DBG.CTOR ();

        Eto.Close ();
    }
}


public enum VMenuPosition
{
    Center,
    Static,
    Pointer,
    //Top,
    Bottom
}


public interface IVolatileMenuSettings : INotifyPropertyChanged
{
    [Option (Tooltip = "Specifies whether the menu remains displayed.")]
    bool Persistent { get; }

    [Option (Tooltip = "Indicates whether the menu is closed when an option is selected.")]
    bool Volatile { get; }
    
    [Option (Tooltip = "Specifies where to display the menu.")]
    VMenuPosition Position { get; }

    [Option (Increment = 1)]
    int OffsetX { get; }

    [Option (Increment = 1)]
    int OffsetY { get; }

    [Option (Tooltip = "Navigation menu opacity", Min = 0.1, Max = 1, Increment = 0.05)]
    double Opacity { get; }
    
    [Option (Tooltip = "Navigation menu background color")]
    ED.Color BackgroundColor { get; }
}

/// <summary>
///     .NET Framework runtime does not support default interface implementation. </summary>
public static class IVolatileMenuSettingsController
{
    public static bool Validate <T> (T obj) where T : IVolatileMenuSettings
    {
        var _datatype = typeof (T);
        OptionAttribute.Get (_datatype, nameof (obj.Volatile)).Enabled = !obj.Persistent;

        var enabled = obj.Position != VMenuPosition.Static;
        OptionAttribute.Get (_datatype, nameof (obj.OffsetX)).Enabled = enabled;
        OptionAttribute.Get (_datatype, nameof (obj.OffsetY)).Enabled = enabled;

        return true;
    }
}


public class VolatileMenu : ResizableWindow
{
    public IVolatileMenuSettings Settings;

    public VolatileMenu (IVolatileMenuSettings settings)
    {
        Settings = settings;
        Settings.PropertyChanged += _OnSettingsChanged;
        
        Eto.WindowStyle     = EF.WindowStyle.None;
        Eto.Opacity         = Settings.Opacity;
        Eto.Resizable       =  false;
        Eto.BackgroundColor = Settings.BackgroundColor;
        _InitializeFormEvent ();
    }

    void _OnSizeChanged (object sender, EventArgs e)
    {
        if (Eto.Loaded == false) return;
        
        DBG.EVENT ();

        UpdatePosition (RhinoDoc.ActiveDoc.Views.ActiveView);
    }

    void _OnShown (object sender, EventArgs e)
    {
        DBG.EVENT ();

        Eto.SizeChanged -= _OnSizeChanged;
    }


    public override bool IsVisible 
    {
        get => base.IsVisible;
        set {
            if (base.IsVisible == value) return;

            DBG.PROP ();

            if (value)
            {
                if (Eto.Loaded)
                    UpdatePosition (RhinoDoc.ActiveDoc.Views.ActiveView);
                else {
                    Eto.SizeChanged += _OnSizeChanged;
                    Eto.Shown += _OnShown;
                }
                base.IsVisible = true;
            }
            else // if (Settings.Persistent == false)
            {
                DBG.Log (false);
                base.IsVisible = false;
            }
        }
    }


    #region Settings

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Settings.Opacity):

            DBG.DATA ();

            Eto.Opacity = Settings.Opacity;
            Eto.Invalidate ();
            break;

        // case nameof (Settings.Persistent):
        // 
        //     DBG.DATA ();
        // 
        //     IsVisible = Settings.Persistent;
        //     break;

        case nameof (Settings.BackgroundColor):

            DBG.DATA ();

            Eto.BackgroundColor = Settings.BackgroundColor;
            break;
        }
    }

    #endregion


    #region Position / Size

    SD.Rectangle _GetViewsRectangle (RD.RhinoView view)
    {
        var rect = view.ScreenRectangle;

        if (view.Maximized || view.Floating)
            return rect;
            
        foreach (var v in view.Document.Views)
        {
            if (v.Floating)
                continue;

            var r = v.ScreenRectangle;
            if (r.X < rect.X) rect.X = r.X;
            if (r.Y < rect.Y) rect.Y = r.Y;
            if (rect.Right  < r.Right)  rect.Width  = r.Right - rect.X;
            if (rect.Bottom < r.Bottom) rect.Height = r.Bottom - rect.Y;
        }
        
        return rect;
    }

    ED.Point _ConstrainToRectangle (ED.Point point, ED.Size size, ED.Rectangle rect)
    {
        if (size.Width < rect.Width)
        {
            if (point.X < rect.X) 
                point.X = rect.X;

            else if (rect.Right < point.X + size.Width)
                point.X = rect.Right - size.Width;
        }
        else
        {
            point.X = rect.X;
        }

        if (size.Height < rect.Height)
        {
            if (point.Y < rect.Y) 
                point.Y = rect.Y;

            else if (rect.Bottom < point.Y + size.Height)
                point.Y = rect.Bottom - size.Height;
        }
        else
        {
            point.Y = rect.Y;
        }

        return point;
    }

    public void UpdatePosition (RD.RhinoView view)
    {
        if (Settings.Position == VMenuPosition.Static)
            return;

        // !!! Capture les viewports cachés.
        //     Impossible de savoir si un viewport est afficher ou non
        // !!!
        // var mspos = EF.Mouse.Position;
        // foreach (var v in RhinoDoc.ActiveDoc.Views) {
        //     if (v.ScreenRectangle.Contains ((int)mspos.X, (int)mspos.Y)) {
        //         if (v.Maximized) { view = v; break; }
        //         view = v;
        //     }
        // }
        // foreach (var v in RhinoDoc.ActiveDoc.Views) {
        //     if (v.Floating) { view = v; break; } // if 2 floating views ?
        // }

        ED.Point loc = default;
        ED.PointF mp;

        switch (Settings.Position)
        {
        case VMenuPosition.Static:
            return;

        case VMenuPosition.Center:
        
            var rect = _GetViewsRectangle (view);
            loc = new ED.Point(
                (int)(rect.X + rect.Width*0.5 - Eto.Width*0.5),
                (int)(rect.Y + rect.Height*0.5 - Eto.Height*0.5)
            );
            break;

        case VMenuPosition.Pointer:
        
            mp = EF.Mouse.Position;
            loc = new ED.Point(
                (int)(mp.X - Eto.Width*0.5),
                (int)(mp.Y - Eto.Height*0.5)
            );
            break;

        case VMenuPosition.Bottom:
        
            mp = EF.Mouse.Position;
            loc = new ED.Point(
                (int)(mp.X - Eto.Width*0.5),
                (int)mp.Y
            );
            break;
        }
            
        loc.X += Settings.OffsetX;
        loc.Y += Settings.OffsetY;
        // EF.Screen.DisplayBounds inclus la barre d'outi Windows.
        Eto.Location = _ConstrainToRectangle (loc, Eto.Size, new (EF.Screen.DisplayBounds));
    }

    #endregion


    #region Form Events

    void _InitializeFormEvent ()
    {
        DBG.CTOR ();

        #if RHP
        Eto.LoadComplete += _OnLoadComplete;
        #endif
        Eto.MouseEnter  += _OnMouseEnter;
        Eto.GotFocus    += _OnGotFocus;
        Eto.LostFocus   += _OnLostFocus;
        Eto.MouseDown   += _OnMouseDown;
        Eto.KeyUp       += _OnKeyUp;
    }

    #if RHP
    void _OnLoadComplete (object _, EventArgs e)
    {
        DBG.EVENT ();

        RUI.EtoExtensions.LocalizeAndRestore (Eto, Settings.GetType ());
    }
    #endif
    
    // Mouse[Enter|Leave] fontionne sans le focus mais pas OnKey[Down|Up]

    void _OnMouseEnter (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        if (Eto.HasFocus == false) Eto.Focus ();
    }

    protected void _OnGotFocus (object _, EventArgs e)
    {
        DBG.EVENT ();

        Eto.AutoSize = true;
        Eto.AutoSize = false;
    }

    // Hide
    void _OnLostFocus (object _, EventArgs e)
    {
        if (FormDrag.InDrag) {
            Eto.Focus ();
            return;
        }

        if (Settings.Persistent) return;

        DBG.EVENT ();

        IsVisible = false;
    }

    // Start drag
    void _OnMouseDown (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();

        FormDrag.Start (Eto);
    }

    // Hide on escape key
    void _OnKeyUp (object _, EF.KeyEventArgs e)
    {
        if (e.Key == EF.Keys.Escape)
        {
        DBG.EVENT ();

            IsVisible = false;
        }
    }

    #endregion
}



/***   ████████ ███████ ███████ ████████  ***/
/***      ██    ██      ██         ██     ***/
/***      ██    █████   ███████    ██     ***/
/***      ██    ██           ██    ██     ***/
/***      ██    ███████ ███████    ██     ***/



#if false
class TEST : EF.Drawable
{
    NavigationOptions _opt;

    readonly int _padding = 5;

    readonly int _fontsize;
    readonly int _lineheight;
    readonly ED.Font _font;
    readonly ED.Color _fontcolor;

    readonly ED.Pen _strokecolor;

    public TEST (NavigationOptions options)
    {
        _opt = options;

        _fontsize = 12;
        _lineheight = _fontsize + 3;
        _font = new ED.Font (ED.FontFamilies.Sans, _fontsize);
        _fontcolor = ED.Colors.Black;

        _strokecolor = ED.Pens.Black;
    }

    struct Box
    {
        public int X, Y, W, H;
        public bool Contains (ED.PointF point)
        {
            return X <= point.X && point.X <= X+W
                && Y <= point.Y && point.Y <= Y+H;
        }
    }
    ED.Rectangle ClipRectangle;
    Box _btnrect = new ();

    protected override void OnPaint (EF.PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = e.ClipRectangle;
        ClipRectangle.X = (int)r.X;
        ClipRectangle.Y = (int)r.Y;
        ClipRectangle.Width = (int)r.Width;
        ClipRectangle.Height = (int)r.Height;
        var x = 10;
        var y = 10;

        var s = g.MeasureString (_font, "Paint");
        _btnrect.X = x;
        _btnrect.Y = y; 
        _btnrect.W = (int)s.Width+_padding*2;
        _btnrect.H = (int)s.Height+_padding*2;
        g.DrawRectangle (_strokecolor, x, y, _btnrect.W, _btnrect.H);
        g.DrawText (_font, _fontcolor, x+_padding, y+_padding, "Paint");
        y += _btnrect.H+_padding;

        g.DrawText (_font, _fontcolor, x, y, "Pan");
        y += _fontsize + _padding;;

        g.DrawText (_font, _fontcolor, x, y, "Zoom");
        y += _fontsize + _padding;;

        g.DrawText (_font, _fontcolor, x, y, "Rotate");
        y += _fontsize + _padding;;

        base.OnPaint (e);
    }

    protected override void OnMouseDown (EF.MouseEventArgs e)
    {
        if (_btnrect.Contains (e.Location))
        {
            Update (ClipRectangle);
        }
        base.OnMouseDown (e);
    }
}
#endif
