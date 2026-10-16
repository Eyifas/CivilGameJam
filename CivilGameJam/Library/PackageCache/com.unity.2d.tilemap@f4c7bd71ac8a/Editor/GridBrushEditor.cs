using System;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor.SceneManagement;
using UnityEngine.Scripting.APIUpdating;
using Object = UnityEngine.Object;

namespace UnityEditor.Tilemaps
{
    /// <summary>Editor for GridBrush.</summary>
    [MovedFrom(true, "UnityEditor", "UnityEditor")]
    [CustomEditor(typeof(GridBrush))]
    public class GridBrushEditor : GridBrushEditorBase
    {
        private static class Styles
        {
            public static readonly GUIContent tileLabel = EditorGUIUtility.TrTextContent("Tile", "Tile set in tilemap");
            public static readonly GUIContent spriteLabel = EditorGUIUtility.TrTextContent("Sprite", "Sprite set when tile is set in tilemap");
            public static readonly GUIContent colorLabel = EditorGUIUtility.TrTextContent("Color", "Color set when tile is set in tilemap");
            public static readonly GUIContent colliderTypeLabel = EditorGUIUtility.TrTextContent("Collider Type", "Collider shape used for tile");
            public static readonly GUIContent gameObjectToInstantiateLabel = EditorGUIUtility.TrTextContent("GameObject to Instantiate", "GameObject to instantiate for tile");
            public static readonly GUIContent lockColorLabel = EditorGUIUtility.TrTextContent("Lock Color", "Prevents tilemap from changing color of tile");
            public static readonly GUIContent lockTransformLabel = EditorGUIUtility.TrTextContent("Lock Transform", "Prevents tilemap from changing transform of tile");
            public static readonly GUIContent gridSelectionPropertiesLabel = EditorGUIUtility.TrTextContent("Grid Selection Properties");
            public static readonly GUIContent modifyTilemapLabel = EditorGUIUtility.TrTextContent("Modify Tilemap");
            public static readonly GUIContent modifyLabel = EditorGUIUtility.TrTextContent("Modify");
            public static readonly GUIContent deleteSelectionLabel = EditorGUIUtility.TrTextContent("Delete Selection");
            public static readonly GUIContent offsetLabel = EditorGUIUtility.TrTextContent("Offset");
            public static readonly GUIContent rotationLabel = EditorGUIUtility.TrTextContent("Rotation");
            public static readonly GUIContent scaleLabel = EditorGUIUtility.TrTextContent("Scale");

            public static readonly GUIContent noTool =
                EditorGUIUtility.TrTextContentWithIcon("None", "No Gizmo in the Scene view", "RectTool");
            public static readonly GUIContent moveTool =
                EditorGUIUtility.TrTextContentWithIcon("Move", "Shows a Gizmo in the Scene view for changing the offset for the Grid Selection", "MoveTool");
            public static readonly GUIContent rotateTool =
                EditorGUIUtility.TrTextContentWithIcon("Rotate", "Shows a Gizmo in the Scene view for changing the rotation for the Grid Selection", "RotateTool");
            public static readonly GUIContent scaleTool =
                EditorGUIUtility.TrTextContentWithIcon("Scale", "Shows a Gizmo in the Scene view for changing the scale for the Grid Selection", "ScaleTool");
            public static readonly GUIContent transformTool =
                EditorGUIUtility.TrTextContentWithIcon("Transform", "Shows a Gizmo in the Scene view for changing the transform for the Grid Selection", "TransformTool");

            public static readonly GUIContent[] selectionTools = new[]
            {
                noTool
                , moveTool
                , rotateTool
                , scaleTool
                , transformTool
            };

            public static readonly Type[] selectionTypes = new[]
            {
                typeof(SelectTool)
                    , typeof(GridSelectionMoveTool)
                    , typeof(GridSelectionRotateTool)
                    , typeof(GridSelectionScaleTool)
                    , typeof(GridSelectionTransformTool)
            };

            public static readonly string tooltipText = L10n.Tr("Use this brush to paint and erase Tiles from a Tilemap.");
            public static readonly string iconPath = "Packages/com.unity.2d.tilemap/Editor/Icons/Tilemap.DefaultBrush.png";
        }

        /// <summary>
        /// Identifiers for operations modifying the Tilemap.
        /// </summary>
        public enum ModifyCells
        {
            /// <summary>
            /// Inserts a row at the target position.
            /// </summary>
            InsertRow,
            /// <summary>
            /// Inserts a column at the target position.
            /// </summary>
            InsertColumn,
            /// <summary>
            /// Inserts a row before the target position.
            /// </summary>
            InsertRowBefore,
            /// <summary>
            /// Inserts a column before the target position.
            /// </summary>
            InsertColumnBefore,
            /// <summary>
            /// Delete a row at the target position.
            /// </summary>
            DeleteRow,
            /// <summary>
            /// Delete a column at the target position.
            /// </summary>
            DeleteColumn,
            /// <summary>
            /// Delete a row before the target position.
            /// </summary>
            DeleteRowBefore,
            /// <summary>
            /// Delete a column before the target position.
            /// </summary>
            DeleteColumnBefore,
        }

