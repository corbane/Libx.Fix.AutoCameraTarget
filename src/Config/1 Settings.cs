/*/
    Vrecq Jean-marie
    2022/12
    Attribution 4.0 International (CC BY 4.0)
/*/

#nullable enable

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Reflection;
using SD = System.Drawing;

using ED = Eto.Drawing;
using EF = Eto.Forms;

using RH = Rhino;
using RUI = Rhino.UI;


#if RHP
using Libx.Fix.AutoCameraTarget.Ui;
namespace Libx.Fix.AutoCameraTarget.Config;
#endif



/***    █████  ████████ ████████ ██████  ██ ██████  ██    ██ ████████ ███████ ███████   ***/
/***   ██   ██    ██       ██    ██   ██ ██ ██   ██ ██    ██    ██    ██      ██        ***/
/***   ███████    ██       ██    ██████  ██ ██████  ██    ██    ██    █████   ███████   ***/
/***   ██   ██    ██       ██    ██   ██ ██ ██   ██ ██    ██    ██    ██           ██   ***/
/***   ██   ██    ██       ██    ██   ██ ██ ██████   ██████     ██    ███████ ███████   ***/



public class Exclude : Attribute { }


public class OptionAttribute : Attribute, INotifyPropertyChanged
{
    #region Cache
    /*/
        TODO: ??? Je pense qu'un OptionAttribute peut être attaché directement aux données plutôt qu'à son type de données. ???
    /*/

    static Dictionary <(IntPtr Ptr, string PropertyNname), OptionAttribute> g_cache = new ();

    public static IEnumerable <PropertyInfo> GetPropertyInfos (Type t)
    {
        return from p in t.GetProperties ()
               where p.SetMethod != null
               where p.GetCustomAttribute <Exclude> () == null
               select p;
    }

    static OptionAttribute? _FindOptionAttribute (Type t, string propname)
    {
        OptionAttribute? a = null;

        var p = t.GetProperty (propname);
        if (p != null) 
        {
            a = p.GetCustomAttribute <OptionAttribute> (inherit: true);
            if (a != null) return a;
        }

        foreach (var iT in t.GetInterfaces ())
        {
            a = _FindOptionAttribute (iT, propname);
            if (a != null) return a;
        }

        return a;
    }

    public static OptionAttribute Get (Type t, string propname)
    {
        var k = (t.TypeHandle.Value, propname);

        if (g_cache.TryGetValue (k, out var a))
            return a;
        
        var p = t.GetProperty (propname);
        if (p == null) {
            DBG.Fail ("p == null");
            return new ();
        }

        a = _FindOptionAttribute (t, propname) ?? new OptionAttribute ();
        a.PropertyInfo = p;
        g_cache[k] = a;

        return a;
    }

    public static void InitializeCache (Type t)
    {
        foreach (var p in GetPropertyInfos (t)) Get (t, p.Name);
    }

    #endregion


    #region Internal Flags

    bool _valid   = true;
    bool _enabled = true;

    public bool Valid
    {
        get => _valid;
        set {
            if (_valid == value) return;
            _valid = value;
            Emit ();
        }
    }
    public bool Enabled
    {
        get => _enabled;
        set {
            if (_enabled == value) return;
            _enabled = value;
            Emit ();
        }
    }

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion


    #region Parameters

    public string? Group;
    public string? DisplayName;
    public string? Tooltip;

    public double Min = int.MinValue;
    public double Max = int.MaxValue;
    public double Increment = 1;

    public bool Editable = true;
    public IEnumerable <object>? Choices = null;

    #endregion


    #region Accessors

    #nullable disable // set by Get()
    public PropertyInfo PropertyInfo { get; private set; }
    #nullable enable

    public string Name => PropertyInfo.Name;

    public Type PropertyType => PropertyInfo.PropertyType;

    public object? GetValue (object obj)
    {
        try {
            return PropertyInfo.GetValue (obj);
        } catch (Exception e) {
            DBG.Fail (e.Message);
            return default;
        }
    }

    public T? GetValue <T> (object obj)
    {
        try {
            return (T)PropertyInfo.GetValue (obj);
        } catch (Exception e) {
            DBG.Fail (e.Message);
            return default;
        }
    }

