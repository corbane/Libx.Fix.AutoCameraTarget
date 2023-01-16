#nullable enable


#if CSX
#load "./1 Ui.cs"
#load "../Ui.Native/0.csx"
#load "../Config/1 Settings.cs"
#load "../Views/1 Camera.cs"
#load "../Sync.CPlanes/1 Cache.cs"
#endif


#if DEBUG
#define DEBUG_EVENT
#define DEBUG_DATA
#define DEBUG_PROP
#define DEBUG_HOOK
#define DEBUG_CTOR
#endif


using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;
using SD = System.Drawing;
using ST = System.Threading;

using EF = Eto.Forms;
using ED = Eto.Drawing;

using RH = Rhino;
using ON = Rhino.Geometry;
using RO = Rhino.DocObjects;
using RD = Rhino.Display;
using RUI = Rhino.UI;
using RhinoApp = Rhino.RhinoApp;
using RhinoDoc = Rhino.RhinoDoc;
using RO_CPlane = Rhino.DocObjects.ConstructionPlane;



#if RHP
using Libx.Fix.AutoCameraTarget.Sync;
using Libx.Fix.AutoCameraTarget.Views;
using Libx.Fix.AutoCameraTarget.Config;
using Libx.Fix.AutoCameraTarget.Ui.Native;
namespace Libx.Fix.AutoCameraTarget.Ui;
#endif



/***   ██████  ██   ██     ██   ██ ███████ ██      ██████  ███████ ██████  ███████   ***/
/***   ██   ██ ██   ██     ██   ██ ██      ██      ██   ██ ██      ██   ██ ██        ***/
/***   ██████  ███████     ███████ █████   ██      ██████  █████   ██████  ███████   ***/
/***   ██   ██ ██   ██     ██   ██ ██      ██      ██      ██      ██   ██      ██   ***/
/***   ██   ██ ██   ██     ██   ██ ███████ ███████ ██      ███████ ██   ██ ███████   ***/



/*/
    Pour afficher une barre d'outils de type "contextuel", il aurait été simple de définir sa position à l'emplacement du curseur.
    Malheureusement, il n'y a rien dans l'API C# pour définir l'emplacement d'une barre d'outils ou d'un groupe de barres d'outils.

    Une solution plus complexe aurait été de recréer la barre d'outils.
    Malheureusement, il n'y a rien dans l'API C# pour lister les boutons contenus dans une barre d'outils.

    La définition des d'une d'outil peut être lue à partir de son fichiers XML.
    Malheureusement, il n'y a pas une spécification du format claire.

    Il y a d'autres possibilités avec l'API C++, mais je ne sais pas lesquelles...
    Il n'y a plus qu'à espérer que Rhino 8 et Eto changeront la donne.
/*/

public static partial class RhinoToolbars
{
    public static bool TryGetToolbarGroup (string? fullname, out RUI.ToolbarGroup? toolbarGroup)
    {
        toolbarGroup = null;

        if (fullname == null || string.IsNullOrWhiteSpace (fullname))
            return false;

        foreach (var uifile in RhinoApp.ToolbarFiles)
        {
            if (fullname.StartsWith (uifile.Name+".") == false)
                continue;

            var count = uifile.GroupCount;
            for (var i = 0 ; i < count ; i++)
            {
                toolbarGroup = uifile.GetGroup (i);
                if (uifile.Name+"."+toolbarGroup.Name == fullname)
                    return true;
            }
        }
        return false;
    }

    public static string[] GetToolbarGroupPaths ()
    {
        var names = new List <string> ();
        foreach (var uifile in RhinoApp.ToolbarFiles)
        {
            var count = uifile.GroupCount;
            
            if (names.Count + count < names.Capacity)
                names.Capacity = names.Count + count;

            for (var i = 0 ; i < count ; i++)
            {
                var toolgroup = uifile.GetGroup (i);
                names.Add ($"{uifile.Name}.{toolgroup.Name}");
                //DBG.Log ($"{uifile.Name}.{toolgroup.Name}");
            }
        }
        return names.ToArray ();
    }
}



/***   ███████ ██   ██  ██████  ██████  ████████  ██████ ██    ██ ████████ ███████   ***/
/***   ██      ██   ██ ██    ██ ██   ██    ██    ██      ██    ██    ██    ██        ***/
/***   ███████ ███████ ██    ██ ██████     ██    ██      ██    ██    ██    ███████   ***/
/***        ██ ██   ██ ██    ██ ██   ██    ██    ██      ██    ██    ██         ██   ***/
/***   ███████ ██   ██  ██████  ██   ██    ██     ██████  ██████     ██    ███████   ***/



public class ModifierButtonMap 
{
    protected const int ModifierCount = 4;
    public enum Modifier : byte
    {
        None  = 0,
        Alt   = 1,
        Ctrl  = 2,
        Shift = 3,
    }
 
    protected const int ButtonCount = 8;
    public enum Mouse : byte
    {
        Enter  = 0,
        Left   = 1,
        Middle = 2,
        Right  = 3,
        Leave  = 4,
        DoubleLeft   = 5,
        DoubleMiddle = 6,
        DoubleRight  = 7,
    }
}


public class ModifierButtonMap <T> : ModifierButtonMap where T : Delegate
{
    /// <summary>
    /// `ModifierCount*ButtonCount` event table:
    /// <code>
    ///             Enter   Left   Middle   Right   Leave   Left2   Middle2   Right2  
    ///     None  |       |      |        |       |       |       |         |        |
    ///     Shift |       |      |        |       |       |       |         |        |
    ///     Alt   |       |      |        |       |       |       |         |        |
    ///     Ctrl  |       |      |        |       |       |       |         |        |
    /// </code>
    /// </summary>
    protected readonly T?[] Map = new T[ModifierCount * ButtonCount];

    #region Accessors

    /*/
        To use with the syntax:

        new ModifierButtonMap {
            NoneNone = ...
            NoneLeft = ...
            AltLeft  = ...
        }
    /*/

    public T? Enter {
        get => Get (Modifier.None, Mouse.Enter);
        set => Set (Modifier.None, Mouse.Enter, value);
    }
    public T? Left {
        get => Get (Modifier.None, Mouse.Left);
        set => Set (Modifier.None, Mouse.Left, value);
    }
    public T? Middle {
        get => Get (Modifier.None, Mouse.Middle);
        set => Set (Modifier.None, Mouse.Middle, value);
    }
    public T? Right {
        get => Get (Modifier.None, Mouse.Right);
        set => Set (Modifier.None, Mouse.Right, value);
    }
    public T? Leave {
        get => Get (Modifier.None, Mouse.Leave);
        set => Set (Modifier.None, Mouse.Leave, value);
    }
    public T? DoubleLeft {
        get => Get (Modifier.None, Mouse.DoubleLeft);
        set => Set (Modifier.None, Mouse.DoubleLeft, value);
    }
    public T? DoubleMiddle {
        get => Get (Modifier.None, Mouse.DoubleMiddle);
        set => Set (Modifier.None, Mouse.DoubleMiddle, value);
    }
    public T? DoubleRight {
        get => Get (Modifier.None, Mouse.DoubleRight);
        set => Set (Modifier.None, Mouse.DoubleRight, value);
    }

    
    public T? AltEnter {
        get => Get (Modifier.Alt, Mouse.Enter);
        set => Set (Modifier.Alt, Mouse.Enter, value);
    }
    public T? AltLeft {
        get => Get (Modifier.Alt, Mouse.Left);
        set => Set (Modifier.Alt, Mouse.Left, value);
    }
    public T? AltMiddle {
        get => Get (Modifier.Alt, Mouse.Middle);
        set => Set (Modifier.Alt, Mouse.Middle, value);
    }
    public T? AltRight {
        get => Get (Modifier.Alt, Mouse.Right);
        set => Set (Modifier.Alt, Mouse.Right, value);
    }
    public T? AltLeave {
        get => Get (Modifier.Alt, Mouse.Leave);
        set => Set (Modifier.Alt, Mouse.Leave, value);
    }
    public T? DoubleAltLeft {
        get => Get (Modifier.Alt, Mouse.DoubleLeft);
        set => Set (Modifier.Alt, Mouse.DoubleLeft, value);
    }
    public T? DoubleAltMiddle {
        get => Get (Modifier.Alt, Mouse.DoubleMiddle);
        set => Set (Modifier.Alt, Mouse.DoubleMiddle, value);
    }
    public T? DoubleAltRight {
        get => Get (Modifier.Alt, Mouse.DoubleRight);
        set => Set (Modifier.Alt, Mouse.DoubleRight, value);
    }
    
