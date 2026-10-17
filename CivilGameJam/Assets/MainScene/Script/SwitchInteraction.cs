using UnityEngine;
using UnityEngine.InputSystem;

public class SwitchInteraction : MonoBehaviour
{
    [SerializeField] private GameObject switchImage;

    private bool playerInside = false;

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
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            switchImage.SetActive(true);
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