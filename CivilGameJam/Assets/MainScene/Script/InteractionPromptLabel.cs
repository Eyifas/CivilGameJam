using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attached to an InteractionPrompt object (or auto-created by InteractionZone).
/// Creates a 3D TextMeshPro component to the right of the interaction prompt icon
/// displaying a customizable label configured in the Editor Inspector.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class InteractionPromptLabel : MonoBehaviour
{
    [Header("Prompt Label Settings")]
    [Tooltip("Text displayed to the right of the interaction prompt (set in Inspector)")]
    [SerializeField] private string labelText = "Interact";

    [Tooltip("Local offset from the prompt icon center to the text (X = right)")]
    [SerializeField] private Vector3 offset = new Vector3(0.85f, 0f, 0f);

    [Tooltip("Font size for the 3D TextMeshPro")]
    [SerializeField] private float fontSize = 3.5f;

    [Tooltip("Color of the label text")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Font styling (Bold, Normal, etc.)")]
    [SerializeField] private FontStyles fontStyle = FontStyles.Bold;

    [Header("Internal Reference")]
    [SerializeField] private TextMeshPro textMesh;

    public string LabelText
    {
        get => labelText;
        set
        {
            labelText = value;
            UpdateVisuals();
        }
    }

    public Vector3 Offset
    {
        get => offset;
        set
        {
            offset = value;
            UpdateVisuals();
        }
    }

    public float FontSize
    {
        get => fontSize;
        set
        {
            fontSize = value;
            UpdateVisuals();
        }
    }

    public Color TextColor
    {
        get => textColor;
        set
        {
            textColor = value;
            UpdateVisuals();
        }
    }

    private void Awake()
    {
        EnsureTextMesh();
        UpdateVisuals();
    }

    private void Start()
    {
        EnsureTextMesh();
        UpdateVisuals();
    }

    private void OnEnable()
    {
        EnsureTextMesh();
        UpdateVisuals();
    }

    /// <summary>
    /// Ensures the child GameObject with TextMeshPro exists and is properly initialized.
    /// </summary>
    public void EnsureTextMesh()
    {
        if (textMesh == null)
        {
            // 1. Look for existing child
            Transform existingChild = transform.Find("PromptLabel");
            if (existingChild != null)
            {
                textMesh = existingChild.GetComponent<TextMeshPro>();
            }

            // 2. Look for any child TMP
            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMeshPro>(true);
            }

            // 3. Create if not found
            if (textMesh == null)
            {
                GameObject labelObj = new GameObject("PromptLabel");
                labelObj.transform.SetParent(transform, false);
                textMesh = labelObj.AddComponent<TextMeshPro>();
            }
        }

        ConfigureTextMesh();
    }

    private void ConfigureTextMesh()
    {
        if (textMesh == null) return;

        // Position to the right with left-centered alignment
        RectTransform rt = textMesh.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.localPosition = offset;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(8f, 2f);

        textMesh.alignment = TextAlignmentOptions.MidlineLeft;
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.overflowMode = TextOverflowModes.Overflow;

        // Synchronize sorting layer and order with the prompt's SpriteRenderer
        if (TryGetComponent<SpriteRenderer>(out var sr))
        {
            var mr = textMesh.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerID = sr.sortingLayerID;
                mr.sortingOrder = Mathf.Max(sr.sortingOrder + 1, 10);
            }
        }
    }

    /// <summary>
    /// Refreshes text content, styling, and position.
    /// </summary>
    public void UpdateVisuals()
    {
        if (textMesh == null)
        {
            EnsureTextMesh();
        }

        if (textMesh != null)
        {
            textMesh.text = labelText;
            textMesh.fontSize = fontSize;
            textMesh.color = textColor;
            textMesh.fontStyle = fontStyle;
            textMesh.rectTransform.localPosition = offset;

            if (TryGetComponent<SpriteRenderer>(out var sr))
            {
                var mr = textMesh.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sortingLayerID = sr.sortingLayerID;
                    mr.sortingOrder = Mathf.Max(sr.sortingOrder + 1, 10);
                }
            }
        }
    }

    /// <summary>
    /// Sets the label string dynamically from external scripts (e.g. InteractionZone).
    /// </summary>
    public void SetLabel(string newLabel)
    {
        labelText = newLabel;
        UpdateVisuals();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (textMesh != null)
        {
            UpdateVisuals();
        }
        else
        {
            // Delay instantiation in editor to prevent SendMessage warnings during serialization
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    EnsureTextMesh();
                    UpdateVisuals();
                }
            };
        }
    }
#endif
}
