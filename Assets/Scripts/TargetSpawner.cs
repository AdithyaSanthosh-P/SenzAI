using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    public GameObject targetPrefab;

    public int spawnCount = 4;

    public float minDistance = 5f;
    public float maxDistance = 25f;

    public float spawnWidth = 8f;
    public float spawnHeight = 3f;

    public Transform player;

    private GameObject[] targets;

    void Start()
    {
        if (player == null && Camera.main != null)
        {
            player = Camera.main.transform;
        }

        targets = new GameObject[spawnCount];

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 pos = GetSpawnPos();

            GameObject obj = Instantiate(targetPrefab, pos, Quaternion.identity);

            obj.name = "Target_" + (i + 1);

            targets[i] = obj;
        }

        Debug.Log("Spawned " + spawnCount + " targets");
    }

    Vector3 GetSpawnPos()
    {
        float dist = Random.Range(minDistance, maxDistance);

        float x = Random.Range(-spawnWidth, spawnWidth);
        float y = Random.Range(-spawnHeight, spawnHeight);

        Vector3 start = Vector3.zero;

        if (player != null)
        {
            start = player.position;
        }

        Vector3 forward = player != null ? player.forward : Vector3.forward;

        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 pos = start +
                      forward * dist +
                      right * x +
                      Vector3.up * y;

        if (pos.y < 1f)
        {
            pos.y = 1f;
        }

        return pos;
    }

    public void RespawnAll()
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].transform.position = GetSpawnPos();
            }
        }
    }
}