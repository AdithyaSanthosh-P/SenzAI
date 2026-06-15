using UnityEngine;

public class TargetController : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 0.5f;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float hitFlashDuration = 0.1f;

    private Renderer rend; 
    private Color startColor;
    private float flashTimer;

    private void Start()
    {
        rend = GetComponent<Renderer>();
        startColor = rend.material.color;
    }

    private void Update()
    {
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            float t = flashTimer / hitFlashDuration;
            rend.material.color = Color.Lerp(startColor, hitColor, t);
        }
    }

    public void OnTargetHit()
    {
        Debug.Log("hit!");
        flashTimer = hitFlashDuration;
        Invoke("ResetColor", respawnDelay);
    }

    private void ResetColor()
    {
        rend.material.color = startColor;
    }
}