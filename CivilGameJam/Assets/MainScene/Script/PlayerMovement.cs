using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 8f;
    private Vector2 movement;

    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
    }

    private void Update()
    {
        Vector3 direction = new Vector3(movement.x, movement.y, 0f);
        transform.position += direction * speed * Time.deltaTime;
    }
}