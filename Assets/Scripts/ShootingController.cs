using UnityEngine;

public class ShootingController : MonoBehaviour
{
    [Header("Shooting")]
    public float maxRange = 500f;
    public LayerMask hitMask = ~0;

    [Header("Tracer")]
    public Material tracerMaterial;
    public float tracerWidth = 0.03f;
    public float tracerLifetime = 0.05f;

    [Header("Debug")]
    public bool drawDebugRay = true;
    public float debugRayDuration = 0.5f;

    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
            Fire();
    }

    private void Fire()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        Vector3 start = ray.origin;
        Vector3 dir = ray.direction;

        RaycastHit hit;
        bool didHit = Physics.Raycast(start, dir, out hit, maxRange, hitMask);

        Vector3 end = didHit ? hit.point : start + dir * maxRange;

        CreateTracer(start, end);

        if (drawDebugRay)
        {
            Debug.DrawLine(start, end, didHit ? Color.red : Color.green, debugRayDuration);
        }

        if (didHit)
        {
            Debug.Log($"Hit {hit.collider.name} at {hit.distance:F1}m");

            TargetController target = hit.collider.GetComponent<TargetController>();
            if (target != null)
                target.OnTargetHit();
        }
        else
        {
            Debug.Log("Miss");
        }
    }

    private void CreateTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = new GameObject("Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();

        line.material = tracerMaterial;
        line.startWidth = tracerWidth;
        line.endWidth = tracerWidth * 0.5f;
        line.positionCount = 2;
        line.useWorldSpace = true;

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Destroy(tracer, tracerLifetime);
    }
}