    public T? CtrlEnter {
        get => Get (Modifier.Ctrl, Mouse.Enter);
        set => Set (Modifier.Ctrl, Mouse.Enter, value);
    }
    public T? CtrlLeft {
        get => Get (Modifier.Ctrl, Mouse.Left);
        set => Set (Modifier.Ctrl, Mouse.Left, value);
    }
    public T? CtrlMiddle {
        get => Get (Modifier.Ctrl, Mouse.Middle);
        set => Set (Modifier.Ctrl, Mouse.Middle, value);
    }
    public T? CtrlRight {
        get => Get (Modifier.Ctrl, Mouse.Right);
        set => Set (Modifier.Ctrl, Mouse.Right, value);
    }
    public T? CtrlLeave {
        get => Get (Modifier.Ctrl, Mouse.Leave);
        set => Set (Modifier.Ctrl, Mouse.Leave, value);
    }
    public T? DoubleCtrlLeft {
        get => Get (Modifier.Ctrl, Mouse.DoubleLeft);
        set => Set (Modifier.Ctrl, Mouse.DoubleLeft, value);
    }
    public T? DoubleCtrlMiddle {
        get => Get (Modifier.Ctrl, Mouse.DoubleMiddle);
        set => Set (Modifier.Ctrl, Mouse.DoubleMiddle, value);
    }
    public T? DoubleCtrlRight {
        get => Get (Modifier.Ctrl, Mouse.DoubleRight);
        set => Set (Modifier.Ctrl, Mouse.DoubleRight, value);
    }
    
    public T? ShiftEnter {
        get => Get (Modifier.Shift, Mouse.Enter);
        set => Set (Modifier.Shift, Mouse.Enter, value);
    }
    public T? ShiftLeft {
        get => Get (Modifier.Shift, Mouse.Left);
        set => Set (Modifier.Shift, Mouse.Left, value);
    }
    public T? ShiftMiddle {
        get => Get (Modifier.Shift, Mouse.Middle);
        set => Set (Modifier.Shift, Mouse.Middle, value);
    }
    public T? ShiftRight {
        get => Get (Modifier.Shift, Mouse.Right);
        set => Set (Modifier.Shift, Mouse.Right, value);
    }
    public T? ShiftLeave {
        get => Get (Modifier.Shift, Mouse.Leave);
        set => Set (Modifier.Shift, Mouse.Leave, value);
    }
    public T? DoubleShiftLeft {
        get => Get (Modifier.Shift, Mouse.DoubleLeft);
        set => Set (Modifier.Shift, Mouse.DoubleLeft, value);
    }
    public T? DoubleShiftMiddle {
        get => Get (Modifier.Shift, Mouse.DoubleMiddle);
        set => Set (Modifier.Shift, Mouse.DoubleMiddle, value);
    }
    public T? DoubleShiftRight {
        get => Get (Modifier.Shift, Mouse.DoubleRight);
        set => Set (Modifier.Shift, Mouse.DoubleRight, value);
    }


    #endregion

    int _dblcount = 0;

    public bool HasDoubleClick => _dblcount > 0;

    public T? Get (Modifier modifier, Mouse button, bool strict = false)
    {
        DBG.Log ("modifier:", modifier, ", button:", button, ", strict:", strict);

        return Map[ButtonCount*(byte)modifier + (byte)button]
            ?? (strict ? null : Map[(byte)button]);
    }

    public void Set (Modifier modifier, Mouse button, T? action)
    {
        // DBG.Log (modifier, button);

        var index = ButtonCount*(byte)modifier + (byte)button;

        if (button == Mouse.DoubleLeft ||
            button == Mouse.DoubleMiddle ||
            button == Mouse.DoubleRight
        ) {
            if (Map[index] == null)
                _dblcount++;
        }

        Map[index] = action;
    }

}



/***   ██████   █████  ████████  █████    ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██   ██ ███████    ██    ███████   ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██████  ██   ██    ██    ██   ██   ***/



public class MenuData
{
    public Tool? ActiveCPlaneTool
    {
        get => _atool;
        set {
            if (_atool_LOCKED || _atool == value) return;

            DBG.DATA ();

            _atool_LOCKED = true;
            if (_atool != null) _atool.IsActive = false;
            _atool = value;
            if (_atool != null) _atool.IsActive = true;
            _atool_LOCKED = false;

            Emit ();
        }
    }

    Tool? _atool;
    bool _atool_LOCKED;



    public Tool? OverCPlaneTool
    {
        get => _otool;
        set {
            if (_otool_LOCKED || _otool == value) return;

            DBG.DATA ();

            _otool_LOCKED = true;
            if (_otool != null) _otool.IsOver = false;
            _otool = value;
            if (_otool != null) _otool.IsOver = true;
            _otool_LOCKED = false;

            Emit ();
        }
    }

    Tool? _otool;
    bool _otool_LOCKED;



    public Dictionary <int, Tab>.ValueCollection Tabs => _tabs.Values;

    Dictionary <int, Tab> _tabs = new ();

    public void AppendTab (Tab tab)
    {
        var hash = tab.GetHashCode ();
        if (_tabs.ContainsKey (hash)) return;

        DBG.DATA ();

        _tabs.Add (hash, tab);

        Emit (nameof (Tabs));
    }

    public void RemoveTab (Tab tab)
    {
        if (_tabs.Remove (tab.GetHashCode ()) == false) return;

        DBG.DATA ();

        Emit (nameof (Tabs));
    }



    public Dictionary <int, Tab>.ValueCollection ActiveTabs => _atabs.Values;

    Dictionary <int, Tab> _atabs = new ();
    bool _atabs_LOCKED;

    public void SetActiveTab (Tab tab)
    {
        if (_atabs_LOCKED) return;
        
        var h = tab.GetHashCode ();
        if (_atabs.Count == 1 && _atabs.ContainsKey (h)) return;
    
        DBG.DATA ();
    
        _atabs_LOCKED = true;
        foreach (var t in _atabs) t.Value.IsActive = false;
        tab.IsActive = true;
        _atabs_LOCKED = false;

        _atabs.Clear ();
        _atabs.Add (h, tab);

        Emit (nameof (ActiveTabs));
    }

    public void AppendActiveTab (Tab tab)
    {
        if (_atabs_LOCKED) return;
        
        var h = tab.GetHashCode ();
        if (_atabs.ContainsKey (h)) return;

        DBG.DATA ();

        _atabs.Add (h, tab);

        _atabs_LOCKED = true;
        tab.IsActive = true;
        _atabs_LOCKED = false;

        Emit (nameof (ActiveTabs));
    }

    public void RemoveActiveTab (Tab tab)
    {
        if (_atabs_LOCKED) return;
        
        if (_atabs.Remove (tab.GetHashCode ()) == false) return;
        
        DBG.DATA ();

        _atabs_LOCKED = true;
        tab.IsActive = false;
        _atabs_LOCKED = false;

        Emit (nameof (ActiveTabs));
    }


    public Tab? OverTab
    {
        get => _otab;
        set {
            if (_otab_LOCKED || _otab == value) return;

            DBG.DATA ();

            _otab_LOCKED = true;
            if (_otab != null) _otab.IsOver = false;
            _otab = value;
            if (_otab != null) _otab.IsOver = true;
            _otab_LOCKED = false;

            Emit ();
        }
    }

    Tab? _otab;
    bool _otab_LOCKED;



    public EF.Keys Modifiers
    {
        get => _modifiers;
        set {
            if (_modifiers == value) return;

            DBG.DATA ();

            _modifiers = value;
            Emit ();
        }
    }

    EF.Keys _modifiers;



    public EF.MouseButtons MouseButtons
    {
        get => _mbuttons;
        set {
            if (_mbuttons == value) return;

            DBG.DATA ();

            _mbuttons = value;
            Emit ();
        }
    }

    EF.MouseButtons _mbuttons;



    #region INotifyPropertyChanged

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    public virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}



