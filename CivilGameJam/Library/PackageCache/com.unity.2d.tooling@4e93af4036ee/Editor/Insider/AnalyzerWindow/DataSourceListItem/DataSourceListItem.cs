using System;
using UnityEditor.Search;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class DataSourceListItem : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.tooling/Editor/Insider/AnalyzerWindow/DataSourceListItem/DataSourceListItem.uxml";
        Toggle m_DataSourceToggle;
        ListView m_AssetSearchPath;
        bool m_SingleItem;

        public DataSourceListItem()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            visualTree.CloneTree(this);
            m_DataSourceToggle = this.Q<Toggle>("DataSourceName");
            m_AssetSearchPath = this.Q<ListView>("AssetSearchPath");
            m_AssetSearchPath.makeItem = () =>
            {
                var objectField = new FolderObjectField();
                objectField.Init();
                objectField.AddToClassList("asset-search-path");
                objectField.objectType = typeof(DefaultAsset);
                objectField.onValueChanged += OnObjectFieldValueChanged;
                objectField.RegisterCallback<FocusEvent>(OnObjectFieldFocusEvent);
                return objectField;
            };
            m_AssetSearchPath.bindItem = (ve, k) =>
            {
                var of = ve as FolderObjectField;
                of.bindIndex = k;
                of.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<DefaultAsset>(m_AssetSearchPath.itemsSource[k] as string));
            };
            m_AssetSearchPath.itemsSourceChanged += () =>
            {
                if (m_AssetSearchPath.itemsSource.Count <= 1)
                {
                    m_AssetSearchPath.allowRemove = false;
                }
                else
                    m_AssetSearchPath.allowRemove = true;
            };
            m_DataSourceToggle.RegisterValueChangedCallback(OnDataToggleValueChanged);
        }

        void OnObjectFieldFocusEvent(FocusEvent evt)
        {
            var objectField = evt.currentTarget as FolderObjectField;
            if (objectField == null)
                return;
            m_AssetSearchPath.selectedIndex = objectField.bindIndex;
        }

        void OnDataToggleValueChanged(ChangeEvent<bool> evt)
        {
            if(!m_SingleItem)
                m_AssetSearchPath.SetEnabled(evt.newValue);
        }

        void OnObjectFieldValueChanged(FolderObjectField obj)
        {
            m_AssetSearchPath.itemsSource[obj.bindIndex] = AssetDatabase.GetAssetPath(obj.value);
        }

        public Toggle dataSourceToggle => m_DataSourceToggle;
        public ListView assetSearchPath => m_AssetSearchPath;

        public void IsSingleItem(bool b)
        {
            dataSourceToggle.Q<VisualElement>("unity-checkmark").style.display = b ? DisplayStyle.None : DisplayStyle.Flex;
            m_AssetSearchPath.SetEnabled(true);
            m_SingleItem = b;
        }
    }

    class FolderObjectField : ObjectField
    {
        int m_Index;
        public event Action<FolderObjectField> onValueChanged;

        public int bindIndex
        {
            get => m_Index;
            set => m_Index = value;
        }
        public void Init()
        {
            this.RegisterValueChangedCallback(OnValueChanged);
        }

        void OnValueChanged(ChangeEvent<Object> evt)
        {
            onValueChanged?.Invoke(this);
        }
    }
}
