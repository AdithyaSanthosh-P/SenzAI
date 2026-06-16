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

    [SerializeField] private float respawnDelay = 0.5f;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;

    public MovementPattern pattern = MovementPattern.Stationary;

    public float moveSpeed = 1.5f;
    public float moveDistance = 2f;

    private Renderer rend;
    private Color startColor;

    private float flashTimer;

    private Vector3 startPos;

    private float bobTime;
    private float orbitAngle;

    private Vector3 wanderTarget;
    private float wanderTimer;

    private const float wanderInterval = 2f;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (rend != null)
        {
            startColor = rend.material.color;
        }

        startPos = transform.position;

        wanderTarget = startPos;

        orbitAngle = Random.Range(0f, 360f);
    }

    void Update()
    {
        HandleFlash();
        HandleMovement();
    }

    public void OnTargetHit()
    {
        Debug.Log("Hit " + gameObject.name);

        flashTimer = hitFlashDuration;

        Invoke(nameof(ResetColor), respawnDelay);
    }

    void HandleFlash()
    {
        if (flashTimer <= 0f)
            return;

        flashTimer -= Time.deltaTime;

        float t = flashTimer / hitFlashDuration;

        if (rend != null)
        {
            rend.material.color = Color.Lerp(startColor, hitColor, t);
        }
    }

    void ResetColor()
    {
        if (rend != null)
        {
            rend.material.color = startColor;
        }
    }

    void HandleMovement()
    {
        if (pattern == MovementPattern.Stationary)
            return;

        if (pattern == MovementPattern.Bob)
        {
            Bob();
        }
        else if (pattern == MovementPattern.Orbit)
        {
            Orbit();
        }
        else if (pattern == MovementPattern.Wander)
        {
            Wander();
        }
    }

    void Bob()
    {
        bobTime += Time.deltaTime * moveSpeed;

        float y = startPos.y + Mathf.Sin(bobTime) * moveDistance;

        transform.position = new Vector3(
            startPos.x,
            y,
            startPos.z
        );
    }

    void Orbit()
    {
        orbitAngle += moveSpeed * Time.deltaTime * 60f;

        float rad = orbitAngle * Mathf.Deg2Rad;

        float x = startPos.x + Mathf.Cos(rad) * moveDistance;
        float z = startPos.z + Mathf.Sin(rad) * moveDistance;

        transform.position = new Vector3(
            x,
            startPos.y,
            z
        );
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
                startPos.z + rand.y
            );

            wanderTimer = wanderInterval;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            wanderTarget,
            moveSpeed * Time.deltaTime
        );
    }
}