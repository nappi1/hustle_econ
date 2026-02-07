using System;
using HustleEconomy.Minigames;

namespace Minigames
{
    public enum MinigameType
    {
        ClickTargets,
        SequenceMatch,
        TimingGame,
        EmailManagement,
        Driving,
        Streaming,
        Coding
    }

    public enum MinigameState
    {
        Running,
        Paused,
        Completed,
        Failed
    }

    [System.Serializable]
    public class MinigameInstance
    {
        public string minigameId;
        public string activityId;
        public MinigameType type;
        public MinigameState state;
        public float currentPerformance;
        public float difficulty;
        public DateTime startTime;
        public float elapsedTime;
        public Minigame behavior;
        public int successfulActions;
        public int failedActions;
        public int totalActions;
    }

    [System.Serializable]
    public struct MinigameResult
    {
        public string minigameId;
        public float finalPerformance;
        public float accuracy;
        public int successfulActions;
        public int failedActions;
        public float timeElapsed;
        public bool completedSuccessfully;
    }
}
