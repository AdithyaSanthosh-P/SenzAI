

using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [Header("Target")]
    public GameObject targetPrefab;
    public int        spawnCount = 5;

    [Header("Map Reference  (auto-found if left empty)")]
    [Tooltip("Drag in the MapGenerator object, or leave empty to find it automatically")]
    public MapGenerator mapGenerator;

    [Header("Manual Bounds  (used ONLY when no MapGenerator is found)")]
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

    void Start()
    {
        ResolveBounds();
        SpawnAll();
    }

    private void ResolveBounds()
    {
        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapGenerator != null &&
            mapGenerator.SpawnBoundsMax != mapGenerator.SpawnBoundsMin)
        {
            boundsMin = mapGenerator.SpawnBoundsMin;
            boundsMax = mapGenerator.SpawnBoundsMax;
            Debug.Log($"[TargetSpawner] Using map bounds: {boundsMin} → {boundsMax}");
        }
        else
        {
            boundsMin = manualBoundsMin;
            boundsMax = manualBoundsMax;
            Debug.LogWarning("[TargetSpawner] MapGenerator not found or bounds not set — using manual bounds.");
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