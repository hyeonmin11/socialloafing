// using System.Collections;
// using UnityEngine;
// using Unity.Netcode;
// using TMPro;
// using System.Linq;

// public class MissionManager_multi : NetworkBehaviour
// {
//     [Header("Countdown UI")]
//     [SerializeField] private CanvasGroup countdownCanvas;
//     [SerializeField] private TMP_Text countdownText;

//     [Header("Dashboard Spawn")]
//     public Transform dashboardSpawnParent;
//     public Transform xrCameraTransform;

//     [Header("Mission Behaviours")]
//     public MonoBehaviour[] missionBehaviours;
//     private IMission[] missions;

//     [Header("Dashboards")]
//     public GameObject personalDashboardPrefab;
//     public GameObject personalTeamDashboardPrefab;
//     public GameObject teamOnlyDashboardPrefab;

//     [Header("Managers")]
//     [SerializeField] private WorkerManager workerManager;
//     [SerializeField] private BoxManager boxManager;

//     [Header("Timing")]
//     public float delayAfterMission = 5f;

//     private GameObject currentDashboard;
//     private int currentMissionIndex = -1;
//     private bool isRunning = false;

//     // 네트워크 상태 변수
//     private readonly NetworkVariable<int>    nvMissionIndex  = new(-1,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     private readonly NetworkVariable<double> nvStartAt       = new(0,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     private readonly NetworkVariable<double> nvEndAt         = new(0,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     private readonly NetworkVariable<int>    nvDashboardKind = new((int)DashboardKind.Personal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     private readonly NetworkVariable<bool>   nvAnonymous     = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
//     private readonly NetworkVariable<bool>   nvActive        = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

//     // 클라 로컬
//     private Coroutine uiRoutine;
//     private Coroutine bindCameraRoutine;
//     private int appliedMissionIndex = -999;

//     private void Awake()
//     {
//         missions = new IMission[missionBehaviours.Length];
//         for (int i = 0; i < missionBehaviours.Length; i++)
//         {
//             missions[i] = missionBehaviours[i] as IMission;
//             if (missions[i] == null)
//                 Debug.LogError($"Mission {i + 1} does not implement IMission interface");
//         }
//     }

//     public override void OnNetworkSpawn()
//     {
//         bindCameraRoutine = StartCoroutine(BindXRCameraWhenReady());

//         // 값 변경 시마다 클라 UI 적용
//         nvMissionIndex.OnValueChanged  += (_, __) => TryApplyClientUIFromState();
//         nvStartAt.OnValueChanged       += (_, __) => TryApplyClientUIFromState();
//         nvEndAt.OnValueChanged         += (_, __) => TryApplyClientUIFromState();
//         nvDashboardKind.OnValueChanged += (_, __) => TryApplyClientUIFromState();
//         nvAnonymous.OnValueChanged     += (_, __) => TryApplyClientUIFromState();
//         nvActive.OnValueChanged        += (_, __) => TryApplyClientUIFromState();

//         TryApplyClientUIFromState();

//         if (IsServer)
//             StartAllMissionsServer();
//     }

//     public override void OnNetworkDespawn()
//     {
//         if (bindCameraRoutine != null) StopCoroutine(bindCameraRoutine);
//         if (uiRoutine != null) StopCoroutine(uiRoutine);
//     }

//     private IEnumerator BindXRCameraWhenReady()
//     {
//         if (xrCameraTransform != null) yield break;
//         while (xrCameraTransform == null)
//         {
//             if (Camera.main != null && Camera.main.enabled)
//             {
//                 xrCameraTransform = Camera.main.transform;
//                 break;
//             }

//             var nm = NetworkManager.Singleton;
//             var po = nm != null ? nm.LocalClient?.PlayerObject : null;
//             if (po != null)
//             {
//                 var cam = po.GetComponentInChildren<Camera>(true);
//                 if (cam != null && cam.enabled)
//                 {
//                     xrCameraTransform = cam.transform;
//                     break;
//                 }
//             }
//             yield return null;
//         }
//     }

//     // ================== SERVER FLOW ==================

