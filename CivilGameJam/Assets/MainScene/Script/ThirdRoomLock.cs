using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdRoomLock : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Collider2D blockingCollider;

    [Header("Interaction")]
    [SerializeField] private GameObject ePrompt;

    private bool playerInside = false;
    private bool doorUnlocked = false;

    private void Awake()
    {
        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        if (ePrompt != null)
        {
            ePrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameTimelineManager.Instance == null)
            return;

        GameStage stage = GameTimelineManager.Instance.CurrentStage;

        bool earthquakeFinished =
            stage == GameStage.Stage3_Utilities ||
            stage == GameStage.Stage4_Stairwell ||
            stage == GameStage.Stage5_StreetFinale ||
            stage == GameStage.Victory;

        // Depremden önce kapıyla etkileşim yok
        if (!earthquakeFinished || doorUnlocked)
        {
            if (ePrompt != null)
                ePrompt.SetActive(false);

            return;
        }

        // Depremden sonra oyuncu kapının yanındaysa E göster
        if (ePrompt != null)
        {
            ePrompt.SetActive(playerInside);
        }

        if (playerInside &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryUnlockDoor();
        }
    }

    private void TryUnlockDoor()
    {
        if (!InventoryManager.HasItem("Anahtar"))
        {
            Debug.Log("Kapı kilitli. Anahtara ihtiyacın var.");
            return;
        }

        doorUnlocked = true;

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        if (ePrompt != null)
        {
            ePrompt.SetActive(false);
        }

        // Anahtar kullanıldıktan sonra envanterden silinsin
        InventoryManager.RemoveItem("Anahtar");

        Debug.Log("Kapının kilidi açıldı!");
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

            if (ePrompt != null)
            {
                ePrompt.SetActive(false);
            }
        }
    }
}