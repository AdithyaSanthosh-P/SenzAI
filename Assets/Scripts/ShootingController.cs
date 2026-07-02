using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    public float maxRange = 500f;
    public LayerMask hitMask = ~0;

    [Header("Tracer")]
    public Material tracerMaterial;                          // leave empty — auto-created at runtime
    [Tooltip("Width of the tracer at the muzzle end.")]
    public float tracerWidth    = 0.008f;                   // thin like a real bullet streak
    [Tooltip("How long the tracer takes to fade out.")]
    public float tracerLifetime = 0.12f;                    // slightly longer so it’s visible
    [Tooltip("Colour of the tracer streak.")]
    public Color tracerColor    = new Color(1f, 0.95f, 0.7f, 1f);  // warm yellow-white
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
        GameObject tracerObj = new GameObject("Tracer");
        LineRenderer line    = tracerObj.AddComponent<LineRenderer>();

        // ── Material ──────────────────────────────────────────────────────────
        // Use the assigned material if any; otherwise build a simple transparent one.
        // "Sprites/Default" is guaranteed to exist in every Unity project and supports alpha.
        if (tracerMaterial != null)
        {
            line.material = new Material(tracerMaterial); // instance so we can tint per-shot
        }
        else
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            line.material       = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
            line.material.color = tracerColor;
        }

        // ── Shape ─────────────────────────────────────────────────────────────
        line.positionCount    = 2;
        line.useWorldSpace    = true;
        line.startWidth       = tracerWidth;          // wider at muzzle
        line.endWidth         = tracerWidth * 0.05f;  // tapers to near-nothing at hit
        line.startColor       = tracerColor;
        line.endColor         = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);

        // TextureMode.Stretch makes the texture run the full length (clean look).
        // Alignment.View always faces the camera so it looks like a thin streak,
        // not a flat plane that vanishes edge-on.
        line.textureMode      = LineTextureMode.Stretch;
        line.alignment        = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows   = false;
        line.generateLightingData = false;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        StartCoroutine(FadeTracer(line, tracerObj));
    }

    // Smoothly shrinks and fades the tracer line over its lifetime, then destroys it.
    private IEnumerator FadeTracer(LineRenderer line, GameObject tracerObj)
    {
        if (line == null || tracerObj == null) yield break;

        Color   startColour = tracerColor;
        Color   endColour   = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);
        float   startWidth  = tracerWidth;
        float   elapsed     = 0f;

        while (elapsed < tracerLifetime)
        {
            if (line == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / tracerLifetime);

            // Ease-out fade: fast initial flash, slow tail
            float fadeT = 1f - (1f - t) * (1f - t);

            line.startColor = Color.Lerp(startColour, endColour, fadeT);
            line.endColor   = Color.Lerp(endColour,   endColour, fadeT);   // tip always fades
            line.startWidth = Mathf.Lerp(startWidth, 0f, fadeT);
            line.endWidth   = line.startWidth * 0.05f;

            yield return null;
        }

        if (tracerObj != null) Destroy(tracerObj);
    }
}