using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DashboardKind { Personal, PersonalTeam, TeamOnly }

public struct MissionConfig {
    public float agentSpeed;
    public bool anonymous;
    public DashboardKind dashboard;
    public int timeLimitSec;
}

public interface IMission {
    MissionConfig GetConfig();
    void StartMission();
    void CompleteMission();
}