    public void SetValue <T> (object obj, T value)
    {
        try {
            PropertyInfo.SetValue (obj, value);
        } catch (Exception e) {
            DBG.Fail (e.Message);
        }
    }

    #endregion
}



/***   ██████   █████  ███████ ███████      ██████ ██       █████  ███████ ███████   ***/
/***   ██   ██ ██   ██ ██      ██          ██      ██      ██   ██ ██      ██        ***/
/***   ██████  ███████ ███████ █████       ██      ██      ███████ ███████ ███████   ***/
/***   ██   ██ ██   ██      ██ ██          ██      ██      ██   ██      ██      ██   ***/
/***   ██████  ██   ██ ███████ ███████      ██████ ███████ ██   ██ ███████ ███████   ***/



public abstract class Settings : INotifyPropertyChanged
{
    #region Event

    public virtual event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void Emit ([CallerMemberName] string? memberName = null)
    {
        PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (memberName));
    }

    #endregion


    #region Helpers

    [Exclude]
    public int DataVersion { get; private set; }

    public IEnumerable <PropertyInfo> PropertyInfos => OptionAttribute.GetPropertyInfos (GetType ());

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

    public T Copy <T> () where T : new()
    {
        var data = new T ();

        foreach (var p in PropertyInfos)
        { 
            var v = p.GetValue (this);
            if (v == null) {
                DBG.Fail ("v == null");
                continue;
            }
            p.SetValue (data, v);
        }

        return data;
    }

    public void Apply (object data)
    {
        foreach (var p in PropertyInfos)
        { 
            var v = p.GetValue (data);
            if (v == null) {
                DBG.Fail ("v == null");
                continue;
            }
            p.SetValue (this, v);
        }
    }
    
    public void Save (RH.PersistentSettings chunk)
    {
        var t_bool   = typeof (bool);
        var t_int    = typeof (int);
        var t_double = typeof (double);
        var t_ecolor = typeof (ED.Color);

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        foreach (var p in props)
        {
            var t = p.PropertyType;
            
            // Enumerations are converted to string to remove dependencies imposed by `PersistentSettings.SetEnumValue<T>`.
            // Where 'T' is usually a type declared in another file or namespace
            // Try catch is here because previous versions used `PersistentSettings.[Set|Get]EnumValue`
            if (t.IsEnum)
            {
                try { chunk.SetString (p.Name, p.GetValue (this).ToString ()); }
                catch { DBG.Fail ("SAVE ERREUR"); }
            }

            else if (t == t_bool)
                chunk.SetBool (p.Name, (bool)p.GetValue (this));
            
            else if (t == t_int)
                chunk.SetInteger (p.Name, (int)p.GetValue (this));
            
            else if (t == t_double)
                chunk.SetDouble (p.Name, (double)p.GetValue (this));

            else if (t == t_ecolor)
                chunk.SetColor (p.Name, RUI.EtoExtensions.ToSystemDrawing ((ED.Color)p.GetValue (this)));

            else DBG.Fail ("Unknown settings type: ", t);
        }
    }

    public void Load (RH.PersistentSettings chunk)
    {
        var t_bool   = typeof (bool);
        var t_int    = typeof (int);
        var t_double = typeof (double);
        var t_ecolor = typeof (ED.Color);

        var props = from p in GetType ().GetProperties ()
                    where p.SetMethod != null
                    where p.GetCustomAttribute <Exclude> () == null
                    select p;

        var keys = chunk.Keys;
        foreach (var p in props)
        {
            if (keys.Contains (p.Name) == false) continue;

            var t = p.PropertyType;

            if (t.IsEnum)
            {
                try { p.SetValue (this, Enum.Parse (t,chunk.GetString (p.Name))); }
                catch { DBG.Fail ("LOAD ERREUR"); }
            }

            else if (t == t_bool)
                p.SetValue (this, chunk.GetBool (p.Name));
            
            else if (t == t_int)
                p.SetValue (this, chunk.GetInteger (p.Name));
            
            else if (t == t_double)
                p.SetValue (this, chunk.GetDouble (p.Name));

            else if (t == t_ecolor)
                p.SetValue (this, RUI.EtoExtensions.ToEto ((SD.Color)chunk.GetColor (p.Name)));

            else DBG.Fail ("Unknown settings type: ", t);
        }
    }

    #endregion


    #region Errors

    [Exclude]
    public List <string> Errors {get; } = new ();

    public void ClearErrors ()
    {
        Errors.Clear ();
        Emit (nameof (Errors));
    }
    public void AddError (string message)
    {
        Errors.Add (message);
        Emit (nameof (Errors));
    }

    #endregion


    /// <summary>
    ///     Validates the data. This is also the right place to update <see cref="OptionAttribute"/> flags </summary>
    /// <returns>
    ///     returns False to indicate that the data cannot be applied/saved. </returns>
    public abstract bool Validate ();
}


