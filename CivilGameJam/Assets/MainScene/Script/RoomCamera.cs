using UnityEngine;

public class RoomCamera : MonoBehaviour
{
    public Transform player;

    [SerializeField] private float shakeDuration = 3f;
    [SerializeField] private float shakeStrength = 0.15f;

    private const float roomWidth = 19.2f;
    private const float leftEdge = -38.4f;

    private float shakeTimeRemaining;
    private float initialY;
    private float initialZ;

    private void Awake()
    {
        initialY = transform.position.y;
        initialZ = transform.position.z;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        int room = Mathf.FloorToInt(
            (player.position.x - leftEdge) / roomWidth
        );

        room = Mathf.Clamp(room, 0, 3);

        float cameraX = leftEdge + roomWidth * (room + 0.5f);

        Vector2 shakeOffset = Vector2.zero;

        if (shakeTimeRemaining > 0f)
        {
            shakeOffset = Random.insideUnitCircle * shakeStrength;
            shakeTimeRemaining -= Time.deltaTime;
        }

        transform.position = new Vector3(
            cameraX + shakeOffset.x,
            initialY + shakeOffset.y,
            initialZ
        );
    }

    [ContextMenu("Test Earthquake")]
    public void StartEarthquake()
    {
        shakeTimeRemaining = shakeDuration;
    }

    public void StopEarthquake()
    {
        shakeTimeRemaining = 0f;
        if (player != null)
        {
            int room = Mathf.FloorToInt((player.position.x - leftEdge) / roomWidth);
            room = Mathf.Clamp(room, 0, 3);
            float cameraX = leftEdge + roomWidth * (room + 0.5f);
            transform.position = new Vector3(cameraX, initialY, initialZ);
        }
    }
}