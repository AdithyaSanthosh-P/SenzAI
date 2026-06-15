using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraHolder;

    [Header("Mouse Settings")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float maxLookAngle = 90f;
    [SerializeField] private bool invertY = false;

    private float xRotation;
    private bool cursorLocked = true;

    public float CurrentPitch => xRotation;

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        HandleCursor();
        Look();
    }

    private void Look()
    {
        if (!cursorLocked)
            return;

        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * sensitivity;
        float mouseY = mouseDelta.y * sensitivity;

        if (!invertY)
            mouseY = -mouseY;

        xRotation += mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        transform.Rotate(Vector3.up * mouseX);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation =
                Quaternion.Euler(xRotation, 0f, 0f);
        }
    }

    private void HandleCursor()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (!cursorLocked &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }
}