using System;
using System.Collections.Generic;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.Tilemaps
{
    /// <summary>
    /// Popup Field for selecting the Active Brush for Grid Painting.
    /// </summary>
    [UxmlElement]
    [EditorToolbarElement(k_ToolbarId)]
    public sealed partial class TilePaletteBrushesPopup : PopupField<GridBrushBase>
    {
        internal const string k_ToolbarId = "Tools/Tile Palette Brushes Popup";

        private static string k_NullGameObjectName = L10n.Tr("No Valid Brush");

        private static string k_LabelTooltip =
            L10n.Tr("Specifies the currently active Brush used for painting in the Scene View.");

        /// <summary>
        /// USS class name of elements of this type.
        /// </summary>
        private new static readonly string ussClassName = "unity-tilepalette-brushes-field";
        /// <summary>
        /// USS class name of labels in elements of this type.
        /// </summary>
        private new static readonly string labelUssClassName = ussClassName + "__label";
        /// <summary>
        /// USS class name of input elements in elements of this type.
        /// </summary>
        private new static readonly string inputUssClassName = ussClassName + "__input";

        private bool m_Active;

        /// <summary>
        /// Initializes and returns an instance of TilePaletteBrushesPopup.
        /// </summary>
        public TilePaletteBrushesPopup() : this(null) {}

        /// <summary>
        /// Initializes and returns an instance of TilePaletteBrushesPopup.
        /// </summary>
        /// <param name="label">Label name for the Popup</param>
        public TilePaletteBrushesPopup(string label)
            : base(label, new List<GridBrushBase>(GridPaintingState.brushes), GetBrushIndex())
        {
            AddToClassList(ussClassName);
            labelElement.AddToClassList(labelUssClassName);
            visualInput.AddToClassList(inputUssClassName);

            TilePaletteOverlayUtility.SetStyleSheet(this);
            labelElement.tooltip = k_LabelTooltip;

            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);

            m_FormatSelectedValueCallback += FormatSelectedValueCallback;
            createMenuCallback += CreateMenuCallback;

            SetValueWithoutNotify(GridPaintingState.gridBrush);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            m_Active = true;
            ShowMenu();
            m_Active = false;
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            GridPaintingState.brushChanged += OnBrushChanged;
            SetValueWithoutNotify(GridPaintingState.gridBrush);
        }

        private void OnBrushChanged(GridBrushBase obj)
        {
            if (obj == null)
                return;
            choices = new List<GridBrushBase>(GridPaintingState.brushes);
            UpdateBrush();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            GridPaintingState.brushChanged -= OnBrushChanged;
        }

        private string FormatSelectedValueCallback(GridBrushBase brush)
        {
            if (brush != null)
                return brush.name;
            return k_NullGameObjectName;
        }

        private AbstractGenericMenu CreateMenuCallback()
        {
            return new TilePaletteBrushesDropdownMenu(m_Active, Mathf.FloorToInt(resolvedStyle.width));
        }

        private static int GetBrushIndex()
        {
            return GridPaintingState.brushes.IndexOf(GridPaintingState.gridBrush);
        }

        private void UpdateBrush()
        {
            index = GetBrushIndex();
        }
    }

    [EditorToolbarElement(k_ToolbarId)]
    internal class TilePaletteBrushesPopupIcon : VisualElement
    {
        internal const string k_ToolbarId = "Tools/Tile Palette Brushes Icon";

        private static string kTooltip = L10n.Tr("Brushes");

        /// <summary>
        /// USS class name of elements of this type.
        /// </summary>
        public static readonly string ussClassName = "unity-tilepalette-brushes-icon";

        private readonly string k_IconPath = "Packages/com.unity.2d.tilemap/Editor/Icons/Tilemap.CustomBrush.png";

        private Texture2D m_DefaultIcon;

        public TilePaletteBrushesPopupIcon()
        {
            AddToClassList(ussClassName);
            TilePaletteOverlayUtility.SetStyleSheet(this);

            m_DefaultIcon = EditorGUIUtility.LoadIcon(k_IconPath);
            style.backgroundImage = m_DefaultIcon;
            tooltip = kTooltip;

            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            GridPaintingState.brushChanged += OnBrushChanged;
            OnBrushChanged(GridPaintingState.gridBrush);
        }

        private void OnBrushChanged(GridBrushBase obj)
        {
            if (obj == null)
            {
                style.backgroundImage = m_DefaultIcon;
                return;
            }

            var editor = Editor.CreateEditor(obj);
            if (editor is GridBrushEditorBase gridBrushEditor && gridBrushEditor.icon != null)
            {
                style.backgroundImage = gridBrushEditor.icon;
            }
            else
            {
                style.backgroundImage = m_DefaultIcon;
            }
            Object.DestroyImmediate(editor);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            GridPaintingState.brushChanged -= OnBrushChanged;
        }
    }

    [EditorToolbarElement(k_ToolbarId)]
    internal sealed class TilePaletteBrushesSyncToggle : EditorToolbarToggle
    {
        internal const string k_ToolbarId = "Tools/Tile Palette Brushes Sync";

        private static readonly string k_ToolSettingsClass = "unity-tool-settings";
        private static readonly string k_ElementClass = "unity-tilepalette-brushes-sync";

        private static readonly string k_TooltipText = L10n.Tr("Sync Active Brush");

        public Action<bool> ToggleChanged;

        public TilePaletteBrushesSyncToggle()
        {
            name = "Tile Palette Brushes Sync";
            AddToClassList(k_ToolSettingsClass);
            AddToClassList(k_ElementClass);
            TilePaletteOverlayUtility.SetStyleSheet(this);

            icon = EditorGUIUtility.LoadIcon("Linked");
            tooltip = k_TooltipText;

            UpdateToggleState();

            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            GridTargetBrushSync.instance.tilemapBrushTargetsChanged += UpdateToggleState;
            GridPaintingState.scenePaintTargetChanged += OnScenePaintTargetChanged;
            GridPaintingState.brushChanged += OnBrushChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            GridTargetBrushSync.instance.tilemapBrushTargetsChanged -= UpdateToggleState;
            GridPaintingState.scenePaintTargetChanged -= OnScenePaintTargetChanged;
            GridPaintingState.brushChanged -= OnBrushChanged;
        }

        private void OnScenePaintTargetChanged(GameObject target)
        {
            UpdateToggleState();
        }

        private void OnBrushChanged(GridBrushBase brush)
        {
            UpdateToggleState();
        }

        private void UpdateToggleState()
        {
            var sceneTarget = GridPaintingState.scenePaintTarget;
            var brush = GridPaintingState.gridBrush;
            var hasTarget = GridTargetBrushSync.instance.HasTilemapBrushTarget(sceneTarget, brush);
            SetValueWithoutNotify(hasTarget);
        }

        protected override void ToggleValue()
        {
            base.ToggleValue();

            var sceneTarget = GridPaintingState.scenePaintTarget;
            var brush = GridPaintingState.gridBrush;

            if (value)
                GridTargetBrushSync.instance.AddTilemapBrushTarget(sceneTarget, brush);
            else
                GridTargetBrushSync.instance.RemoveTilemapBrushTarget(sceneTarget, brush);

            UpdateToggleState();
            ToggleChanged?.Invoke(value);
        }
    }
}
