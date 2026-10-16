using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor.Tilemaps
{
    [Serializable]
    internal class TilemapBrushTarget
    {
        [SerializeField]
        private string m_TargetGlobalObjectId;

        [SerializeField]
        private string m_BrushAssetPath;

        public string targetGlobalObjectId
        {
            get => m_TargetGlobalObjectId;
            set => m_TargetGlobalObjectId = value;
        }

        public string brushAssetPath
        {
            get => m_BrushAssetPath;
            set => m_BrushAssetPath = value;
        }
    }

    [Serializable]
    internal class TilemapPaletteTarget
    {
        [SerializeField]
        private string m_TargetGlobalObjectId;

        [SerializeField]
        private GameObject m_Palette;

        public string targetGlobalObjectId
        {
            get => m_TargetGlobalObjectId;
            set => m_TargetGlobalObjectId = value;
        }

        public GameObject palette
        {
            get => m_Palette;
            set => m_Palette = value;
        }
    }

    [FilePath("Library/GridTargetBrushSync.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class GridTargetBrushSync : ScriptableSingleton<GridTargetBrushSync>
    {
        [SerializeField]
        private bool m_SyncEnabled;

        [SerializeField]
        private List<TilemapBrushTarget> m_TilemapBrushTargets = new List<TilemapBrushTarget>();

        public List<TilemapBrushTarget> tilemapBrushTargets => m_TilemapBrushTargets;

        public event Action tilemapBrushTargetsChanged;

        private static string GetGlobalObjectIdString(GameObject sceneTarget)
        {
            if (sceneTarget == null)
                return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(sceneTarget);
            if (id.targetObjectId == 0)
                return null;
            return id.ToString();
        }

        private class PendingTilemapBrushTarget
        {
            public GameObject sceneTarget;
            public GridBrushBase brush;
            public string brushAssetPath;
        }

        [NonSerialized]
        private List<PendingTilemapBrushTarget> m_PendingTilemapBrushTargets = new List<PendingTilemapBrushTarget>();

        public GridBrushBase GetLinkedBrush(GameObject sceneTarget)
        {
            if (sceneTarget == null)
                return null;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                foreach (var pending in m_PendingTilemapBrushTargets)
                {
                    if (pending.sceneTarget == sceneTarget)
                        return pending.brush;
                }
                return null;
            }

            foreach (var entry in m_TilemapBrushTargets)
            {
                if (entry.targetGlobalObjectId == targetId)
                {
                    if (string.IsNullOrEmpty(entry.brushAssetPath))
                        return null;
                    return GridPaletteBrushes.GetBrushFromAssetPath(entry.brushAssetPath);
                }
            }
            return null;
        }

        public bool HasTilemapBrushTarget(GameObject sceneTarget, GridBrushBase brush)
        {
            if (sceneTarget == null || brush == null)
                return false;

            var brushPath = GridPaletteBrushes.GetAssetPathFromBrush(brush);
            if (string.IsNullOrEmpty(brushPath))
                return false;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                foreach (var pending in m_PendingTilemapBrushTargets)
                {
                    if (pending.sceneTarget == sceneTarget && pending.brush == brush)
                        return true;
                }
                return false;
            }

            foreach (var entry in m_TilemapBrushTargets)
            {
                if (entry.targetGlobalObjectId == targetId && entry.brushAssetPath == brushPath)
                    return true;
            }
            return false;
        }

        public void AddTilemapBrushTarget(GameObject sceneTarget, GridBrushBase brush)
        {
            if (sceneTarget == null || brush == null)
                return;

            var brushPath = GridPaletteBrushes.GetAssetPathFromBrush(brush);
            if (string.IsNullOrEmpty(brushPath))
                return;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                m_PendingTilemapBrushTargets.RemoveAll(p => p.sceneTarget == sceneTarget);
                m_PendingTilemapBrushTargets.Add(new PendingTilemapBrushTarget
                {
                    sceneTarget = sceneTarget,
                    brush = brush,
                    brushAssetPath = brushPath
                });
                return;
            }

            if (HasTilemapBrushTarget(sceneTarget, brush))
                return;

            m_TilemapBrushTargets.RemoveAll(entry => entry.targetGlobalObjectId == targetId);

            m_TilemapBrushTargets.Add(new TilemapBrushTarget
            {
                targetGlobalObjectId = targetId,
                brushAssetPath = brushPath
            });
            Save(true);
            tilemapBrushTargetsChanged?.Invoke();
        }

        public void RemoveTilemapBrushTarget(GameObject sceneTarget, GridBrushBase brush)
        {
            if (sceneTarget == null || brush == null)
                return;

            var brushPath = GridPaletteBrushes.GetAssetPathFromBrush(brush);
            if (string.IsNullOrEmpty(brushPath))
                return;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                if (m_PendingTilemapBrushTargets.RemoveAll(p => p.sceneTarget == sceneTarget && p.brush == brush) > 0)
                    tilemapBrushTargetsChanged?.Invoke();
                return;
            }

            for (int i = m_TilemapBrushTargets.Count - 1; i >= 0; i--)
            {
                var entry = m_TilemapBrushTargets[i];
                if (entry.targetGlobalObjectId == targetId && entry.brushAssetPath == brushPath)
                {
                    m_TilemapBrushTargets.RemoveAt(i);
                    Save(true);
                    tilemapBrushTargetsChanged?.Invoke();
                    return;
                }
            }
        }

        private void FlushPendingTilemapBrushTargets()
        {
            if (m_PendingTilemapBrushTargets.Count == 0)
                return;

            var changed = false;
            for (int i = m_PendingTilemapBrushTargets.Count - 1; i >= 0; i--)
            {
                var pending = m_PendingTilemapBrushTargets[i];
                if (pending.sceneTarget == null || string.IsNullOrEmpty(pending.brushAssetPath))
                {
                    m_PendingTilemapBrushTargets.RemoveAt(i);
                    continue;
                }

                var targetId = GetGlobalObjectIdString(pending.sceneTarget);
                if (string.IsNullOrEmpty(targetId))
                    continue;

                m_TilemapBrushTargets.RemoveAll(entry => entry.targetGlobalObjectId == targetId);
                m_TilemapBrushTargets.Add(new TilemapBrushTarget
                {
                    targetGlobalObjectId = targetId,
                    brushAssetPath = pending.brushAssetPath
                });
                m_PendingTilemapBrushTargets.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                Save(true);
                tilemapBrushTargetsChanged?.Invoke();
            }
        }

        [SerializeField]
        private List<TilemapPaletteTarget> m_TilemapPaletteTargets = new List<TilemapPaletteTarget>();

        public List<TilemapPaletteTarget> tilemapPaletteTargets => m_TilemapPaletteTargets;

        public event Action tilemapPaletteTargetsChanged;

        private class PendingTilemapPaletteTarget
        {
            public GameObject sceneTarget;
            public GameObject palette;
        }

        [NonSerialized]
        private List<PendingTilemapPaletteTarget> m_PendingTilemapPaletteTargets = new List<PendingTilemapPaletteTarget>();

        public GameObject GetLinkedPalette(GameObject sceneTarget)
        {
            if (sceneTarget == null)
                return null;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                foreach (var pending in m_PendingTilemapPaletteTargets)
                {
                    if (pending.sceneTarget == sceneTarget)
                        return pending.palette;
                }
                return null;
            }

            foreach (var entry in m_TilemapPaletteTargets)
            {
                if (entry.targetGlobalObjectId == targetId)
                    return entry.palette;
            }
            return null;
        }

        public bool HasTilemapPaletteTarget(GameObject sceneTarget, GameObject palette)
        {
            if (sceneTarget == null || palette == null)
                return false;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                foreach (var pending in m_PendingTilemapPaletteTargets)
                {
                    if (pending.sceneTarget == sceneTarget && pending.palette == palette)
                        return true;
                }
                return false;
            }

            foreach (var entry in m_TilemapPaletteTargets)
            {
                if (entry.targetGlobalObjectId == targetId && entry.palette == palette)
                    return true;
            }
            return false;
        }

        public void AddTilemapPaletteTarget(GameObject sceneTarget, GameObject palette)
        {
            if (sceneTarget == null || palette == null)
                return;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                m_PendingTilemapPaletteTargets.RemoveAll(p => p.sceneTarget == sceneTarget);
                m_PendingTilemapPaletteTargets.Add(new PendingTilemapPaletteTarget
                {
                    sceneTarget = sceneTarget,
                    palette = palette
                });
                return;
            }

            if (HasTilemapPaletteTarget(sceneTarget, palette))
                return;

            m_TilemapPaletteTargets.RemoveAll(entry => entry.targetGlobalObjectId == targetId);

            m_TilemapPaletteTargets.Add(new TilemapPaletteTarget
            {
                targetGlobalObjectId = targetId,
                palette = palette
            });
            Save(true);
            tilemapPaletteTargetsChanged?.Invoke();
        }

        public void RemoveTilemapPaletteTarget(GameObject sceneTarget, GameObject palette)
        {
            if (sceneTarget == null || palette == null)
                return;

            var targetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(targetId))
            {
                if (m_PendingTilemapPaletteTargets.RemoveAll(p => p.sceneTarget == sceneTarget && p.palette == palette) > 0)
                    tilemapPaletteTargetsChanged?.Invoke();
                return;
            }

            for (int i = m_TilemapPaletteTargets.Count - 1; i >= 0; i--)
            {
                var entry = m_TilemapPaletteTargets[i];
                if (entry.targetGlobalObjectId == targetId && entry.palette == palette)
                {
                    m_TilemapPaletteTargets.RemoveAt(i);
                    Save(true);
                    tilemapPaletteTargetsChanged?.Invoke();
                    return;
                }
            }
        }

        private void FlushPendingTilemapPaletteTargets()
        {
            if (m_PendingTilemapPaletteTargets.Count == 0)
                return;

            var changed = false;
            for (int i = m_PendingTilemapPaletteTargets.Count - 1; i >= 0; i--)
            {
                var pending = m_PendingTilemapPaletteTargets[i];
                if (pending.sceneTarget == null || pending.palette == null)
                {
                    m_PendingTilemapPaletteTargets.RemoveAt(i);
                    continue;
                }

                var targetId = GetGlobalObjectIdString(pending.sceneTarget);
                if (string.IsNullOrEmpty(targetId))
                    continue;

                m_TilemapPaletteTargets.RemoveAll(entry => entry.targetGlobalObjectId == targetId);
                m_TilemapPaletteTargets.Add(new TilemapPaletteTarget
                {
                    targetGlobalObjectId = targetId,
                    palette = pending.palette
                });
                m_PendingTilemapPaletteTargets.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                Save(true);
                tilemapPaletteTargetsChanged?.Invoke();
            }
        }

        public void OnEnable()
        {
            GridPaintingState.scenePaintTargetChanged -= SyncToTarget;
            GridPaintingState.scenePaintTargetChanged += SyncToTarget;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public void OnDisable()
        {
            GridPaintingState.scenePaintTargetChanged -= SyncToTarget;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }

        private void OnSceneClosed(Scene scene)
        {
            tilemapBrushTargetsChanged?.Invoke();
            tilemapPaletteTargetsChanged?.Invoke();
        }

        private void OnSceneSaved(Scene scene)
        {
            FlushPendingTilemapBrushTargets();
            FlushPendingTilemapPaletteTargets();
        }

        public void SyncToTarget(GameObject sceneTarget)
        {
            if (sceneTarget == null)
                return;

            var sceneTargetId = GetGlobalObjectIdString(sceneTarget);
            if (string.IsNullOrEmpty(sceneTargetId))
            {
                foreach (var pending in m_PendingTilemapBrushTargets)
                {
                    if (pending.sceneTarget == sceneTarget && pending.brush != null)
                    {
                        GridPaintingState.gridBrush = pending.brush;
                        break;
                    }
                }
                foreach (var pending in m_PendingTilemapPaletteTargets)
                {
                    if (pending.sceneTarget == sceneTarget && pending.palette != null)
                    {
                        GridPaintingState.palette = pending.palette;
                        break;
                    }
                }
            }
            else
            {
                foreach (var brushTarget in tilemapBrushTargets)
                {
                    var targetLock = brushTarget.targetGlobalObjectId;
                    if (targetLock == sceneTargetId)
                    {
                        var brushAssetPath = brushTarget.brushAssetPath;
                        if (!String.IsNullOrWhiteSpace(brushAssetPath))
                        {
                            var brush = GridPaletteBrushes.GetBrushFromAssetPath(brushAssetPath);
                            if (brush != null)
                            {
                                GridPaintingState.gridBrush = brush;
                                break;
                            }
                        }
                    }
                }
                foreach (var paletteTarget in tilemapPaletteTargets)
                {
                    var targetLock = paletteTarget.targetGlobalObjectId;
                    if (targetLock == sceneTargetId)
                    {
                        var palette = paletteTarget.palette;
                        if (palette != null)
                            GridPaintingState.palette = palette;
                        break;
                    }
                }
            }
        }
    }
}
