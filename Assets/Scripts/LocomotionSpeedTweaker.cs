using UnityEngine;
using Oculus.Interaction.Locomotion; // FirstPersonLocomotor
using OVR;                           // Oculus Integration이 프로젝트에 설치돼 있어야 합니다.

public class LocomotionSpeedTweaker : MonoBehaviour
{
    [SerializeField] private FirstPersonLocomotor locomotor;

    [Header("Speed Tuning")]
    [SerializeField] private float step = 2.0f;     // 한번 누를 때 증감량
    [SerializeField] private float minSpeed = 5f;   // 최저 속도
    [SerializeField] private float maxSpeed = 80f;  // 최고 속도

    [Header("Optional UI")]
    [SerializeField] private bool logToConsole = true;

    void Reset()
    {
        locomotor = FindObjectOfType<FirstPersonLocomotor>();
    }

    void Update()
    {
        if (locomotor == null) return;

        float cur = locomotor.SpeedFactor;

        // 왼손 X(Three) → 감소
        if (OVRInput.GetDown(OVRInput.Button.Three)) // X 버튼 (Left Touch)
        {
            cur = Mathf.Clamp(cur - step, minSpeed, maxSpeed);
            locomotor.SpeedFactor = cur;
            if (logToConsole) Debug.Log($"[Locomotion] SpeedFactor ↓ : {cur:0.##}");
        }

        // 왼손 Y(Four) → 증가
        if (OVRInput.GetDown(OVRInput.Button.Four)) // Y 버튼 (Left Touch)
        {
            cur = Mathf.Clamp(cur + step, minSpeed, maxSpeed);
            locomotor.SpeedFactor = cur;
            if (logToConsole) Debug.Log($"[Locomotion] SpeedFactor ↑ : {cur:0.##}");
        }
    }
}