// public readonly struct Item
// {
//     public readonly IPropertyControl Control;
//     public readonly object Context;
//     public readonly PropertyInfo ControlProperty;
//     public readonly PropertyInfo ContextProperty;
//     public readonly OptionAttribute ContextAttribute;
// 
//     void ControlHandler (object sender, PropertyChangedEventArgs e)
//     {
//         if (e.PropertyName != ControlProperty.Name) return;
// 
//         if (ContextProperty.PropertyType.IsAssignableFrom (ControlProperty.PropertyType) == false &&
//             ContextProperty.PropertyType.IsSubclassOf (ControlProperty.PropertyType) == false
//         ) {
//             DBG.Fail ($"cannot assign {ContextProperty.PropertyType} {ContextProperty.Name}] to {ControlProperty.PropertyType} {ControlProperty.Name}]");
//             return;
//         }
// 
//         var cvalue = ControlProperty.GetValue (Control);
//         var ovalue = ContextProperty.GetValue (Context);
//         
//         if (object.Equals (cvalue, ovalue))
//             return;
//                
//         if (cvalue == null) { 
//             DBG.Fail ("cvalue == null");
//             return;
//         }
// 
//         ContextProperty.SetValue (Context, cvalue);
//     }
// 
//     public void UpdateControlValue ()
//     {
//         if (ControlProperty.PropertyType.IsAssignableFrom (ContextProperty.PropertyType) == false &&
//             ControlProperty.PropertyType.IsSubclassOf (ContextProperty.PropertyType) == false
//         ) {
//             DBG.Fail ($"cannot assign {ControlProperty.PropertyType} {ControlProperty.Name}] to {ContextProperty.PropertyType} {ContextProperty.Name}]");
//             return;
//         }
// 
//         var cvalue = ControlProperty.GetValue (Control);
//         var ovalue = ContextProperty.GetValue (Context);
//         
//         if (object.Equals (cvalue, ovalue))
//             return;
//         
//         if (ovalue == null) { 
//             DBG.Fail ("ovalue == null");
//             return;
//         }
// 
//         DBG.Log (ContextProperty.Name);
// 
//         ControlProperty.SetValue (Control, ovalue);
//     }
// }
// 
// public void Bind (IPropertyControl control, string cp, object context, string op)
// {
//     // var cT = control.GetType ();
//     // var oT = context.GetType ();
// 
//     // PropertyInfo? cpi;
//     // PropertyInfo? opi;
// 
//     // try
//     // { cpi = cT.GetProperty (cp, BindingFlags.Instance | BindingFlags.Public); }
//     // catch (AmbiguousMatchException) // "new" keyword
//     // { cpi = cT.GetProperty (cp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly); }
//     // catch (Exception e) { DBG.Fail (e.Message); return; }
// 
//     // try
//     // { opi = oT.GetProperty (op, BindingFlags.Instance | BindingFlags.Public); }
//     // catch (AmbiguousMatchException) // "new" keyword
//     // { opi = oT.GetProperty (op, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly); }
//     // catch (Exception e) { DBG.Fail (e.Message); return; }
// 
//     // if (cpi == null) { DBG.Fail ($"Unknown {cp} on {cT}"); return; }
//     // if (opi == null) { DBG.Fail ($"Unknown {op} on {oT}"); return; }
// 
//     // _links.Add (new Item (
//     //     control: control,
//     //     context: context,
//     //     controlProperty: cpi!,
//     //     contextProperty: opi!,
//     //     contextAttribute: OptionAttribute.Get (oT, op)
//     // ));
// }



