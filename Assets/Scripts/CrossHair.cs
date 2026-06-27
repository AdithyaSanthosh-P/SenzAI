using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private Image crosshair;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private float hitDuration = 0.1f;

    private float hitTimer = 0f;

    private void Start()
    {
        if (crosshair == null)
            crosshair = GetComponent<Image>();
        crosshair.color = normalColor;
    }

    private void Update()
    {
        if (hitTimer > 0)
        {
            hitTimer -= Time.deltaTime;
            float t = hitTimer / hitDuration;
            crosshair.color = Color.Lerp(normalColor, hitColor, t);
        }
    }

    public void OnShot(bool hit)
    {
        if (hit)
            hitTimer = hitDuration;
    }
}