//     private void StartAllMissionsServer()
//     {
//         if (isRunning) return;
//         isRunning = true;
//         currentMissionIndex = -1;
//         StartCoroutine(StartNextMissionServer(10)); // 준비 카운트다운(초)
//     }

//     private IEnumerator StartNextMissionServer(int prepSeconds)
//     {
//         currentMissionIndex++;
//         if (currentMissionIndex >= missions.Length)
//         {
//             isRunning = false;
//             nvActive.Value = false;
//             // 미션 완전 종료 → 모든 클라 UI 클리어
//             ClearDashboardClientRpc();
//             yield break;
//         }

//         var cfg = missions[currentMissionIndex].GetConfig();

//         // (A) 월드 리셋 (서버 권한)
//         ResetWorldForMission_Server();

//         // (B) 클라 UI 클리어(이전 대시보드 강제 제거)
//         ClearDashboardClientRpc();

//         // (C) 스케줄 공개
//         double now     = NetworkManager.Singleton.ServerTime.Time;
//         double startAt = now + prepSeconds;
//         double endAt   = startAt + cfg.timeLimitSec;

//         nvMissionIndex.Value  = currentMissionIndex;
//         nvDashboardKind.Value = (int)cfg.dashboard;
//         nvAnonymous.Value     = cfg.anonymous;
//         nvStartAt.Value       = startAt;
//         nvEndAt.Value         = endAt;
//         nvActive.Value        = true;

//         // (D) 시작 시각까지 대기
//         while (NetworkManager.Singleton.ServerTime.Time < startAt) yield return null;

//         // (E) 시작: 에이전트 구동
//         ApplyAgentSpeed_Server(cfg.agentSpeed);
//         BeginAgents_Server();

//         // (F) 종료 시각까지 대기
//         while (NetworkManager.Singleton.ServerTime.Time < endAt) yield return null;

//         // (G) 종료: 에이전트 정지(선택) + UI 종료 브로드캐스트
//         StopAgents_Server();
//         ClearDashboardClientRpc();

//         // (H) 다음 미션 지연
//         if (delayAfterMission > 0f)
//             yield return new WaitForSeconds(delayAfterMission);

//         if (isRunning)
//             StartCoroutine(StartNextMissionServer(prepSeconds));
//     }

//     private void ResetWorldForMission_Server()
//     {
//         var workers = workerManager.GetWorkers();
//         foreach (var w in workers)
//         {
//             if (w.IsPlayer) continue;
//             var mover = w.GetComponent<MovToDestination_multi>();
//             if (mover) mover.StopDelivery();
//         }

//         // 네가 만든 멀티 전용 리셋 호출
//         workerManager.ResetAllWorkers_multi();
//         boxManager.ResetAllBoxes_multi();
//     }

//     private void ApplyAgentSpeed_Server(float speed)
//     {
//         if (!IsServer) return;
//         foreach (var w in workerManager.GetWorkers())
//         {
//             if (w.IsPlayer) continue;
//             var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
//             if (agent) agent.speed = speed;
//         }
//     }

//     private void BeginAgents_Server()
//     {
//         if (!IsServer) return;
//         foreach (var w in workerManager.GetWorkers())
//         {
//             if (w.IsPlayer) continue;
//             var mover = w.GetComponent<MovToDestination_multi>();
//             if (mover) mover.BeginDelivery();
//         }
//     }

//     private void StopAgents_Server()
//     {
//         if (!IsServer) return;
//         foreach (var w in workerManager.GetWorkers())
//         {
//             if (w.IsPlayer) continue;
//             var mover = w.GetComponent<MovToDestination_multi>();
//             if (mover) mover.StopDelivery();
//         }
//     }

//     [ClientRpc]
//     private void ClearDashboardClientRpc()
//     {
//         if (currentDashboard != null)
//         {
//             Destroy(currentDashboard);
//             currentDashboard = null;
//         }
//         if (countdownCanvas != null)
//         {
//             countdownCanvas.gameObject.SetActive(false);
//             countdownCanvas.alpha = 0f;
//         }
//         if (countdownText != null) countdownText.text = "";
//     }

//     // ================== CLIENT UI ==================

//     private void TryApplyClientUIFromState()
//     {
//         if (!IsSpawned) return;
//         if (!nvActive.Value) return;