/***    ██████  ██████  ███    ██ ████████ ██████   ██████  ██      ███████   ***/
/***   ██      ██    ██ ████   ██    ██    ██   ██ ██    ██ ██      ██        ***/
/***   ██      ██    ██ ██ ██  ██    ██    ██████  ██    ██ ██      ███████   ***/
/***   ██      ██    ██ ██  ██ ██    ██    ██   ██ ██    ██ ██           ██   ***/
/***    ██████  ██████  ██   ████    ██    ██   ██  ██████  ███████ ███████   ***/



public class Tool : EF.Panel
{
    public EF.Control Eto => this;

    Tool ()
    {
        Padding = 0;
        Cursor = EF.Cursors.Pointer;
    }

    public Tool (string text) : this ()
    {
        var lbl = new EF.Label {
            Text              = $" {text.Trim ()} ", // je n'ais pas trouvé comme définir un "padding"
            Wrap              = EF.WrapMode.None,
            VerticalAlignment = EF.VerticalAlignment.Center,
            TextColor         = RhinoTheme.TextEnabled,
        };
        lbl.Height = (int)lbl.Font.Size * 3;
        Content = lbl;
    }
    
    public Tool (ED.Bitmap image) : this ()
    {
        var img = new EF.ImageView  { Image = image };
        Content = img;
    }
    

    #region States

    bool _isover;
    bool _isactive;

    public virtual bool IsActive
    {
        get => _isactive;
        set {
            if (_isactive == value) return;
            
            DBG.PROP ();
            
            _isactive = value;
            _UpdateBackground ();
        }
    }

    public virtual bool IsOver
    {
        get => _isover;
        set {
            if (_isover == value) return;
            
            DBG.PROP ();
            
            _isover = value;
            _UpdateBackground ();
        }
    }

    public ED.Color OverColor { get; set; } = LightTheme.OverBackground;

    public ED.Color ActivedColor { get; set; } = LightTheme.ActiveBackground;

    void _UpdateBackground ()
    {
        if (_isactive)
            Content.BackgroundColor = ActivedColor;
        else if (_isover)
            Content.BackgroundColor = OverColor;
        else
            Content.BackgroundColor = ED.Colors.Transparent;
    }

    #endregion


    #region Mouse

    protected override void OnMouseEnter (EF.MouseEventArgs e)
    {
        base.OnMouseEnter (e);

        if (FormDrag.InDrag) {
            e.Handled = true;
            return;
        }

        DBG.EVENT ();

        IsOver = true;
    }

    protected override void OnMouseLeave (EF.MouseEventArgs e)
    {
        base.OnMouseLeave (e);
        
        if (FormDrag.InDrag) {
            e.Handled = true;
            return;
        }

        DBG.EVENT ();

        IsOver = false;
    }

    protected override void OnMouseUp (EF.MouseEventArgs e)
    {
        // Fool's guard
        FormDrag.Stop ();

        base.OnMouseUp (e);

        if (FormDrag.WindowMoved) {
            return;
        }

        DBG.EVENT ();

        IsActive = !IsActive;
    }

    #endregion
}


class ToggleTool : Tool
{
    ED.Bitmap _defaultImage;
    ED.Bitmap _activeImage;

    public ToggleTool (ED.Bitmap defaultImage, ED.Bitmap activeImage) : base (defaultImage)
    {
        _defaultImage = defaultImage;
        _activeImage  = activeImage;
    }

    public override bool IsActive
    {
        get => base.IsActive;
        set {
            if (base.IsActive == value) return;
            
            base.IsActive = value;
            if (Content is not EF.ImageView imgv) return;

            DBG.PROP ();
            
            imgv.Image = value ? _activeImage : _defaultImage;
        }
    }
}


class ToolProjection : ToggleTool
{
    ViewportData ViewportData;

    public ToolProjection (ViewportData viewportData, ED.Bitmap defaultImage, ED.Bitmap activeImage) : base (defaultImage, activeImage)
    {
        ViewportData = viewportData;
        viewportData.PropertyChanged += _ViewportDataChanged;

        ActivedColor   = ED.Colors.Transparent;
        OverColor      = ED.Colors.Transparent;
        IsActive       = ViewportData.ViewportProjection == ViewportProjection.Parallel ? false : true;
        // Callbacks.Left = delegate { Toggle (); };
    }

    void _ViewportDataChanged (object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof (ViewportData.ViewportProjection)) {
            IsActive = ViewportData.ViewportProjection == ViewportProjection.Perspective;
        }
    }

    public override bool IsActive
    {
        get => base.IsActive;
        set {
            if (base.IsActive == value) return;
            base.IsActive = value;
            
            DBG.PROP ();
            
            ViewportData.ViewportProjection = base.IsActive ? ViewportProjection.Perspective : ViewportProjection.Parallel;
        }
    }
}


public class ToolCPlane : Tool
{
    MenuData Data;

    ViewportData ViewportData;
    CGrid _cplane;

    public ToolCPlane (string text, CGrid cplane, MenuData data, ViewportData viewportData) : base (text)
    {
        Data = data;
        ViewportData = viewportData;
        _cplane = cplane;
        
        Eto.MouseDoubleClick += _OnDoubleClick;
        Data.PropertyChanged += _OnDataChanged;
    }
    
    public ToolCPlane (ED.Bitmap image, CGrid cplane, MenuData data, ViewportData viewportData) : base (image)
    {
        Data = data;
        ViewportData = viewportData;
        _cplane = cplane;
        
        Eto.MouseDoubleClick += _OnDoubleClick;
    }

    void _UpdateViewportData (int phase)
    {
        Data.Modifiers = EF.Keyboard.Modifiers; // fool's guard
        
        switch (phase)
        {
        case 1: // enter
        
            DBG.Log ();

            ViewportData.CPlaneVisualized = Data.Modifiers switch
            {
            EF.Keys.Control => _cplane.YZ,
            EF.Keys.Shift   => _cplane.XZ,
            _               => _cplane.XY,
            };
            break;

        case 2: // click

            if (Data.MouseButtons != EF.MouseButtons.Primary) break;
            
            DBG.Log ();

            ViewportData.CPlaneActive = Data.Modifiers switch
            {
            EF.Keys.Control => _cplane.InvertedYZ.CPlane,
            EF.Keys.Shift   => _cplane.XZ.CPlane,
            _               => _cplane.XY.CPlane,
            };
            break;

        case 3: // double click

            if (Data.MouseButtons != EF.MouseButtons.Primary) break;
            
            DBG.Log ();

            ViewportData.CameraTargetCPlane = Data.Modifiers switch
            {
            EF.Keys.Control => _cplane.InvertedYZ.CPlane,
            EF.Keys.Shift   => _cplane.XZ.CPlane,
            _               => _cplane.XY.CPlane,
            };
            break;
        }
    }

    void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        if (IsOver && e.PropertyName == nameof (Data.Modifiers))
        {
            DBG.DATA ();

            _UpdateViewportData (1);
        }
    }


    public override bool IsOver
    {
        get => base.IsOver;
        set {
            base.IsOver = value;
            if (base.IsOver == false) return;

            DBG.PROP ();
            
            Data.OverCPlaneTool = this;
            _UpdateViewportData (1);
        }
    }

    public override bool IsActive
    {
        get => base.IsActive;
        set {
            base.IsActive = value;
            // if (base.IsActive == false) return; // Si le modifier a changé.

            DBG.PROP ();

            Data.ActiveCPlaneTool = this;
            _UpdateViewportData (2);
        }
    }


    void _OnDoubleClick (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();

        _UpdateViewportData (3);
    }
}


public class Tab : EF.Panel
{
    public EF.Control Eto => this;

    public Tab (MenuData data, ED.Bitmap image, TabContent layout)
    {
        Data = data;
        Content = new EF.ImageView  { Image = image };
        Layout = layout;

        Eto.MouseEnter += _OnMouseEnter;
        Eto.MouseLeave += _OnMouseLeave;
        Eto.MouseUp    += _OnMouseUp;
    }

    MenuData Data;


    public TabContent Layout { get; }
    

    bool _isover;
    bool _isactive;

    public bool IsActive
    {
        get => _isactive;
        set {
            if (_isactive == value) return;
            
            _isactive = value;
            _UpdateBackground ();

            if (Data.Modifiers == EF.Keys.Shift)
            {
                DBG.PROP ("Shift");

                if (_isactive) Data.AppendActiveTab (this);
                else Data.RemoveActiveTab (this);
            }
            else
            {
                DBG.PROP ("Unary");

                Data.SetActiveTab (this);
            }
        }
    }

