using System.Collections;
using UnityEngine;

public class FollowHeadUI : MonoBehaviour
{
    public Transform xrCameraTransform;
    public float followSpeed = 5f;
    public float distance = 2f;
    public Vector3 offset = new Vector3(100f, 100f, 100f);
    public    float yawOffsetDeg   = 0f;   // 좌우(수평) 미세 보정
    public    float pitchOffsetDeg = 0f;   // 위/아래 기울기 보정
    public    float rollOffsetDeg  = 0f;   // 좌/우로 기울기 보정
    IEnumerator Start()
    {
        // 어디에도 바인딩 안 되어 있으면 여기서 찾아 붙임
        if (xrCameraTransform == null)
            yield return BindXRCameraWhenReady();
    }

    private IEnumerator BindXRCameraWhenReady()
    {
        // 카메라를 계속 탐색 (MainCamera 또는 로컬 플레이어 하위)
        while (xrCameraTransform == null)
        {
            var main = Camera.main;
            if (main && main.enabled) { xrCameraTransform = main.transform; break; }

            // 필요하면 태그 대신 이름/컴포넌트 기준으로도 탐색
            var anyCam = FindObjectOfType<Camera>(true);
            if (anyCam && anyCam.enabled) { xrCameraTransform = anyCam.transform; break; }

            yield return null;
        }
    }

    void LateUpdate()
    {
        if (!xrCameraTransform)
        {
            // 런타임 중 카메라가 바뀌어도 다시 시도
            StartCoroutine(BindXRCameraWhenReady());
            return;
        }

        // 카메라 로컬축 기준 오프셋
        var cam = xrCameraTransform;
        Vector3 camSpaceOffset = cam.right * offset.x + cam.up * offset.y + cam.forward * offset.z;

        Vector3 targetPos = cam.position + cam.forward * distance + camSpaceOffset;
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

        Quaternion targetRot = Quaternion.LookRotation(transform.position - cam.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);


        // --- 여기부터 기울어짐(roll/pitch) 제거: yaw만 유지 ---
        Vector3 flatForward = xrCameraTransform.forward;
        flatForward.y = 0f;                                // 수평 성분만
        if (flatForward.sqrMagnitude < 1e-6f)
            flatForward = xrCameraTransform.forward;       // 안전장치

        Quaternion faceCamYawOnly = Quaternion.LookRotation(flatForward.normalized, Vector3.up);

        // 필요하면 미세 보정 각도(도) 넣기

        Quaternion rotOffset = Quaternion.Euler(pitchOffsetDeg, yawOffsetDeg, rollOffsetDeg);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            faceCamYawOnly * rotOffset,
            Time.deltaTime * followSpeed
        );

    }
}


// using System.Collections;
// using UnityEngine;
// using Unity.Netcode;

// public class FollowHeadUI : NetworkBehaviour
// {
//     public Transform xrCameraTransform;
//     public float followSpeed = 5f;
//     public float distance = 2f;

//     // 카메라 기준 오프셋: x=좌우, y=위/아래(음수면 아래), z=앞/뒤
//     public Vector3 offset = new Vector3(0f, -0.1f, 0f); // ← 여기 y를 더 음수로 낮추면 더 아래

//     public override void OnNetworkSpawn()
//     {
//         StartCoroutine(BindXRCameraWhenReady());
//     }

//     private IEnumerator BindXRCameraWhenReady()
//     {
//         if (xrCameraTransform != null) yield break;

//         while (xrCameraTransform == null)
//         {
//             if (Camera.main && Camera.main.enabled)
//             {
//                 xrCameraTransform = Camera.main.transform;
//                 break;
//             }
//             var nm = NetworkManager.Singleton;
//             var po = nm != null ? nm.LocalClient?.PlayerObject : null;
//             if (po != null)
//             {
//                 var cam = po.GetComponentInChildren<Camera>(true);
//                 if (cam && cam.enabled)
//                 {
//                     xrCameraTransform = cam.transform;
//                     break;
//                 }
//             }
//             yield return null;
//         }
//     }

