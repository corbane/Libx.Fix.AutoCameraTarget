Waiting for version 8 and the `Rhino.Options.View.RotateViewAroundObjectAtMouseCursor` option,
this plugin tries to interpret the `Ctrl+Shift+RMB` keys as the default navigation behavior.

You can enable auto-targeting of the camera when Rhino starts with the command line in `Rhino Options > General > Command Lists` settings:
```
ToggleAutoCameraTarget active=Yes marker=Yes _Enter
```

**NOTE**:

Note that this is not a robust implementation with all situations.
This code is intended to be practical and fast.

If the rotation is not correctly targeted, the standard shortcuts `Ctrl+Shift+RMB` are still active and you can use it.

**ISSUE**:

Intersections are computed only with their bounding box for:
- block instances, extrusion or curve.
- Objects created in wireframe mode until the display mode is set to Shaded, Rendered, or other.

Linked file objects are ignored.

From about 10,000 objects in the views, a slowdown can be felt at the start of a rotation.