    public virtual bool IsOver
    {
        get => _isover;
        set {
            if (_isover == value) return;
            
            DBG.PROP ();
            
            _isover = value;
            _UpdateBackground ();
            
            if (_isover) Data.OverTab = this;
        }
    }

    public ED.Color OverColor { get; set; } = LightTheme.OverBackground;

    public ED.Color ActivedColor { get; set; } = LightTheme.ActiveBackground;

    void _UpdateBackground ()
    {
        if (_isactive)
            Content.BackgroundColor = ActivedColor;
        else if (_isover)
            Content.BackgroundColor = OverColor;
        else
            Content.BackgroundColor = ED.Colors.Transparent;
    }


    public bool Resizable { get => false; set {} }
    public int OwnerHeight { get; set; }
    public int OwnerWidth { get; set; }
    public ED.Size OwnerSize { get; set; }

    
    void _OnMouseEnter (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();

        if (FormDrag.InDrag) {
            e.Handled = true;
            return;
        }
        IsOver = true;
    }

    void _OnMouseLeave (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        
        if (FormDrag.InDrag) {
            e.Handled = true;
            return;
        }
        IsOver = false;
    }

    void _OnMouseUp (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        
        if (FormDrag.WindowMoved) {
            e.Handled = true;
            return;
        }
        IsActive = !IsActive;
    }
}


public class TabContent : Control <EF.Panel>
{
    bool _isover;
    bool _isactive;

    public virtual bool IsVisible
    {
        get => Eto.Visible;
        set {
            if (Eto.Visible == value) return;
            
            DBG.PROP (value);

            Eto.Visible = value;
            if (value == false)
                IsActive = false;
        }
    }

    public virtual bool IsActive
    {
        get => _isactive;
        set {
            if (_isactive == value) return;

            DBG.PROP (value);

            _isactive = value;
            if (value)
                IsVisible = true;
        }
    }

    public bool IsOver
    {
        get => _isover;
        set {
            if (_isover == value) return;

            DBG.PROP (value);

            _isover = value;
        }
    }
}


class ThisIsFree : EF.Label
{
    ViewportMenuSettings _settings;

    public ThisIsFree (ViewportMenuSettings settings)
    {
        _settings = settings;
        _settings.PropertyChanged += _OnSettingsChanged;

        Text = Shout;
        _SetTextColor ();
    }


    const string Shout = " free by Vrecq Jean-marie ";

    public void SetText (string text)
    {
        var w = Width;
        Text = text;
        Width = w;
    }

    public void ResetText ()
    {
        Text = Shout;
        Width = -1;
    }
    
    void _SetTextColor ()
    {
        var c = ED.Colors.Black.ToHSL ();
        var bg = _settings.BackgroundColor.ToHSL ();
        c.L = bg.L < 0.5 ? bg.L+0.5f : bg.L-0.5f;
        TextColor = c;
    }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (_settings.BackgroundColor):

            _SetTextColor ();
            break;
        }
    }
}



/***   ███████ ███████ ████████ ████████ ██ ███    ██  ██████  ███████   ***/
/***   ██      ██         ██       ██    ██ ████   ██ ██       ██        ***/
/***   ███████ █████      ██       ██    ██ ██ ██  ██ ██   ███ ███████   ***/
/***        ██ ██         ██       ██    ██ ██  ██ ██ ██    ██      ██   ***/
/***   ███████ ███████    ██       ██    ██ ██   ████  ██████  ███████   ***/



public static partial class Ressources
{

    #if RHP
    const string _rscpath = "Libx.Fix.AutoCameraTarget.ico.";
    static Stream _GetStream (string path) => typeof (VirtualCursor).Assembly.GetManifestResourceStream (path);
    static SD.Bitmap _Get (string filename) => new (_GetStream (_rscpath + filename));
    #else
    static string _ressourceDiectory = @"E:\Projet\Rhino\Libx\Libx.Fix.AutoCameraTarget\ico";
    static SD.Bitmap _Get (string filename) => new (Path.Combine (_ressourceDiectory, filename));
    #endif

    public static readonly ED.Bitmap ParallelPng    = Rhino.UI.EtoExtensions.ToEto (_Get ("Parallel.png"));
    public static readonly ED.Bitmap PerspectivePng = Rhino.UI.EtoExtensions.ToEto (_Get ("Perspective.png"));

    public static readonly ED.Bitmap AxesPng           = Rhino.UI.EtoExtensions.ToEto (_Get ("Axes.png"));
    public static readonly ED.Bitmap AxesNotAlignedPng = Rhino.UI.EtoExtensions.ToEto (_Get ("AxesNotAligned.png"));
    public static readonly ED.Bitmap ScreenPng         = Rhino.UI.EtoExtensions.ToEto (_Get ("Screen.png"));
    public static readonly ED.Bitmap GearPng           = Rhino.UI.EtoExtensions.ToEto (_Get ("Gear.png"));

    public static readonly ED.Bitmap SwapYZPng = Rhino.UI.EtoExtensions.ToEto (_Get ("SwapYZ.png"));
    public static readonly ED.Bitmap SwapXZPng = Rhino.UI.EtoExtensions.ToEto (_Get ("SwapXZ.png"));

    public static readonly ED.Bitmap PlanTopPng    = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanTop.png"));
    public static readonly ED.Bitmap PlanBottomPng = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanBottom.png"));
    public static readonly ED.Bitmap PlanLeftPng   = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanLeft.png"));
    public static readonly ED.Bitmap PlanRightPng  = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanRight.png"));
    public static readonly ED.Bitmap PlanFrontPng  = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanFront.png"));
    public static readonly ED.Bitmap PlanBackPng   = Rhino.UI.EtoExtensions.ToEto (_Get ("PlanBack.png"));
}


public enum NavigationMenuModifier { Alt, Ctrl, Shift, Disabled }


public class ViewportMenuSettings : Settings, IVolatileMenuSettings
{
    #region Viewport Menu

    [Option (
        Group = "Viewport Menu", 
        DisplayName = "Modifier",
        Tooltip = "The modifier key to use with the spacebar to display the navigation menu."
    )]
    public NavigationMenuModifier Modier { get => _modifier1; set => Set (ref _modifier1, value); }
    NavigationMenuModifier _modifier1 = NavigationMenuModifier.Ctrl;

    #endregion


    #region IVolatileMenuSettings

    public bool Persistent { get => _persistent; set => Set (ref _persistent, value); }
    bool _persistent = false;

    public bool Volatile { get => _volatile && _persistent == false; set => Set (ref _volatile, value); }
    bool _volatile = false;
    
    public VMenuPosition Position { get => _pos; set => Set (ref _pos, value); }
    VMenuPosition _pos = VMenuPosition.Bottom;

    public int OffsetX { get => _posoffsetX; set => Set (ref _posoffsetX, value); }
    int _posoffsetX = 0;

    public int OffsetY { get => _posoffsetY; set => Set (ref _posoffsetY, value); }
    int _posoffsetY = 0;

    public ED.Color BackgroundColor { get => _bgcolor; set => Set (ref _bgcolor, value); }
    ED.Color _bgcolor = ED.Colors.White; // RhinoTheme.ControlBackground;

    public double Opacity { get => _opacity; set => Set (ref _opacity, value); }
    double _opacity = 0.85;

    #endregion


    #region Grasshopper

    [Option (
        Group = "Grasshopper", 
        DisplayName = "Modifier for GH"
    )]
    public NavigationMenuModifier SecondaryModifier { get => _modifier2; set => Set (ref _modifier2, value); }
    NavigationMenuModifier _modifier2 = NavigationMenuModifier.Alt;

    #endregion


    #region TODO

    [Exclude]
    [Option (
        DisplayName = "Custom Modifier"
    )]
    public NavigationMenuModifier TertiaryModifier { get => _modifier3; set => Set (ref _modifier3, value); }
    NavigationMenuModifier _modifier3 = NavigationMenuModifier.Disabled;
    
    
    [Exclude]
    [Option (Editable = true)]
    public string Toolbar { get => _toolbar; set => Set (ref _toolbar, value); }
    string _toolbar = "";
    
    [Exclude]
    public string TestTextBox { get => _ttb; set => Set (ref _ttb, value); }
    string _ttb = "";

    #endregion