//         if (appliedMissionIndex == nvMissionIndex.Value && uiRoutine != null)
//             return;

//         if (uiRoutine != null) StopCoroutine(uiRoutine);
//         uiRoutine = StartCoroutine(ClientUIRoutineFromState());
//         appliedMissionIndex = nvMissionIndex.Value;
//     }

//     private IEnumerator ClientUIRoutineFromState()
//     {
//         double startAt   = nvStartAt.Value;
//         double endAt     = nvEndAt.Value;
//         var    kind      = (DashboardKind)nvDashboardKind.Value;
//         bool   anonymous = nvAnonymous.Value;

//         // 0) 기존 UI 정리(방어)
//         if (currentDashboard != null) { Destroy(currentDashboard); currentDashboard = null; }

//         // 1) 카운트다운
//         if (countdownCanvas)
//         {
//             countdownCanvas.alpha = 1f;
//             countdownCanvas.gameObject.SetActive(true);
//         }

//         while (true)
//         {
//             double now = NetworkManager.Singleton.ServerTime.Time;
//             double remain = startAt - now;
//             if (remain <= 0) break;

//             if (countdownText)
//             {
//                 int show = Mathf.CeilToInt((float)remain);
//                 countdownText.text = show.ToString();
//             }
//             yield return null;
//         }

//         if (countdownText) countdownText.text = "START!";
//         yield return new WaitForSeconds(0.5f);
//         if (countdownText) countdownText.text = "";

//         // 2) 대시보드 생성 전, WorkerManager에 모든 Worker가 채워질 때까지 최대 2초 대기
//         yield return StartCoroutine(EnsureWorkersReadyClient(2f));

//         // 3) 대시보드 생성
//         ResetUIForMission_Client();
//         currentDashboard = Instantiate(GetDashboardPrefab(kind), dashboardSpawnParent);
//         PositionDashboardInFrontOfPlayer(currentDashboard);

//         var dc = currentDashboard.GetComponent<DashboardController>();
//         if (dc != null)
//         {
//             dc.SetWorkers(workerManager.GetWorkers(), anonymous);

//             // 남은 시간 타이머 가동(중간 합류도 정렬)
//             double now = NetworkManager.Singleton.ServerTime.Time;
//             int remainSec = Mathf.Max(0, Mathf.CeilToInt((float)(endAt - now)));
//             dc.StartTimer(remainSec, null);
//         }

//         // 4) 미션 종료 시간까지 기다렸다가 UI 자동 정리(서버에서 ClearDashboardClientRpc도 오지만, 이중 방어)
//         while (NetworkManager.Singleton.ServerTime.Time < endAt)
//             yield return null;

//         if (currentDashboard != null)
//         {
//             Destroy(currentDashboard);
//             currentDashboard = null;
//         }
//     }

//     private IEnumerator EnsureWorkersReadyClient(float timeout)
//     {
//         float t = 0f;
//         while (t < timeout)
//         {
//             // 씬에 존재하는 Worker 총 수
//             var allWorkers = FindObjectsOfType<Worker>(true).Length;
//             var listCount  = workerManager.GetWorkers().Count;

//             if (allWorkers > 0 && listCount >= allWorkers)
//                 yield break; // 충분히 채워짐

//             t += Time.deltaTime;
//             yield return null;
//         }
//     }

//     private void ResetUIForMission_Client()
//     {
//         if (currentDashboard != null) Destroy(currentDashboard);
//     }

//     private GameObject GetDashboardPrefab(DashboardKind kind)
//     {
//         switch (kind)
//         {
//             case DashboardKind.PersonalTeam: return personalTeamDashboardPrefab;
//             case DashboardKind.TeamOnly:     return teamOnlyDashboardPrefab;
//             default:                         return personalDashboardPrefab;
//         }
//     }

//     private void PositionDashboardInFrontOfPlayer(GameObject dashboard)
//     {
//         var rt = dashboard.GetComponent<RectTransform>();
//         if (rt != null)
//         {
//             rt.localPosition = Vector3.zero;
//             rt.localRotation = Quaternion.identity;
//         }
//         var follow = currentDashboard.GetComponent<FollowHeadUI>();
//         if (follow != null)
//             follow.xrCameraTransform = xrCameraTransform;
//     }
// }