        private class GridBrushProperties
        {
            public static readonly string floodFillPreviewEditorPref = "GridBrush.EnableFloodFillPreview";
            public static readonly string floodFillPreviewFillExtentsEditorPref = "GridBrush.FloodFillPreviewFillExtents";
            public static readonly string floodFillPreviewEraseExtentsEditorPref = "GridBrush.FloodFillPreviewEraseExtents";
        }

        internal static bool showFloodFillPreview
        {
            get => EditorPrefs.GetBool(GridBrushProperties.floodFillPreviewEditorPref, true);
            set => EditorPrefs.SetBool(GridBrushProperties.floodFillPreviewEditorPref, value);
        }

        internal static int floodFillPreviewFillExtents
        {
            get => Math.Max(0, EditorPrefs.GetInt(GridBrushProperties.floodFillPreviewFillExtentsEditorPref, 0));
            set => EditorPrefs.SetInt(GridBrushProperties.floodFillPreviewFillExtentsEditorPref, Math.Max(0, value));
        }

        internal static int floodFillPreviewEraseExtents
        {
            get => Math.Max(0, EditorPrefs.GetInt(GridBrushProperties.floodFillPreviewEraseExtentsEditorPref, 0));
            set => EditorPrefs.SetInt(GridBrushProperties.floodFillPreviewEraseExtentsEditorPref, Math.Max(0, value));
        }

        /// <summary>The GridBrush that is the target for this editor.</summary>
        public GridBrush brush { get { return target as GridBrush; } }
        private int m_LastPreviewRefreshHash;

        // These are used to clean out previews that happened on previous update
        private GridLayout m_LastGrid;
        private GameObject m_LastBrushTarget;
        private BoundsInt? m_LastBounds;
        private GridBrushBase.Tool? m_LastTool;

        // These are used to handle selection in Selection Inspector
        private TileBase[] m_SelectionTiles;
        private Color[] m_SelectionColors;
        // internal for tests
        internal Vector3[] m_SelectionPositions;
        internal Vector3[] m_SelectionEulerAngles;
        internal Vector3[] m_SelectionScales;
        private TileFlags[] m_SelectionFlagsArray;
        private Sprite[] m_SelectionSprites;
        private Tile.ColliderType[] m_SelectionColliderTypes;
        private GameObject[] m_SelectionGameObjectToInstantiate;
        private BoundsInt m_LastSelectionBounds;
        private int selectionCellCount => Math.Abs(GridSelection.position.size.x * GridSelection.position.size.y * GridSelection.position.size.z);

        // These are used to handle insert/delete cells on the Tilemap
        private int m_CellCount = 1;
        private ModifyCells m_ModifyCells = ModifyCells.InsertRow;

        private Texture2D m_Icon;

        private static GridSelectionTool[] s_GridSelectionTools;
        private static Tile s_EmptySpriteTile;

        /// <summary>
        /// Initializes the GridBrushEditor.
        /// </summary>
        protected virtual void OnEnable()
        {
            Undo.undoRedoPerformed += ClearLastPreview;

            if (s_GridSelectionTools == null || s_GridSelectionTools[0] == null)
            {
                s_GridSelectionTools = new GridSelectionTool[]
                {
                    CreateInstance<GridSelectionMoveTool>(),
                    CreateInstance<GridSelectionRotateTool>(),
                    CreateInstance<GridSelectionScaleTool>(),
                    CreateInstance<GridSelectionTransformTool>()
                };
            }

            if (s_EmptySpriteTile == null)
            {
                s_EmptySpriteTile = ScriptableObject.CreateInstance<Tile>();
                s_EmptySpriteTile.sprite = null;
            }
        }

        /// <summary>
        /// Deinitialises the GridBrushEditor.
        /// </summary>
        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= ClearLastPreview;
            ClearLastPreview();
        }

        private void ClearLastPreview()
        {
            ClearPreview();
            m_LastPreviewRefreshHash = 0;
        }

