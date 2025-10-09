using UnityEngine;

public class TargetFps : MonoBehaviour
{
    void Awake()
    {
        QualitySettings.vSyncCount = 0;     // mobile usually ignores, but disable just in case
        Application.targetFrameRate = 60;    // request 60 FPS
    }
}
