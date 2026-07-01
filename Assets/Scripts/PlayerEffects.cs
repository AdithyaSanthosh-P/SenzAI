using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField] private float bobSpeed = 10f;
    [SerializeField] private float bobAmount = 0.04f;

    [Header("Landing Tilt")]
    [SerializeField] private bool enableLandingTilt = true;
    [SerializeField] private float landingAmount = 4f;
    [SerializeField] private float landingRecoverSpeed = 8f;

    [Header("Sprint FOV")]
    [SerializeField] private bool enableSprintFov = true;
    [SerializeField] private float normalFov = 90f;
    [SerializeField] private float sprintFov = 100f;
    [SerializeField] private float fovSpeed = 8f;

    [Header("Footsteps")]
    [SerializeField] private bool enableFootsteps = true;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepInterval = 0.45f;

    [Header("References")]
    [SerializeField] private Transform cameraHolder;

    private PlayerMovement movement;
    private CharacterController controller;
    private Camera cam;

    private Vector3 startLocalPos;
    private float footstepTimer;
    private bool wasGrounded;
    private float landingTilt;

    private void Start()
    {
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();

        if (cameraHolder == null)
            cameraHolder = Camera.main.transform;

        cam = Camera.main;
        startLocalPos = cameraHolder.localPosition;

        if (cam != null)
            normalFov = cam.fieldOfView;
    }

    private void Update()
    {
        if (enableHeadBob)
            HandleHeadBob();

        if (enableLandingTilt)
            HandleLandingTilt();

        if (enableSprintFov)
            HandleSprintFov();

        if (enableFootsteps)
            HandleFootsteps();
    }

    private void HandleHeadBob()
    {
        if (!controller.isGrounded || controller.velocity.magnitude < 0.1f)
        {
            cameraHolder.localPosition = Vector3.Lerp(
                cameraHolder.localPosition,
                startLocalPos,
                Time.deltaTime * bobSpeed);

            return;
        }

        float offset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        cameraHolder.localPosition = Vector3.Lerp(
            cameraHolder.localPosition,
            startLocalPos + Vector3.up * offset,
            Time.deltaTime * bobSpeed);
    }

    private void HandleLandingTilt()
    {
        if (!wasGrounded && controller.isGrounded)
        {
            landingTilt = landingAmount;
        }

        wasGrounded = controller.isGrounded;

        landingTilt = Mathf.Lerp(landingTilt, 0f, Time.deltaTime * landingRecoverSpeed);

        Vector3 rot = cameraHolder.localEulerAngles;
        rot.x -= landingTilt;
        cameraHolder.localEulerAngles = rot;
    }

    private void HandleSprintFov()
    {
        if (cam == null)
            return;

        bool sprinting =
            Input.GetKey(KeyCode.LeftShift) &&
            controller.velocity.magnitude > 0.1f;

        float target = sprinting ? sprintFov : normalFov;

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            target,
            Time.deltaTime * fovSpeed);
    }

    private void HandleFootsteps()
    {
        if (footstepSource == null || footstepClips.Length == 0)
            return;

        if (!controller.isGrounded || controller.velocity.magnitude < 0.1f)
        {
            footstepTimer = 0f;
            return;
        }

        footstepTimer += Time.deltaTime;

        if (footstepTimer >= footstepInterval)
        {
            footstepTimer = 0f;

            int index = Random.Range(0, footstepClips.Length);
            footstepSource.PlayOneShot(footstepClips[index]);
        }
    }
}