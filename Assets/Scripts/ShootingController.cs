using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    public float maxRange = 500f;
    public LayerMask hitMask = ~0;

    [Header("Tracer")]
    public Material tracerMaterial;
    [Tooltip("Width of the tracer core at its thickest point (tail).")]
    public float tracerWidth      = 0.004f;
    [Tooltip("Visual speed of the bullet in units/sec.")]
    public float bulletSpeed      = 350f;
    [Tooltip("Length of the visible streak segment in world units.")]
    public float trailLength      = 2.5f;
    [Tooltip("How fast the streak fades after reaching the hit point.")]
    public float tracerFadeTime   = 0.06f;
    [Tooltip("Base colour of the tracer.")]
    public Color tracerColor      = new Color(1f, 0.97f, 0.75f, 1f);

    [Header("Tracer Glow")]
    [Tooltip("HDR brightness of the core streak. Values > 1 trigger URP/HDRP Bloom automatically.")]
    [Range(1f, 8f)]
    public float tracerBrightness = 4f;
    [Tooltip("Draws a wide soft halo around the core streak (fake glow, works without post-processing).")]
    public bool  glowHalo         = true;
    [Tooltip("How many times wider the halo is vs the core.")]
    [Range(2f, 12f)]
    public float glowHaloScale    = 6f;
    [Tooltip("Opacity of the outer halo (0 = invisible, 1 = same as core).")]
    [Range(0f, 1f)]
    public float glowHaloAlpha    = 0.25f;
    [Tooltip("How far forward of the camera the muzzle sits (avoids disconnect).")]
    public float gunOffsetForward = 0.4f;
    [Tooltip("How far right of camera centre.")]
    public float gunOffsetRight   = 0.12f;
    [Tooltip("How far below camera centre.")]
    public float gunOffsetDown    = 0.10f;

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

        // Muzzle: pushed forward so the streak visually starts in front of you,
        // not right beside the camera eye. The raycast origin is unchanged.
        Vector3 muzzlePos = cam.transform.position
            + cam.transform.forward *  gunOffsetForward
            + cam.transform.right   *  gunOffsetRight
            + cam.transform.up      * -gunOffsetDown;

        StartCoroutine(AnimateTracer(muzzlePos, end));

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

    // Returns an additive-blended material.
    // Additive blending means the tracer's colour is ADDED to whatever is behind it,
    // so it looks luminous/bright regardless of background and naturally glows when
    // URP/HDRP Bloom is enabled (especially with tracerBrightness > 1).
    private Material BuildAdditiveMaterial()
    {
        if (tracerMaterial != null)
            return new Material(tracerMaterial);

        // Shader priority: Particles/Additive (Built-in) → URP Particles Unlit (URP)
        Shader sh = Shader.Find("Particles/Additive")
                 ?? Shader.Find("Legacy Shaders/Particles/Additive")
                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Sprites/Default");   // fallback (alpha-blend, not additive)

        var mat = new Material(sh);

        // If we landed on the Sprites/Default fallback, force additive blend manually
        // so at least the blending is correct even if the shader doesn't expose it.
        if (sh != null && sh.name.Contains("Sprites"))
        {
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        return mat;
    }

    // Sets up a LineRenderer that shares the same positions as the core tracer
    // but is wider and dimmer — creating a soft corona / glow halo effect.
    private LineRenderer BuildGlowLayer(Transform parent)
    {
        GameObject glowObj = new GameObject("TracerGlow");
        glowObj.transform.SetParent(parent, worldPositionStays: false);

        LineRenderer glowLR         = glowObj.AddComponent<LineRenderer>();
        glowLR.material             = BuildAdditiveMaterial();
        glowLR.positionCount        = 2;
        glowLR.useWorldSpace        = true;
        glowLR.textureMode          = LineTextureMode.Stretch;
        glowLR.alignment            = LineAlignment.View;
        glowLR.shadowCastingMode    = ShadowCastingMode.Off;
        glowLR.receiveShadows       = false;
        glowLR.generateLightingData = false;
        return glowLR;
    }

    // Animates a short bullet-streak from muzzle toward hitPoint.
    // A fixed-length tail segment chases the bullet head across the scene,
    // so it looks like a tracer round leaving the gun and traveling forward.
    private IEnumerator AnimateTracer(Vector3 muzzle, Vector3 hitPoint)
    {
        // ── Core streak ────────────────────────────────────────────────────────
        GameObject   go = new GameObject("TracerRoot");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material             = BuildAdditiveMaterial();
        lr.positionCount        = 2;
        lr.useWorldSpace        = true;
        lr.textureMode          = LineTextureMode.Stretch;
        lr.alignment            = LineAlignment.View;
        lr.shadowCastingMode    = ShadowCastingMode.Off;
        lr.receiveShadows       = false;
        lr.generateLightingData = false;
        lr.SetPosition(0, muzzle);
        lr.SetPosition(1, muzzle);

        // ── Glow halo (optional wider outer layer) ─────────────────────────────
        LineRenderer glowLR = glowHalo ? BuildGlowLayer(go.transform) : null;
        if (glowLR != null)
        {
            glowLR.SetPosition(0, muzzle);
            glowLR.SetPosition(1, muzzle);
        }

        // HDR core colour — brightness > 1 triggers URP Bloom automatically.
        // The alpha channel still controls fade; additive blending makes it luminous.
        Color coreColour = new Color(
            tracerColor.r * tracerBrightness,
            tracerColor.g * tracerBrightness,
            tracerColor.b * tracerBrightness,
            1f);
        Color coreEnd = new Color(coreColour.r, coreColour.g, coreColour.b, 0f);

        Color haloColour = new Color(
            tracerColor.r * tracerBrightness * glowHaloAlpha,
            tracerColor.g * tracerBrightness * glowHaloAlpha,
            tracerColor.b * tracerBrightness * glowHaloAlpha,
            1f);
        Color haloEnd = new Color(haloColour.r, haloColour.g, haloColour.b, 0f);

        // ── Travel phase ───────────────────────────────────────────────────────
        float   totalDist  = Mathf.Max(0.01f, Vector3.Distance(muzzle, hitPoint));
        Vector3 dir        = (hitPoint - muzzle) / totalDist;
        float   travelTime = totalDist / Mathf.Max(1f, bulletSpeed);
        float   elapsed    = 0f;

        while (elapsed < travelTime)
        {
            if (go == null) yield break;
            elapsed += Time.deltaTime;

            float   headDist = Mathf.Min(elapsed * bulletSpeed, totalDist);
            float   tailDist = Mathf.Max(0f, headDist - trailLength);
            Vector3 headPos  = muzzle + dir * headDist;
            Vector3 tailPos  = muzzle + dir * tailDist;

            // Core
            lr.startWidth = tracerWidth;
            lr.endWidth   = 0f;
            lr.startColor = coreColour;
            lr.endColor   = coreEnd;
            lr.SetPosition(0, tailPos);
            lr.SetPosition(1, headPos);

            // Halo
            if (glowLR != null)
            {
                glowLR.startWidth = tracerWidth * glowHaloScale;
                glowLR.endWidth   = 0f;
                glowLR.startColor = haloColour;
                glowLR.endColor   = haloEnd;
                glowLR.SetPosition(0, tailPos);
                glowLR.SetPosition(1, headPos);
            }

            yield return null;
        }

        // ── Fade phase ─────────────────────────────────────────────────────────
        float   fadeElapsed = 0f;
        Vector3 finalHead   = hitPoint;

        while (fadeElapsed < tracerFadeTime)
        {
            if (go == null) yield break;
            fadeElapsed += Time.deltaTime;

            float   t       = fadeElapsed / tracerFadeTime;
            float   td      = Mathf.Lerp(Mathf.Max(0f, totalDist - trailLength), totalDist, t);
            Vector3 tailPos = muzzle + dir * td;
            float   w       = 1f - t;   // width shrinks to 0

            // Core fade
            lr.startColor = new Color(coreColour.r, coreColour.g, coreColour.b, w);
            lr.endColor   = coreEnd;
            lr.startWidth = tracerWidth * w;
            lr.endWidth   = 0f;
            lr.SetPosition(0, tailPos);
            lr.SetPosition(1, finalHead);

            // Halo fade
            if (glowLR != null)
            {
                glowLR.startColor = new Color(haloColour.r, haloColour.g, haloColour.b, w);
                glowLR.endColor   = haloEnd;
                glowLR.startWidth = tracerWidth * glowHaloScale * w;
                glowLR.endWidth   = 0f;
                glowLR.SetPosition(0, tailPos);
                glowLR.SetPosition(1, finalHead);
            }

            yield return null;
        }

        if (go != null) Destroy(go);
    }
}