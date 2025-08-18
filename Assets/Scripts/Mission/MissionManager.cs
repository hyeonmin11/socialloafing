using System.Collections;
using UnityEngine;
using TMPro;

public class MissionManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup countdownCanvas; // 알파/활성 제어용(없으면 GameObject로 대체 가능)
    [SerializeField] private TMP_Text countdownText; 
    public Transform dashboardSpawnParent;
    public Transform xrCameraTransform;
    public Transform playerTransform;
    //public Transform missionPosition;

    public MonoBehaviour[] missionBehaviours; // Mission1 ~ Mission8 MonoBehaviour
    private IMission[] missions;

    public GameObject personalDashboardPrefab;
    public GameObject personalTeamDashboardPrefab;
    public GameObject teamOnlyDashboardPrefab;
    public Transform dashboardSpawnPoint;

    private GameObject currentDashboard;
    private int currentMissionIndex = -1;

    public float delayAfterMission = 5f;
    [SerializeField] private WorkerManager workerManager;
    [SerializeField] private BoxManager boxManager;

    
    private bool isRunning = false;
    private void Awake()
    {
        // MonoBehaviour 배열을 IMission 인터페이스 배열로 변환
        missions = new IMission[missionBehaviours.Length];
        for (int i = 0; i < missionBehaviours.Length; i++)
        {
            missions[i] = missionBehaviours[i] as IMission;
            if (missions[i] == null)
                Debug.LogError($"Mission {i + 1} does not implement IMission interface");
        }

    }
    void Start()
    {
        StartAllMissions();
    }
    public void StartAllMissions()
    {
        if (isRunning) return;
        isRunning = true;
        currentMissionIndex = -1;
        StartCoroutine(StartNextMissionWithCountdown(10)); // 10초 카운트다운
    }
    public IEnumerator StartNextMissionWithCountdown(int seconds)
    {
        currentMissionIndex++;
        if (currentMissionIndex >= missions.Length)
        {
            Debug.Log("🚩 모든 미션 완료!");
            isRunning = false;
            countdownText.text = "END!";
            // foreach (var w in workerManager.GetWorkers())
            // {
            //     var mover = w.GetComponent<MovToDestination>();
            //     mover.StopDelivery();
            // }

            yield break;
        }

        var cfg = missions[currentMissionIndex].GetConfig();

        // 1) 환경 초기화
        ResetForMission();
        yield return StartCoroutine(PreCountdown(seconds));
        // 2) 대시보드 생성
        currentDashboard = Instantiate(GetDashboardPrefab(cfg.dashboard), dashboardSpawnParent);
        //WireFollowHead(currentDashboard);
        PositionDashboardInFrontOfPlayer(currentDashboard);
        // 3) 워커 바인딩 + 익명/실명 적용
        var dc = currentDashboard.GetComponent<DashboardController>();
        dc.SetWorkers(workerManager.GetWorkers(), cfg.anonymous);

        // 4) 속도 일괄 반영
        ApplyAgentSpeed(cfg.agentSpeed);

        // 5) 타이머 시작 -> 끝나면 자동 다음 미션
        dc.StartTimer(cfg.timeLimitSec, () =>
        {
            if (isRunning) StartCoroutine(StartNextMissionWithCountdown(10));
        });

    }
    private IEnumerator PreCountdown(int seconds)
    {
        if (countdownCanvas != null) countdownCanvas.alpha = 1f;
        if (countdownCanvas != null) countdownCanvas.gameObject.SetActive(true);

        for (int t = seconds; t >= 1; t--)
        {
            if (countdownText != null) countdownText.text = t.ToString();
            yield return new WaitForSeconds(1f);
        }

        // (선택) 0 순간 "START!" 잠깐 보여주고 숨기기
        if (countdownText != null) countdownText.text = "START!";
        yield return new WaitForSeconds(0.5f);
        countdownText.text = "";
        // if (countdownCanvas != null) countdownCanvas.alpha = 0f;
        // if (countdownCanvas != null) countdownCanvas.gameObject.SetActive(false);
    }

    // public void CompleteCurrentMission()
    // {
    //     if (currentMissionIndex < 0 || currentMissionIndex >= missions.Length) return;
    //     if (!isRunning) return;
    //     missions[currentMissionIndex].CompleteMission();
    //     //StartNextMission();


    //     isRunning = false;
    // }
    private GameObject GetDashboardPrefab(DashboardKind kind)
    {
        switch (kind)
        {
            case DashboardKind.PersonalTeam: return personalTeamDashboardPrefab;
            case DashboardKind.TeamOnly:     return teamOnlyDashboardPrefab;
            default:                         return personalDashboardPrefab;
        }
    }
    private void ResetForMission()
    {
        if (currentDashboard != null) Destroy(currentDashboard);

        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            var mover = w.GetComponent<MovToDestination>();
            if (mover != null) mover.StopDelivery();
        }

        // 워커/박스 초기화
        workerManager.ResetAllWorkers();    // 아래 3) 참고
        boxManager.ResetAllBoxes();      // 박스도 리셋하려면 별도 매니저 추천
    }

    private GameObject GetDashboardForMission(int missionNumber)
    {
        if (missionNumber >= 1 && missionNumber <= 4)
            return personalDashboardPrefab;
        else if (missionNumber == 5 || missionNumber == 7)
            return personalTeamDashboardPrefab;
        else if (missionNumber == 6 || missionNumber == 8)
            return teamOnlyDashboardPrefab;

        return personalDashboardPrefab;
    }

    private void PositionDashboardInFrontOfPlayer(GameObject dashboard)
    {
        RectTransform rt = dashboard.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
        }
        FollowHeadUI follow = currentDashboard.GetComponent<FollowHeadUI>();
        if (follow != null)
            follow.xrCameraTransform = xrCameraTransform;
    }

    // private IEnumerator WaitAndStartNextMission()
    // {
    //     yield return new WaitForSeconds(delayAfterMission);
    //     StartNextMission();
    // }
    private void ApplyAgentSpeed(float speed)
    {
        var workers = workerManager.GetWorkers();
        foreach (var w in workers)
        {
            if (w.IsPlayer)
            {
                continue;
            }
            if (w.WorkerData.WorkerName == "Mia")
            {
                var agent = w.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent) agent.speed = speed;
            }


            var mover = w.GetComponent<MovToDestination>();
            if (mover != null) mover.BeginDelivery();
        }
    }
}