using System.Collections;
using UnityEngine;
using Unity.Netcode;
using TMPro;

public class MissionManager_multi : NetworkBehaviour
{
    [Header("Countdown UI")]
    [SerializeField] private CanvasGroup countdownCanvas;
    [SerializeField] private TMP_Text countdownText;

    [Header("Dashboard Spawn")]
    public Transform dashboardSpawnParent;
    public Transform xrCameraTransform;

    [Header("Mission Behaviours")]
    public MonoBehaviour[] missionBehaviours;
    private IMission[] missions;

    [Header("Dashboards")]
    public GameObject personalDashboardPrefab;
    public GameObject personalTeamDashboardPrefab;
    public GameObject teamOnlyDashboardPrefab;

    [Header("Managers")]
    [SerializeField] private WorkerManager workerManager;
    [SerializeField] private BoxManager boxManager;

    [Header("Timing")]
    public float delayAfterMission = 5f;

    private GameObject currentDashboard;
    private int currentMissionIndex = -1;
    private bool isRunning = false;


    private readonly NetworkVariable<int> nvMissionIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> nvStartAt = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> nvEndAt = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> nvDashboardKind = new((int)DashboardKind.Personal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> nvAnonymous = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> nvActive = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 클라 로컬용
    private Coroutine uiRoutine;
    private Coroutine bindCameraRoutine;
    private int appliedMissionIndex = -999; // 중복 UI 실행 방지

    private void Awake()
    {
        // MonoBehaviour[] → IMission[] 변환/검증
        missions = new IMission[missionBehaviours.Length];
        for (int i = 0; i < missionBehaviours.Length; i++)
        {
            missions[i] = missionBehaviours[i] as IMission;
            if (missions[i] == null)
            {
                Debug.LogError($"Mission {i + 1} does not implement IMission interface");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // 모든 피어에서 자기 카메라 바인딩 시도
        bindCameraRoutine = StartCoroutine(BindXRCameraWhenReady());

        // 클라: 접속 즉시/값 변경 시 현재 상태로 UI 세팅
        nvMissionIndex.OnValueChanged += (_, __) => TryApplyClientUIFromState();
        nvStartAt.OnValueChanged += (_, __) => TryApplyClientUIFromState();
        nvEndAt.OnValueChanged += (_, __) => TryApplyClientUIFromState();
        nvDashboardKind.OnValueChanged += (_, __) => TryApplyClientUIFromState();
        nvAnonymous.OnValueChanged += (_, __) => TryApplyClientUIFromState();
        nvActive.OnValueChanged += (_, __) => TryApplyClientUIFromState();

        TryApplyClientUIFromState(); // 첫 진입 시 1회 적용

        if (IsServer)
        {
            StartAllMissionsServer();
        }
    }
    public override void OnNetworkDespawn()
    {
        if (bindCameraRoutine != null) StopCoroutine(bindCameraRoutine);
        if (uiRoutine != null) StopCoroutine(uiRoutine);
    }
    private IEnumerator BindXRCameraWhenReady()
    {
        // 이미 세팅돼 있으면 패스
        if (xrCameraTransform != null) yield break;

        while (xrCameraTransform == null)
        {
            // 1) 가장 신뢰 가능한 방법: 로컬만 MainCamera 태그를 가지게 해뒀다면 이게 최고
            if (Camera.main != null && Camera.main.enabled)
            {
                xrCameraTransform = Camera.main.transform;
                break;
            }

            // 2) 로컬 플레이어의 Transform (원한다면 '플레이어 트랜스폼' 그대로 사용)
            var nm = NetworkManager.Singleton;
            var po = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (po != null)
            {
                // (A) 플레이어 루트로 쓰고 싶다면 ↓ 한 줄이면 끝
                //xrCameraTransform = po.transform;

                // (B) 또는 플레이어 자식에 카메라가 있으면 그걸 쓰고 싶다면:
                var cam = po.GetComponentInChildren<Camera>(true);
                if (cam != null && cam.enabled)
                    xrCameraTransform = cam.transform;

                if (xrCameraTransform != null) break;
            }

            // 3) 로컬 리그가 한 프레임 뒤에 생성되는 경우가 흔함 → 다음 프레임 재시도
            yield return null;
        }

        // (선택) 디버그
        // Debug.Log($"[MissionManager] xrCameraTransform bound to: {xrCameraTransform?.name}");
    }
    private void StartAllMissionsServer()
    {
        if (isRunning) return;
        isRunning = true;
        currentMissionIndex = -1;
        StartCoroutine(StartNextMissionServer(30)); // 10초 카운트다운
    }

    private IEnumerator StartNextMissionServer(int prepSeconds)
    {
        currentMissionIndex++;
        if (currentMissionIndex >= missions.Length)
        {
            Debug.Log("every mission complete");
            isRunning = false;
            countdownText.text = "END!";
            // 상태 비활성화
            nvActive.Value = false;
            yield break;
        }

        var cfg = missions[currentMissionIndex].GetConfig();

        // 1) 월드 리셋(서버 전용)
        ResetWorldForMission_Server();

        // 2) 서버 시간 기준 스케줄 기록 (늦게 들어온 클라가 읽을 수 있게!)
        double now = NetworkManager.Singleton.ServerTime.Time;
        double startAt = now + prepSeconds;
        double endAt = startAt + cfg.timeLimitSec;

        nvMissionIndex.Value = currentMissionIndex;
        nvDashboardKind.Value = (int)cfg.dashboard;
        nvAnonymous.Value = cfg.anonymous;
        nvStartAt.Value = startAt;
        nvEndAt.Value = endAt;
        nvActive.Value = true;

        // 3) 실제 시작 시각까지 대기
        while (NetworkManager.Singleton.ServerTime.Time < startAt)
            yield return null;

        // 4) 시작: 서버가 에이전트 속도/구동 (※ 네 원래 순서 버그 수정)
        ApplyAgentSpeed_Server(cfg.agentSpeed);
        BeginAgents_Server();

        // 5) 종료 시각까지 대기
        while (NetworkManager.Singleton.ServerTime.Time < endAt)
            yield return null;

        // (선택) StopAgents_Server();

        // 6) 다음 미션까지 딜레이
        if (delayAfterMission > 0f)
            yield return new WaitForSeconds(delayAfterMission);

        if (isRunning)
            StartCoroutine(StartNextMissionServer(prepSeconds));
    }

    private void ResetWorldForMission_Server()
    {
        // 클라 UI는 각자 지움, 여기서는 월드 상태만 정리
        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            if (w.IsPlayer) continue;
            var mover = w.GetComponent<MovToDestination_multi>();
            if (mover != null) mover.StopDelivery(); // 서버 전용 구현 전제
        }

        // workerManager.ResetAllWorkers();
        // boxManager.ResetAllBoxes();
        workerManager.ResetAllWorkers_multi();
        boxManager.ResetAllBoxes_multi();
    }

    private void ApplyAgentSpeed_Server(float speed)
    {
        if (!IsServer) return;

        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            if (w.IsPlayer) continue;

            var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent) agent.speed = speed;
        }
    }

