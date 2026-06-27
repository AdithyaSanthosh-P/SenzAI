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

    // last-frame mouse delta magnitude — ShootingController samples this at fire time
    private float lastMouseSpeed;

    public float CurrentPitch => xRotation;

    // public read so ShootingController can log it per shot
    public float CurrentSensitivity => sensitivity;

    // magnitude of mouse delta from the previous frame (pixels-ish)
    public float CurrentMouseSpeed => lastMouseSpeed;

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
        lastMouseSpeed = mouseDelta.magnitude;

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

    // called by SensitivityUI when the player hits "Apply"
    public void SetSensitivity(float newSens)
    {
        sensitivity = Mathf.Clamp(newSens, 0.01f, 2f);
    }

    // called by SensitivityUI.OpenPanel() to guarantee cursor is free,
    // regardless of which script's Update() runs first on the ESC frame
    public void ForceUnlockCursor() => UnlockCursor();

    private void HandleCursor()
    {
        if (Mouse.current == null)
            return;

        // SensitivityUI owns the ESC toggle — don't duplicate it here.
        // Only re-lock when: cursor is free AND the panel is closed AND player clicks in the world.
        if (!cursorLocked &&
            !SensitivityUI.PanelOpen &&
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