//     void LateUpdate()
//     {
//         if (!xrCameraTransform) return;

//         // (변경점) 오프셋을 "카메라 로컬축" 기준으로 적용
//         Vector3 camSpaceOffset =
//               xrCameraTransform.right   * offset.x
//             + xrCameraTransform.up      * offset.y   // y가 음수면 "아래"
//             + xrCameraTransform.forward * offset.z;

//         Vector3 targetPosition =
//               xrCameraTransform.position
//             + xrCameraTransform.forward * distance
//             + camSpaceOffset;

//         transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);

//         // UI가 카메라를 보게 (뒤집힘 방지용으로 up 벡터 명시)
//         Quaternion targetRotation = Quaternion.LookRotation(transform.position - xrCameraTransform.position, Vector3.up);
//         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
//     }
// }





// using System.Collections;
// using UnityEngine;
// using Unity.Netcode;
// using UnityEngine.UI;

// [RequireComponent(typeof(RectTransform))]
// public class FollowHeadUI : NetworkBehaviour
// {
//     [Header("Targets")]
//     public Transform xrCameraTransform; // 비워두면 자동 바인딩

//     [Header("Placement (camera-relative)")]
//     public float distance     = 2f;   // 카메라 앞 거리
//     public float verticalDown = 0f; // "양수 = 아래로" 내림 (0.35~0.7 추천)
//     public float horizontal   = 0f;   // +오른쪽, -왼쪽

//     [Header("Smoothing")]
//     public float followSpeed = 8f;    // 위치 보간 속도

//     Canvas _canvas;
//     RectTransform _rect;
//     Coroutine _bindCo;

//     void Awake()
//     {
//         _rect = GetComponent<RectTransform>();
//         _canvas = GetComponentInParent<Canvas>();
//     }

//     public override void OnNetworkSpawn()
//     {
//         // UI가 NetworkObject가 아닐 수도 있으니 Start에서도 한 번 더 시작
//         if (_bindCo == null) _bindCo = StartCoroutine(BindXRCameraWhenReady());
//     }

//     void Start()
//     {
//         if (_bindCo == null) _bindCo = StartCoroutine(BindXRCameraWhenReady());
//     }

//     IEnumerator BindXRCameraWhenReady()
//     {
//         if (xrCameraTransform != null) yield break;

//         while (xrCameraTransform == null)
//         {
//             // MainCamera 우선
//             if (Camera.main && Camera.main.enabled)
//             {
//                 xrCameraTransform = Camera.main.transform;
//                 break;
//             }

//             // 로컬 플레이어 카메라 탐색
//             var nm = NetworkManager.Singleton;
//             var po = nm != null ? nm.LocalClient?.PlayerObject : null;
//             if (po)
//             {
//                 var cam = po.GetComponentInChildren<Camera>(true);
//                 if (cam && cam.enabled)
//                 {
//                     xrCameraTransform = cam.transform;
//                     break;
//                 }
//             }
//             yield return null;
//         }
//     }

//     void LateUpdate()
//     {
//         if (!xrCameraTransform || _canvas == null) return;

//         // 카메라 기준 목표 점(월드 좌표)
//         Vector3 worldTarget =
//               xrCameraTransform.position
//             + xrCameraTransform.forward * distance
//             - xrCameraTransform.up      * verticalDown   // 양수면 아래로
//             + xrCameraTransform.right   * horizontal;

//         switch (_canvas.renderMode)
//         {
//             case RenderMode.WorldSpace:
//                 // 월드 스페이스: Transform.position 직접 갱신
//                 transform.position = Vector3.Lerp(
//                     transform.position, worldTarget,
//                     Time.deltaTime * followSpeed
//                 );
//                 // 바라보기(원하면 수평만 돌리기)
//                 Vector3 toCam = transform.position - xrCameraTransform.position;
//                 if (toCam.sqrMagnitude < 1e-4f) toCam = xrCameraTransform.forward;
//                 transform.rotation = Quaternion.Slerp(
//                     transform.rotation,
//                     Quaternion.LookRotation(toCam, Vector3.up),
//                     Time.deltaTime * followSpeed
//                 );
//                 break;