/***   ██       █████  ██    ██  ██████  ██    ██ ████████   ***/
/***   ██      ██   ██  ██  ██  ██    ██ ██    ██    ██      ***/
/***   ██      ███████   ████   ██    ██ ██    ██    ██      ***/
/***   ██      ██   ██    ██    ██    ██ ██    ██    ██      ***/
/***   ███████ ██   ██    ██     ██████   ██████     ██      ***/



public class SettingsLayout <S> : EF.StackLayout where S : Settings, new ()
{
    public const int SCRW = 14;
    public const int PAD = 8;
    public const int LINEH = 22;
    public const int CTRLW = 140;


    readonly Type _datatype;
    public readonly S Settings;
    public S Copy { get; private set; }

    readonly EF.Scrollable _wrap;
    readonly EF.StackLayout _body;
    readonly EF.StackLayout _foot;
    readonly EF.Button _bok;
    readonly EF.Button _bno;


    public SettingsLayout (S settings)
    {
        Settings    = settings;
        DataContext = settings;
        _datatype   = settings.GetType ();
        Copy        = Settings.Copy <S> ();
        
        Orientation                = EF.Orientation.Vertical;
        HorizontalContentAlignment = EF.HorizontalAlignment.Stretch;
        VerticalContentAlignment   = EF.VerticalAlignment.Stretch;
        MinimumSize                = new (CTRLW + PAD*3 + SCRW, LINEH);

        _bno = new EF.Button { Text = "Cancel" };
        _bno.Click += delegate { RestoreOptions (); /*Close ();*/ };

        _bok = new EF.Button { Text = "Ok" };
        _bok.Click += delegate { ApplyOptions (); /*Close ();*/ };

        _body = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            VerticalContentAlignment = EF.VerticalAlignment.Stretch,
        };

        _wrap = new EF.Scrollable
        {
            Border = EF.BorderType.None,
            Padding = new (PAD, PAD, PAD + SCRW, 0 + SCRW),
            ExpandContentHeight = true,
            ExpandContentWidth  = false,
            Content = _body
        };

