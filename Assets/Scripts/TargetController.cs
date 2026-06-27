using UnityEngine;

public class TargetController : MonoBehaviour
{
    public enum MovementPattern
    {
        Stationary,
        Bob,
        Orbit,
        Wander
    }

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 0.8f;

    [Header("Respawn Bounds")]
    [SerializeField] private float minRespawnX = -10f;
    [SerializeField] private float maxRespawnX =  10f;
    [SerializeField] private float minRespawnZ =  30f;
    [SerializeField] private float maxRespawnZ =  55f;
    [SerializeField] private float minRespawnY =   1f;
    [SerializeField] private float maxRespawnY =   4f;

    [Header("Hit Effect")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] private float reappearFlashDuration = 0.12f;

    [Header("Audio")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 0.85f;
    [SerializeField] private AudioClip respawnSound;
    [SerializeField] [Range(0f, 1f)] private float respawnVolume = 0.5f;

    [Header("Movement")]
    public MovementPattern pattern = MovementPattern.Stationary;
    public float moveSpeed = 1.5f;
    public float moveDistance = 2f;

    // ── private state ─────────────────────────────────────────────────
    private Renderer rend;
    private Color startColor;
    private AudioSource audioSource;

    private bool isHidden = false;
    private float reappearFlashTimer = 0f;

    private Vector3 startPos;
    private float bobTime;
    private float orbitAngle;
    private Vector3 wanderTarget;
    private float wanderTimer;
    private const float wanderInterval = 2f;

    // timestamp (Time.time) when this target last became visible — used for reaction time calc
    public float BecameVisibleTime { get; private set; } = -1f;

    // ── lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            startColor = rend.material.color;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance  = 80f;
        audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;

        startPos     = transform.position;
        wanderTarget = startPos;
        orbitAngle   = Random.Range(0f, 360f);

        // record the initial spawn time as visible-from time
        BecameVisibleTime = Time.time;
    }

    void Update()
    {
        // Freeze target movement and flash timers while ESC panel is open
        if (SensitivityUI.PanelOpen) return;

        HandleReappearFlash();

        if (!isHidden)
            HandleMovement();
    }

    // ── public API ────────────────────────────────────────────────────

    public void OnTargetHit()
    {
        if (isHidden) return;

        Debug.Log("Hit " + gameObject.name);

        SetVisible(false);
        isHidden = true;

        PlaySound(hitSound, hitVolume, Random.Range(0.92f, 1.08f));

        Invoke(nameof(DoRespawn), respawnDelay);
    }

    // ── internal ──────────────────────────────────────────────────────

    private void PlaySound(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch  = pitch;
        audioSource.volume = volume;
        audioSource.PlayOneShot(clip);
    }

    private void SetVisible(bool visible)
    {
        if (rend != null)
            rend.enabled = visible;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = visible;
    }

    private void DoRespawn()
    {
        Respawn();

        SetVisible(true);
        isHidden = false;
        reappearFlashTimer = reappearFlashDuration;

        if (rend != null)
            rend.material.color = hitFlashColor;

        PlaySound(respawnSound, respawnVolume);


        BecameVisibleTime = Time.time;
    }

    private void HandleReappearFlash()
    {
        if (reappearFlashTimer <= 0f) return;

        reappearFlashTimer -= Time.deltaTime;

        if (rend != null)
        {
            float t = reappearFlashTimer / reappearFlashDuration;
            rend.material.color = Color.Lerp(startColor, hitFlashColor, t);
        }

        if (reappearFlashTimer <= 0f && rend != null)
            rend.material.color = startColor;
    }

    private void Respawn()
    {
        float x = Random.Range(minRespawnX, maxRespawnX);
        float z = Random.Range(minRespawnZ, maxRespawnZ);
        float y = Random.Range(minRespawnY, maxRespawnY);

        Vector3 newPos = new Vector3(x, y, z);
        transform.position = newPos;

        startPos     = newPos;
        bobTime      = 0f;
        orbitAngle   = Random.Range(0f, 360f);
        wanderTarget = newPos;
        wanderTimer  = 0f;
    }

    

    void HandleMovement()
    {
        switch (pattern)
        {
            case MovementPattern.Bob:    Bob();    break;
            case MovementPattern.Orbit:  Orbit();  break;
            case MovementPattern.Wander: Wander(); break;
        }
    }

    void Bob()
    {
        bobTime += Time.deltaTime * moveSpeed;
        float y = startPos.y + Mathf.Sin(bobTime) * moveDistance;
        transform.position = new Vector3(startPos.x, y, startPos.z);
    }

    void Orbit()
    {
        orbitAngle += moveSpeed * Time.deltaTime * 60f;
        float rad = orbitAngle * Mathf.Deg2Rad;
        float x   = startPos.x + Mathf.Cos(rad) * moveDistance;
        float z   = startPos.z + Mathf.Sin(rad) * moveDistance;
        transform.position = new Vector3(x, startPos.y, z);
    }

    void Wander()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            Vector2 rand = Random.insideUnitCircle * moveDistance;
            wanderTarget = new Vector3(
                startPos.x + rand.x,
                startPos.y,
                startPos.z + rand.y);
            wanderTimer = wanderInterval;
        }
        transform.position = Vector3.MoveTowards(
            transform.position, wanderTarget, moveSpeed * Time.deltaTime);
    }
}