//             case RenderMode.ScreenSpaceOverlay:
//             case RenderMode.ScreenSpaceCamera:
//                 // 스크린 스페이스: 화면 좌표 → 캔버스 로컬좌표(anchoredPosition)로 변환
//                 Camera uiCam = (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
//                     ? _canvas.worldCamera
//                     : null;

//                 Vector3 screen = Camera.main
//                     ? Camera.main.WorldToScreenPoint(worldTarget)
//                     : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

//                 RectTransform canvasRect = _canvas.transform as RectTransform;
//                 if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
//                         canvasRect, screen, uiCam, out Vector2 local))
//                 {
//                     // anchoredPosition 보간
//                     _rect.anchoredPosition = Vector2.Lerp(
//                         _rect.anchoredPosition, local,
//                         Time.deltaTime * followSpeed
//                     );
//                 }
//                 break;
//         }
//     }
// }


// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using Unity.Netcode;

// public class FollowHeadUI : NetworkBehaviour
// {
//     public Transform xrCameraTransform;
//     public float followSpeed = 5f;
//     public float distance = 2f;
//     public float verticalDown =-5f; // 카메라 기준 아래로 내리는 정도(0.35 → 더 내리려면 0.5~0.7)
//     public float horizontal = -5f;      // 필요하면 좌/우 오프셋(+오른쪽, -왼쪽)
//     public Vector3 offset = new Vector3(0, -0.3f, 0);
//     public override void OnNetworkSpawn()
//     {
//         StartCoroutine(BindXRCameraWhenReady());
//     }

//     private IEnumerator BindXRCameraWhenReady()
//     {
//         // 이미 세팅돼 있으면 패스
//         if (xrCameraTransform != null) yield break;

//         while (xrCameraTransform == null)
//         {
//             // 1) 가장 신뢰 가능한 방법: 로컬만 MainCamera 태그를 가지게 해뒀다면 이게 최고
//             if (Camera.main != null && Camera.main.enabled)
//             {
//                 xrCameraTransform = Camera.main.transform;
//                 break;
//             }

//             // 2) 로컬 플레이어의 Transform (원한다면 '플레이어 트랜스폼' 그대로 사용)
//             var nm = NetworkManager.Singleton;
//             var po = nm != null ? nm.LocalClient?.PlayerObject : null;
//             if (po != null)
//             {
//                 // (A) 플레이어 루트로 쓰고 싶다면 ↓ 한 줄이면 끝
//                 //xrCameraTransform = po.transform;

//                 // (B) 또는 플레이어 자식에 카메라가 있으면 그걸 쓰고 싶다면:
//                 var cam = po.GetComponentInChildren<Camera>(true);
//                 if (cam != null && cam.enabled)
//                     xrCameraTransform = cam.transform;

//                 if (xrCameraTransform != null) break;
//             }

//             // 3) 로컬 리그가 한 프레임 뒤에 생성되는 경우가 흔함 → 다음 프레임 재시도
//             yield return null;
//         }

//         // (선택) 디버그
//         // Debug.Log($"[MissionManager] xrCameraTransform bound to: {xrCameraTransform?.name}");
//     }
    
//     void LateUpdate()
//     {
//         if (!xrCameraTransform) return;

//         // 카메라 기준으로: 앞(거리), 아래(verticalDown), 좌우(horizontal)
//         Vector3 targetPosition =
//             xrCameraTransform.position
//         + xrCameraTransform.forward * distance
//         - xrCameraTransform.up      * verticalDown   // verticalDown은 "양수면 아래로"
//         + xrCameraTransform.right   * horizontal;

//         transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);

//         // 카메라 바라보게
//         Quaternion targetRotation = Quaternion.LookRotation(transform.position - xrCameraTransform.position, Vector3.up);
//         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
//     } 

// }

