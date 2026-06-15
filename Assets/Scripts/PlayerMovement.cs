using UnityEngine;
using UnityEngine.InputSystem;

// handles movement and jumping stuff
// uses CharacterController for physics (change the values for snappiness)

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;

    private Vector3 velocity;
    private bool isSprinting;

    public bool IsSprinting => isSprinting;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        if (Keyboard.current == null)
            return;

        bool grounded = controller.isGrounded;
        // need to keep velocity.y negative so we stay grounded
        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        float x = 0;
        float z = 0;

        if (Keyboard.current.aKey.isPressed) x--;
        if (Keyboard.current.dKey.isPressed) x++;

        if (Keyboard.current.sKey.isPressed) z--;
        if (Keyboard.current.wKey.isPressed) z++;

        Vector3 move = (transform.right * x + transform.forward * z).normalized;

        isSprinting =
            grounded &&
            Keyboard.current.leftShiftKey.isPressed &&
            z > 0;

        float speed = isSprinting ? sprintSpeed : walkSpeed;

        controller.Move(move * speed * Time.deltaTime);

        if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(Vector3.up * velocity.y * Time.deltaTime);
    }
}