using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WorkerMissionBase : MonoBehaviour, IMission
{
    //[SerializeField] protected WorkerManager workerManager;

    [Header("Mission Settings")]
    [SerializeField] private float agentSpeed = 3.5f;
    [SerializeField] private bool anonymous = false;
    [SerializeField] private DashboardKind dashboard = DashboardKind.Personal;
    [SerializeField] private int timeLimitSec = 300;//300; // 5분

    public virtual MissionConfig GetConfig() => new MissionConfig {
        agentSpeed = agentSpeed,
        anonymous = anonymous,
        dashboard = dashboard,
        timeLimitSec = timeLimitSec
    };
    public virtual void StartMission() { /* 필요시 훅 */ }
    public virtual void CompleteMission() { Debug.Log($"{name} mission complete"); }
}