    public ViewportMenuSettings ()
    {
        // Sinon la propriété Toolbar est affiché en premier.
        OptionAttribute.InitializeCache (GetType ());
        var a = OptionAttribute.Get (GetType (), nameof (Toolbar));
        a.Choices = new string[] { "" }.Union (RhinoToolbars.GetToolbarGroupPaths ());
    }

    public override bool Validate ()
    {
        OptionAttribute a;
        var t = GetType ();
        var valid = true;

        a = OptionAttribute.Get (t, nameof (Modier));
        a.Valid = Modier == NavigationMenuModifier.Disabled
               || Modier != SecondaryModifier && Modier != TertiaryModifier;
        valid &= a.Valid;

        a = OptionAttribute.Get (t, nameof (SecondaryModifier));
        a.Valid = SecondaryModifier == NavigationMenuModifier.Disabled
               || SecondaryModifier != Modier && SecondaryModifier != TertiaryModifier;
        valid &= a.Valid;

        a = OptionAttribute.Get (t, nameof (TertiaryModifier));
        a.Valid = TertiaryModifier == NavigationMenuModifier.Disabled
               || TertiaryModifier != Modier && TertiaryModifier != SecondaryModifier;
        valid &= a.Valid;

        a = OptionAttribute.Get (t, nameof (Toolbar));
        a.Valid = RhinoToolbars.TryGetToolbarGroup (Toolbar, out var _) || string.IsNullOrWhiteSpace (Toolbar);
        valid &= a.Valid;

        valid &= IVolatileMenuSettingsController.Validate (this);

        return valid;
    }
}


public class ViewportMenuSettingsForm : FloatingForm
{
    public ViewportMenuSettingsForm (ViewportMenuSettings settings)
    {
        Title = "Viewport Menu";

        var layout = SettingsLayout <ViewportMenuSettings>.CreateDefaultLayout (settings);
        layout.ButtonCancel.Click += delegate { Close (); };
        layout.ButtonOk.Click += delegate { Close (); };

        Content = layout;
    }
}



/***   ██████   █████  ████████  █████    ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██   ██ ███████    ██    ███████   ***/
/***   ██   ██ ██   ██    ██    ██   ██   ***/
/***   ██████  ██   ██    ██    ██   ██   ***/



// !!!
// RD.RhinoViewport 
// - has `SetProjection`, has no `GetProjection`
// RD.DefinedViewportProjection
// - has `Back`, `Top`, ..., has no `Parallel`
// !!!
public enum ViewportProjection
{
    Parallel,
    Perspective,
    TwoPointPerspective,
}


public class ViewportData : INotifyPropertyChanged, IDisposable
{
    public RhinoDoc Document => RhinoDoc.ActiveDoc;
    

    public ViewportData ()
    {
        DBG.CTOR ();

        DocumentObserver.OnBeginDocument += _OnBeginDocument;
        DocumentObserver.OnEndDocument   += _onEndDocument;
    }

    ~ViewportData ()
    {
        DBG.CTOR ();
        
        Dispose ();
    }
    
    public void Dispose ()
    {
        DBG.CTOR ();
        
        DocumentObserver.OnBeginDocument -= _OnBeginDocument;
        DocumentObserver.OnEndDocument   -= _onEndDocument;
        StopSync ();
        CPlanes.Dispose ();
    }


    bool _prevstarted;

    void _OnBeginDocument ()
    {
        DBG.CTOR ();
        
        _prevstarted = _started;
        StopSync ();
    }
    
    void _onEndDocument (RhinoDoc? _)
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
        
        _acplane = Viewport.GetConstructionPlane ();

        // Ecoute tout (déplacement de la caméra, séléction) et n'est pas toujours envoyer.
        // RD.RhinoView.Modified += _OnViewChanged;
        RhinoApp.Idle += _OnIdle;
        
        // CPlanes.StartSync (); Controlled by NamedCPLayout
    }

    public void StopSync ()
    {
        DBG.CTOR ();

        RhinoApp.Idle -= _OnIdle;
        _started = false;
        
        CPlanes.StopSync ();
    }

    void _OnIdle (object sender, EventArgs e)
    {
        var vp = Viewport;
        ViewportProjection = _GetProjection (vp);
        CPlaneActive = vp.GetConstructionPlane ();
    }


    #region Viewport

    public RD.RhinoViewport Viewport => Document.Views.ActiveView.ActiveViewport;

    ViewportProjection _viewportproj;
    public ViewportProjection ViewportProjection 
    {
        get => _viewportproj;
        set {
            if (_viewportproj == value) return;

            DBG.DATA ();

            _viewportproj = value;
            Emit ();
        }
    }

    ViewportProjection _GetProjection (RD.RhinoViewport vp)
    {
        return vp.IsParallelProjection ? ViewportProjection.Parallel
             : vp.IsPerspectiveProjection ? ViewportProjection.Perspective
             : ViewportProjection.TwoPointPerspective;
    }

    #endregion


    #region CPlane

    public CPlaneRegistry CPlanes { get; } = new ();

    RO_CPlane? _acplane;
    CGridItem? _scplane;

    public CGridItem? CPlaneVisualized
    {
        get => _scplane;
        set {
            if (CGridItem.Equals (_scplane, value)) return;
            
            DBG.DATA ();
            
            _scplane = value;
            Emit ();
        }
    }

    public RO_CPlane? CPlaneActive
    {
        get => _acplane;
        set {
            if (RhinoHelpers.EqualCPlanes (_acplane, value)) return;
            
            DBG.DATA ();
            
            _acplane = value;
            Emit ();
        }
    }

    RO_CPlane? _camcplane;
    public RO_CPlane? CameraTargetCPlane
    {
        get => _camcplane;
        set {
            if (RhinoHelpers.EqualCPlanes (_camcplane, value)) return;

            DBG.Log ();

            CPlaneActive = value;
            _camcplane = value;
            Emit ();
        }
    }

    #endregion


    #region INotifyPropertyChanged

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    public virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion
}



/***   ██       █████  ██    ██  ██████  ██    ██ ████████   ***/
/***   ██      ██   ██  ██  ██  ██    ██ ██    ██    ██      ***/
/***   ██      ███████   ████   ██    ██ ██    ██    ██      ***/
/***   ██      ██   ██    ██    ██    ██ ██    ██    ██      ***/
/***   ███████ ██   ██    ██     ██████   ██████     ██      ***/



public class WorldCPlaneLayout : TabContent
{
    ViewportData ViewportData;

    public WorldCPlaneLayout (MenuData data, ViewportData viewportData)
    {
        ViewportData = viewportData;

        var top    = new ToolCPlane (Ressources.PlanTopPng    , new (ArchiCPlane.WorldXY), data, ViewportData);
        var bottom = new ToolCPlane (Ressources.PlanBottomPng , new (ArchiCPlane.WorldInvertedXY), data, ViewportData);
        var left   = new ToolCPlane (Ressources.PlanLeftPng   , new (ArchiCPlane.WorldInvertedYZ), data, ViewportData);
        var right  = new ToolCPlane (Ressources.PlanRightPng  , new (ArchiCPlane.WorldYZ), data, ViewportData);
        var front  = new ToolCPlane (Ressources.PlanFrontPng  , new (ArchiCPlane.WorldXZ), data, ViewportData);
        var back   = new ToolCPlane (Ressources.PlanBackPng   , new (ArchiCPlane.WorldInvertedXZ), data, ViewportData);

        var table = new EF.TableLayout
        {
            Spacing = new (0, 0),
            Padding = 0,
            Rows = {
                new EF.TableRow (null, top, null, null),
                new EF.TableRow (left, front, right, back),
                new EF.TableRow (null, bottom, null, null),
            }
        };

        Eto.Content = table;
    }
}


public class NamedCPlaneLayout : TabContent
{
    public const int SCRW = 16;

    MenuData Data { get; }
    ViewportData ViewportData { get; }
    EF.StackLayout _stack;


