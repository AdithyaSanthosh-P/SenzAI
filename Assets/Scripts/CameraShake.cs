using System.Collections;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Shake Settings")]
    public float duration  = 0.09f;
    public float magnitude = 0.045f;

    private Vector3 originalLocalPos;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        Instance = this;
        originalLocalPos = transform.localPosition;
    }

    private void OnDisable()
    {
 
        transform.localPosition = originalLocalPos;
    }
    public void Shake(float dur = -1f, float mag = -1f)
    {
        if (dur < 0f) dur = duration;
        if (mag < 0f) mag = magnitude;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(dur, mag));
    }

    private IEnumerator ShakeRoutine(float dur, float mag)
    {
        float elapsed = 0f;

        while (elapsed < dur)
        {
            float t      = 1f - (elapsed / dur);  
            float offset = mag * t;

            transform.localPosition = originalLocalPos + new Vector3(
                Random.Range(-offset, offset),
                Random.Range(-offset, offset),
                0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeRoutine = null;
    }
}