        /// <summary>Callback for painting the GUI for the GridBrush in the Scene View.</summary>
        /// <param name="gridLayout">Grid that the brush is being used on.</param>
        /// <param name="brushTarget">Target of the GridBrushBase::ref::Tool operation. By default the currently selected GameObject.</param>
        /// <param name="position">Current selected location of the brush.</param>
        /// <param name="tool">Current GridBrushBase::ref::Tool selected.</param>
        /// <param name="executing">Whether brush is being used.</param>
        public override void OnPaintSceneGUI(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, bool executing)
        {
            BoundsInt gizmoRect = position;
            bool refreshPreviews = false;
            if (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint)
            {
                int newPreviewRefreshHash = GetHash(gridLayout, brushTarget, position, tool, brush);
                refreshPreviews = newPreviewRefreshHash != m_LastPreviewRefreshHash;
                if (refreshPreviews)
                    m_LastPreviewRefreshHash = newPreviewRefreshHash;
            }
            if (tool == GridBrushBase.Tool.Move)
            {
                if (refreshPreviews && executing)
                {
                    ClearPreview();
                    PaintPreview(gridLayout, brushTarget, position.min);
                }
            }
            else if (tool == GridBrushBase.Tool.Paint || tool == GridBrushBase.Tool.Erase)
            {
                if (refreshPreviews)
                {
                    ClearPreview();
                    if (tool != GridBrushBase.Tool.Erase)
                    {
                        PaintPreview(gridLayout, brushTarget, position.min);
                    }
                    else
                    {
                        ErasePreview(gridLayout, brushTarget, position.min);
                    }
                }
                gizmoRect = new BoundsInt(position.min - brush.pivot, brush.size);
            }
            else if (tool == GridBrushBase.Tool.Box)
            {
                if (refreshPreviews)
                {
                    ClearPreview();
                    BoxFillPreview(gridLayout, brushTarget, position);
                }
            }
            else if (tool == GridBrushBase.Tool.FloodFill)
            {
                if (refreshPreviews)
                {
                    if (CheckFloodFillPreview(gridLayout, brushTarget, position.min))
                        ClearPreview();
                    FloodFillPreview(gridLayout, brushTarget, position.min);
                }
            }

            base.OnPaintSceneGUI(gridLayout, brushTarget, gizmoRect, tool, executing);
        }

        // True while the user is scrubbing or typing into an Inspector field.
        private static bool isEditingInInspector => GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField;

        internal void UpdateSelection(Tilemap tilemap)
        {
            var selection = GridSelection.position;
            var cellCount = selectionCellCount;
            bool reallocated = m_SelectionTiles == null || m_SelectionTiles.Length != cellCount;
            if (reallocated)
            {
                m_SelectionTiles = new TileBase[cellCount];
                m_SelectionColors = new Color[cellCount];
                m_SelectionPositions = new Vector3[cellCount];
                m_SelectionEulerAngles = new Vector3[cellCount];
                m_SelectionScales = new Vector3[cellCount];
                m_SelectionFlagsArray = new TileFlags[cellCount];
                m_SelectionSprites = new Sprite[cellCount];
                m_SelectionColliderTypes = new Tile.ColliderType[cellCount];
                m_SelectionGameObjectToInstantiate = new GameObject[cellCount];
            }

            bool selectionMoved = m_LastSelectionBounds != selection;
            m_LastSelectionBounds = selection;
            bool refreshTransforms = reallocated || selectionMoved || !isEditingInInspector;

            int index = 0;
            foreach (var p in selection.allPositionsWithin)
            {
                m_SelectionTiles[index] = tilemap.GetTile(p);
                m_SelectionColors[index] = tilemap.GetColor(p);
                m_SelectionFlagsArray[index] = tilemap.GetTileFlags(p);
                m_SelectionSprites[index] = tilemap.GetSprite(p);
                m_SelectionColliderTypes[index] = tilemap.GetColliderType(p);
                m_SelectionGameObjectToInstantiate[index] = tilemap.GetObjectToInstantiate(p);
                if (refreshTransforms)
                {
                    var matrix = tilemap.GetTransformMatrix(p);
                    m_SelectionPositions[index] = matrix.GetPosition();
                    m_SelectionEulerAngles[index] = matrix.rotation.eulerAngles;
                    m_SelectionScales[index] = matrix.lossyScale;
                }
                index++;
            }
        }

        internal void SetSelectionTransform(Tilemap tilemap, BoundsInt selection, int cellCount, Vector3 position, Vector3 eulerAngles, Vector3 scale)
        {
            if (!(float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z)
                && float.IsFinite(eulerAngles.x) && float.IsFinite(eulerAngles.y) && float.IsFinite(eulerAngles.z)
                && float.IsFinite(scale.x) && float.IsFinite(scale.y) && float.IsFinite(scale.z)))
                return;

