using UnityEngine;
using UnityEngine.InputSystem;

public class GhostTestMovement : MonoBehaviour
{
    public float moveSpeed = 3f;

    void Update()
    {
        float movement = 0f;

        if (Keyboard.current.wKey.isPressed)
        {
            movement = 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            movement = -1f;
        }

        transform.Translate(
            Vector3.forward * movement * moveSpeed * Time.deltaTime,
            Space.Self
        );
    }
}