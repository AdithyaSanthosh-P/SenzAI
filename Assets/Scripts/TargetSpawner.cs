

using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [Header("Target")]
    public GameObject targetPrefab;
    public int        spawnCount = 5;

    [Header("Map Reference  (auto-found if left empty)")]
    [Tooltip("Drag in the MapGenerator object, or leave empty to find it automatically")]
    public MapGenerator mapGenerator;

    [Tooltip("If true, ignores the MapGenerator and strictly uses the Manual Bounds below.")]
    public bool useManualBounds = false;

    [Header("Manual Bounds")]
    public Vector3 manualBoundsMin = new Vector3(-4f, 0.5f,  3f);
    public Vector3 manualBoundsMax = new Vector3( 4f, 4.0f, 14f);

    [Header("Spawn Rules")]
    [Tooltip("Minimum distance between any two targets")]
    public float minSeparation = 1.2f;

    [Tooltip("How many random attempts before giving up on separation")]
    public int maxAttempts = 30;


    private GameObject[] targets;
    private Vector3      boundsMin;
    private Vector3      boundsMax;

    public static TargetSpawner Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResolveBounds();
        SpawnAll();
    }

    private void ResolveBounds()
    {
        if (useManualBounds)
        {
            boundsMin = manualBoundsMin;
            boundsMax = manualBoundsMax;
            Debug.Log("[TargetSpawner] Using strictly manual bounds.");
            return;
        }

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapGenerator != null)
        {
            // Always compute live from the map's current dimensions —
            // never read pre-baked serialized fields that may be stale.
            float halfX  = mapGenerator.arenaWidth  * 0.5f - 0.8f;  // inset from side walls
            float zNear  = 2.0f;                                      // don't spawn right behind player
            float zFar   = mapGenerator.arenaLength - 1.5f;          // inset from target wall
            float yLow   = 0.5f;
            float yHigh  = mapGenerator.wallHeight  - 0.6f;

            boundsMin = new Vector3(-halfX, yLow,  zNear);
            boundsMax = new Vector3( halfX, yHigh, zFar);
            Debug.Log($"[TargetSpawner] Live map bounds: {boundsMin} to {boundsMax}");
        }
        else
        {
            boundsMin = manualBoundsMin;
            boundsMax = manualBoundsMax;
            Debug.LogWarning("[TargetSpawner] No MapGenerator found — using manual bounds.");
        }
    }

    private void SpawnAll()
    {
        if (targetPrefab == null)
        {
            Debug.LogError("[TargetSpawner] No target prefab assigned!");
            return;
        }

        targets = new GameObject[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 pos = FindOpenPosition(i);
            GameObject obj = Instantiate(targetPrefab, pos, Quaternion.identity);
            obj.name = $"Target_{i + 1}";
            targets[i] = obj;
        }

        Debug.Log($"[TargetSpawner] Spawned {spawnCount} targets inside the room.");
    }

    private Vector3 FindOpenPosition(int placedCount)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = RandomPointInBounds();

            bool tooClose = false;
            for (int j = 0; j < placedCount; j++)
            {
                if (targets[j] != null &&
                    Vector3.Distance(candidate, targets[j].transform.position) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                return candidate;
        }

        return RandomPointInBounds();
    }

    private Vector3 RandomPointInBounds()
    {
        return new Vector3(
            Random.Range(boundsMin.x, boundsMax.x),
            Random.Range(boundsMin.y, boundsMax.y),
            Random.Range(boundsMin.z, boundsMax.z));
    }

    public Vector3 GetRespawnPosition(GameObject targetToRespawn)
    {
        if (targets == null) return RandomPointInBounds();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = RandomPointInBounds();
            bool tooClose = false;

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && targets[i] != targetToRespawn &&
                    Vector3.Distance(candidate, targets[i].transform.position) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                return candidate;
        }

        return RandomPointInBounds();
    }

    public void RespawnAll()
    {
        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].transform.position = FindOpenPosition(i);
        }
    }

    private void OnDrawGizmosSelected()
    {
        ResolveBounds();
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        Vector3 center = (boundsMin + boundsMax) * 0.5f;
        Vector3 size   = boundsMax - boundsMin;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}