            Undo.RecordObject(tilemap, "Edit Tilemap");
            var newTransformMatrix = Matrix4x4.TRS(position, Quaternion.Euler(eulerAngles), scale);
            for (int i = 0; i < cellCount; ++i)
            {
                m_SelectionPositions[i] = position;
                m_SelectionEulerAngles[i] = eulerAngles;
                m_SelectionScales[i] = scale;
            }
            foreach (var p in selection.allPositionsWithin)
                tilemap.SetTransformMatrix(p, newTransformMatrix);
        }

        /// <summary>Callback for drawing the Inspector GUI when there is an active GridSelection made in a Tilemap.</summary>
        public override void OnSelectionInspectorGUI()
        {
            var selection = GridSelection.position;
            var tilemap = GridSelection.target.GetComponent<Tilemap>();

            int cellCount = selectionCellCount;
            if (tilemap != null && cellCount > 0)
            {
                var canEditTilemap = !GridPaintingState.IsPartOfActivePalette(tilemap.gameObject) ||
                                     GridPaintingState.isPaletteEditable;

                base.OnSelectionInspectorGUI();

                if (canEditTilemap
                    && !EditorGUIUtility.editingTextField
                    && Event.current.type == EventType.KeyDown
                    && (Event.current.keyCode == KeyCode.Delete
                        || Event.current.keyCode == KeyCode.Backspace))
                {
                    GUI.changed = true;
                    DeleteSelection(tilemap, selection);
                    Event.current.Use();
                }

                GUILayout.Space(10f);

                EditorGUILayout.LabelField(Styles.gridSelectionPropertiesLabel, EditorStyles.boldLabel);

                UpdateSelection(tilemap);

                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = HasMixedValues(m_SelectionTiles);
                var position = new Vector3Int(selection.xMin, selection.yMin, selection.zMin);
                TileBase newTile = EditorGUILayout.ObjectField(Styles.tileLabel, tilemap.GetTile(position), typeof(TileBase), false) as TileBase;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tilemap, "Edit Tilemap");
                    foreach (var p in selection.allPositionsWithin)
                        tilemap.SetTile(p, newTile);
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.showMixedValue = HasMixedValues(m_SelectionSprites);
                    EditorGUILayout.ObjectField(Styles.spriteLabel, m_SelectionSprites[0], typeof(Sprite), false, GUILayout.Height(EditorGUI.kSingleLineHeight));
                }

                bool colorFlagsAllEqual = AllFlagsEqual(m_SelectionFlagsArray, TileFlags.LockColor);
                using (new EditorGUI.DisabledScope(!colorFlagsAllEqual || (m_SelectionFlagsArray[0] & TileFlags.LockColor) != 0))
                {
                    EditorGUI.showMixedValue = HasMixedValues(m_SelectionColors);
                    EditorGUI.BeginChangeCheck();
                    Color newColor = EditorGUILayout.ColorField(Styles.colorLabel, m_SelectionColors[0]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(tilemap, "Edit Tilemap");
                        foreach (var p in selection.allPositionsWithin)
                            tilemap.SetColor(p, newColor);
                    }
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.showMixedValue = HasMixedValues(m_SelectionColliderTypes);
                    EditorGUILayout.EnumPopup(Styles.colliderTypeLabel, m_SelectionColliderTypes[0]);
                    EditorGUI.showMixedValue = HasMixedValues(m_SelectionGameObjectToInstantiate);
                    EditorGUILayout.ObjectField(Styles.gameObjectToInstantiateLabel, m_SelectionGameObjectToInstantiate[0], typeof(GameObject), false);
                }

                bool transformFlagsAllEqual = AllFlagsEqual(m_SelectionFlagsArray, TileFlags.LockTransform);
                using (new EditorGUI.DisabledScope(!transformFlagsAllEqual || (m_SelectionFlagsArray[0] & TileFlags.LockTransform) != 0))
                {
                    EditorGUI.showMixedValue = HasMixedValues(m_SelectionPositions)
                        || HasMixedValues(m_SelectionEulerAngles)
                        || HasMixedValues(m_SelectionScales);
                    // Edit the base components directly. Keeping the raw scale Vector3 and Euler
                    // angles avoids the Matrix4x4.lossyScale / Quaternion.eulerAngles round-trips,
                    // which would flicker negative scales and rotations past 90 degrees while dragging.
                    var newPosition = m_SelectionPositions[0];
                    var newEulerAngles = m_SelectionEulerAngles[0];
                    var newScale = m_SelectionScales[0];
                    if (TransformMatrixOnGUI(ref newPosition, ref newEulerAngles, ref newScale))
                        SetSelectionTransform(tilemap, selection, cellCount, newPosition, newEulerAngles, newScale);
                }

                var lockedTransform = (m_SelectionFlagsArray[0] & TileFlags.LockTransform) != 0;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.showMixedValue = !colorFlagsAllEqual;
                    EditorGUILayout.Toggle(Styles.lockColorLabel, (m_SelectionFlagsArray[0] & TileFlags.LockColor) != 0);
                    EditorGUI.showMixedValue = !transformFlagsAllEqual;
                    EditorGUILayout.Toggle(Styles.lockTransformLabel, lockedTransform);
                }

                EditorGUI.showMixedValue = false;

                if (GUILayout.Button(Styles.deleteSelectionLabel))
                {
                    DeleteSelection(tilemap, selection);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(Styles.modifyTilemapLabel, EditorStyles.boldLabel);
                EditorGUILayout.Space();

                var active = -1;
                for (var i = 0; i < Styles.selectionTypes.Length; ++i)
                {
                    if (ToolManager.activeToolType == Styles.selectionTypes[i])
                    {
                        active = i;
                        break;
                    }
                }

                using (new EditorGUI.DisabledScope(lockedTransform))
                {
                    EditorGUI.BeginChangeCheck();
                    var selected  = GUILayout.Toolbar(active, Styles.selectionTools);
                    if (EditorGUI.EndChangeCheck() && selected != -1)
                    {
                        ToolManager.SetActiveTool(Styles.selectionTypes[selected]);
                    }
                }

                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                m_ModifyCells = (ModifyCells)EditorGUILayout.EnumPopup(m_ModifyCells);
                m_CellCount = EditorGUILayout.IntField(m_CellCount);
                if (GUILayout.Button(Styles.modifyLabel))
                {
                    RegisterUndoForTilemap(tilemap, Enum.GetName(typeof(ModifyCells), m_ModifyCells));
                    switch (m_ModifyCells)
                    {
                        case ModifyCells.InsertRow:
                        {
                            tilemap.InsertCells(GridSelection.position.position, 0, m_CellCount, 0);
                            break;
                        }
                        case ModifyCells.InsertRowBefore:
                        {
                            tilemap.InsertCells(GridSelection.position.position, 0, -m_CellCount, 0);
                            break;
                        }
                        case ModifyCells.InsertColumn:
                        {
                            tilemap.InsertCells(GridSelection.position.position, m_CellCount, 0, 0);
                            break;
                        }
                        case ModifyCells.InsertColumnBefore:
                        {
                            tilemap.InsertCells(GridSelection.position.position, -m_CellCount, 0, 0);
                            break;
                        }
                        case ModifyCells.DeleteRow:
                        {
                            tilemap.DeleteCells(GridSelection.position.position, 0, m_CellCount, 0);
                            break;
                        }
                        case ModifyCells.DeleteRowBefore:
                        {
                            tilemap.DeleteCells(GridSelection.position.position,  0, -m_CellCount, 0);
                            break;
                        }
                        case ModifyCells.DeleteColumn:
                        {
                            tilemap.DeleteCells(GridSelection.position.position, m_CellCount, 0, 0);
                            break;
                        }
                        case ModifyCells.DeleteColumnBefore:
                        {
                            tilemap.DeleteCells(GridSelection.position.position, -m_CellCount, 0, 0);
                            break;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        // Whether any element differs from the first, used to drive EditorGUI.showMixedValue.
        // Overloads use each type's own != operator to match the original comparisons.
        private static bool HasMixedValues(Object[] values)
        {
            for (int i = 1; i < values.Length; ++i)
                if (values[i] != values[0])
                    return true;
            return false;
        }

        private static bool HasMixedValues(Color[] values)
        {
            for (int i = 1; i < values.Length; ++i)
                if (values[i] != values[0])
                    return true;
            return false;
        }

        private static bool HasMixedValues(Vector3[] values)
        {
            for (int i = 1; i < values.Length; ++i)
                if (values[i] != values[0])
                    return true;
            return false;
        }

        private static bool HasMixedValues(Tile.ColliderType[] values)
        {
            for (int i = 1; i < values.Length; ++i)
                if (values[i] != values[0])
                    return true;
            return false;
        }

        private static bool AllFlagsEqual(TileFlags[] flags, TileFlags mask)
        {
            var first = flags[0] & mask;
            for (int i = 1; i < flags.Length; ++i)
                if ((flags[i] & mask) != first)
                    return false;
            return true;
        }

        // Edits offset/rotation/scale as base components rather than a Matrix4x4, so the raw
        // scale (including negative axes) and Euler angles survive instead of being normalized
        // by lossyScale / Quaternion.eulerAngles. Updates the components in place and returns
        // whether they changed.
        private static bool TransformMatrixOnGUI(ref Vector3 position, ref Vector3 eulerAngles, ref Vector3 scale)
        {
            EditorGUI.BeginChangeCheck();

            Vector3 pos = Round(position, 3);
            Vector3 euler = Round(eulerAngles, 3);
            Vector3 scl = Round(scale, 3);
            pos = EditorGUILayout.Vector3Field(Styles.offsetLabel, pos);
            euler = EditorGUILayout.Vector3Field(Styles.rotationLabel, euler);
            scl = EditorGUILayout.Vector3Field(Styles.scaleLabel, scl);

            if (EditorGUI.EndChangeCheck() && scl.x != 0f && scl.y != 0f && scl.z != 0f)
            {
                position = pos;
                eulerAngles = euler;
                scale = scl;
                return true;
            }
            return false;
        }

        private static Vector3 Round(Vector3 value, int digits)
        {
            float mult = Mathf.Pow(10.0f, (float)digits);
            return new Vector3(
                Mathf.Round(value.x * mult) / mult,
                Mathf.Round(value.y * mult) / mult,
                Mathf.Round(value.z * mult) / mult
            );
        }

        private void DeleteSelection(Tilemap tilemap, BoundsInt selection)
        {
            if (tilemap == null)
                return;

            RegisterUndo(tilemap.gameObject, GridBrushBase.Tool.Erase);
            brush.BoxErase(tilemap.layoutGrid, tilemap.gameObject, selection);
        }

        /// <summary> Callback when the mouse cursor leaves and editing area. </summary>
        /// <remarks> Cleans up brush previews. </remarks>
        public override void OnMouseLeave()
        {
            ClearLastPreview();
        }

        /// <summary> Callback when the GridBrush Tool is deactivated. </summary>
        /// <param name="tool">GridBrush Tool that is deactivated.</param>
        /// <remarks> Cleans up brush previews. </remarks>
        public override void OnToolDeactivated(GridBrushBase.Tool tool)
        {
            ClearLastPreview();
        }

        /// <summary> Describes the usage of the GridBrush. </summary>
        public override string tooltip
        {
            get { return Styles.tooltipText; }
        }

        /// <summary> Returns an icon identifying the Grid Brush. </summary>
        public override Texture2D icon
        {
            get
            {
                if (m_Icon == null)
                {
                    m_Icon = EditorGUIUtility.LoadIcon(Styles.iconPath);
                }
                return m_Icon;
            }
        }

        /// <summary> Whether the GridBrush can change Z Position. </summary>
        public override bool canChangeZPosition
        {
            get { return brush.canChangeZPosition; }
            set { brush.canChangeZPosition = value; }
        }

        /// <summary>
        /// Whether the Brush is in a state that should be saved for selection.
        /// </summary>
        public override bool shouldSaveBrushForSelection
        {
            get
            {
                if (brush.cells != null)
                {
                    foreach (var cell in brush.cells)
                    {
                        if (cell != null && cell.tile != null)
                            return true;
                    }
                }
                return false;
            }
        }
        /// <summary>Callback for registering an Undo action before the GridBrushBase does the current GridBrushBase::ref::Tool action.</summary>
        /// <param name="brushTarget">Target of the GridBrushBase::ref::Tool operation. By default the currently selected GameObject.</param>
        /// <param name="tool">Current GridBrushBase::ref::Tool selected.</param>
        /// <remarks>Implement this for any special Undo behaviours when a brush is used.</remarks>
        public override void RegisterUndo(GameObject brushTarget, GridBrushBase.Tool tool)
        {
            if (brushTarget != null)
            {
                var tilemap = brushTarget.GetComponent<Tilemap>();
                if (tilemap != null)
                {
                    RegisterUndoForTilemap(tilemap, tool.ToString());
                }
            }
        }

        /// <summary>Returns all valid targets that the brush can edit.</summary>
        /// <remarks>Valid targets for the GridBrush are any GameObjects with a Tilemap component.</remarks>
        public override GameObject[] validTargets
        {
            get
            {
                StageHandle currentStageHandle = StageUtility.GetCurrentStageHandle();
                var tilemaps = currentStageHandle.FindComponentsOfType<Tilemap>();
                var targets = new List<GameObject>(tilemaps.Length);
                foreach (var tilemap in tilemaps)
                {
                    var gameObject = tilemap.gameObject;
                    if (gameObject.scene.isLoaded
                        && gameObject.activeInHierarchy
                        && !gameObject.hideFlags.HasFlag(HideFlags.NotEditable))
                        targets.Add(gameObject);
                }
                return targets.ToArray();
            }
        }

        /// <summary>Paints preview data into a cell of a grid given the coordinates of the cell.</summary>
        /// <param name="gridLayout">The grid to paint data to.</param>
        /// <param name="brushTarget">The target of the paint operation. This is the currently selected GameObject by default.</param>
        /// <param name="position">The coordinates of the cell to paint data to.</param>
        /// <remarks>The GridBrush will paint preview Sprites in its brush cells onto an associated Tilemap. This will not instantiate objects associated with the painted Tiles.</remarks>
        public virtual void PaintPreview(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
        {
            var min = position - brush.pivot;
            var max = min + brush.size;
            var bounds = new BoundsInt(min, max - min);

            if (brushTarget != null)
            {
                var map = brushTarget.GetComponent<Tilemap>();
                if (map != null)
                {
                    foreach (var location in bounds.allPositionsWithin)
                    {
                        var brushPosition = location - min;
                        var cell = brush.cells[brush.GetCellIndex(brushPosition)];
                        if (cell.tile != null)
                        {
                            SetTilemapPreviewCell(map, location, cell.tile, cell.matrix, cell.color);
                        }
                    }
                }
            }

            m_LastGrid = gridLayout;
            m_LastBounds = bounds;
            m_LastBrushTarget = brushTarget;
            m_LastTool = GridBrushBase.Tool.Paint;
        }

        /// <summary>Displays a preview of the Tile (after erasure) on the cell of a grid at the given coordinates of the cell.</summary>
        /// <param name="gridLayout">The grid to paint data to.</param>
        /// <param name="brushTarget">The target of the erase operation. This is the currently selected GameObject by default.</param>
        /// <param name="position">The coordinates of the cell to paint data to.</param>
        /// <remarks>The GridBrush will paint preview Sprites of the Tiles (after erasure) into its brush cells on an associated Tilemap. This will not instantiate objects associated with the painted Tiles.</remarks>
        public virtual void ErasePreview(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
        {
            var min = position - brush.pivot;
            var max = min + brush.size;
            var bounds = new BoundsInt(min, max - min);

            if (brushTarget != null)
            {
                var map = brushTarget.GetComponent<Tilemap>();
                if (map != null)
                {
                    foreach (var location in bounds.allPositionsWithin)
                    {
                        var brushPosition = location - min;
                        var cell = brush.cells[brush.GetCellIndex(brushPosition)];
                        SetTilemapPreviewCell(map, location, s_EmptySpriteTile, cell.matrix, cell.color);
                    }
                }
            }

            m_LastGrid = gridLayout;
            m_LastBounds = bounds;
            m_LastBrushTarget = brushTarget;
            m_LastTool = GridBrushBase.Tool.Erase;
        }

        /// <summary>Does a preview of what happens when a GridBrush.BoxFill is done with the same parameters.</summary>
        /// <param name="gridLayout">Grid to box fill data to.</param>
        /// <param name="brushTarget">Target of box fill operation. By default the currently selected GameObject.</param>
        /// <param name="position">The bounds to box fill data to.</param>
        public virtual void BoxFillPreview(GridLayout gridLayout, GameObject brushTarget, BoundsInt position)
        {
            if (brushTarget != null)
            {
                var map = brushTarget.GetComponent<Tilemap>();
                if (map != null)
                {
                    foreach (var location in position.allPositionsWithin)
                    {
                        var local = location - position.min;
                        var cell = brush.cells[brush.GetCellIndexWrapAround(local.x, local.y, local.z)];
                        if (cell.tile != null)
                        {
                            SetTilemapPreviewCell(map, location, cell.tile, cell.matrix, cell.color);
                        }
                    }
                }
            }

            m_LastGrid = gridLayout;
            m_LastBounds = position;
            m_LastBrushTarget = brushTarget;
            m_LastTool = GridBrushBase.Tool.Box;
        }

        private bool CheckFloodFillPreview(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
        {
            if (m_LastGrid == gridLayout
                && m_LastBrushTarget == brushTarget
                && m_LastBounds.HasValue && m_LastBounds.Value.Contains(position)
                && brushTarget != null && brush.cellCount > 0)
            {
                var map = brushTarget.GetComponent<Tilemap>();
                if (map != null)
                {
                    var cell = brush.cells[0];
                    var hasTile = cell.tile != null;
                    if ((hasTile && floodFillPreviewFillExtents == 0 || !hasTile && floodFillPreviewEraseExtents == 0)
                        && (cell.tile == map.GetEditorPreviewTile(position)))
                        return false;
                }
            }
            return true;
        }

        /// <summary>Does a preview of what happens when a GridBrush.FloodFill is done with the same parameters.</summary>
        /// <param name="gridLayout">Grid to paint data to.</param>
        /// <param name="brushTarget">Target of the flood fill operation. By default the currently selected GameObject.</param>
        /// <param name="position">The coordinates of the cell to flood fill data to.</param>
        public virtual void FloodFillPreview(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
        {
            // This can be quite taxing on a large Tilemap, so users can choose whether to do this or not
            if (!showFloodFillPreview)
                return;

            var bounds = new BoundsInt(position, Vector3Int.one);
            if (brushTarget != null && brush.cellCount > 0)
            {
                var map = brushTarget.GetComponent<Tilemap>();
                if (map != null)
                {
                    var cell = brush.cells[0];
                    var eraseExtents = floodFillPreviewEraseExtents;
                    var fillExtents = floodFillPreviewFillExtents;
                    var validFloodFillTile = cell.tile != null;
                    var floodFillTile = validFloodFillTile ? cell.tile : s_EmptySpriteTile;
                    if (!validFloodFillTile && floodFillPreviewEraseExtents > 0)
                    {
                        map.EditorPreviewBoxFill(position, floodFillTile, position.x - eraseExtents, position.y - eraseExtents, position.x + eraseExtents, position.y + eraseExtents);
                    }
                    else if (validFloodFillTile && floodFillPreviewFillExtents > 0)
                    {
                        map.EditorPreviewBoxFill(position, floodFillTile, position.x - fillExtents, position.y - fillExtents, position.x + fillExtents, position.y + fillExtents);
                    }
                    else
                    {
                        map.EditorPreviewFloodFill(position, floodFillTile);
                    }

                    // Set floodfill bounds as tilemap bounds
                    var origin = map.origin;
                    bounds.min = origin;
                    bounds.max = origin + map.size;
                }
            }

            m_LastGrid = gridLayout;
            m_LastBounds = bounds;
            m_LastBrushTarget = brushTarget;
            m_LastTool = GridBrushBase.Tool.FloodFill;
        }

        /// <summary>Callback for painting custom gizmos when there is an active GridSelection made in a GridLayout.</summary>
        /// <param name="gridLayout">Grid that the brush is being used on.</param>
        /// <param name="brushTarget">Target of the GridBrushBase::ref::Tool operation. By default the currently selected GameObject.</param>
        /// <remarks>Override this to show custom gizmos for the current selection.</remarks>
        public override void OnSelectionSceneGUI(GridLayout gridLayout, GameObject brushTarget)
        {
            base.OnSelectionSceneGUI(gridLayout, brushTarget);

            var canEditTilemap = !GridPaintingState.IsPartOfActivePalette(brushTarget) ||
                                 GridPaintingState.isPaletteEditable;

            if (canEditTilemap
                && GridSelection.active
                && !EditorGUIUtility.editingTextField
                && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Delete
                    || Event.current.keyCode == KeyCode.Backspace))
            {
                var tilemap = gridLayout.GetComponentInChildren<Tilemap>();
                DeleteSelection(tilemap, GridSelection.position);
                if (GridPaintingState.IsPartOfActivePalette(brushTarget))
                {
                    GridPaintingState.UnlockGridPaintPaletteClipboardForEditing();
                }
                else
                {
                    GridSelection.SaveStandalone();
                }
                Event.current.Use();
            }
        }

        /// <summary>Clears any preview drawn previously by the GridBrushEditor.</summary>
        public virtual void ClearPreview()
        {
            if (m_LastGrid == null || m_LastBounds == null || m_LastBrushTarget == null || m_LastTool == null)
                return;

            Tilemap map = m_LastBrushTarget.GetComponent<Tilemap>();
            if (map != null)
            {
                switch (m_LastTool)
                {
                    case GridBrushBase.Tool.FloodFill:
                    {
                        map.ClearAllEditorPreviewTiles();
                        break;
                    }
                    case GridBrushBase.Tool.Box:
                    {
                        Vector3Int min = m_LastBounds.Value.position;
                        Vector3Int max = min + m_LastBounds.Value.size;
                        BoundsInt bounds = new BoundsInt(min, max - min);
                        foreach (Vector3Int location in bounds.allPositionsWithin)
                        {
                            ClearTilemapPreview(map, location);
                        }
                        break;
                    }
                    case GridBrushBase.Tool.Erase:
                    case GridBrushBase.Tool.Paint:
                    {
                        BoundsInt bounds = m_LastBounds.Value;
                        foreach (Vector3Int location in bounds.allPositionsWithin)
                        {
                            ClearTilemapPreview(map, location);
                        }
                        break;
                    }
                }
            }

            m_LastBrushTarget = null;
            m_LastGrid = null;
            m_LastBounds = null;
            m_LastTool = null;
        }

        /// <summary>
        /// Creates a static preview of the GridBrush with its current selection.
        /// </summary>
        /// <param name="assetPath">The asset to operate on.</param>
        /// <param name="subAssets">An array of all Assets at assetPath.</param>
        /// <param name="width">Width of the created texture.</param>
        /// <param name="height">Height of the created texture.</param>
        /// <returns>Generated texture or null.</returns>
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            if (brush == null)
                return null;

            var previewInstance = new GameObject("Brush Preview", typeof(Grid), typeof(Tilemap), typeof(TilemapRenderer));
            var previewGrid = previewInstance.GetComponent<Grid>();
            previewGrid.cellLayout = brush.lastPickedCellLayout;
            previewGrid.cellSize = brush.lastPickedCellSize;
            if (previewGrid.cellLayout != GridLayout.CellLayout.Hexagon)
            {
                previewGrid.cellGap = brush.lastPickedCellGap;
            }
            else
            {
                var tilemap = previewInstance.GetComponent<Tilemap>();
                tilemap.tileAnchor = Vector3.zero;
            }
            previewGrid.cellSwizzle = brush.lastPickedCellSwizzle;

            brush.Paint(previewGrid, previewInstance, Vector3Int.zero);

            var bounds = previewGrid.GetBoundsLocal(Vector3.zero, brush.size);
            var pivotLocal = previewGrid.CellToLocal(brush.pivot);
            var center = bounds.center - pivotLocal;
            center.z -= 10f;

            var rect = new Rect(0, 0, width, height);
            var previewUtility = new PreviewRenderUtility(true, true);
            previewUtility.camera.orthographic = true;
            previewUtility.camera.orthographicSize = 0.5f * Math.Max(brush.size.x, brush.size.y);
            if (rect.height > rect.width)
                previewUtility.camera.orthographicSize *= rect.height / rect.width;
            previewUtility.camera.transform.position = center;
            previewUtility.AddSingleGO(previewInstance);
            previewUtility.BeginStaticPreview(rect);
            previewUtility.camera.Render();
            var tex = previewUtility.EndStaticPreview();
            previewUtility.Cleanup();

            DestroyImmediate(previewInstance);

            return tex;
        }

        private void RegisterUndoForTilemap(Tilemap tilemap, string undoMessage)
        {
            Undo.RegisterCompleteObjectUndo(new Object[] { tilemap, tilemap.gameObject }, undoMessage);
        }

        private static void SetTilemapPreviewCell(Tilemap map, Vector3Int location, TileBase tile, Matrix4x4 transformMatrix, Color color)
        {
            if (map == null)
                return;
            map.SetEditorPreviewTile(location, tile);
            map.SetEditorPreviewTransformMatrix(location, transformMatrix);
            map.SetEditorPreviewColor(location, color);
        }

        private static void ClearTilemapPreview(Tilemap map, Vector3Int location)
        {
            if (map == null)
                return;
            map.SetEditorPreviewTile(location, null);
            map.SetEditorPreviewTransformMatrix(location, Matrix4x4.identity);
            map.SetEditorPreviewColor(location, Color.white);
        }

        private static int GetHash(GridLayout gridLayout, GameObject brushTarget, BoundsInt position, GridBrushBase.Tool tool, GridBrush brush)
        {
            int hash;
            unchecked
            {
                hash = gridLayout != null ? gridLayout.GetHashCode() : 0;
                hash = hash * 33 + (brushTarget != null ? brushTarget.GetHashCode() : 0);
                hash = hash * 33 + position.GetHashCode();
                hash = hash * 33 + tool.GetHashCode();
                hash = hash * 33 + (brush != null ? brush.GetHashCode() : 0);
            }
            return hash;
        }
    }
}
