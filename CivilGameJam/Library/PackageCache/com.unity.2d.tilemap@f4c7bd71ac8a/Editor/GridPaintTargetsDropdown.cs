using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.Experimental;
using UnityEngine;

namespace UnityEditor.Tilemaps
{
    internal class GridPaintTargetsDropdown : PopupWindowContent
    {
        private class Styles
        {
            public class IconState
            {
                public GUIContent visible;
                public GUIContent hidden;
                public GUIContent ping;
            }

            public GUIStyle menuItem = "MenuItem";
            public GUIContent backIcon = EditorGUIUtility.TrIconContent("tab_next");

            public static readonly Color backgroundColor = EditorResources.GetStyle("game-object-tree-view-scene-visibility")
                .GetColor("background-color");

            public static readonly Color hoveredBackgroundColor = EditorResources.GetStyle("game-object-tree-view-scene-visibility")
                .GetColor("-unity-object-hovered-color");

            public static readonly Color selectedBackgroundColor = EditorResources.GetStyle("game-object-tree-view-scene-visibility")
                .GetColor("-unity-object-selected-color");

            public static readonly Color selectedNoFocusBackgroundColor = EditorResources.GetStyle("game-object-tree-view-scene-visibility")
                .GetColor("-unity-object-selected-no-focus-color");

            public static readonly GUIStyle sceneVisibilityStyle = "SceneVisibility";

            public static readonly IconState iconNormal = new()
            {
                visible = EditorGUIUtility.TrIconContent("scenevis_visible", "Click to hide Target in Scene View"),
                hidden = EditorGUIUtility.TrIconContent("scenevis_hidden", "Click to show Target in Scene View"),
                ping = EditorGUIUtility.TrIconContent("Packages/com.unity.2d.tilemap/Editor/Icons/EditorUI.Target.png", "Click to ping Target in Hierarchy"),
            };
            public static readonly IconState iconHovered = new()
            {
                visible = EditorGUIUtility.TrIconContent("scenevis_visible_hover", "Click to hide Target in Scene View"),
                hidden = EditorGUIUtility.TrIconContent("scenevis_hidden_hover", "Click to show Target in Scene View"),
                ping = EditorGUIUtility.TrIconContent("Packages/com.unity.2d.tilemap/Editor/Icons/EditorUI.TargetHover.png", "Click to ping Target in Hierarchy"),
            };
        }

        internal static string k_CreateNewPaintTargetName = L10n.Tr("Create New Tilemap");

        private static Styles s_Styles;

        private IFlexibleMenuItemProvider m_ItemProvider;
        private FlexibleMenuModifyItemUI m_ModifyItemUI;
        private readonly Action<int, object> m_ItemClickedCallback;
        private readonly Action<int, Rect> m_ItemHoveredCallback;
        private Vector2 m_ScrollPosition = Vector2.zero;
        private bool m_ShowAddNewPresetItem;
        private int m_HoverIndex;
        private int[] m_SeperatorIndices;
        private float m_CachedWidth = -1f;
        private float m_ParentWidth;
        private string m_FilterString;
        private List<int> m_FilteredIndices = new List<int>();

        private static readonly string activeTargetFilterTextEditorPref = "TilePaletteActiveTargetFilterText";

        private string activeTargetFilterText
        {
            get => EditorPrefs.GetString(activeTargetFilterTextEditorPref, String.Empty);
            set => EditorPrefs.SetString(activeTargetFilterTextEditorPref, value);
        }

        private Dictionary<GameObject, GUIContent> m_BrushTargets = new Dictionary<GameObject, GUIContent>();

        private const float LineHeight = 18f;
        private const float SeperatorHeight = 8f;
        private int maxIndex { get { return m_ShowAddNewPresetItem ? m_ItemProvider.Count() : m_ItemProvider.Count() - 1; } }
        public int selectedIndex { get; set; }
        protected float parentWidth { get { return m_ParentWidth; } set { m_ParentWidth = value; ClearCachedWidth(); } }

        public enum VisibilityState
        {
            IsVisible,
            NotVisible,
            InvalidForVisible
        }

