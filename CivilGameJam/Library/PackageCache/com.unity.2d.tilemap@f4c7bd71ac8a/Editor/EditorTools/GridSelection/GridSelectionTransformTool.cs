using UnityEditor.EditorTools;
using UnityEngine;

namespace UnityEditor.Tilemaps
{
    /// <summary>
    /// An `EditorTool` for handling Transform for a `GridSelection`.
    /// </summary>
    public class GridSelectionTransformTool : GridSelectionTool
    {
        private static class Styles
        {
            public static readonly GUIContent toolbarIcon = EditorGUIUtility.TrTextContentWithIcon("Transform", "Shows a Gizmo in the Scene view for changing the transform for the Grid Selection", "TransformTool");
        }

        /// <summary>
        /// Toolbar icon for the `GridSelectionTransformTool`.
        /// </summary>
        public override GUIContent toolbarIcon => Styles.toolbarIcon;

        private Quaternion before = Quaternion.identity;

        /// <summary>
        /// Handles the gizmo for managing Transforms for the `GridSelectionTransformTool`.
        /// </summary>
        /// <param name="position">Position of the `GridSelection` gizmo.</param>
        /// <param name="rotation">Rotation of the `GridSelection` gizmo.</param>
        /// <param name="scale">Scale of the `GridSelection` gizmo.</param>
        public override void HandleTool(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            // Outside of an active drag, align the gizmo with the tile's current rotation so the
            // position/scale sub-handles draw in the tile's local space rather than world-aligned.
            if (GUIUtility.hotControl == 0)
                before = rotation;

            EditorGUI.BeginChangeCheck();
            var after = before;
            Handles.TransformHandle(ref position, ref after, ref scale);
            if (EditorGUI.EndChangeCheck())
            {
                rotation *= Quaternion.Inverse(before) * after;
            }
            before = after;
        }
    }
}
