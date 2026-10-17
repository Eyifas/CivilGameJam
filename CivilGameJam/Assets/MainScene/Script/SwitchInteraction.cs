using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwitchInteraction : MonoBehaviour
{
    [SerializeField] private GameObject switchImage;

    [Header("Death Settings")]
    [SerializeField] private float imageDuration = 1.5f;

    private bool playerInside = false;
    private bool triggered = false;

    private void Start()
    {
        if (switchImage != null)
        {
            switchImage.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInside &&
            !triggered &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(SwitchDeathSequence());
        }
    }

    private IEnumerator SwitchDeathSequence()
    {
        triggered = true;

        // Ölüm görselini aç
        if (switchImage != null)
        {
            switchImage.SetActive(true);
        }

        // Görsel 1.5 saniye tek başına görünsün
        yield return new WaitForSecondsRealtime(imageDuration);

        // Görsel HALA açıkken rewind başlasın
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

                    triggered = false;
                },

                onComplete: () =>
                {
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