        _foot = new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            Padding = new (PAD),
            Spacing = 8,
            Items = { null, _bok, _bno, null }
        };

        Items.Add (new EF.StackLayoutItem (_wrap, expand: true));
        Items.Add (new EF.StackLayoutItem (_foot, expand: false));
    }


    #region Controls

    public EF.Button ButtonOk => _bok;
    
    public EF.Button ButtonCancel => _bno;

    public new EF.Control Content
    {
        get => _body;
        set { throw new NotImplementedException ("Cannot define the content"); }
    }


    // Bindings ControlBindings = new ();

    protected virtual void UpdateControls (string? propertyChanged)
    {
        _bok.Enabled = Settings.Validate ();
        // ControlBindings.UpdateControls (propertyChanged);
    }

    #endregion


    #region Settings Events

    void _AttachPropertyChangedEvent ()
    {
        Settings.PropertyChanged += _OnSettingsChanged;
    }

    void _DetachPropertyChangedEvent ()
    {
        Settings.PropertyChanged -= _OnSettingsChanged;
    }

    void _OnSettingsChanged (object sender, PropertyChangedEventArgs e)
    {
        UpdateControls (e.PropertyName);
    }

    protected virtual void RestoreOptions ()
    {
        Settings.Apply (Copy);
    }

    protected virtual void ApplyOptions ()
    {
        if (Settings.Validate () == false) Settings.Apply (Copy);
        else Copy = Settings.Copy <S> ();
    }
    
    #endregion


    #region Form Events

    protected override void OnLoad (EventArgs e)
    {
        _AttachPropertyChangedEvent ();
        // ControlBindings.AttachControlEvents ();

        base.OnLoad(e);
    }

    protected override void OnUnLoad (EventArgs e)
    {
        _DetachPropertyChangedEvent ();
        // ControlBindings.DetachControlEvents ();
        // ControlBindings.UnBind ();

        if (Settings.DataVersion != Copy.DataVersion)
            ApplyOptions ();
            
        base.OnUnLoad(e);
    }

    protected override void OnShown (EventArgs e)
    {
        base.OnShown (e);
        UpdateControls (null);
        if (_wrap.Content != null)
            SizeChanged += _OnSizeChanged;
    }

    // La barre de défilement vertical pose des problémes visuels:
    // - Si les controles enfants peuvent être décaler, ils sont décaler l'orsque la barre s'affiche.
    // - Si les controles enfants ne peuvent pas être décaler, la barre est déssiner par dessus les controles enfants.
    // - Sous windows 11 cette barre de défilement est énorme, donc les bouton Up/Down d'une entrée numérique sont cachées.
    // - Eto n'offre pas de possibilité de réduire la largeur de la barre ou de la laisser toujours affiché.
    // L'idée ici est de toujours laisser un espae vide pour l'emplacemet de la barre de défilement.
    // Pour que cela fonctionne il faut que la propriété `ExpandContentWidth` du Scrollable soit sur false.
    protected void _OnSizeChanged (object sender, EventArgs e)
    {
        if (this.HasFocus == false) return;
        _body.Width = Width - (PAD*2 + SCRW);
    }

    #endregion


    #region Controls Factory


    public static SettingsLayout <S> CreateDefaultLayout (S settings)
    {
        var t = typeof(S);

        var layout = new SettingsLayout <S> (settings);
        var section = layout.Section ((string?)null);
        foreach (var p in settings.PropertyInfos)
        {
            var a = OptionAttribute.Get (t, p.Name);
            
            if (string.IsNullOrWhiteSpace (a.Group) == false)
            {
                section.Rows.Add (
                    layout._Divider (a.Group!) 
                );
            }
        
            var c = layout.GetControl (p.Name);
            if (c == null) continue;

            section.Rows.Add (layout.Row (a.DisplayName ?? p.Name, c));
        }

        return layout;
    }


    // Avoid the GC to remove the object
    List <IControl> _store = new ();

    protected EF.Control GetControl (string propname)
    {
        IControl c;
        
        var a = OptionAttribute.Get (_datatype, propname);

        switch (a.GetValue (Settings))
        {
        default:
            DBG.Fail ($"Unknow property type: {a.PropertyType} {propname}");
            return new EF.Panel ();

        case null:
            DBG.Fail ($"No default value for property {propname}");
            return new EF.Panel ();

        case bool:
            c = new CheckBox (Settings, a);
            break;
            
        case int:
            c = new IntNumericStepper (Settings, a);
            break;
            
        case double:
            c = new DoubleNumericStepper (Settings, a);
            break;
            
        case Enum:
            c = new EnumDropDown (Settings, a);
            break;
            
        case ED.Color:
            c = new ColorPicker (Settings, a);
            break;
            
        case string:
            c = a.Choices == null ? new TextBox (Settings, a)
              : a.Editable ? new ComboBox (Settings, a)
              : new DropDown (Settings, a);
            break;
        }

        _store.Add (c);
        return c.Eto;
    }


    protected EF.TableLayout Section (string? label, params EF.TableRow[] rows)
    {
        if (label != null)
            _body.Items.Add (_Divider (label));
        return Section (rows);
    }

    protected EF.TableLayout Section (params EF.TableRow[] rows)
    {
        var c = new EF.TableLayout { Spacing = new (PAD, PAD) };

        foreach (var r in rows) c.Rows.Add (r);

        // without this line (without the label) the column widths of a table with one row are not correctly calculed.
        c.Rows.Add (new EF.TableRow (null, new EF.Label { Width = CTRLW, Height = 0 }));

        _body.Items.Add (new EF.StackLayoutItem (c, EF.VerticalAlignment.Stretch));
        return c;
    }

    protected EF.TableRow Row (string label, EF.Control? cell)
    {
        var row =  new EF.TableRow ();
        row.Cells.Add (new EF.TableCell (_Label (label, 3), scaleWidth: true));
        row.Cells.Add (new EF.TableCell (cell, scaleWidth: false));
        return row;
    }

    protected EF.StackLayout HBox (params EF.Control[] controls)
    {
        var hbox = new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            HorizontalContentAlignment = EF.HorizontalAlignment.Stretch,
            Width = CTRLW
        };
        var w = CTRLW / controls.Length;
        foreach (var c in controls) 
        {
            c.Width = w;
            hbox.Items.Add (new EF.StackLayoutItem (c, expand: true));
        }
        return hbox;
    }

    protected EF.StackLayout _Divider (string label)
    {
        return new EF.StackLayout
        {
            Orientation = EF.Orientation.Horizontal,
            Spacing = 8,
            Items = {
                new EF.StackLayoutItem (_Label ( label ), EF.VerticalAlignment.Stretch, expand: false),
                new EF.StackLayoutItem (new RUI.Controls.Divider { }, EF.VerticalAlignment.Stretch, expand: true)
            }
        };
    }
    
    EF.Expander _Expander (string label, params EF.Control?[] items)
    {
        var stack = new EF.StackLayout
        {
            Orientation = EF.Orientation.Vertical,
        };
        foreach (var c in items) stack.Items.Add (c);
        return new EF.Expander 
        {
            Header = _Divider (label),
            Expanded = false,
            Content = stack
        };
    }

    EF.Label _Label (string text, int indent = 0)
    {
        return new EF.Label {
            Text   = new string(' ', indent)+text,
            Height = LINEH,
            Wrap   = EF.WrapMode.None,
        };
    }

    #endregion
}



