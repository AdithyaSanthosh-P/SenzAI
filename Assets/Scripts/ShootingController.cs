using UnityEngine;

public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    public float maxRange = 500f;
    public LayerMask hitMask = ~0;

    [Header("Tracer")]
    public Material tracerMaterial;
    public float tracerWidth    = 0.03f;
    public float tracerLifetime = 0.05f;
    [Tooltip("How far right of camera centre the tracer spawns (gun position).")]
    public float gunOffsetRight = 0.25f;
    [Tooltip("How far below camera centre the tracer spawns (gun position). Positive = down.")]
    public float gunOffsetDown  = 0.20f;

    [Header("Camera Shake")]
    public float shakeDuration  = 0.09f;
    public float shakeMagnitude = 0.045f;

    [Header("Gun Audio")]
    public AudioClip gunShotSound;
    [Range(0f, 1f)] public float gunShotVolume = 0.85f;
    public AudioClip gunMissSound;
    [Range(0f, 1f)] public float gunMissVolume = 0.6f;

    [Header("Debug")]
    public bool drawDebugRay      = true;
    public float debugRayDuration = 0.5f;

    private Camera cam;
    private AudioSource audioSource;
    private CameraShake cameraShake;
    private MouseLook mouseLook;
    private SensitivityCalibrator sensCalibrator;

    // session-level shot tracking
    private float lastShotTime = -1f;
    private int   sessionShotIndex = 0;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;

        Camera mainCam = cam != null ? cam : Camera.main;
        if (mainCam != null)
        {
            cameraShake = mainCam.GetComponent<CameraShake>();
            if (cameraShake == null)
                cameraShake = mainCam.gameObject.AddComponent<CameraShake>();
        }

        mouseLook = FindObjectOfType<MouseLook>();
        sensCalibrator = FindObjectOfType<SensitivityCalibrator>();
    }

    private void Update()
    {
        // Block firing while the esc panel is open
        if (SensitivityUI.PanelOpen) return;

        if (Input.GetButtonDown("Fire1"))
            Fire();
    }

    private void Fire()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 start = ray.origin;
        Vector3 dir   = ray.direction;

        RaycastHit hit;
        bool didHit = Physics.Raycast(start, dir, out hit, maxRange, hitMask);
        Vector3 end = didHit ? hit.point : start + dir * maxRange;

        float currentSens   = mouseLook != null ? mouseLook.CurrentSensitivity : -1f;
        float currentMSpeed = mouseLook != null ? mouseLook.CurrentMouseSpeed   : -1f;
        float timeSinceLastShot = lastShotTime < 0f ? -1f : Time.time - lastShotTime;
        int   shotIdx = sessionShotIndex;

        lastShotTime = Time.time;
        sessionShotIndex++;

        // Muzzle position: offset from camera in world space so the tracer looks like
        // it comes from a gun held at hip/chest level rather than straight from the eye.
        // The raycast still fires from the true camera centre so hit detection is unaffected.
        Vector3 muzzlePos = cam.transform.position
            + cam.transform.right   *  gunOffsetRight
            + cam.transform.up      * -gunOffsetDown;

        CreateTracer(muzzlePos, end);

        cameraShake?.Shake(shakeDuration, shakeMagnitude);

        if (drawDebugRay)
            Debug.DrawLine(start, end, didHit ? Color.red : Color.green, debugRayDuration);

        if (didHit)
        {
            TargetController target = hit.collider.GetComponent<TargetController>();

            if (target != null)
            {
                // 2D signed aim error — project to-target vector onto camera local axes
                Vector3 toTarget  = (target.transform.position - start).normalized;
                float deviation   = Vector3.Angle(dir, toTarget);
                float offsetX     = Mathf.Asin(Mathf.Clamp(Vector3.Dot(toTarget, cam.transform.right), -1f, 1f)) * Mathf.Rad2Deg;
                float offsetY     = Mathf.Asin(Mathf.Clamp(Vector3.Dot(toTarget, cam.transform.up),    -1f, 1f)) * Mathf.Rad2Deg;

                // apparent target size based on renderer bounds
                float angularSize = GetTargetAngularSize(target, hit.distance);

                // reaction time
                float becameVis = target.BecameVisibleTime;
                float reactTime = becameVis >= 0f ? Mathf.Max(0f, Time.time - becameVis) : -1f;

                target.OnTargetHit();
                PlayGunShot(isHit: true);

                SessionLogger.Instance?.LogShot(
                    hit: true, distance: hit.distance, targetName: hit.collider.name,
                    sensitivity: currentSens, aimDeviation: deviation, reactionTime: reactTime,
                    mouseSpeed: currentMSpeed, crosshairOffsetX: offsetX, crosshairOffsetY: offsetY,
                    timeSinceLastShot: timeSinceLastShot, shotIndex: shotIdx,
                    targetAngularSize: angularSize
                );

                // gp
                SensitivityOptimizer.Instance?.RecordShot(true, currentSens, deviation, reactTime, hit.distance);
                sensCalibrator?.OnShotFired();
            }
            else
            {
                // hit environment — no target reference
                PlayGunShot(isHit: false);

                SessionLogger.Instance?.LogShot(
                    hit: false, distance: -1f, targetName: "none",
                    sensitivity: currentSens, aimDeviation: -1f, reactionTime: -1f,
                    mouseSpeed: currentMSpeed, crosshairOffsetX: -999f, crosshairOffsetY: -999f,
                    timeSinceLastShot: timeSinceLastShot, shotIndex: shotIdx,
                    targetAngularSize: -1f
                );

                SensitivityOptimizer.Instance?.RecordShot(false, currentSens, -1f, -1f, -1f);
                sensCalibrator?.OnShotFired();
            }
        }
        else
        {
            PlayGunShot(isHit: false);

            SessionLogger.Instance?.LogShot(
                hit: false, distance: -1f, targetName: "none",
                sensitivity: currentSens, aimDeviation: -1f, reactionTime: -1f,
                mouseSpeed: currentMSpeed, crosshairOffsetX: -999f, crosshairOffsetY: -999f,
                timeSinceLastShot: timeSinceLastShot, shotIndex: shotIdx,
                targetAngularSize: -1f
            );

            SensitivityOptimizer.Instance?.RecordShot(false, currentSens, -1f, -1f, -1f);
            sensCalibrator?.OnShotFired();
        }

        FindObjectOfType<Crosshair>()?.OnShot(didHit);
    }

    private float GetTargetAngularSize(TargetController target, float distance)
    {
        if (distance <= 0f) return -1f;
        Renderer r = target.GetComponent<Renderer>();
        if (r == null) return -1f;
        float radius = r.bounds.extents.magnitude;
        return 2f * Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
    }

    private void PlayGunShot(bool isHit)
    {
        AudioClip clip   = (isHit || gunMissSound == null) ? gunShotSound : gunMissSound;
        float     volume = (isHit || gunMissSound == null) ? gunShotVolume : gunMissVolume;

        if (clip == null || audioSource == null) return;

        audioSource.pitch = Random.Range(0.94f, 1.06f);
        audioSource.PlayOneShot(clip, volume);
    }

    private void CreateTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = new GameObject("Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();

        line.material      = tracerMaterial;
        line.startWidth    = tracerWidth;
        line.endWidth      = tracerWidth * 0.5f;
        line.positionCount = 2;
        line.useWorldSpace = true;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Destroy(tracer, tracerLifetime);
    }
}