    public NamedCPlaneLayout (MenuData data, ViewportData viewportData)
    {
        Data = data;
        ViewportData = viewportData;

        var btnprops = new EF.Button { Text="Properties" };

        // var head = new EF.StackLayout
        // {
        //     Orientation                = EF.Orientation.Horizontal,
        //     HorizontalContentAlignment = EF.HorizontalAlignment.Center,
        //     VerticalContentAlignment   = EF.VerticalAlignment.Stretch,
        //     Items = { btnprops }
        // };

        _stack = new EF.StackLayout
        {
            Orientation                = EF.Orientation.Vertical,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment   = EF.VerticalAlignment.Stretch
        };

        var body = new EF.Scrollable
        {
            Padding = new (0, 0, SCRW, 0),
            Border = EF.BorderType.None,
            Content = _stack,
        };

        Eto.Content = new EF.StackLayout
        {
            Orientation                = EF.Orientation.Vertical,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment   = EF.VerticalAlignment.Stretch,
            Spacing = 8,
            Items = {
                // new (head, expand: false),
                new (body, expand: true)
            }
        };

        _Update ();

        ViewportData.CPlanes.PropertyChanged += _CPlaneRegistryChanged;

        Eto.Shown += _OnShown;
        Eto.MouseLeave += _OnMouseLeave;
    }

    public override bool IsVisible 
    {
        get => base.IsVisible;
        set {
            if (base.IsVisible == value) return;

            DBG.PROP (value);

            _Sync (); // +Idle before Eto.Visible = true
            base.IsVisible = value;
        }
    }


    void _Sync ()
    {
        DBG.CTOR ();

        if (base.IsVisible)
            ViewportData.CPlanes.StartSync ();
        else
            ViewportData.CPlanes.StopSync ();
    }

    void _Update ()
    {
        DBG.Log ();

        Eto.SuspendLayout();
        _stack.Items.Clear ();

        foreach (var cp in ViewportData.CPlanes.Entries)
        {
            _stack.Items.Add (new ToolCPlane (cp.Name, cp, Data, ViewportData));
        }
        
        _stack.Items.Add (null);

        Eto.ResumeLayout ();
        Eto.Invalidate (invalidateChildren: true);
    }

    void _CPlaneRegistryChanged (object _, PropertyChangedEventArgs e)
    {
        DBG.Log ();

        _Update ();
    }


    void _OnShown (object _, EventArgs e)
    {
        DBG.EVENT ();

        _Sync ();
    }

    void _OnMouseLeave (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();

        ViewportData.CPlaneVisualized = null;
    }
}


public class MainLayout : Control <EF.StackLayout>
{
    public MainLayout (MenuData data)
    {
        Data = data;
        Data.PropertyChanged += _OnDataChanged;

        Eto.Orientation = EF.Orientation.Horizontal;
        Eto.VerticalContentAlignment = EF.VerticalAlignment.Top;
        Eto.Spacing = 8;
        Eto.Padding = 0;
    }


    Dictionary <int, TabContent> _tabcontents = new ();
    List <(int[] Key, ED.Size Size)> _compositions = new ();


    public void Append (TabContent content, bool expand)
    {
        if (_tabcontents.ContainsKey (content.GetHashCode ())) return;

        if (Eto.Items.Count > 0)
            Eto.Items[Eto.Items.Count-1].Expand = false;

        if (expand)
            Eto.Items.Add (new (content.Eto, EF.VerticalAlignment.Stretch, true));
        else
            Eto.Items.Add (new (content.Eto, EF.VerticalAlignment.Top, false));

        _tabcontents.Add (content.GetHashCode (), content);
    }
    

    MenuData Data;

    void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Data.ActiveTabs):

            DBG.DATA ();

            _UpdateContents ();
            break;
        }
    }

    void _UpdateContents ()
    {
        DBG.Log ();

        _SuspendScrollable ();
        foreach (var tab in Data.Tabs)
        {
            tab.Layout.IsVisible = tab.IsActive;
        }
        _ResumeScrollable ();
    }

    bool _Test (int[] A, int[] B)
    {
        int cA = A.Length;
        int cB = B.Length;
        if (cA != cB)
            return false;
        
        if (cA == 0)
            return true;

        int count = cA;
        for (int i = 0; i < count; i++)
        {
            if (A[i] != B[i])
                return false;
        }

        return true;
    }

    EF.Window? _GetOwnerWindow ()
    {
        var p = Eto.Parent;
        while (p != null)
        {
            if (p is EF.Window win) return win;
            p = p.Parent;
        }
        return null;
    }

    void _SuspendScrollable ()
    {
        var win = _GetOwnerWindow ();
        if (win == null)
            return;

        var key = (
            from tab in Data.Tabs
            where tab.Layout.IsVisible
            select tab.GetHashCode ()
        ).ToArray ();
        Array.Sort (key);

        DBG.Log (win.Size);

        var index = _compositions.FindIndex ((item) => _Test (item.Key, key));
        if (index < 0)
            _compositions.Add ((key, win.Size));
        else
            _compositions[index] = new (key, win.Size);
    }

    void _ResumeScrollable ()
    {
        var win = _GetOwnerWindow ();
        if (win == null)
            return;
    
        var key = (
            from tab in Data.Tabs
            where tab.Layout.IsVisible
            select tab.GetHashCode ()
        ).ToArray ();
        Array.Sort (key);

        var index = _compositions.FindIndex ((item) => _Test (item.Key, key));
        if (index < 0)
        {
            DBG.Log ("AutoSize");
            win.AutoSize = true;
            win.Height = -1;
            win.AutoSize = true;
            win.Width = -1;
            win.AutoSize = true;
            win.AutoSize = false;
        }
        else
        {
            DBG.Log ("Rescure");
            win.Size = _compositions[index].Size;
        }
    }
}


public class SideBar : Control <EF.StackLayout>
{
    MenuData Data;

    public SideBar (MenuData data)
    {
        Data = data;
        Eto.Orientation = EF.Orientation.Vertical;
        Eto.Padding = new (0);

        Eto.LoadComplete += _OnLoadComplete;
    }

    void _InitializeTabs ()
    {
        var first = Tabs.First ();
        if (first != null)
            first.IsActive = true;
    }

    void _OnLoadComplete (object _, EventArgs e)
    {
        DBG.EVENT ();

        _InitializeTabs ();
    }

    int _tabcount;

    public IEnumerable <Tab> Tabs => from c in Eto.Items where c != null && c.Control is Tab select (Tab)c.Control;

    public void Append (Tab child)
    {
        Eto.Items.Insert (_tabcount, new (child, EF.HorizontalAlignment.Stretch, expand: false));
        _tabcount++;
    }

    public void Append (Tool child)
    {
        Eto.Items.Add (new (child, EF.HorizontalAlignment.Stretch, expand: false));
    }

}



/***   ██     ██ ██ ███    ██ ██████   ██████  ██     ██   ***/
/***   ██     ██ ██ ████   ██ ██   ██ ██    ██ ██     ██   ***/
/***   ██  █  ██ ██ ██ ██  ██ ██   ██ ██    ██ ██  █  ██   ***/
/***   ██ ███ ██ ██ ██  ██ ██ ██   ██ ██    ██ ██ ███ ██   ***/
/***    ███ ███  ██ ██   ████ ██████   ██████   ███ ███    ***/



public class ViewportMenu : VolatileMenu
{
    NavigationMenu Controller;

    SideBar Side;
    MainLayout Body;

    WorldCPlaneLayout WCPLayout;
    NamedCPlaneLayout NCPLayout;

    public ViewportMenu (NavigationMenu controller) : base (controller.Settings)
    {
        Controller = controller;

        Eto.Title = "Navigation menu";

        WCPLayout = new WorldCPlaneLayout (Data, ViewportData) { IsVisible = false };

        NCPLayout = new NamedCPlaneLayout (Data, ViewportData) { IsVisible = false };

        const string tooltip = "\nClick to activate the tool.\nShift + click to activate multiple tools.";

        Body = new MainLayout (Data);
        Body.Append (WCPLayout, false);
        Body.Append (NCPLayout, true);

        var wcptab = new Tab (Data, Ressources.AxesPng, WCPLayout)
        {
            ToolTip = "Aligned CPlanes" + tooltip,
            Resizable = false
        };
        var ncptab = new Tab (Data, Ressources.AxesNotAlignedPng, NCPLayout)
        {
            ToolTip = "Named CPlanes" + tooltip,
            Resizable = false //true
        };
        
        Data.AppendTab (wcptab);
        Data.AppendTab (ncptab);

        Side = new SideBar (Data);
        Side.Append (wcptab);
        Side.Append (ncptab);
        Side.Append (new ToolProjection (ViewportData, Ressources.ParallelPng, Ressources.PerspectivePng));

        Content = new VHLayout
        {
            Side = Side.Eto,
            Body = Body.Eto,
            Foot = _CreateFoot ()
        };

        _InitializeFormEvents ();
    }