/***    ██████  ██████  ███    ██ ████████ ██████   ██████  ██        ***/
/***   ██      ██    ██ ████   ██    ██    ██   ██ ██    ██ ██        ***/
/***   ██      ██    ██ ██ ██  ██    ██    ██████  ██    ██ ██        ***/
/***   ██      ██    ██ ██  ██ ██    ██    ██   ██ ██    ██ ██        ***/
/***    ██████  ██████  ██   ████    ██    ██   ██  ██████  ███████   ***/



public abstract class PropertyControl <T> : Control <T> where T : EF.Control, new()
{
    public PropertyControl (INotifyPropertyChanged data, OptionAttribute attr)
    {
        Data = data;
        Attribute = attr;
        
        attr.PropertyChanged += _OnAttributeChanged;
        data.PropertyChanged += _OnDataChanged;

        Eto.Load += delegate { SetEtoValue (attr.GetValue (Data)); };
    }


    public OptionAttribute Attribute { get; }

    protected virtual void UpdateEto ()
    {
        var eto = Eto;
        eto.Enabled = Attribute.Enabled;
        if (Attribute.Valid)
            eto.BackgroundColor = Attribute.Enabled ? LightTheme.ActiveInputBackground : LightTheme.InactiveInputBackground;
        else
            eto.BackgroundColor = LightTheme.InvalidInputBackground;
    }

    void _OnAttributeChanged (object sender, PropertyChangedEventArgs e)
    {
        UpdateEto ();
    }


    public INotifyPropertyChanged Data { get; }

    // Occur when the property is displayed in a visible window
    /// <summary>
    ///     Occur when the property is displayed in a visible window
    ///     and whan the Data changed. </summary>
    /// <param name="value"></param>
    protected abstract void SetEtoValue (object? value);
    
    public void _OnDataChanged (object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Attribute.Name)
            SetEtoValue (Attribute.GetValue (Data));
    }
}

public abstract class ListControl <T> : PropertyControl <T> where T : EF.ListControl, new()
{
    object[] _values;

    public ListControl (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        var choices = attr.Choices.ToArray ();

        if (choices == null) {
            DBG.Fail ("choices == null");
            choices = Array.Empty <object> ();
        }
        
        Eto.DataStore = _values = (from v in choices where v != null select v).ToArray ();
        
        if (_values.Length == 0) {
            DBG.Fail ("choices.Length == 0");
            return;
        }

        Eto.SelectedValueChanged += _OnValueIndexChanged;
    }

