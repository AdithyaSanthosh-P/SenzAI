using UnityEngine;
using UnityEngine.InputSystem;

// PlayerEffects.cs
// Head bob, landing tilt, sprint FOV, footsteps.
//
// IMPORTANT — conflict notes:
//   MouseLook owns cameraHolder (pitch rotation) every frame.
//   PlayerEffects must NEVER write to cameraHolder.localPosition or
//   cameraHolder.localEulerAngles, or the two scripts will fight and
//   cause cursor/camera lock issues.
//
//   Solution: this script only moves a dedicated BobPivot child that
//   sits between cameraHolder and the Camera. Create it in the Inspector
//   or let Start() create it automatically.
//
// Hierarchy should look like:
//   Player
//     └─ CameraHolder          ← MouseLook writes pitch here
//          └─ BobPivot         ← PlayerEffects writes bob + tilt here
//               └─ Main Camera ← renders the scene

public class PlayerEffects : MonoBehaviour
{
    // ── Head Bob ────────────────────────────────────────────────────────────
    [Header("Head Bob")]
    [SerializeField] private bool  enableHeadBob = true;
    [SerializeField] private float bobFrequency  = 10f;   // cycles per second while walking
    [SerializeField] private float bobAmplitude  = 0.04f; // metres up/down
    [SerializeField] private float bobReturnSpeed = 12f;  // how fast it centres when still

    // ── Landing Tilt ────────────────────────────────────────────────────────
    [Header("Landing Tilt")]
    [SerializeField] private bool  enableLandingTilt    = true;
    [SerializeField] private float landingTiltDegrees   = 3f;   // Z-roll kick on landing
    [SerializeField] private float landingRecoverSpeed  = 9f;

    // ── Sprint FOV ──────────────────────────────────────────────────────────
    [Header("Sprint FOV")]
    [SerializeField] private bool  enableSprintFov = true;
    [SerializeField] private float sprintFovAdd    = 10f;  // added to base FOV while sprinting
    [SerializeField] private float fovLerpSpeed    = 8f;

    // ── Footsteps ───────────────────────────────────────────────────────────
    [Header("Footsteps")]
    [SerializeField] private bool        enableFootsteps   = true;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float       walkStepInterval  = 0.48f;
    [SerializeField] private float       sprintStepInterval = 0.30f;

    // ── References ──────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Optional — if left empty, Start() creates a BobPivot child automatically.")]
    [SerializeField] private Transform bobPivot;

    // ── Private state ────────────────────────────────────────────────────────
    private PlayerMovement     movement;
    private CharacterController controller;
    private Camera             cam;

    private float baseFov;
    private float bobTimer;
    private float footstepTimer;
    private bool  wasGrounded;
    private float landingTilt;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        movement   = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        cam        = Camera.main;

        if (cam != null)
            baseFov = cam.fieldOfView;

        // Auto-create a BobPivot child between CameraHolder and the Camera
        // so we never touch the transform MouseLook owns.
        if (bobPivot == null && cam != null)
        {
            GameObject pivot = new GameObject("BobPivot");

            // Insert: CameraHolder → BobPivot → Camera
            pivot.transform.SetParent(cam.transform.parent, worldPositionStays: false);
            cam.transform.SetParent(pivot.transform, worldPositionStays: false);

            bobPivot = pivot.transform;
        }
    }

    private void Update()
    {
        if (enableHeadBob)    HandleHeadBob();
        if (enableLandingTilt) HandleLandingTilt();
        if (enableSprintFov)  HandleSprintFov();
        if (enableFootsteps)  HandleFootsteps();
    }

    // ── Head Bob ──────────────────────────────────────────────────────────────

    private void HandleHeadBob()
    {
        if (bobPivot == null) return;

        bool moving  = controller != null && controller.isGrounded &&
                       controller.velocity.magnitude > 0.1f;

        Vector3 targetOffset;

        if (moving)
        {
            bobTimer += Time.deltaTime * bobFrequency;
            // Sine on Y, half-amplitude cosine on X for a figure-8 feel
            float bobY = Mathf.Sin(bobTimer)             * bobAmplitude;
            float bobX = Mathf.Sin(bobTimer * 0.5f)      * bobAmplitude * 0.35f;
            targetOffset = new Vector3(bobX, bobY, 0f);
        }
        else
        {
            bobTimer = 0f;      // reset so bob restarts smoothly
            targetOffset = Vector3.zero;
        }

        bobPivot.localPosition = Vector3.Lerp(
            bobPivot.localPosition,
            targetOffset,
            Time.deltaTime * bobReturnSpeed);
    }

    // ── Landing Tilt ──────────────────────────────────────────────────────────
    // Uses Z-rotation (roll) on BobPivot — never touches the pitch axis
    // that MouseLook writes, so there is no conflict.

    private void HandleLandingTilt()
    {
        if (bobPivot == null) return;

        bool grounded = controller != null && controller.isGrounded;

        if (!wasGrounded && grounded)
            landingTilt = landingTiltDegrees;   // kick on landing

        wasGrounded = grounded;

        landingTilt = Mathf.Lerp(landingTilt, 0f, Time.deltaTime * landingRecoverSpeed);

        // Apply only the Z-roll; leave X and Y alone (MouseLook owns those)
        Vector3 angles = bobPivot.localEulerAngles;
        angles.z = landingTilt;
        bobPivot.localEulerAngles = angles;
    }

    // ── Sprint FOV ────────────────────────────────────────────────────────────
    // Uses the new Input System — consistent with PlayerMovement and MouseLook.

    private void HandleSprintFov()
    {
        if (cam == null) return;

        // Read sprint state from PlayerMovement so there's a single source of truth
        bool sprinting = movement != null && movement.IsSprinting;

        float targetFov = sprinting ? baseFov + sprintFovAdd : baseFov;

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFov,
            Time.deltaTime * fovLerpSpeed);
    }

    // ── Footsteps ─────────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
            return;

        bool grounded = controller != null && controller.isGrounded;
        bool moving   = grounded && controller.velocity.magnitude > 0.1f;

        if (!moving)
        {
            footstepTimer = 0f;
            return;
        }

        bool   sprinting = movement != null && movement.IsSprinting;
        float  interval  = sprinting ? sprintStepInterval : walkStepInterval;

        footstepTimer += Time.deltaTime;

        if (footstepTimer >= interval)
        {
            footstepTimer = 0f;
            int idx = Random.Range(0, footstepClips.Length);
            footstepSource.PlayOneShot(footstepClips[idx]);
        }
    }
}