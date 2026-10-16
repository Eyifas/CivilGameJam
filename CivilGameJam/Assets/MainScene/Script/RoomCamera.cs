using UnityEngine;

public class RoomCamera : MonoBehaviour
{
    public Transform player;

    private const float roomWidth = 19.2f;
    private const float leftEdge = -38.4f;

    private void LateUpdate()
    {
        if (player == null) return;

        int room = Mathf.FloorToInt(
            (player.position.x - leftEdge) / roomWidth
        );

        room = Mathf.Clamp(room, 0, 3);

        float cameraX = leftEdge + roomWidth * (room + 0.5f);

        transform.position = new Vector3(
            cameraX,
            transform.position.y,
            transform.position.z
        );
    }
}