    MenuData Data = new ();
    
    ViewportData ViewportData => Controller.ViewportData;

    public new ViewportMenuSettings Settings => (ViewportMenuSettings)base.Settings;


    #region Foot

    EF.Control _CreateFoot ()
    {
        var gear = new EF.ImageView
        {
            Height = 22,
            Width = 22,
            Image = Ressources.GearPng
        };

        var free = new ThisIsFree (Settings);

        var foot = new EF.StackLayout
        {
            Orientation                = EF.Orientation.Horizontal,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment   = EF.VerticalAlignment.Stretch,
            Cursor = EF.Cursors.Pointer,
            Items = {
                new EF.StackLayoutItem (gear, EF.VerticalAlignment.Center, expand: false),
                new EF.StackLayoutItem (free, EF.VerticalAlignment.Center, expand: true),
            }
        };

        foot.MouseEnter += delegate { free.SetText ("Settings"); };
        foot.MouseLeave += delegate { free.ResetText (); };
        foot.MouseUp    += delegate
        {
            if (FormDrag.WindowMoved)
                return;

            Controller.ShowSettings ();

            // if (Settings.Persistent == false)
            //     Controller.HideForm ();
        };

        return foot;
    }

    #endregion


    #region Form Events

    void _InitializeFormEvents ()
    {
        DBG.Log ();
        
        Eto.LoadComplete += _LoadComplete;
        Eto.MouseLeave   += _OnMouseLeave;
        Eto.MouseDown    += _OnMouseDown;
        Eto.MouseUp      += _OnMouseUp;
        Eto.KeyDown      += _OnKeyDown;
        Eto.KeyUp        += _OnKeyUp;
    }

    void _LoadComplete (object _, EventArgs e)
    {
        DBG.EVENT ();
        
        AutoSize = true;
        AutoSize = false;
    }
    
    void _OnMouseLeave (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        
        ViewportData.CPlaneVisualized = null;
    }

    void _OnMouseDown (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        
        Data.MouseButtons = e.Buttons;
    }

    void _OnMouseUp (object _, EF.MouseEventArgs e)
    {
        DBG.EVENT ();
        
        Data.MouseButtons = e.Buttons;
    }

    EF.Keys _lastkey; 

    void _OnKeyDown (object _, EF.KeyEventArgs e)
    {
        if (e.IsChar && Eto.HasFocus)
        {
            DBG.EVENT ("RhinoApp.SetFocusToMainWindow");

            RhinoApp.SendKeystrokes (""+e.KeyChar, appendReturn: false);
            RhinoApp.SetFocusToMainWindow ();
        }
        else if (_lastkey != e.Modifiers) // Avoid repeating when a key is pressed
        {
            DBG.EVENT ();

            Data.Modifiers = _lastkey = e.Modifiers;
        }
    }

    void _OnKeyUp (object _, EF.KeyEventArgs e)
    {
        DBG.EVENT ();

        Data.Modifiers = _lastkey = e.Modifiers;
    }

    #endregion
}



/***    ██████  ██████  ███    ██ ██████  ██    ██ ██ ████████   ***/
/***   ██      ██    ██ ████   ██ ██   ██ ██    ██ ██    ██      ***/
/***   ██      ██    ██ ██ ██  ██ ██   ██ ██    ██ ██    ██      ***/
/***   ██      ██    ██ ██  ██ ██ ██   ██ ██    ██ ██    ██      ***/
/***    ██████  ██████  ██   ████ ██████   ██████  ██    ██      ***/


class CPlaneConduit : RD.DisplayConduit
{
    CGridItem? _cplane;

    public CGridItem? CPlane
    {
        get => _cplane;
        set {
            // if (CGridItem.Equals (_cplane, value)) return;
            if (value == null)
                Enabled = false;
            _cplane = value;
        }
    }

    protected override void CalculateBoundingBox (RD.CalculateBoundingBoxEventArgs e)
    {
        if (_cplane == null)
        {
            DBG.Fail ("_cplane == null");
            return;
        }
        e.IncludeBoundingBox (_cplane.BoundingBox);
    }

    protected override void DrawForeground (RD.DrawEventArgs e)
    {
        if (_cplane == null)
        {
            DBG.Fail ("_cplane == null");
            return;
        }
        _cplane.Draw (e);
    }
}



/***    ██████  ██████  ███    ██ ████████ ██████   ██████  ██      ██      ███████ ██████    ***/
/***   ██      ██    ██ ████   ██    ██    ██   ██ ██    ██ ██      ██      ██      ██   ██   ***/
/***   ██      ██    ██ ██ ██  ██    ██    ██████  ██    ██ ██      ██      █████   ██████    ***/
/***   ██      ██    ██ ██  ██ ██    ██    ██   ██ ██    ██ ██      ██      ██      ██   ██   ***/
/***    ██████  ██████  ██   ████    ██    ██   ██  ██████  ███████ ███████ ███████ ██   ██   ***/



public class NavigationMenu : IDisposable
{
    public NavigationMenu (ViewportMenuSettings settings)
    {
        DBG.CTOR ();
        
        Settings = settings;
        Settings.PropertyChanged += _OnSettingsChanged;

        ViewportData = new ();
        ViewportData.PropertyChanged += _OnDataChanged;
        
        DocumentObserver.OnBeginDocument += _OnBeginDocument;
        DocumentObserver.OnEndDocument   += _onEndDocument;
    }

    ~NavigationMenu ()
    {
        DBG.CTOR ();

        try { Dispose (); } // when app close, Eto form is destroy
        catch { }
    }
    
    public void Dispose ()
    {
        DBG.CTOR ();
        
        Stop ();
        ViewportData.Dispose ();
        
        DocumentObserver.OnBeginDocument -= _OnBeginDocument;
        DocumentObserver.OnEndDocument   -= _onEndDocument;
    }


    bool _isstarted;

    public void Start ()
    {
        DBG.CTOR ();

        _isstarted = true;    
        _InitializeHookSettings ();
        _StartKeyboardObserver ();
    }

    public void Stop ()
    {
        DBG.CTOR ();
        
        _StopKeyboardObserver ();
        _DestroyForm ();
        _isstarted = false;
    }



    #region Settings

    public ViewportMenuSettings Settings { get; }

    public void ShowSettings ()
    {
        var form = new ViewportMenuSettingsForm (Settings);
        form.Show ();
    }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (Settings.Modier):
        case nameof (Settings.SecondaryModifier):
        case nameof (Settings.TertiaryModifier):
        case nameof (Settings.Toolbar):
                
            DBG.DATA (e.PropertyName);

