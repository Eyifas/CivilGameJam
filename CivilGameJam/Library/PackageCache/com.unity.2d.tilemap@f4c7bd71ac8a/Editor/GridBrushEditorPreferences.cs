using UnityEngine;

namespace UnityEditor.Tilemaps
{
    internal class GridBrushEditorPreferences
    {
        public static readonly string sceneViewTextEnableEditorPref = "TilePalette.SceneViewTextEnable";
        public static readonly string sceneViewTextEnableLookup = "Draw Grid Brush Name in Scene View";
        public static readonly string sceneViewTextSizeEditorPref = "TilePalette.SceneViewTextSize";
        public static readonly string sceneViewTextSizeLookup = "Font size for Scene View";

        internal class SettingsProperties
        {
            public static readonly GUIContent floodfillLabel = EditorGUIUtility.TrTextContent("Grid Brush Flood Fill Extents");
            public static readonly GUIContent floodFillPreviewLabel = EditorGUIUtility.TrTextContent("Show Flood Fill Preview", "Whether a preview is shown while painting a Tilemap when Flood Fill mode is enabled");
            public static readonly GUIContent floodFillPreviewFillExtentsLabel = EditorGUIUtility.TrTextContent("Flood Fill Preview Fill Extents", "Extents from the selected position when flood filling with a Tile. Set this to 0 to flood fill to the full extents.");
            public static readonly GUIContent floodFillPreviewEraseExtentsLabel = EditorGUIUtility.TrTextContent("Flood Fill Preview Erase Extents", "Extents from the selected position when flood filling without a Tile. Set this to 0 to flood erase to the full extents.");

            public static readonly GUIContent sceneViewTextLabel = new GUIContent("Grid Brush Scene View Text");

            public static readonly GUIContent sceneViewTextEnableLabel =
                EditorGUIUtility.TrTextContent(sceneViewTextEnableLookup,
                    "Draws the Grid Brush name on the marquee in the Scene View");

            public static readonly GUIContent sceneViewTextSizeLabel = EditorGUIUtility.TrTextContent(
                sceneViewTextSizeLookup
                , "Grid Brush Name font size");
        }


        [SettingsProvider]
        internal static SettingsProvider CreateSettingsProvider()
        {
            var settingsProvider = new SettingsProvider("Preferences/2D/Grid Brush", SettingsScope.User, SettingsProvider.GetSearchKeywordsFromGUIContentProperties<SettingsProperties>()) {
                guiHandler = _ =>
                {
                    PreferencesGUI();
                }
            };
            return settingsProvider;
        }

        private static bool s_PrefsLoaded;
        private static bool s_SceneViewTextEnable;
        private static int s_SceneViewTextSize;

        private static void LoadPrefs()
        {
            if (s_PrefsLoaded)
                return;
            s_SceneViewTextEnable = EditorPrefs.GetBool(sceneViewTextEnableEditorPref, false);
            s_SceneViewTextSize = EditorPrefs.GetInt(sceneViewTextSizeEditorPref, 12);
            s_PrefsLoaded = true;
        }

        public static bool sceneViewTextEnable
        {
            get
            {
                LoadPrefs();
                return s_SceneViewTextEnable;
            }
            set
            {
                LoadPrefs();
                if (s_SceneViewTextEnable == value)
                    return;
                s_SceneViewTextEnable = value;
                EditorPrefs.SetBool(sceneViewTextEnableEditorPref, value);
            }
        }

        public static int sceneViewTextSize
        {
            get
            {
                LoadPrefs();
                return s_SceneViewTextSize;
            }
            set
            {
                LoadPrefs();
                if (s_SceneViewTextSize == value)
                    return;
                s_SceneViewTextSize = value;
                EditorPrefs.SetInt(sceneViewTextSizeEditorPref, value);
            }
        }

        internal static void PreferencesGUI()
        {
            using (new SettingsWindow.GUIScope())
            {
                GUILayout.Label(SettingsProperties.floodfillLabel, EditorStyles.boldLabel, GUILayout.MinWidth(160));

                EditorGUI.BeginChangeCheck();
                var val = EditorGUILayout.Toggle(SettingsProperties.floodFillPreviewLabel, GridBrushEditor.showFloodFillPreview);
                if (EditorGUI.EndChangeCheck())
                {
                    GridBrushEditor.showFloodFillPreview = val;
                }
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!val))
                {
                    EditorGUI.BeginChangeCheck();
                    var fill = EditorGUILayout.IntField(SettingsProperties.floodFillPreviewFillExtentsLabel, GridBrushEditor.floodFillPreviewFillExtents);
                    if (EditorGUI.EndChangeCheck())
                    {
                        GridBrushEditor.floodFillPreviewFillExtents = fill;
                    }
                    EditorGUI.BeginChangeCheck();
                    var erase = EditorGUILayout.IntField(SettingsProperties.floodFillPreviewEraseExtentsLabel, GridBrushEditor.floodFillPreviewEraseExtents);
                    if (EditorGUI.EndChangeCheck())
                    {
                        GridBrushEditor.floodFillPreviewEraseExtents = erase;
                    }
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                GUILayout.Label(SettingsProperties.sceneViewTextLabel, EditorStyles.boldLabel, GUILayout.MinWidth(160));

                EditorGUI.BeginChangeCheck();
                var sceneViewTextEnableValue = EditorGUILayout.Toggle(SettingsProperties.sceneViewTextEnableLabel, sceneViewTextEnable);
                if (EditorGUI.EndChangeCheck())
                {
                    sceneViewTextEnable = sceneViewTextEnableValue;
                }
                using (new EditorGUI.DisabledScope(!sceneViewTextEnableValue))
                {
                    EditorGUI.BeginChangeCheck();
                    var sceneViewTextSizeValue = EditorGUILayout.IntSlider(SettingsProperties.sceneViewTextSizeLabel, sceneViewTextSize, 4, 64);
                    if (EditorGUI.EndChangeCheck())
                    {
                        sceneViewTextSize = Mathf.Clamp(sceneViewTextSizeValue, 4, 64);
                    }
                }
            }
        }
    }
}
