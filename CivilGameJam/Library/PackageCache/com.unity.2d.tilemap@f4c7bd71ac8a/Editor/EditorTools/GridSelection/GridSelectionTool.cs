using System;
using System.Linq;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace UnityEditor.Tilemaps
{
    /// <summary>
    /// Abstract class for Editor Tool used to handle a GridSelection.
    /// </summary>
    public abstract class GridSelectionTool : EditorTool
    {
        private TileBase[] m_SelectionTiles;
        private TileFlags[] m_SelectionFlagsArray;
        private Vector3[] m_SelectionPositions;
        private Quaternion[] m_SelectionRotations;
        private Vector3[] m_SelectionScales;
        private int m_FirstCellWithTile;
        // Gizmo state across ticks of a single drag. m_Selection* hold the drag-start anchors
        // (frozen during a drag); these hold what the handle was last set to so we hand it back
        // next tick. Anchoring deltas against drag-start (rather than the previous tick) means
        // momentarily passing scale through 0 doesn't seed a divide-by-zero in the next divide.
        private bool m_Dragging;
        private Vector3 m_DragGizmoP;
        private Quaternion m_DragGizmoR;
        private Vector3 m_DragGizmoS;

        private int selectionCellCount => Math.Abs(GridSelection.position.size.x * GridSelection.position.size.y * GridSelection.position.size.z);

        /// <summary>
        /// Does the GUI for the GridSelectionTool for an EditorWindow.
        /// </summary>
        /// <param name="window">EditorWindow which GUI is being done.</param>
        public override void OnToolGUI(EditorWindow window)
        {
            var selection = Selection.activeObject as GridSelection;
            if (selection == null)
                return;

            if (window is SceneView && GridSelection.target != null && GridPaintingState.IsPartOfActivePalette(GridSelection.target))
                return;

            OnToolGUI();
        }

        internal void OnToolGUI()
        {
            if (GridSelection.target == null)
                return;

            var brushTarget = GridSelection.target;
            var tilemap = brushTarget.GetComponent<Tilemap>();
            if (tilemap == null)
                return;

            UpdateSelection(tilemap);
            if (m_SelectionFlagsArray == null || m_SelectionFlagsArray.Length <= 0)
                return;

            bool transformFlagsAllEqual = m_SelectionFlagsArray.All(flags => (flags & TileFlags.LockTransform) == (m_SelectionFlagsArray.First() & TileFlags.LockTransform));
            if (!transformFlagsAllEqual || (m_SelectionFlagsArray[0] & TileFlags.LockTransform) != 0)
                return;

            // End any prior drag once no handle is held, so the next drag re-anchors against fresh state.
            if (GUIUtility.hotControl == 0)
                m_Dragging = false;

            var index = m_FirstCellWithTile != -1 ? m_FirstCellWithTile : 0;
            var startP = m_SelectionPositions[index];
            var startR = m_SelectionRotations[index];
            var startS = m_SelectionScales[index];

            var p = m_Dragging ? m_DragGizmoP : startP;
            var r = m_Dragging ? m_DragGizmoR : startR;
            var s = m_Dragging ? m_DragGizmoS : startS;

            Vector3 selectionPosition = GridSelection.position.position;
            selectionPosition += tilemap.tileAnchor;
            if (selectionCellCount > 1)
            {
                selectionPosition.x = GridSelection.position.center.x;
                selectionPosition.y = GridSelection.position.center.y;
            }
            var anchorWorld = tilemap.LocalToWorld(tilemap.CellToLocalInterpolated(selectionPosition));
            var gizmoPosition = anchorWorld + p;
            var dragStartGizmoPosition = anchorWorld + startP;

            EditorGUI.BeginChangeCheck();
            HandleTool(ref gizmoPosition, ref r, ref s);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { tilemap, tilemap.gameObject }, "Move");

                // Deltas measured against drag-start, not the previous tick — so passing scale
                // through 0 doesn't seed a divide-by-zero on the next tick. Falling back to 1 on
                // a zero start-axis freezes that axis (no meaningful ratio exists from 0).
                var deltaPos = gizmoPosition - dragStartGizmoPosition;
                var deltaRotation = (Quaternion.Inverse(startR) * r).normalized;
                var deltaScale = new Vector3(
                    startS.x != 0f ? s.x / startS.x : 1f,
                    startS.y != 0f ? s.y / startS.y : 1f,
                    startS.z != 0f ? s.z / startS.z : 1f);

                m_Dragging = true;
                m_DragGizmoP = gizmoPosition - anchorWorld;
                m_DragGizmoR = r;
                m_DragGizmoS = s;

                int cellIndex = 0;
                foreach (var cellPosition in GridSelection.position.allPositionsWithin)
                {
                    if (tilemap.HasTile(cellPosition))
                    {
                        var newPosition = m_SelectionPositions[cellIndex] + deltaPos;
                        var newRotation = (m_SelectionRotations[cellIndex] * deltaRotation).normalized;
                        var newScale = new Vector3(
                            m_SelectionScales[cellIndex].x * deltaScale.x,
                            m_SelectionScales[cellIndex].y * deltaScale.y,
                            m_SelectionScales[cellIndex].z * deltaScale.z);

                        // Matrix4x4.ValidTRS only validates the matrix's bottom row (which TRS
                        // always fills with [0,0,0,1]), so we have to check finiteness ourselves
                        // to catch NaN/Inf propagation from upstream deltas.
                        if (float.IsFinite(newPosition.x) && float.IsFinite(newPosition.y) && float.IsFinite(newPosition.z)
                            && float.IsFinite(newRotation.x) && float.IsFinite(newRotation.y) && float.IsFinite(newRotation.z) && float.IsFinite(newRotation.w)
                            && float.IsFinite(newScale.x) && float.IsFinite(newScale.y) && float.IsFinite(newScale.z))
                        {
                            var trs = Matrix4x4.TRS(newPosition, newRotation, newScale);
                            tilemap.SetTransformMatrix(cellPosition, trs);
                        }
                    }
                    cellIndex++;
                }
                InspectorWindow.RepaintAllInspectors();
            }
        }

        /// <summary>
        /// Handles the gizmo for the GridSelectionTool.
        /// Implement this the handle the gizmo for the GridSelectionTool.
        /// </summary>
        /// <param name="position">Position of the GridSelection gizmo.</param>
        /// <param name="rotation">Rotation of the GridSelection gizmo.</param>
        /// <param name="scale">Scale of the GridSelection gizmo.</param>
        public abstract void HandleTool(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale);

        internal static bool IsActive()
        {
            return ToolManager.activeToolType != null && ToolManager.activeToolType.IsSubclassOf(typeof(GridSelectionTool));
        }

        private void UpdateSelection(Tilemap tilemap)
        {
            var cellCount = selectionCellCount;

            // Skip during an active handle drag — the TRS shadow (m_SelectionPositions/Rotations/
            // Scales) accumulates per-tick deltas in place, and re-reading the matrix here would
            // overwrite it with an ambiguous decomposition of the negative-scale matrix we wrote.
            if (GUIUtility.hotControl != 0
                && m_SelectionTiles != null && m_SelectionTiles.Length == cellCount
                && m_SelectionPositions != null && m_SelectionPositions.Length == cellCount
                && m_SelectionRotations != null && m_SelectionRotations.Length == cellCount
                && m_SelectionScales != null && m_SelectionScales.Length == cellCount)
            {
                return;
            }

            m_FirstCellWithTile = -1;
            var selection = GridSelection.position;
            if (m_SelectionTiles == null || m_SelectionTiles.Length != cellCount)
                m_SelectionTiles = new TileBase[cellCount];
            if (m_SelectionFlagsArray == null || m_SelectionFlagsArray.Length != cellCount)
                m_SelectionFlagsArray = new TileFlags[cellCount];
            if (m_SelectionPositions == null || m_SelectionPositions.Length != cellCount)
                m_SelectionPositions = new Vector3[cellCount];
            if (m_SelectionRotations == null || m_SelectionRotations.Length != cellCount)
                m_SelectionRotations = new Quaternion[cellCount];
            if (m_SelectionScales == null || m_SelectionScales.Length != cellCount)
                m_SelectionScales = new Vector3[cellCount];

            int index = 0;
            foreach (var p in selection.allPositionsWithin)
            {
                m_SelectionTiles[index] = tilemap.GetTile(p);
                var matrix = tilemap.GetTransformMatrix(p);
                m_SelectionFlagsArray[index] = tilemap.GetTileFlags(p);
                m_SelectionPositions[index] = matrix.GetPosition();
                m_SelectionRotations[index] = matrix.rotation;
                m_SelectionScales[index] = matrix.lossyScale;

                if (m_FirstCellWithTile == -1 && m_SelectionTiles[index] != null)
                    m_FirstCellWithTile = index;
                index++;
            }
        }
    }
}