    void _OnValueIndexChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, Eto.SelectedValue ?? _values[0]);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value == null) {
            DBG.Fail ("value == null");
            return;
        }
        Eto.SelectedValue = value;
    }
}

public class CheckBox : PropertyControl <EF.Button>
{
    // protected override Type EtoValueType => typeof (bool);

    bool _checked;

    public CheckBox (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        UpdateEto ();

        Eto.Click += _OnClick;
    }

    void _OnClick (object sender, EventArgs e)
    {
        _checked = !_checked;
        this.Attribute.SetValue (Data, _checked);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value is not bool b) return;
        _checked = b;
        UpdateEto ();
    }

    protected override void UpdateEto ()
    {
        base.UpdateEto ();
        if (_checked)
        {
            Eto.Text = "Enabled";
            Eto.TextColor = Attribute.Enabled ? LightTheme.ActiveInputText : LightTheme.DisabledInputText;
        }
        else
        {
            Eto.Text = "Disabled";
            Eto.TextColor = Attribute.Enabled ? LightTheme.InactiveInputText : LightTheme.DisabledInputText;
        }
    }
}

public class DropDown : ListControl <EF.DropDown>
{
    public DropDown (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr) { /**/ }
}

public class EnumDropDown : PropertyControl <EF.DropDown>
{
    List <string> _names;
    List <object> _values;

    public EnumDropDown (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        var enumT = attr.PropertyType;

        _names = enumT.GetEnumNames ().ToList ();
        _values = new List <object> (enumT.GetEnumValues ().OfType <object> ());

        if (_values.Count == 0) DBG.Fail ("enumT.GetEnumValues ().Count == 0");

        Eto.DataStore = _names;
        Eto.SelectedValueChanged += _OnSelectedIndexChanged;
    }

    protected override void SetEtoValue (object? value)
    {
        // Je viens de voir la class `PersistentSettingsConverter`
        
        var index = value == null ? -1 : _values.IndexOf (value);
        if (index < 0) { DBG.Fail ("index < 0"); return; }
        
        if (Eto.SelectedIndex == index) return;

        Eto.SelectedValue = _names[index];
    }

    void _OnSelectedIndexChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, _values[Eto.SelectedIndex]);
    }
}

public class IntNumericStepper : PropertyControl <EF.NumericStepper> 
{
    public IntNumericStepper (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        Eto.MinValue  = (int)attr.Min;
        Eto.MaxValue  = (int)attr.Max;
        Eto.Increment = (int)attr.Increment;

        Eto.ValueChanged += _OnValueChanged;
    }

    void _OnValueChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, (int)Eto.Value);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value is int v)
            Eto.Value = v;
    }
}

public class DoubleNumericStepper : PropertyControl <EF.NumericStepper> 
{
    public DoubleNumericStepper (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        Eto.MinValue  = attr.Min;
        Eto.MaxValue  = attr.Max;
        Eto.Increment = attr.Increment;
        Eto.DecimalPlaces = BitConverter.GetBytes(decimal.GetBits((decimal)attr.Increment)[3])[2];
        
        Eto.ValueChanged += _OnValueChanged;
    }

    void _OnValueChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, Eto.Value);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value is double v)
            Eto.Value = v;
    }
}

public class ColorPicker : PropertyControl <EF.ColorPicker>
{
    public ColorPicker (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        Eto.ValueChanged += _OnValueChanged;
    }
    
    void _OnValueChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, Eto.Value);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value is ED.Color v)
            Eto.Value = v;
    }
}

public class TextBox : PropertyControl <EF.TextBox>
{
    public TextBox (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr)
    {
        Eto.TextChanged += _OnTextChanged;
    }

    void _OnTextChanged (object sender, EventArgs e)
    {
        this.Attribute.SetValue (Data, Eto.Text);
    }

    protected override void SetEtoValue (object? value)
    {
        if (value is string v)
            Eto.Text = v;
    }
}

public class ComboBox : ListControl <EF.ComboBox>
{
    public ComboBox (INotifyPropertyChanged data, OptionAttribute attr) : base (data, attr) { /**/ }
}
