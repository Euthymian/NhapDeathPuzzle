using System;
using UnityEngine;

public class ClickEventVisual : MonoBehaviour
{
    [SerializeField] private Canvas parentCanvas;

    [SerializeField] private GameObject clickEventPrefab;

    [SerializeField] private bool allowMultipleTouches = true;

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Allow mouse click in editor
        if (Input.GetMouseButtonDown(0))
            SpawnRippleAt(Input.mousePosition);
#endif

        // Touches on device
        if (allowMultipleTouches)
        {
            foreach (var t in Input.touches)
                if (t.phase == TouchPhase.Began)
                    SpawnRippleAt(t.position);
        }
        else
        {
            if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began)
                SpawnRippleAt(Input.touches[0].position);
        }
    }

    private void SpawnRippleAt(Vector3 mousePosition)
    {
        GameObject tmp = Instantiate(clickEventPrefab, mousePosition, Quaternion.identity, parentCanvas.transform);
        Destroy(tmp, 1.0f);
    }
}
