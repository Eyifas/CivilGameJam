using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace SampleReport.Reports
{
    /// <summary>
    /// Provides a settings UI for configuring Sprite mesh size filters, including vertex and triangle count thresholds.
    /// </summary>
    public class SpriteMeshSizeReportSettings : VisualElement
    {
        /// The GUID for the associated UXML asset.
        const string k_Uxml = "3cb80fa2ec284f43952b6270b3baf195";

        /// The integer field for setting the minimum vertex count filter.
        IntegerField m_VertexCountField;

        /// The integer field for setting the minimum triangle count filter.
        IntegerField m_TriangleCountField;

        /// The button to apply the current filter settings.
        Button m_ApplyButton;

        /// The current minimum vertex count filter value.
        int m_VertexCount;

        /// The current minimum triangle count filter value.
        int m_TriangleCount;

        /// Event triggered when the apply button is clicked, passing the current filter values.
        public event Action<int, int> onApplyClickedEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpriteMeshSizeReportSettings"/> class with the specified filter values.
        /// </summary>
        /// <param name="vertexCount">The initial vertex count filter value.</param>
        /// <param name="triangleCount">The initial triangle count filter value.</param>
        public SpriteMeshSizeReportSettings(int vertexCount, int triangleCount)
        {
            var path = AssetDatabase.GUIDToAssetPath(new UnityEngine.GUID(k_Uxml));
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            uxml.CloneTree(this);
            m_VertexCountField = this.Q<IntegerField>("verticesCount");
            m_TriangleCountField = this.Q<IntegerField>("triangleCount");
            m_ApplyButton = this.Q<Button>("apply");
            m_ApplyButton.SetEnabled(false);
            m_ApplyButton.clickable.clicked += OnApplyClicked;
            m_VertexCount = vertexCount;
            m_TriangleCount = triangleCount;
            m_VertexCountField.SetValueWithoutNotify(m_VertexCount);
            m_TriangleCountField.SetValueWithoutNotify(m_TriangleCount);

            m_VertexCountField.RegisterValueChangedCallback(x =>
            {
                m_VertexCountField.SetValueWithoutNotify(Math.Clamp(x.newValue, 0, int.MaxValue));
                m_ApplyButton.SetEnabled(true);
            });
            m_TriangleCountField.RegisterValueChangedCallback(x =>
            {
                m_TriangleCountField.SetValueWithoutNotify(Math.Clamp(x.newValue, 0, int.MaxValue));
                m_ApplyButton.SetEnabled(true);
            });
        }

        /// <summary>
        /// Applies the current filter values and triggers the apply event.
        /// </summary>
        void OnApplyClicked()
        {
            m_VertexCount = m_VertexCountField.value;
            m_TriangleCount = m_TriangleCountField.value;
            onApplyClickedEvent?.Invoke(m_VertexCount, m_TriangleCount);
            m_ApplyButton.SetEnabled(false);
        }
    }
}
