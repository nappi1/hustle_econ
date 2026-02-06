using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public class JobSystem : MonoBehaviour
    {
        public enum JobType
        {
            Janitorial,
            Retail,
            Restaurant,
            Office,
            Warehouse,
            Gig,
            Creative
        }

        public enum MinigameType
        {
            ClickTargets,
            SequenceMatch,
            TimingGame,
            EmailManagement,
            Driving
        }

        [System.Serializable]
        public struct ShiftSchedule
        {
            public DayOfWeek day;
            public TimeSpan startTime;
            public float durationHours;
        }

        [System.Serializable]
        public struct ShiftResults
        {
            public float payEarned;
            public float performanceScore;
            public int warningsIssued;
            public bool caughtSlacking;
            public float bonusPay;
        }

        [System.Serializable]
        public class Job
        {
            public string id;
            public string title;
            public float hourlyWage;
            public JobType type;

            public List<ShiftSchedule> shifts;
            public float hoursPerWeek;

            public Dictionary<ReputationSystem.ReputationTrack, float> reputationRequired;
            public bool requiresCleanRecord;

            public MinigameType minigameType;
            public float detectionSensitivity;

            public List<string> promotionPath;
            public float promotionThreshold;

            public int warningCount;
            public float performanceScore;
            public DateTime hireDate;
            public DateTime lastShift;
            public bool isActive;
        }

        private static JobSystem instance;
        public static JobSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<JobSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("JobSystem");
                        instance = go.AddComponent<JobSystem>();
                    }
                }
                return instance;
            }
        }

        public event Action<string> OnJobHired;
        public event Action<string> OnJobQuit;
        public event Action<string, string> OnJobFired;
        public event Action<string> OnShiftStarted;
        public event Action<ShiftResults> OnShiftEnded;
        public event Action<string, string> OnWarningIssued;
        public event Action<string, string> OnPromotion;

        private Dictionary<string, Job> jobs;
        private Dictionary<string, List<string>> playerJobs;
        private Dictionary<string, string> activeShifts;
        private Dictionary<string, float> testShiftPerformance;
        private Dictionary<string, bool> testShiftDetection;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            jobs = new Dictionary<string, Job>();
            playerJobs = new Dictionary<string, List<string>>();
            activeShifts = new Dictionary<string, string>();
            testShiftPerformance = new Dictionary<string, float>();
            testShiftDetection = new Dictionary<string, bool>();
        }

        public bool ApplyForJob(string playerId, string jobId)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return false;
            }

            if (job.isActive)
            {
                return false;
            }

            if (job.reputationRequired != null)
            {
                foreach (KeyValuePair<ReputationSystem.ReputationTrack, float> req in job.reputationRequired)
                {
                    float playerRep = ReputationSystem.Instance.GetReputation(playerId, req.Key);
                    if (playerRep < req.Value)
                    {
                        return false;
                    }
                }
            }

            if (job.requiresCleanRecord)
            {
                float legalRep = ReputationSystem.Instance.GetReputation(playerId, ReputationSystem.ReputationTrack.Legal);
                if (legalRep < 50f)
                {
                    return false;
                }
            }

            job.hireDate = TimeEnergySystem.Instance.GetCurrentTime();
            job.warningCount = 0;
            job.performanceScore = 50f;
            job.isActive = true;

            if (!playerJobs.ContainsKey(playerId))
            {
                playerJobs[playerId] = new List<string>();
            }

            if (!playerJobs[playerId].Contains(jobId))
            {
                playerJobs[playerId].Add(jobId);
            }

            OnJobHired?.Invoke(jobId);
            return true;
        }

        public void QuitJob(string playerId, string jobId)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return;
            }

            job.isActive = false;

            if (playerJobs.ContainsKey(playerId))
            {
                playerJobs[playerId].Remove(jobId);
            }

            ReputationSystem.Instance.ModifyReputation(
                playerId,
                ReputationSystem.ReputationTrack.Professional,
                -5f,
                "Quit job"
            );

            OnJobQuit?.Invoke(jobId);
        }

        public void StartShift(string playerId, string jobId)
        {
            Job job = GetJob(jobId);
            if (job == null || !job.isActive)
            {
                Debug.LogWarning($"StartShift: Job {jobId} not found or not active");
                return;
            }

            job.lastShift = TimeEnergySystem.Instance.GetCurrentTime();
            activeShifts[playerId] = jobId;
            OnShiftStarted?.Invoke(jobId);
        }

        public ShiftResults EndShift(string playerId, string jobId)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return new ShiftResults { payEarned = 0f };
            }

            float performanceScore = GetShiftPerformance(playerId, jobId);
            bool caughtSlacking = GetShiftDetectionStatus(playerId, jobId);

            float hoursWorked = 8f;
            if (job.shifts != null && job.shifts.Count > 0)
            {
                hoursWorked = job.shifts[0].durationHours > 0f ? job.shifts[0].durationHours : 8f;
            }

            float basePay = job.hourlyWage * hoursWorked;
            float bonusPay = 0f;
            if (performanceScore > 80f)
            {
                bonusPay = basePay * 0.1f;
            }

            float totalPay = basePay + bonusPay;

            EconomySystem.Instance.AddIncome(
                playerId,
                totalPay,
                EconomySystem.IncomeSource.Salary,
                $"{job.title} shift"
            );

            job.performanceScore = (job.performanceScore * 0.8f) + (performanceScore * 0.2f);

            int warningsThisShift = 0;
            if (caughtSlacking)
            {
                TriggerWarning(jobId, "Caught slacking off");
                warningsThisShift++;
            }

            if (performanceScore < 30f)
            {
                TriggerWarning(jobId, "Poor performance");
                warningsThisShift++;
            }

            if (job.performanceScore > job.promotionThreshold && job.warningCount == 0)
            {
                CheckPromotion(jobId);
            }

            activeShifts.Remove(playerId);

            ShiftResults results = new ShiftResults
            {
                payEarned = totalPay,
                performanceScore = performanceScore,
                warningsIssued = warningsThisShift,
                caughtSlacking = caughtSlacking,
                bonusPay = bonusPay
            };

            OnShiftEnded?.Invoke(results);
            return results;
        }

        public int GetWarningCount(string jobId)
        {
            Job job = GetJob(jobId);
            return job != null ? job.warningCount : 0;
        }

        public void TriggerWarning(string jobId, string reason)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return;
            }

            job.warningCount++;
            OnWarningIssued?.Invoke(jobId, reason);

            if (job.warningCount >= 3)
            {
                FirePlayer(jobId, "Exceeded warning limit");
            }
        }

        public void GetPromotion(string jobId, string newTitle)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return;
            }

            string oldTitle = job.title;
            job.title = newTitle;
            job.hourlyWage *= UnityEngine.Random.Range(1.2f, 1.4f);

            if (ContainsManagerTitle(newTitle))
            {
                job.hoursPerWeek *= 0.8f;
                job.detectionSensitivity *= 0.7f;
            }

            string playerId = GetPlayerForJob(jobId);
            ReputationSystem.Instance.ModifyReputation(
                playerId,
                ReputationSystem.ReputationTrack.Professional,
                15f,
                $"Promoted to {newTitle}"
            );

            Core.RelationshipSystem relationshipSystem = FindAnyObjectByType<Core.RelationshipSystem>();
            if (relationshipSystem != null)
            {
                relationshipSystem.ObservePlayerAction(new Core.RelationshipSystem.PlayerAction
                {
                    type = Core.RelationshipSystem.ActionType.GotPromoted,
                    details = $"{oldTitle} -> {newTitle}",
                    timestamp = TimeEnergySystem.Instance.GetCurrentTime(),
                    memorability = 6
                });
            }

            OnPromotion?.Invoke(jobId, newTitle);
        }

        public void CreateJob(Job job)
        {
            if (job == null || string.IsNullOrEmpty(job.id))
            {
                return;
            }

            jobs[job.id] = job;
        }

        public void SetShiftPerformanceForTesting(string playerId, string jobId, float performance)
        {
            testShiftPerformance[$"{playerId}_{jobId}"] = performance;
        }

        public void SetShiftDetectionForTesting(string playerId, string jobId, bool detected)
        {
            testShiftDetection[$"{playerId}_{jobId}"] = detected;
        }

        private Job GetJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                Debug.LogWarning("GetJob: jobId is null or empty");
                return null;
            }

            if (!jobs.TryGetValue(jobId, out Job job))
            {
                Debug.LogWarning($"GetJob: Job {jobId} not found");
                return null;
            }

            return job;
        }

        private void FirePlayer(string jobId, string reason)
        {
            Job job = GetJob(jobId);
            if (job == null)
            {
                return;
            }

            job.isActive = false;

            string playerId = GetPlayerForJob(jobId);
            ReputationSystem.Instance.ModifyReputation(
                playerId,
                ReputationSystem.ReputationTrack.Professional,
                -20f,
                $"Fired from {job.title}"
            );

            OnJobFired?.Invoke(jobId, reason);
        }

        private void CheckPromotion(string jobId)
        {
            Job job = GetJob(jobId);
            if (job == null || job.promotionPath == null || job.promotionPath.Count == 0)
            {
                return;
            }

            int currentIndex = job.promotionPath.IndexOf(job.title);
            if (currentIndex < 0 || currentIndex >= job.promotionPath.Count - 1)
            {
                return;
            }

            string newTitle = job.promotionPath[currentIndex + 1];
            GetPromotion(jobId, newTitle);
        }

        private float GetShiftPerformance(string playerId, string jobId)
        {
            string key = $"{playerId}_{jobId}";
            if (testShiftPerformance.TryGetValue(key, out float performance))
            {
                return performance;
            }

            return 70f;
        }

        private bool GetShiftDetectionStatus(string playerId, string jobId)
        {
            string key = $"{playerId}_{jobId}";
            if (testShiftDetection.TryGetValue(key, out bool detected))
            {
                return detected;
            }

            return false;
        }

        private string GetPlayerForJob(string jobId)
        {
            foreach (KeyValuePair<string, List<string>> entry in playerJobs)
            {
                if (entry.Value != null && entry.Value.Contains(jobId))
                {
                    return entry.Key;
                }
            }

            return "player";
        }

        private static bool ContainsManagerTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }

            return title.IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("supervisor", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
