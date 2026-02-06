using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class JobSystemTests
    {
        private GameObject jobGameObject;
        private GameObject timeGameObject;
        private GameObject reputationGameObject;
        private GameObject economyGameObject;
        private JobSystem jobSystem;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(JobSystem));
            ResetSingleton(typeof(TimeEnergySystem));
            ResetSingleton(typeof(ReputationSystem));
            ResetSingleton(typeof(EconomySystem));
            ResetSingleton(typeof(RelationshipSystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            reputationGameObject = new GameObject("ReputationSystem");
            reputationGameObject.AddComponent<ReputationSystem>();

            economyGameObject = new GameObject("EconomySystem");
            economyGameObject.AddComponent<EconomySystem>();

            jobGameObject = new GameObject("JobSystem");
            jobSystem = jobGameObject.AddComponent<JobSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (jobGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(jobGameObject);
            }

            if (economyGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(economyGameObject);
            }

            if (reputationGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(reputationGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void ApplyForJob_MeetsRequirements_Hired()
        {
            var job = CreateBaseJob("job1", "Janitor", 15f);
            job.reputationRequired = new Dictionary<ReputationSystem.ReputationTrack, float>
            {
                { ReputationSystem.ReputationTrack.Professional, 40f }
            };
            jobSystem.CreateJob(job);

            bool hired = jobSystem.ApplyForJob("player", job.id);

            Assert.IsTrue(hired, "Should hire when requirements met");
        }

        [Test]
        public void ApplyForJob_InsufficientReputation_Rejected()
        {
            var job = CreateBaseJob("job2", "Office Manager", 20f);
            job.reputationRequired = new Dictionary<ReputationSystem.ReputationTrack, float>
            {
                { ReputationSystem.ReputationTrack.Professional, 70f }
            };
            jobSystem.CreateJob(job);

            bool hired = jobSystem.ApplyForJob("player", job.id);

            Assert.IsFalse(hired, "Should be rejected due to insufficient reputation");
        }

        [Test]
        public void ApplyForJob_CriminalRecord_Rejected()
        {
            var job = CreateBaseJob("job3", "Bank Teller", 22f);
            job.requiresCleanRecord = true;
            jobSystem.CreateJob(job);

            ReputationSystem.Instance.ModifyReputation("player", ReputationSystem.ReputationTrack.Legal, -40f, "record");

            bool hired = jobSystem.ApplyForJob("player", job.id);

            Assert.IsFalse(hired, "Should be rejected due to criminal record proxy");
        }

        [Test]
        public void ApplyForJob_SetsInitialValues_CorrectlyInitialized()
        {
            var job = CreateBaseJob("job4", "Retail Associate", 13f);
            jobSystem.CreateJob(job);

            DateTime now = TimeEnergySystem.Instance.GetCurrentTime();
            bool hired = jobSystem.ApplyForJob("player", job.id);

            Assert.IsTrue(hired, "Job should be hired");
            Assert.AreEqual(0, job.warningCount, "Warning count should start at 0");
            Assert.AreEqual(50f, job.performanceScore, 0.001f, "Performance should start at 50");
            Assert.IsTrue(job.isActive, "Job should be active after hire");
            Assert.AreEqual(now, job.hireDate, "Hire date should match current time");
        }

        [Test]
        public void ApplyForJob_FiresOnJobHiredEvent()
        {
            var job = CreateBaseJob("job5", "Warehouse Worker", 14f);
            jobSystem.CreateJob(job);

            string captured = null;
            jobSystem.OnJobHired += jobId => captured = jobId;

            jobSystem.ApplyForJob("player", job.id);

            Assert.AreEqual(job.id, captured, "OnJobHired should fire with job id");
        }

        [Test]
        public void ApplyForJob_AddsToPlayerJobsList()
        {
            var job = CreateBaseJob("job6", "Cook", 16f);
            jobSystem.CreateJob(job);

            jobSystem.ApplyForJob("player", job.id);

            Dictionary<string, List<string>> playerJobs = GetPrivateField<Dictionary<string, List<string>>>(jobSystem, "playerJobs");
            Assert.IsTrue(playerJobs.ContainsKey("player"), "Player jobs list should exist");
            Assert.IsTrue(playerJobs["player"].Contains(job.id), "Player jobs should include job id");
        }

        [Test]
        public void StartShift_ActiveJob_StartsSuccessfully()
        {
            var job = CreateBaseJob("job7", "Retail", 15f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.StartShift("player", job.id);

            Dictionary<string, string> activeShifts = GetPrivateField<Dictionary<string, string>>(jobSystem, "activeShifts");
            Assert.AreEqual(job.id, activeShifts["player"], "Active shift should be tracked");
        }

        [Test]
        public void StartShift_InactiveJob_FailsGracefully()
        {
            var job = CreateBaseJob("job8", "Office", 20f);
            jobSystem.CreateJob(job);

            jobSystem.StartShift("player", job.id);

            Dictionary<string, string> activeShifts = GetPrivateField<Dictionary<string, string>>(jobSystem, "activeShifts");
            Assert.IsFalse(activeShifts.ContainsKey("player"), "Inactive job should not start shift");
        }

        [Test]
        public void StartShift_FiresOnShiftStartedEvent()
        {
            var job = CreateBaseJob("job9", "Driver", 18f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            string captured = null;
            jobSystem.OnShiftStarted += jobId => captured = jobId;

            jobSystem.StartShift("player", job.id);

            Assert.AreEqual(job.id, captured, "OnShiftStarted should fire with job id");
        }

        [Test]
        public void EndShift_CalculatesPayCorrectly()
        {
            var job = CreateBaseJob("job10", "Retail Associate", 15f);
            job.shifts = new List<JobSystem.ShiftSchedule>
            {
                new JobSystem.ShiftSchedule { durationHours = 8f }
            };
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 70f);
            jobSystem.SetShiftDetectionForTesting("player", job.id, false);

            JobSystem.ShiftResults results = jobSystem.EndShift("player", job.id);

            Assert.AreEqual(120f, results.payEarned, 0.01f, "Pay should be wage * hours");
            Assert.AreEqual(0f, results.bonusPay, 0.01f, "No bonus for 70 performance");
        }

        [Test]
        public void EndShift_PerformanceAbove80_ReceivesBonus()
        {
            var job = CreateBaseJob("job11", "Sales Associate", 20f);
            job.shifts = new List<JobSystem.ShiftSchedule>
            {
                new JobSystem.ShiftSchedule { durationHours = 8f }
            };
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 85f);

            JobSystem.ShiftResults results = jobSystem.EndShift("player", job.id);

            Assert.AreEqual(16f, results.bonusPay, 0.01f, "Should receive 10% bonus");
            Assert.AreEqual(176f, results.payEarned, 0.01f, "Total pay includes bonus");
        }

        [Test]
        public void EndShift_UpdatesPerformanceScore_WeightedAverage()
        {
            var job = CreateBaseJob("job12", "Office Assistant", 17f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 70f);

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(54f, job.performanceScore, 0.01f, "Weighted average should update");
        }

        [Test]
        public void EndShift_FiresOnShiftEndedEvent()
        {
            var job = CreateBaseJob("job13", "Warehouse", 14f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            bool fired = false;
            jobSystem.OnShiftEnded += results => fired = true;

            jobSystem.EndShift("player", job.id);

            Assert.IsTrue(fired, "OnShiftEnded should fire");
        }

        [Test]
        public void TriggerWarning_IncrementsWarningCount()
        {
            var job = CreateBaseJob("job14", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.TriggerWarning(job.id, "warning");

            Assert.AreEqual(1, job.warningCount, "Warning count should increment");
        }

        [Test]
        public void TriggerWarning_FiresOnWarningIssuedEvent()
        {
            var job = CreateBaseJob("job15", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            string capturedJob = null;
            string capturedReason = null;
            jobSystem.OnWarningIssued += (jobId, reason) =>
            {
                capturedJob = jobId;
                capturedReason = reason;
            };

            jobSystem.TriggerWarning(job.id, "late");

            Assert.AreEqual(job.id, capturedJob, "Warning event should pass job id");
            Assert.AreEqual("late", capturedReason, "Warning event should pass reason");
        }

        [Test]
        public void TriggerWarning_ThirdWarning_FiresPlayer()
        {
            var job = CreateBaseJob("job16", "Warehouse", 14f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            bool fired = false;
            jobSystem.OnJobFired += (jobId, reason) => fired = true;

            jobSystem.TriggerWarning(job.id, "1");
            jobSystem.TriggerWarning(job.id, "2");
            jobSystem.TriggerWarning(job.id, "3");

            Assert.AreEqual(3, job.warningCount, "Should have 3 warnings");
            Assert.IsTrue(fired, "Should fire OnJobFired event");
            Assert.IsFalse(job.isActive, "Job should be deactivated");
        }

        [Test]
        public void TriggerWarning_Firing_AffectsProfessionalReputation()
        {
            var job = CreateBaseJob("job17", "Warehouse", 14f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            jobSystem.TriggerWarning(job.id, "1");
            jobSystem.TriggerWarning(job.id, "2");
            jobSystem.TriggerWarning(job.id, "3");

            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(before - 20f, after, 0.01f, "Firing should reduce professional reputation by 20");
        }

        [Test]
        public void TriggerWarning_Firing_DeactivatesJob()
        {
            var job = CreateBaseJob("job18", "Warehouse", 14f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.TriggerWarning(job.id, "1");
            jobSystem.TriggerWarning(job.id, "2");
            jobSystem.TriggerWarning(job.id, "3");

            Assert.IsFalse(job.isActive, "Job should be deactivated after firing");
        }

        [Test]
        public void EndShift_CaughtSlacking_IssuesWarning()
        {
            var job = CreateBaseJob("job19", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 70f);
            jobSystem.SetShiftDetectionForTesting("player", job.id, true);

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(1, job.warningCount, "Caught slacking should issue warning");
        }

        [Test]
        public void EndShift_PoorPerformance_IssuesWarning()
        {
            var job = CreateBaseJob("job20", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 20f);
            jobSystem.SetShiftDetectionForTesting("player", job.id, false);

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(1, job.warningCount, "Poor performance should issue warning");
        }

        [Test]
        public void EndShift_Performance80_NoBonus()
        {
            var job = CreateBaseJob("job21", "Sales", 20f);
            job.shifts = new List<JobSystem.ShiftSchedule>
            {
                new JobSystem.ShiftSchedule { durationHours = 8f }
            };
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 80f);

            JobSystem.ShiftResults results = jobSystem.EndShift("player", job.id);

            Assert.AreEqual(0f, results.bonusPay, 0.01f, "80 performance should not receive bonus");
        }

        [Test]
        public void EndShift_Performance79_NoBonus()
        {
            var job = CreateBaseJob("job22", "Sales", 20f);
            job.shifts = new List<JobSystem.ShiftSchedule>
            {
                new JobSystem.ShiftSchedule { durationHours = 8f }
            };
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 79f);

            JobSystem.ShiftResults results = jobSystem.EndShift("player", job.id);

            Assert.AreEqual(0f, results.bonusPay, 0.01f, "79 performance should not receive bonus");
        }

        [Test]
        public void EndShift_PerformanceBelow30_IssuesWarning()
        {
            var job = CreateBaseJob("job23", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 25f);

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(1, job.warningCount, "Performance below 30 should issue warning");
        }

        [Test]
        public void EndShift_HighPerformance_UpdatesAverage()
        {
            var job = CreateBaseJob("job24", "Office", 18f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 90f);

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(58f, job.performanceScore, 0.01f, "Average should update toward high performance");
        }

        [Test]
        public void EndShift_MultipleShifts_AveragesCorrectly()
        {
            var job = CreateBaseJob("job25", "Office", 18f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.StartShift("player", job.id);
            jobSystem.SetShiftPerformanceForTesting("player", job.id, 90f);
            jobSystem.EndShift("player", job.id);

            jobSystem.StartShift("player", job.id);
            jobSystem.SetShiftPerformanceForTesting("player", job.id, 30f);
            jobSystem.EndShift("player", job.id);

            float expected = (58f * 0.8f) + (30f * 0.2f);
            Assert.AreEqual(expected, job.performanceScore, 0.01f, "Multiple shifts should update average correctly");
        }

        [Test]
        public void GetPromotion_IncreasesWage_Between20And40Percent()
        {
            var job = CreateBaseJob("job26", "Worker", 20f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            float before = job.hourlyWage;
            jobSystem.GetPromotion(job.id, "Supervisor");

            Assert.GreaterOrEqual(job.hourlyWage, before * 1.2f, "Wage should increase at least 20%");
            Assert.LessOrEqual(job.hourlyWage, before * 1.4f, "Wage should increase at most 40%");
        }

        [Test]
        public void GetPromotion_ManagerTitle_ReducesHoursAndDetection()
        {
            var job = CreateBaseJob("job27", "Worker", 20f);
            job.hoursPerWeek = 40f;
            job.detectionSensitivity = 0.8f;
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.GetPromotion(job.id, "Manager");

            Assert.AreEqual(32f, job.hoursPerWeek, 0.01f, "Manager should work fewer hours");
            Assert.AreEqual(0.56f, job.detectionSensitivity, 0.01f, "Manager should have reduced detection");
        }

        [Test]
        public void GetPromotion_UpdatesProfessionalReputation()
        {
            var job = CreateBaseJob("job28", "Worker", 20f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            jobSystem.GetPromotion(job.id, "Supervisor");
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(before + 15f, after, 0.01f, "Promotion should increase professional reputation");
        }

        [Test]
        public void GetPromotion_FiresOnPromotionEvent()
        {
            var job = CreateBaseJob("job29", "Worker", 20f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            string capturedJob = null;
            string capturedTitle = null;
            jobSystem.OnPromotion += (jobId, title) =>
            {
                capturedJob = jobId;
                capturedTitle = title;
            };

            jobSystem.GetPromotion(job.id, "Supervisor");

            Assert.AreEqual(job.id, capturedJob, "Promotion event should include job id");
            Assert.AreEqual("Supervisor", capturedTitle, "Promotion event should include new title");
        }

        [Test]
        public void CheckPromotion_HighPerformanceNoWarnings_Promotes()
        {
            var job = CreateBaseJob("job30", "Worker", 20f);
            job.promotionPath = new List<string> { "Worker", "Supervisor" };
            job.promotionThreshold = 40f;
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.StartShift("player", job.id);
            jobSystem.SetShiftPerformanceForTesting("player", job.id, 90f);
            jobSystem.EndShift("player", job.id);

            Assert.AreEqual("Supervisor", job.title, "High performance should promote");
        }

        [Test]
        public void CheckPromotion_HasWarnings_DoesNotPromote()
        {
            var job = CreateBaseJob("job31", "Worker", 20f);
            job.promotionPath = new List<string> { "Worker", "Supervisor" };
            job.promotionThreshold = 40f;
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            job.warningCount = 1;

            jobSystem.StartShift("player", job.id);
            jobSystem.SetShiftPerformanceForTesting("player", job.id, 90f);
            jobSystem.EndShift("player", job.id);

            Assert.AreEqual("Worker", job.title, "Warnings should block promotion");
        }

        [Test]
        public void QuitJob_DeactivatesJob()
        {
            var job = CreateBaseJob("job32", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            jobSystem.QuitJob("player", job.id);

            Assert.IsFalse(job.isActive, "Quit should deactivate job");
        }

        [Test]
        public void QuitJob_AffectsProfessionalReputation()
        {
            var job = CreateBaseJob("job33", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            float before = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);
            jobSystem.QuitJob("player", job.id);
            float after = ReputationSystem.Instance.GetReputation("player", ReputationSystem.ReputationTrack.Professional);

            Assert.AreEqual(before - 5f, after, 0.01f, "Quitting should reduce professional reputation by 5");
        }

        [Test]
        public void OnJobFired_WhenFired_FiresWithCorrectReason()
        {
            var job = CreateBaseJob("job34", "Retail", 12f);
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);

            string capturedReason = null;
            jobSystem.OnJobFired += (jobId, reason) => capturedReason = reason;

            jobSystem.TriggerWarning(job.id, "1");
            jobSystem.TriggerWarning(job.id, "2");
            jobSystem.TriggerWarning(job.id, "3");

            Assert.AreEqual("Exceeded warning limit", capturedReason, "Fired reason should match");
        }

        [Test]
        public void OnShiftEnded_ContainsCorrectResults()
        {
            var job = CreateBaseJob("job35", "Retail", 10f);
            job.shifts = new List<JobSystem.ShiftSchedule>
            {
                new JobSystem.ShiftSchedule { durationHours = 8f }
            };
            jobSystem.CreateJob(job);
            jobSystem.ApplyForJob("player", job.id);
            jobSystem.StartShift("player", job.id);

            jobSystem.SetShiftPerformanceForTesting("player", job.id, 85f);
            jobSystem.SetShiftDetectionForTesting("player", job.id, true);

            JobSystem.ShiftResults captured = default;
            jobSystem.OnShiftEnded += results => captured = results;

            jobSystem.EndShift("player", job.id);

            Assert.AreEqual(88f, captured.payEarned, 0.01f, "Pay earned should include bonus");
            Assert.AreEqual(8f, captured.bonusPay, 0.01f, "Bonus should be 10%");
            Assert.AreEqual(85f, captured.performanceScore, 0.01f, "Performance score should match test value");
            Assert.IsTrue(captured.caughtSlacking, "Caught slacking should be true");
            Assert.AreEqual(1, captured.warningsIssued, "Warnings issued should be 1");
        }

        private static JobSystem.Job CreateBaseJob(string id, string title, float wage)
        {
            return new JobSystem.Job
            {
                id = id,
                title = title,
                hourlyWage = wage,
                type = JobSystem.JobType.Retail,
                shifts = new List<JobSystem.ShiftSchedule>(),
                hoursPerWeek = 40f,
                reputationRequired = new Dictionary<ReputationSystem.ReputationTrack, float>(),
                requiresCleanRecord = false,
                minigameType = JobSystem.MinigameType.ClickTargets,
                detectionSensitivity = 0.5f,
                promotionPath = new List<string>(),
                promotionThreshold = 70f,
                warningCount = 0,
                performanceScore = 50f,
                isActive = false
            };
        }

        private static void ResetSingleton(Type systemType)
        {
            FieldInfo field = systemType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(instance);
        }
    }
}
