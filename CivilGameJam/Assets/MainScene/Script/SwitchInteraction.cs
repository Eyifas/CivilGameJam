using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwitchInteraction : MonoBehaviour
{
    [SerializeField] private GameObject switchImage;

    [Header("Death Settings")]
    [SerializeField] private float imageDuration = 1.5f;

    [Header("Sorting / Layer Settings")]
    [Tooltip("Sorting Order (Layer) to display switchImage on top of everything (Default: 20)")]
    [SerializeField] private int sortingOrder = 20;
    [SerializeField] private string sortingLayerName = "Default";

    private bool playerInside = false;
    private bool triggered = false;

    private readonly List<Canvas> disabledCanvases = new List<Canvas>();
    private readonly List<GameObject> disabledObjects = new List<GameObject>();

    private void Awake()
    {
        EnsureSwitchImageReference();
        ApplySortingOrder(switchImage);
    }

    private void Start()
    {
        EnsureSwitchImageReference();
        ApplySortingOrder(switchImage);

        if (switchImage != null)
        {
            switchImage.SetActive(false);
        }
    }

    private void OnValidate()
    {
        ApplySortingOrder(switchImage);
    }

    private void OnDisable()
    {
        RestoreGameUI();
    }

    private void EnsureSwitchImageReference()
    {
        if (switchImage == null)
        {
            if (transform.childCount > 0)
            {
                switchImage = transform.GetChild(0).gameObject;
            }
            else
            {
                var candidate = GameObject.Find("1000000106_0") ??
                                GameObject.Find("SwitchImage") ??
                                GameObject.Find("switchImage") ??
                                GameObject.Find("DeathImage") ??
                                GameObject.Find("Patlama");
                if (candidate != null)
                {
                    switchImage = candidate;
                }
            }
        }
    }

    public void ApplySortingOrder(GameObject obj)
    {
        if (obj == null) return;

        // 1. SpriteRenderers
        var spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sr.sortingLayerName = sortingLayerName;
            }
            sr.sortingOrder = sortingOrder;
        }

        // 2. Canvases (if UI)
        var canvases = obj.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            canvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                canvas.sortingLayerName = sortingLayerName;
            }
            canvas.sortingOrder = sortingOrder;
        }

        // 3. If obj has a RectTransform inside a parent Canvas but has no Canvas component of its own
        if (obj.GetComponent<RectTransform>() != null && obj.GetComponent<Canvas>() == null)
        {
            var canvas = obj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                canvas.sortingLayerName = sortingLayerName;
            }
            canvas.sortingOrder = sortingOrder;
        }

        // 4. SortingGroups
        var sortingGroups = obj.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true);
        foreach (var sg in sortingGroups)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sg.sortingLayerName = sortingLayerName;
            }
            sg.sortingOrder = sortingOrder;
        }

        // 5. MeshRenderers
        var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in meshRenderers)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                mr.sortingLayerName = sortingLayerName;
            }
            mr.sortingOrder = sortingOrder;
        }

        // Ensure Z position is in front of room sprites
        Vector3 pos = obj.transform.position;
        if (pos.z > -1f)
        {
            obj.transform.position = new Vector3(pos.x, pos.y, -1f);
        }
    }

    private void HideGameUI()
    {
        disabledCanvases.Clear();
        disabledObjects.Clear();

        // 1. Tell GameTimelineManager to hide timer & inventory text
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetHUDVisibility(false);
        }

        // 2. Hide PersistentCanvas (holds inventory and timer)
        if (PersistentCanvas.Instance != null)
        {
            var c = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (c != null && c.enabled)
            {
                c.enabled = false;
                disabledCanvases.Add(c);
            }
        }

        // 3. Disable any other active screen-space canvases (except switchImage and TimeRewind)
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (switchImage != null && c.transform.IsChildOf(switchImage.transform)) continue;
            if (TimeRewindController.Instance != null && c.transform.IsChildOf(TimeRewindController.Instance.transform)) continue;
            if (c.name.IndexOf("TimeRewind", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (c.enabled)
            {
                c.enabled = false;
                if (!disabledCanvases.Contains(c))
                {
                    disabledCanvases.Add(c);
                }
            }
        }

        // 4. Directly hide any standalone timer or inventory objects by common names
        string[] uiNames = new string[] { "InventoryPanel", "Inventory", "TimerText", "Timer", "QTE", "HUDStatusPanel" };
        foreach (var name in uiNames)
        {
            var go = GameObject.Find(name);
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
                disabledObjects.Add(go);
            }
        }
    }

    private void RestoreGameUI()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetHUDVisibility(true);
        }

        foreach (var c in disabledCanvases)
        {
            if (c != null) c.enabled = true;
        }
        disabledCanvases.Clear();

        foreach (var go in disabledObjects)
        {
            if (go != null) go.SetActive(true);
        }
        disabledObjects.Clear();

        if (PersistentCanvas.Instance != null)
        {
            var c = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (c != null) c.enabled = true;
        }
    }

    private void Update()
    {
        if (playerInside &&
            !triggered &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(SwitchDeathSequence());
        }
    }

    private IEnumerator SwitchDeathSequence()
    {
        triggered = true;

        // 1. Hide UI so the death visual has full screen focus
        HideGameUI();

        // 2. Ölüm görselini aç ve layer 20'ye (en üste) taşı
        if (switchImage != null)
        {
            ApplySortingOrder(switchImage);
            switchImage.SetActive(true);
        }

        // 3. Görsel 1.5 saniye tek başına görünsün
        yield return new WaitForSecondsRealtime(imageDuration);

        // 4. Görsel HALA açıkken rewind başlasın
        if (TimeRewindController.Instance != null)
        {
            TimeRewindController.Instance.PlayTimeRewind(
                "ELECTRICAL HAZARD: You used the light switch after the earthquake while a gas leak was present.",
                5f,

                onRewindStateRestore: () =>
                {
                    // Ekran tamamen karardıktan sonra resmi kapat
                    if (switchImage != null)
                    {
                        switchImage.SetActive(false);
                    }

                    // Restore UI when the time loop resets
                    RestoreGameUI();

                    triggered = false;
                },

                onComplete: () =>
                {
                    RestoreGameUI();
                    Debug.Log("Switch death rewind completed.");
                }
            );
        }
        else
        {
            if (switchImage != null)
            {
                switchImage.SetActive(false);
            }

            RestoreGameUI();
            triggered = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }
}