        internal class MenuItemProvider : IFlexibleMenuItemProvider
        {
            public int Count()
            {
                return GridPaintingState.validTargets != null ? GridPaintingState.validTargets.Length + 1 : 1;
            }

            public object GetItem(int index)
            {
                if (GridPaintingState.validTargets != null && index < GridPaintingState.validTargets.Length)
                    return GridPaintingState.validTargets[index];
                return GridPaintingState.scenePaintTarget;
            }

            public int Add(object obj)
            {
                throw new NotImplementedException();
            }

            public void Replace(int index, object newPresetObject)
            {
                throw new NotImplementedException();
            }

            public void Remove(int index)
            {
                throw new NotImplementedException();
            }

            public object Create()
            {
                throw new NotImplementedException();
            }

            public void Move(int index, int destIndex, bool insertAfterDestIndex)
            {
                throw new NotImplementedException();
            }

            public string GetName(int index)
            {
                if (GridPaintingState.validTargets != null
                    && index < GridPaintingState.validTargets.Length)
                {
                    return GridPaintingState.validTargets[index].name;
                }
                return "Create New Tilemap";
            }

            public bool IsModificationAllowed(int index)
            {
                return false;
            }

            public int[] GetSeperatorIndices()
            {
                if (GridPaintingState.validTargets != null)
                    return new int[] { GridPaintingState.validTargets.Length - 1 };
                return new int[] { -1 };
            }
        }

        // itemClickedCallback arguments is clicked index, clicked item object
        public GridPaintTargetsDropdown(IFlexibleMenuItemProvider itemProvider
            , int selectionIndex
            , FlexibleMenuModifyItemUI modifyItemUi
            , Action<int, object> itemClickedCallback
            , Action<int, Rect> itemHoveredCallback
            , float minWidth)
        {
            m_ItemProvider = itemProvider;
            m_ModifyItemUI = modifyItemUi;
            m_ItemClickedCallback = itemClickedCallback;
            m_ItemHoveredCallback = itemHoveredCallback;
            m_SeperatorIndices = m_ItemProvider.GetSeperatorIndices();
            selectedIndex = selectionIndex;
            m_ShowAddNewPresetItem = m_ModifyItemUI != null;
            m_ParentWidth = minWidth;
            m_FilterString = activeTargetFilterText;
            UpdateFilter();
        }

        public override Vector2 GetWindowSize()
        {
            return CalcSize();
        }

        private void UpdateFilter()
        {
            m_FilteredIndices.Clear();
            m_FilteredIndices.Capacity = maxIndex;

            Regex regex = null;
            var hasFilter = !String.IsNullOrWhiteSpace(m_FilterString);
            if (hasFilter)
            {
                try
                {
                    regex = new Regex(m_FilterString,
                        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(50));
                }
                catch (ArgumentException)
                {
                    hasFilter = false;
                }
            }

            for (var i = 0; i < maxIndex; i++)
            {
                if (hasFilter && regex != null && i != selectedIndex)
                {
                    var name = m_ItemProvider.GetName(i);
                    bool match;
                    try
                    {
                        match = regex.IsMatch(name);
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        match = false;
                    }
                    if (!match)
                        continue;
                }
                m_FilteredIndices.Add(i);
            }
        }

        public override void OnGUI(Rect rect)
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            Event evt = Event.current;