            _InitializeHookSettings ();
            break;
        }
    }

    #endregion


    #region Viewport Data

    public ViewportData ViewportData { get; }

    void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
        case nameof (ViewportData.CameraTargetCPlane):

            DBG.DATA (e.PropertyName);

            if (Settings.Volatile && Settings.Persistent == false)
                HideForm ();

            if (ViewportData.CameraTargetCPlane == null)
                break;
            
            _SetCPlane (ViewportData.CameraTargetCPlane);
            _AlignCamera (ViewportData.CameraTargetCPlane);
            RedrawViewport ();
            break;

        case nameof (ViewportData.CPlaneActive):

            DBG.DATA (e.PropertyName);

            if (Settings.Volatile && Settings.Persistent == false)
                HideForm ();

            if (ViewportData.CPlaneActive == null)
                break;
            
            _SetCPlane (ViewportData.CPlaneActive);
            RedrawViewport ();
            break;

        case nameof (ViewportData.CPlaneVisualized):
            
            DBG.DATA (e.PropertyName);

            if (FormIsVisible == false)
                return;

            _VisualizeCPlane (ViewportData.CPlaneVisualized);
            RedrawViewport ();
            break;

        case nameof (ViewportData.ViewportProjection):
        
            DBG.DATA (e.PropertyName);

            SetProjection (ViewportData.ViewportProjection);
            RedrawViewport ();
            break;
        }
    }

    #endregion


    #region Form

    public ViewportMenu? Form { get; private set; }

    public bool FormIsVisible => (Form?.IsVisible) ?? false;

    public void ToggleForm ()
    {
        DBG.Log ();
        
        if (Form == null)
            ShowForm ();
        else if (FormIsVisible)
            HideForm ();
        else
            ShowForm ();
    }

    public void ShowForm ()
    {
        DBG.Log ();
        
        if (Form == null)
        {
            Form = new (this);
            Form.PropertyChanged += _OnFormChanged;
        }
        Form.IsVisible = true;
    }

    public async void HideForm ()
    {
        DBG.Log ();
        
        if (Form != null)
        {
            // A delay for a double click when the menu is volatile.
            await System.Threading.Tasks.Task.Delay (100); 
            Form.IsVisible = false;
        }
    }

    void _DestroyForm ()
    {
        DBG.CTOR ();
        
        if (Form != null) {
            Form.IsVisible = false;
            Form.PropertyChanged -= _OnFormChanged;
            Form.Dispose ();
            Form = null;
        }
    }

    void _OnFormChanged (object _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof (Form.IsVisible))
            return;
        
        DBG.PROP (e.PropertyName, Form!.IsVisible);
        
        if (Form!.IsVisible)
        {
            ViewportData.StartSync ();
            InitializeGrasshopperEditor ();
        }
        else
        {
            // Fool's guard
            FormDrag.Stop ();

            ViewportData.StopSync ();
            _StopCPlaneConduit ();
            RUI.RhinoEtoApp.MainWindow.Focus ();
        }
    }

    #endregion


    #region Hook


    EF.Window? _gheditor;

    public void InitializeGrasshopperEditor ()
    {
        DBG.Log ();
        
        _gheditor = Grasshopper.Instances.EtoDocumentEditor;
    }

    public void ToogleGrasshopperEditor ()
    {
        if (_gheditor == null) return;

        DBG.Log ();
        
        if (_gheditor.WindowState == EF.WindowState.Minimized)
            _gheditor.WindowState = EF.WindowState.Normal;
        else _gheditor.Minimize ();
    }


    static IKeyboardObserver? g_kbobserver;
    
    void _StartKeyboardObserver ()
    {
        DBG.CTOR ();

        g_kbobserver ??= Keyboard.CreateObserver ();
        if (g_kbobserver.IsEnabled == false)
            g_kbobserver.Start (_OnKeyDown, _OnKeyUp);
    
    }
    
    void _StopKeyboardObserver ()
    {
        DBG.CTOR ();

        g_kbobserver?.Stop ();
    }
    

    bool _prevstarted;

    void _OnBeginDocument ()
    {
        DBG.CTOR ();
        
        _prevstarted = (g_kbobserver?.IsEnabled) ?? false;
        _StopKeyboardObserver ();
    }
    
    void _onEndDocument (RhinoDoc? doc)
    {
        DBG.CTOR ();
        
        if (_prevstarted)
            _StartKeyboardObserver ();
    }


    delegate void HookAction ();

    // [ Alt, Ctrl, Shift ]
    readonly HookAction?[] _hookactions = new HookAction[3];

    int _GetActionIndex (NavigationMenuModifier modifier)
    {
        switch (modifier)
        {
        case NavigationMenuModifier.Alt   : return 0;
        case NavigationMenuModifier.Ctrl  : return 1;
        case NavigationMenuModifier.Shift : return 2;
        default : return -1;
        }
    }

    void _InitializeHookSettings ()
    {
        DBG.Log ();
        
        int index;
        _hookactions[0] = null;
        _hookactions[1] = null;
        _hookactions[2] = null;

        index = _GetActionIndex (Settings.Modier);
        if (index != -1)
        {
            _hookactions[index] = new HookAction(ToggleForm);
        }

        index = _GetActionIndex (Settings.SecondaryModifier);
        if (index != -1)
        {
            _hookactions[index] = new HookAction(ToogleGrasshopperEditor);
            InitializeGrasshopperEditor ();
        }
    }

    bool _inhookaction;

    bool _OnKeyUp (int key)
    {
        _inhookaction = false;
        return false;
    }

    bool _OnKeyDown (int key)
    {
        if (key != NativeKeys.SPACE)
        {
            return false;
        }

        if (_inhookaction) return true;

        var flag
            = (Keyboard.AltIsDown () ? 0b_001 : 0)
            | (Keyboard.CtrlIsDown () ? 0b_010 : 0)
            | (Keyboard.ShiftIsDown () ? 0b_100 : 0);
        
        var action = flag switch
        {
        1 => _hookactions[0],
        2 => _hookactions[1],
        4 => _hookactions[2],
        _ => null
        };

        if (action != null)
        {
            _inhookaction = true;
            DBG.Log (action);

            action.Invoke ();
            return true;
        }
          
        return false;
    }

    #endregion


    #region View

    LiveCamera _cam = new ();

    CPlaneConduit _cpconduit = new ();

    void _StopCPlaneConduit ()
    {
        DBG.CTOR ();
        
        _cpconduit.Enabled = false;
        RedrawViewport ();
    }

    void _VisualizeCPlane (CGridItem? cgrid)
    {
        DBG.Log ();
        
        _cpconduit.CPlane = cgrid;
        _cpconduit.Enabled = cgrid != null;
    }

    void _SetCPlane (RO_CPlane cplane)
    {
        DBG.Log ();

        ViewportData.Viewport.SetConstructionPlane (cplane);
    }

    void _AlignCamera (RO_CPlane cplane)
    {
        /*/
            NOTE:
            Les vues de dessus (Z plane == Z world) ne peuvent fonctionné avec de pers 2 points.
        /*/

        if (ViewportData.Document == null)
            return;

        DBG.Log ();
        
        var target = cplane.Plane;
        ON.Point3d p;

        // Finalement
        //
        // // if (Data.Viewport.GetFrustumCenter (out p) == false)
        // // {
        // //     // Le point pivot est placé entre l'origine du plan deconstruction actuel et celui de destination.
        // //     // La rotation du modéle n'est pas trés instinctive et peut sortir de la vue.
        // //     // Il y aurais mieux à faire comme récupérer la boite englobante visible et tourner autour de son centre.
        // //
        // //     p = Data.PreviousCPlane.Plane.Origin + (target.Origin - Data.PreviousCPlane.Plane.Origin) * 0.5;
        // // }

        p = ViewportData.Viewport.CameraLocation;
        _cam.Init (ViewportData.Viewport, p);
        (_cam.RotX, _cam.RotZ) = CameraMatrix.GetRotationsPlane (target);

        // Mais ça fonctionne !
        // _cam.PosX += target.OriginX - p.X;
        // _cam.PosY += target.OriginY - p.Y;
        // _cam.PosZ += target.OriginZ - p.Z;

        _cam.ApplyTransforms ();

        DBG.Log (""+ViewportData.Viewport.ScreenPortAspect);

        var size = cplane.GridLineCount*cplane.GridSpacing;
        var half = size; //*0.5*Data.Viewport.ScreenPortAspect;
        var b = ViewportData.Viewport.ZoomBoundingBox (new ON.BoundingBox(
            new ON.Point3d (target.OriginX-half, target.OriginY-half, target.OriginZ-half),
            new ON.Point3d (target.OriginX+half, target.OriginY+half, target.OriginZ+half)
        ));
        DBG.Log (""+b);
    }

    void SetProjection (ViewportProjection proj)
    {
        var vi = new RO.ViewportInfo (ViewportData.Viewport);
        switch (proj)
        {
        case ViewportProjection.Parallel:

            DBG.Log (proj);

            vi.ChangeToParallelProjection (symmetricFrustum: true);
            break;

        case ViewportProjection.Perspective:

            DBG.Log (proj);

            vi.ChangeToPerspectiveProjection (
                targetDistance: RH.RhinoMath.UnsetValue, // vi.TargetDistance (useFrustumCenterFallback: true),
                symmetricFrustum: true,
                lensLength: 50
            );
            break;

        case ViewportProjection.TwoPointPerspective:

            DBG.Log (proj);

            vi.ChangeToTwoPointPerspectiveProjection (
                targetDistance: RH.RhinoMath.UnsetValue, // vi.TargetDistance (useFrustumCenterFallback: true),
                up: ON.Vector3d.Zero,
                lensLength: 50
            );
            break;
        }

        ViewportData.Viewport.SetViewProjection (vi, updateTargetLocation: true);
    }

    void RedrawViewport ()
    {
        DBG.Log ();
        
        ViewportData.Viewport.ParentView.Redraw ();
    }

    #endregion
}