    private void BeginAgents_Server()
    {
        if (!IsServer) return;

        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            if (w.IsPlayer) continue;

            var mover = w.GetComponent<MovToDestination_multi>();
            if (mover != null) mover.BeginDelivery();
        }
    }

    private void StopAgents_Server()
    {
        if (!IsServer) return;

        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            if (w.IsPlayer) continue;

            var mover = w.GetComponent<MovToDestination_multi>();
            if (mover != null) mover.StopDelivery();
        }
    }

    // ===================== 클라: 상태를 읽어 UI 세팅 =====================

    private void TryApplyClientUIFromState()
    {
        if (!IsSpawned) return;      // 네트워크 준비 전
        if (!nvActive.Value) return; // 비활성 상태

        // 같은 미션 인덱스에 대해 중복 실행 방지
        if (appliedMissionIndex == nvMissionIndex.Value && uiRoutine != null)
            return;

        if (uiRoutine != null) StopCoroutine(uiRoutine);
        uiRoutine = StartCoroutine(ClientUIRoutineFromState());
        appliedMissionIndex = nvMissionIndex.Value;
    }
    private IEnumerator ClientUIRoutineFromState()
    {
        // 0) 이전 UI 정리 (혹시 남아있을 수 있는 대시보드 제거)
        ResetUIForMission_Client();
        if (countdownText) countdownText.text = "";

        // 1) 서버가 스케줄(nvStartAt/nvEndAt)을 채울 때까지 잠깐 대기 (중간 합류/초기 프레임 대비)
        yield return new WaitUntil(() =>
            nvActive.Value &&
            nvStartAt.Value > 0.0001 &&
            nvEndAt.Value   > nvStartAt.Value
        );

        // 2) 서버가 배포한 값만 읽기
        double startAt   = nvStartAt.Value;
        double endAt     = nvEndAt.Value;
        var    kind      = (DashboardKind)nvDashboardKind.Value;
        bool   anonymous = nvAnonymous.Value;

        // 3) 카운트다운 표시 (ServerTime 기준)
        if (countdownCanvas)
        {
            countdownCanvas.gameObject.SetActive(true);
            countdownCanvas.alpha = 1f;
        }

        while (true)
        {
            double now    = NetworkManager.Singleton.ServerTime.Time;
            double remain = startAt - now;
            if (remain <= 0) break;

            if (countdownText)
            {
                int show = Mathf.CeilToInt((float)remain);
                countdownText.text = show.ToString();
            }
            yield return null;
        }

        // 4) START 플래시
        if (countdownText) countdownText.text = "START!";
        yield return new WaitForSeconds(0.5f);
        if (countdownText) countdownText.text = "";

        // 5) 대시보드 생성 (동일 시각에)
        ResetUIForMission_Client(); // 혹시 잔존물 제거
        currentDashboard = Instantiate(GetDashboardPrefab(kind), dashboardSpawnParent);
        PositionDashboardInFrontOfPlayer(currentDashboard);

        var dc = currentDashboard.GetComponent<DashboardController_multi>();
        if (dc != null)
        {
            dc.SetWorkers(workerManager.GetWorkers(), anonymous);

            // 남은 시간으로 타이머 시작(중간 합류 보정)
            double now = NetworkManager.Singleton.ServerTime.Time;
            int remainSec = Mathf.Max(0, Mathf.CeilToInt((float)(endAt - now)));
            dc.StartTimer(remainSec, null);
        }

        // 6) 미션 종료까지 대기
        while (NetworkManager.Singleton.ServerTime.Time < endAt)
            yield return null;

        // 7) 종료 시 UI 정리 (다음 라운드에서 서버가 다시 nv값 갱신 → TryApply가 새 루틴 시작)
        bool isLastMission = nvMissionIndex.Value >= missions.Length - 1;

        ResetUIForMission_Client();
        if (countdownCanvas)
        {
            countdownCanvas.alpha = 1f;
            countdownCanvas.gameObject.SetActive(true);
            if (countdownText)
                countdownText.text = isLastMission ? "END!" : "";
        }
    }
    // private IEnumerator ClientUIRoutineFromState()
    // {
    //     ResetUIForMission_Client();


    //     var cfg = missions[currentMissionIndex].GetConfig();
    //     double now = NetworkManager.Singleton.ServerTime.Time;
    //     double startAt = now + 30; //prepSeconds;
    //     double endAt = startAt + cfg.timeLimitSec;
    //     nvStartAt.Value = startAt;
    //     nvEndAt.Value = endAt;


        
    //     startAt = nvStartAt.Value;
    //     endAt = nvEndAt.Value;
    //     var kind = (DashboardKind)nvDashboardKind.Value;
    //     bool anonymous = nvAnonymous.Value;

    //     // 1) 카운트다운 (ServerTime 기준)
    //     if (countdownCanvas)
    //     {
    //         countdownCanvas.alpha = 1f;
    //         countdownCanvas.gameObject.SetActive(true);
    //     }

    //     while (true)
    //     {
    //         now = NetworkManager.Singleton.ServerTime.Time;
    //         double remain = startAt - now;
    //         if (remain <= 0) break;

    //         if (countdownText)
    //         {
    //             int show = Mathf.CeilToInt((float)remain);
    //             countdownText.text = show.ToString();
    //         }
    //         yield return null;
    //     }

    //     if (countdownText) countdownText.text = "START!";
    //     yield return new WaitForSeconds(0.5f);
    //     if (countdownText) countdownText.text = "";
    //     // 필요 시 countdownCanvas 비활성 가능

    //     // 2) 대시보드 생성 (모든 클라에서 동일 시각에)
    //     //ResetUIForMission_Client();
    //     currentDashboard = Instantiate(GetDashboardPrefab(kind), dashboardSpawnParent);
    //     PositionDashboardInFrontOfPlayer(currentDashboard);

    //     var dc = currentDashboard.GetComponent<DashboardController_multi>();
    //     if (dc != null)
    //     {
    //         dc.SetWorkers(workerManager.GetWorkers(), anonymous);

    //         // 남은 시간으로 타이머 가동(중간 합류 지원)
    //         now = NetworkManager.Singleton.ServerTime.Time;
    //         int remainSec = Mathf.Max(0, Mathf.CeilToInt((float)(endAt - now)));
    //         dc.StartTimer(remainSec, null);
    //     }

    // }

    // ---------- 클라: UI 리셋 ----------
    private void ResetUIForMission_Client()
    {
        if (currentDashboard != null) Destroy(currentDashboard);
    }

    private GameObject GetDashboardPrefab(DashboardKind kind)
    {
        switch (kind)
        {
            case DashboardKind.PersonalTeam: return personalTeamDashboardPrefab;
            case DashboardKind.TeamOnly: return teamOnlyDashboardPrefab;
            default: return personalDashboardPrefab;
        }
    }

    private void PositionDashboardInFrontOfPlayer(GameObject dashboard)
    {
        // 간단 중앙 정렬
        var rt = dashboard.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
        }

        // 카메라 추적 필요 시 참조 주입
        var follow = currentDashboard.GetComponent<FollowHeadUI>();
        if (follow != null)
            follow.xrCameraTransform = xrCameraTransform;
    }
}

















    // private IEnumerator StartNextMissionServer(int prepSeconds)
    // {
    //     currentMissionIndex++;
    //     if (currentMissionIndex >= missions.Length)
    //     {
    //         Debug.Log("every mission complete");
    //         isRunning = false;
    //         yield break;
    //     }

    //     var cfg = missions[currentMissionIndex].GetConfig();

    //     ResetWorldForMission_Server();

    //     double now = NetworkManager.Singleton.ServerTime.Time;
    //     double startAt = now + prepSeconds;
    //     double endAt = startAt + cfg.timeLimitSec;

    //     ScheduleMissionClientRPC(startAt, cfg.dashboard, cfg.anonymous, cfg.timeLimitSec);

    //     while (NetworkManager.Singleton.ServerTime.Time < endAt)
    //     {
    //         yield return null;
    //     }

    //     // 4) 시작 시점: 서버에서 에이전트 속도/구동 적용(월드 제어는 서버만)
    //     ApplyAgentSpeed_Server(cfg.agentSpeed);  // 속도 세팅
    //     BeginAgents_Server();                    // 필요한 경우 워커에 BeginDelivery를 일괄 실행

    //     // 5) ServerTime 기준으로 미션 종료까지 대기
    //     while (NetworkManager.Singleton.ServerTime.Time < endAt)
    //         yield return null;

    //     if (isRunning)
    //     {
    //         StartCoroutine(StartNextMissionServer(prepSeconds));
    //     }
    // }


    // [ClientRpc]
    // private void ScheduleMissionClientRPC(double startAtServerTime, DashboardKind kind, bool anonymous, int timeLimitSec)
    // {
    //     StartCoroutine(ClientMissionUIRoutine(startAtServerTime, kind, anonymous, timeLimitSec));
    // }

    // private IEnumerator ClientMissionUIRoutine(double startAt, DashboardKind kind, bool anonymous, int timeLimitSec)
    // {
    //     if (countdownCanvas)
    //     {
    //         countdownCanvas.alpha = 1f;
    //         countdownCanvas.gameObject.SetActive(true);
    //     }

    //     while (true)
    //     {
    //         double serverNow = NetworkManager.Singleton.ServerTime.Time;
    //         double remain = startAt - serverNow;
    //         if (remain <= 0) break;

    //         if (countdownText)
    //         {
    //             int show = Mathf.CeilToInt((float)remain);
    //             countdownText.text = show.ToString();
    //         }
    //         yield return null;
    //     }
    //     if (countdownText) countdownText.text = "START!";
    //     yield return new WaitForSeconds(0.5f);
    //     if (countdownText) countdownText.text = "";

    //     ResetUIForMission_Client();
    //     currentDashboard = Instantiate(GetDashboardPrefab(kind), dashboardSpawnParent);
    //     PositionDashboardInFrontOfPlayer(currentDashboard);

    //     // 워커 바인딩 + 익명/실명 적용
    //     var dc = currentDashboard.GetComponent<DashboardController>();
    //     if (dc != null)
    //     {
    //         dc.SetWorkers(workerManager.GetWorkers(), anonymous);
    //         // 타이머는 서버가 종료를 관리하지만, 로컬 UI 진행을 맞추려면 여기서도 시작
    //         dc.StartTimer(timeLimitSec, null); // 콜백은 로컬 UI용이면 null로 둬도 됨
    //     }
    // }
    // private void ResetWorldForMission_Server()
    // {
    //     // 클라 UI는 각자 지움(ClientRpc에서), 여기서는 월드 상태만 정리
    //     var workers = workerManager.GetWorkers();
    //     foreach (var w in workers)
    //     {
    //         if (w.IsPlayer) continue;
    //         var mover = w.GetComponent<MovToDestination_multi>();
    //         if (mover != null) mover.StopDelivery(); // 서버에서만 동작하도록 구현되어 있어야 함
    //     }

    //     workerManager.ResetAllWorkers();
    //     boxManager.ResetAllBoxes();
    // }

    // // ---------- 클라: UI 리셋 ----------
    // private void ResetUIForMission_Client()
    // {
    //     if (currentDashboard != null) Destroy(currentDashboard);
    // }

    // private GameObject GetDashboardPrefab(DashboardKind kind)
    // {
    //     switch (kind)
    //     {
    //         case DashboardKind.PersonalTeam: return personalTeamDashboardPrefab;
    //         case DashboardKind.TeamOnly: return teamOnlyDashboardPrefab;
    //         default: return personalDashboardPrefab;
    //     }
    // }

    // private void PositionDashboardInFrontOfPlayer(GameObject dashboard)
    // {
    //     // 각자 프로젝트 UI 구성에 맞게 간단히 중앙 정렬
    //     RectTransform rt = dashboard.GetComponent<RectTransform>();
    //     if (rt != null)
    //     {
    //         rt.localPosition = Vector3.zero;
    //         rt.localRotation = Quaternion.identity;
    //     }

    //     // 카메라 추적 UI라면 카메라 참조 주입
    //     var follow = currentDashboard.GetComponent<FollowHeadUI>();
    //     if (follow != null)
    //         follow.xrCameraTransform = xrCameraTransform;
    // }

    // private void ApplyAgentSpeed_Server(float speed)
    // {
    //     if (!IsServer) return;

    //     var workers = workerManager.GetWorkers();
    //     foreach (var w in workers)
    //     {
    //         if (w.IsPlayer) continue;

    //         var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
    //         if (agent) agent.speed = speed;
    //     }
    // }

    // private void BeginAgents_Server()
    // {
    //     if (!IsServer) return;

    //     var workers = workerManager.GetWorkers();
    //     foreach (var w in workers)
    //     {
    //         if (w.IsPlayer) continue;

    //         var mover = w.GetComponent<MovToDestination_multi>();
    //         if (mover != null) mover.BeginDelivery(); // 서버 전용으로 돌아가도록 MovToDestination 구현돼 있어야 함
    //     }
    // }

    // private void StopAgents_Server()
    // {
    //     if (!IsServer) return;

    //     var workers = workerManager.GetWorkers();
    //     foreach (var w in workers)
    //     {
    //         if (w.IsPlayer) continue;

    //         var mover = w.GetComponent<MovToDestination_multi>();
    //         if (mover != null) mover.StopDelivery();
    //     }
    // }
// }