            EditorGUI.BeginChangeCheck();
            m_FilterString = EditorGUI.ToolbarSearchField(new Rect(0, 0, rect.width, LineHeight), m_FilterString, false);
            if (EditorGUI.EndChangeCheck() && !m_FilterString.Equals(activeTargetFilterText))
            {
                activeTargetFilterText = m_FilterString;
                UpdateFilter();
            }
            Rect scrollRect = new Rect(0, LineHeight, rect.width, rect.height - LineHeight);
            Rect contentRect = new Rect(0, 0, 1, CalcSize().y - LineHeight);
            m_ScrollPosition = GUI.BeginScrollView(scrollRect, m_ScrollPosition, contentRect);
            {
                int itemMaxIndex = m_FilteredIndices.Count;
                float curY = 0;
                for (int i = 0; i <= maxIndex; ++i)
                {
                    var validItemIndex = i < itemMaxIndex;
                    var j = validItemIndex ? m_FilteredIndices[i] : i < maxIndex ? -1 : maxIndex;
                    var itemControlID = j + 1000000;
                    var fullRect = new Rect(0, curY, rect.width, LineHeight);
                    var visRect = new Rect(0, curY, 16, LineHeight);
                    var pingRect = new Rect(16, curY, 16, LineHeight);
                    var arrowRect = new Rect(rect.width - 16 - 1, curY, 16, LineHeight);
                    var itemRect = new Rect(16 + 16, curY, rect.width - 16 - 16, LineHeight);
                    var addSeparator = i == (itemMaxIndex - 1);
                    var isCreate = i == maxIndex;
                    var isSelected = j == selectedIndex;

                    // Handle event
                    switch (evt.type)
                    {
                        case EventType.Repaint:
                            bool hover = false;
                            if (validItemIndex || isCreate)
                            {
                                if (m_HoverIndex == j)
                                {
                                    if (fullRect.Contains(evt.mousePosition))
                                        hover = true;
                                    else
                                        m_HoverIndex = -1;
                                }

                                var visibilityState = GetVisibilityState(j);
                                var showIcon = visibilityState != VisibilityState.InvalidForVisible;
                                var isItemVisible = visibilityState != VisibilityState.NotVisible || isCreate;
                                if (showIcon && (hover || isSelected || !isItemVisible) && !isCreate)
                                {
                                    var isVisHover = visRect.Contains(evt.mousePosition);
                                    var visIconState = isVisHover
                                        ? Styles.iconHovered
                                        : Styles.iconNormal;
                                    var visIcon = isItemVisible ? visIconState.visible : visIconState.hidden;
                                    GUI.Button(visRect, visIcon, Styles.sceneVisibilityStyle);
                                }
                                if (showIcon && (hover || isSelected) && !isCreate)
                                {
                                    var isPingHover = pingRect.Contains(evt.mousePosition);
                                    var pingIconState = isPingHover
                                        ? Styles.iconHovered
                                        : Styles.iconNormal;
                                    GUI.Button(pingRect, pingIconState.ping, Styles.sceneVisibilityStyle);
                                }

                                using (new EditorGUI.DisabledScope(!isItemVisible))
                                {
                                    var width = (isCreate ? fullRect : itemRect).size.x;
                                    var name = m_ItemProvider.GetName(j);
                                    var tname = TextUtility.TruncateWithEllipsis(s_Styles.menuItem, name, width);
                                    s_Styles.menuItem.Draw(isCreate ? fullRect : itemRect, GUIContent.Temp(tname), hover, false, isSelected, false);
                                }
                            }

                            if (isCreate)
                            {
                                GUI.Button(arrowRect, s_Styles.backIcon, Styles.sceneVisibilityStyle);
                            }
                            if (addSeparator)
                            {
                                const float margin = 4f;
                                Rect seperatorRect = new Rect(fullRect.x + margin, fullRect.y + fullRect.height + SeperatorHeight * 0.5f, fullRect.width - 2 * margin, 1);
                                DrawRect(seperatorRect, (EditorGUIUtility.isProSkin) ? new Color(0.32f, 0.32f, 0.32f, 1.333f) : new Color(0.6f, 0.6f, 0.6f, 1.333f)); // dark : light
                            }
                            break;

                        case EventType.MouseDown:
                            if (evt.button == 0 && visRect.Contains(evt.mousePosition) && j >= 0)
                            {
                                GUIUtility.hotControl = itemControlID;
                                if (evt.clickCount == 1)
                                {
                                    GUIUtility.hotControl = 0;
                                    ToggleVisibility(j, !evt.alt);
                                    evt.Use();
                                }
                            }
                            if (evt.button == 0 && pingRect.Contains(evt.mousePosition) && j >= 0)
                            {
                                GUIUtility.hotControl = itemControlID;
                                if (evt.clickCount == 1)
                                {
                                    GUIUtility.hotControl = 0;
                                    PingItem(j);
                                    evt.Use();
                                }
                            }
                            if (evt.button == 0 && itemRect.Contains(evt.mousePosition)
                                                && j >= 0
                                                && (GetVisibilityState(j) != VisibilityState.NotVisible || (j == 0 && maxIndex == 0)))
                            {
                                GUIUtility.hotControl = itemControlID;
                                if (evt.clickCount == 1)
                                {
                                    GUIUtility.hotControl = 0;
                                    SelectItem(j);
                                    editorWindow.Close();
                                    evt.Use();
                                }
                            }
                            break;
                        case EventType.MouseUp:
                            if (GUIUtility.hotControl == itemControlID)
                            {
                                GUIUtility.hotControl = 0;
                            }
                            break;
                        case EventType.MouseMove:
                            if (fullRect.Contains(evt.mousePosition) && j >= 0)
                            {
                                m_HoverIndex = j;
                                HoverItem(itemRect, m_HoverIndex);
                            }
                            else if (m_HoverIndex == j)
                            {
                                m_HoverIndex = -1;
                            }
                            Repaint();
                            break;
                    }

                    if (validItemIndex)
                        curY += LineHeight;
                    if (addSeparator)
                        curY += SeperatorHeight;
                } // end foreach item
            } GUI.EndScrollView();
        }

