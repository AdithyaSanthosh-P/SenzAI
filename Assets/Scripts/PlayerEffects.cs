using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Head Bob")]
    [SerializeField] private bool enableHeadBob = false;

    [Header("Landing Tilt")]
    [SerializeField] private bool enableLandingTilt = false;

    [Header("Sprint FOV")]
    [SerializeField] private bool enableSprintFov = false;

    [Header("Footsteps")]
    [SerializeField] private bool enableFootsteps = false;

    private PlayerMovement movement;
    private MouseLook mouseLook;

    private void Start()
    {
        movement = GetComponent<PlayerMovement>();
        mouseLook = GetComponent<MouseLook>();
    }


    private void Update()
    {
        if (enableHeadBob)
        {
            HandleHeadBob();
        }

        if (enableLandingTilt)
        {
            HandleLandingTilt();
        }

        if (enableSprintFov)
        {
            HandleSprintFov();
        }

        if (enableFootsteps)
        {
            HandleFootsteps();
        }
    }
    //implement after ml work
    private void HandleHeadBob()
    {
        // TODO
    }

    private void HandleLandingTilt()
    {
        // TODO
    }

    private void HandleSprintFov()
    {
        // TODO
    }

    private void HandleFootsteps()
    {
        // TODO
    }
}