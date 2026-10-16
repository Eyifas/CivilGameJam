using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class SpriteAtlasIssuesContainer : VisualElement
    {
        //ListView m_ReportListView;
        TreeView m_ReportListView;
        List<ISpriteAtlasIssue> m_ReportListItems;
        event Action<IAnalyzerReport> m_OnItemDoubleClicked;
        const string k_UxmlPath = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteAtlasIssuesContainer/SpriteAtlasIssuesContainer.uxml";
        const string k_UssPath = "Packages/com.unity.2d.tooling/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteAtlasIssuesContainer/SpriteAtlasIssuesContainer.uss";

        SpriteAtlasIssuesContainer()
        { }

        public static SpriteAtlasIssuesContainer Create()
        {
            VisualTreeAsset ve = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
            SpriteAtlasIssuesContainer container = new ();
            ve.CloneTree(container);
            container.InitView();
            return container;
        }

        void InitView()
        {
            // Setup report list view
            m_ReportListView = this.Q<TreeView>("TreeView");
            m_ReportListView.makeItem = MakeItem;
            m_ReportListView.selectionChanged += OnSelectionChanged;
            m_ReportListView.bindItem += BindItem;
            m_ReportListView.unbindItem += UnbindItem;
            m_ReportListView.selectionType = SelectionType.Single;

            Add(m_ReportListView);

        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            //obj.menu.AppendAction("Show in New Report Window", (a) => ShowInNewReportWindow(), DropdownMenuAction.Status.Normal);
        }

        public void SetListDataSource(List<ISpriteAtlasIssue> reportListItems)
        {
            m_ReportListItems = reportListItems;
            m_ReportListView.SetRootItems(BuildTree());
            //m_ReportListView.dataSource = m_ReportListItems;
            m_ReportListView.Rebuild();
        }

        List<TreeViewItemData<ISpriteAtlasIssue>> BuildTree()
        {
            var list = new List<TreeViewItemData<ISpriteAtlasIssue>>();

            for (int i = 0; i < m_ReportListItems.Count; i++)
            {
                var item = m_ReportListItems[i];
                var treeItem = new TreeViewItemData<ISpriteAtlasIssue>(i, item);
                list.Add(treeItem);
            }
            return new List<TreeViewItemData<ISpriteAtlasIssue>>
            {
                    new TreeViewItemData<ISpriteAtlasIssue>(-1, null, list)
                };
        }

        void UnbindItem(VisualElement arg1, int arg2)
        {
            arg1.Clear();
        }

        public IAnalyzerReport listRootItem
        {
            get;
            set;
        }

        void BindItem(VisualElement arg1, int arg2)
        {
            var issue = m_ReportListView.GetItemDataForIndex<ISpriteAtlasIssue>(arg2);
            if(issue != null)
                arg1.Add(issue.listItem);
            else if(listRootItem != null)
                arg1.Add(listRootItem.listItem);
            else
                arg1.Add(new Label("Atlas issues"));
        }

        void OnSelectionChanged(IEnumerable<object> _)
        {
            // var index = m_ReportListView.selectedIndex;
            // if(index >= 0 && index < m_ReportListItems.Count)
            // {
            //     var contentItem = m_ReportListItems[index].contentItem;
            //     var contentContainer = this.Q("RightPaneContainer");
            //     contentContainer.Clear();
            //     contentContainer.Add(contentItem);
            // }
        }

        VisualElement MakeItem()
        {
            var item = new VisualElement();
            item.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.clickCount == 2)
                {
                    var i = m_ReportListView.GetIdForIndex(m_ReportListView.selectedIndex);
                    if(i < 0)
                        m_OnItemDoubleClicked?.Invoke(listRootItem);
                    else
                        m_OnItemDoubleClicked?.Invoke(m_ReportListItems[i]);
                }
            });
            item.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath));
            item.AddToClassList("report-list-item-container");
            return item;
        }

        public event Action<IAnalyzerReport> onItemDoubleClicked
        {
            add => m_OnItemDoubleClicked += value;
            remove => m_OnItemDoubleClicked -= value;
        }
    }
}