        private void SelectItem(int index)
        {
            selectedIndex = index;
            if (m_ItemClickedCallback != null && index >= 0)
                m_ItemClickedCallback(index, m_ItemProvider.GetItem(index));
        }

        private void HoverItem(Rect rect, int index)
        {
            if (m_ItemHoveredCallback != null && index >= 0)
                m_ItemHoveredCallback(index, rect);
        }

        private VisibilityState GetVisibilityState(int index)
        {
            var obj = m_ItemProvider.GetItem(index) as GameObject;
            if (obj != null)
            {
                if ((obj.hideFlags & HideFlags.HideInHierarchy) > 0)
                    return VisibilityState.InvalidForVisible;
                return SceneVisibilityManager.instance.IsHidden(obj) ? VisibilityState.NotVisible : VisibilityState.IsVisible;
            }
            return VisibilityState.InvalidForVisible;
        }

        private void ToggleVisibility(int index, bool includeDescendants)
        {
            var obj = m_ItemProvider.GetItem(index) as GameObject;
            if (obj != null)
                SceneVisibilityManager.instance.ToggleVisibility(obj, includeDescendants);
        }

        private void PingItem(int index)
        {
            var obj = m_ItemProvider.GetItem(index) as UnityEngine.Object;
            if (obj != null)
                EditorGUIUtility.PingObject(obj);
        }

        protected Vector2 CalcSize()
        {
            var validSeperators = 0;
            foreach (var seperator in m_SeperatorIndices)
            {
                if (seperator >= 0)
                    validSeperators++;
            }
            float height = (m_FilteredIndices.Count + 2) * LineHeight + validSeperators * SeperatorHeight;
            if (m_CachedWidth < 0)
                m_CachedWidth = Math.Max(m_ParentWidth, CalcWidth());
            return new Vector2(m_CachedWidth, height);
        }

        private void ClearCachedWidth()
        {
            m_CachedWidth = -1f;
        }

        private float CalcWidth()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            var count = m_ItemProvider.Count();
            float maxWidth = 0;
            for (int i = 0; i < count - 1; ++i)
            {
                float w = s_Styles.menuItem.CalcSize(GUIContent.Temp(m_ItemProvider.GetName(i))).x;
                maxWidth = Mathf.Max(w, maxWidth);
            }
            if (count > 0)
            {
                float limitWidth = s_Styles.menuItem.CalcSize(GUIContent.Temp(m_ItemProvider.GetName(count - 1))).x;
                if (maxWidth > limitWidth)
                    maxWidth = limitWidth;
            }

            const float rightMargin = 6f;
            return maxWidth + rightMargin;
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color orgColor = GUI.color;
            GUI.color = GUI.color * color;
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = orgColor;
        }

        private void Repaint()
        {
            HandleUtility.Repaint(); // repaints current guiview (needs rename)
        }
    }
}
