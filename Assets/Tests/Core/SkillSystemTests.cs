using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.Core
{
    public class SkillSystemTests
    {
        private GameObject skillGameObject;
        private GameObject timeGameObject;
        private SkillSystem system;

        [SetUp]
        public void SetUp()
        {
            ResetSingleton(typeof(SkillSystem));
            ResetSingleton(typeof(TimeEnergySystem));

            timeGameObject = new GameObject("TimeEnergySystem");
            timeGameObject.AddComponent<TimeEnergySystem>();

            skillGameObject = new GameObject("SkillSystem");
            system = skillGameObject.AddComponent<SkillSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (skillGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(skillGameObject);
            }

            if (timeGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(timeGameObject);
            }
        }

        [Test]
        public void GetSkillLevel_NewPlayer_Returns0()
        {
            float level = system.GetSkillLevel("player", SkillSystem.SkillType.Driving);
            Assert.AreEqual(0f, level, 0.001f, "New player skill should be 0");
        }

        [Test]
        public void ModifySkill_IncreasesLevel()
        {
            system.ModifySkill("player", SkillSystem.SkillType.Stealth, 10f, "test");
            float level = system.GetSkillLevel("player", SkillSystem.SkillType.Stealth);
            Assert.AreEqual(10f, level, 0.001f, "Skill should increase");
        }

        [Test]
        public void ModifySkill_ClampsAt0And100()
        {
            system.ModifySkill("player", SkillSystem.SkillType.Stealth, -10f, "test");
            float low = system.GetSkillLevel("player", SkillSystem.SkillType.Stealth);
            system.ModifySkill("player", SkillSystem.SkillType.Stealth, 200f, "test");
            float high = system.GetSkillLevel("player", SkillSystem.SkillType.Stealth);

            Assert.AreEqual(0f, low, 0.001f, "Skill should clamp at 0");
            Assert.AreEqual(100f, high, 0.001f, "Skill should clamp at 100");
        }

        [Test]
        public void CheckSkillRequirement_MeetsThreshold_ReturnsTrue()
        {
            system.ModifySkill("player", SkillSystem.SkillType.Social, 60f, "test");
            bool ok = system.CheckSkillRequirement("player", SkillSystem.SkillType.Social, 50f);
            Assert.IsTrue(ok, "Requirement should pass when met");
        }

        [Test]
        public void CheckSkillRequirement_BelowThreshold_ReturnsFalse()
        {
            system.ModifySkill("player", SkillSystem.SkillType.Social, 40f, "test");
            bool ok = system.CheckSkillRequirement("player", SkillSystem.SkillType.Social, 50f);
            Assert.IsFalse(ok, "Requirement should fail when below");
        }

        [Test]
        public void ImproveSkillFromUse_IncreasesXP()
        {
            SkillSystem.Skill before = GetSkill("player", SkillSystem.SkillType.Trading);
            system.ImproveSkillFromUse("player", SkillSystem.SkillType.Trading, 10f);
            SkillSystem.Skill after = GetSkill("player", SkillSystem.SkillType.Trading);
            Assert.Greater(after.xp, before.xp, "XP should increase");
        }

        [Test]
        public void ImproveSkillFromUse_XPThreshold_LevelsUp()
        {
            bool leveled = false;
            float oldLevel = 0f;
            float newLevel = 0f;
            system.OnSkillChanged += (pid, skill, oldLvl, newLvl) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Stealth)
                {
                    leveled = true;
                    oldLevel = oldLvl;
                    newLevel = newLvl;
                }
            };

            for (int i = 0; i < 10; i++)
            {
                system.ImproveSkillFromUse("player", SkillSystem.SkillType.Stealth, 10f);
            }

            Assert.IsTrue(leveled, "Should have leveled up");
            Assert.AreEqual(0f, oldLevel, 0.001f, "Old level should be 0");
            Assert.AreEqual(1f, newLevel, 0.001f, "New level should be 1");
        }

        [Test]
        public void ImproveSkillFromUse_DiminishingReturns_LowerGainAtHighLevel()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Persuasion, 10f);
            float lowGain = CaptureImprovedGain("player", SkillSystem.SkillType.Persuasion, 10f);

            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Persuasion, 90f);
            float highGain = CaptureImprovedGain("player", SkillSystem.SkillType.Persuasion, 10f);

            Assert.Greater(lowGain, highGain, "High skill should gain less");
        }

        [Test]
        public void ImproveSkillFromUse_Skill0_FullBaseGain()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Cleaning, 0f);
            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Cleaning, 10f);
            Assert.AreEqual(10f, gain, 0.01f, "Skill 0 should gain full base");
        }

        [Test]
        public void ImproveSkillFromUse_Skill75_HalfBaseGain()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Cleaning, 75f);
            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Cleaning, 10f);
            Assert.AreEqual(5f, gain, 0.01f, "Skill 75 should yield half gain");
        }

        [Test]
        public void ImproveSkillFromUse_Skill100_StillGainsSlow()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Cleaning, 100f);
            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Cleaning, 10f);
            Assert.Greater(gain, 0f, "Skill 100 should still gain some XP");
        }

        [Test]
        public void ImproveSkillFromUse_UpdatesLastUsed()
        {
            DateTime before = TimeEnergySystem.Instance.GetCurrentTime().AddDays(-5);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Mechanical, before);

            system.ImproveSkillFromUse("player", SkillSystem.SkillType.Mechanical, 10f);
            SkillSystem.Skill skill = GetSkill("player", SkillSystem.SkillType.Mechanical);

            Assert.Greater(skill.lastUsed, before, "Last used should update");
        }

        [Test]
        public void ImproveSkillFromUse_UpdatesPeakLevel()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Mechanical, 0f);

            for (int i = 0; i < 10; i++)
            {
                system.ImproveSkillFromUse("player", SkillSystem.SkillType.Mechanical, 10f);
            }

            SkillSystem.Skill skill = GetSkill("player", SkillSystem.SkillType.Mechanical);
            Assert.AreEqual(1f, skill.peakLevel, 0.001f, "Peak level should update");
        }

        [Test]
        public void ImproveSkillFromUse_AfterDecay_2xFasterRelearning()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Persuasion, 100f);
            system.ModifySkill("player", SkillSystem.SkillType.Persuasion, -20f, "decay");

            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Persuasion, 10f);
            float expected = 10f * (1f - (80f / 150f)) * 2f;

            Assert.AreEqual(expected, gain, 0.05f, "Relearning bonus should double gain");
        }

        [Test]
        public void ImproveSkillFromUse_NoDecay_NormalSpeed()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Persuasion, 80f);
            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Persuasion, 10f);
            float expected = 10f * (1f - (80f / 150f));
            Assert.AreEqual(expected, gain, 0.05f, "No decay should use normal gain");
        }

        [Test]
        public void ImproveSkillFromUse_SmallDecay_NoBonus()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Persuasion, 100f);
            system.ModifySkill("player", SkillSystem.SkillType.Persuasion, -5f, "small");

            float gain = CaptureImprovedGain("player", SkillSystem.SkillType.Persuasion, 10f);
            float expected = 10f * (1f - (95f / 150f));

            Assert.AreEqual(expected, gain, 0.05f, "Small decay should not trigger bonus");
        }

        [Test]
        public void Milestone_Reaching25_FiresEvent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Social, 24f);
            bool fired = false;
            int milestone = 0;
            system.OnSkillMilestone += (pid, skill, value) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Social)
                {
                    fired = true;
                    milestone = value;
                }
            };

            system.ModifySkill("player", SkillSystem.SkillType.Social, 2f, "test");

            Assert.IsTrue(fired, "Milestone should fire");
            Assert.AreEqual(25, milestone, "Milestone should be 25");
        }

        [Test]
        public void Milestone_Reaching50_FiresEvent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Social, 49f);
            bool fired = false;
            int milestone = 0;
            system.OnSkillMilestone += (pid, skill, value) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Social)
                {
                    fired = true;
                    milestone = value;
                }
            };

            system.ModifySkill("player", SkillSystem.SkillType.Social, 2f, "test");

            Assert.IsTrue(fired, "Milestone should fire");
            Assert.AreEqual(50, milestone, "Milestone should be 50");
        }

        [Test]
        public void Milestone_Reaching75_FiresEvent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Social, 74f);
            bool fired = false;
            int milestone = 0;
            system.OnSkillMilestone += (pid, skill, value) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Social)
                {
                    fired = true;
                    milestone = value;
                }
            };

            system.ModifySkill("player", SkillSystem.SkillType.Social, 2f, "test");

            Assert.IsTrue(fired, "Milestone should fire");
            Assert.AreEqual(75, milestone, "Milestone should be 75");
        }

        [Test]
        public void Milestone_Reaching100_FiresEvent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Social, 99f);
            bool fired = false;
            int milestone = 0;
            system.OnSkillMilestone += (pid, skill, value) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Social)
                {
                    fired = true;
                    milestone = value;
                }
            };

            system.ModifySkill("player", SkillSystem.SkillType.Social, 2f, "test");

            Assert.IsTrue(fired, "Milestone should fire");
            Assert.AreEqual(100, milestone, "Milestone should be 100");
        }

        [Test]
        public void Milestone_DrivingSkill50_UnlocksRideshare()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Driving, 49f);
            bool logged = false;
            Application.LogCallback handler = (condition, stackTrace, type) =>
            {
                if (type == LogType.Log && condition == "Unlock: Rideshare driver job available")
                {
                    logged = true;
                }
            };

            Application.logMessageReceived += handler;
            try
            {
                system.ModifySkill("player", SkillSystem.SkillType.Driving, 2f, "test");
            }
            finally
            {
                Application.logMessageReceived -= handler;
            }

            Assert.IsTrue(logged, "Should log rideshare unlock");
        }

        [Test]
        public void SkillDecay_Before30Days_NoDecay()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Fitness, 80f);
            DateTime lastUsed = TimeEnergySystem.Instance.GetCurrentTime().AddDays(-20);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Fitness, lastUsed);
            float before = system.GetSkillLevel("player", SkillSystem.SkillType.Fitness);

            system.ProcessSkillDecayForTesting(1f);

            float after = system.GetSkillLevel("player", SkillSystem.SkillType.Fitness);
            Assert.AreEqual(before, after, 0.001f, "No decay before 30 days");
        }

        [Test]
        public void SkillDecay_After30Days_StartsDecaying()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Fitness, 80f);
            DateTime lastUsed = TimeEnergySystem.Instance.GetCurrentTime().AddDays(-35);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Fitness, lastUsed);
            float before = system.GetSkillLevel("player", SkillSystem.SkillType.Fitness);

            system.ProcessSkillDecayForTesting(1f);

            float after = system.GetSkillLevel("player", SkillSystem.SkillType.Fitness);
            Assert.Less(after, before, "Skill should decay after 30 days");
        }

        [Test]
        public void SkillDecay_NeverBelowFloor_StopsAt60Percent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Fitness, 100f);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Fitness, TimeEnergySystem.Instance.GetCurrentTime().AddDays(-200));

            for (int i = 0; i < 50; i++)
            {
                system.ProcessSkillDecayForTesting(1f);
            }

            float level = system.GetSkillLevel("player", SkillSystem.SkillType.Fitness);
            Assert.GreaterOrEqual(level, 60f, "Skill should not decay below 60% of peak");
        }

        [Test]
        public void SkillDecay_DrivingSkill_NeverDecays()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Driving, 80f);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Driving, TimeEnergySystem.Instance.GetCurrentTime().AddDays(-100));
            float before = system.GetSkillLevel("player", SkillSystem.SkillType.Driving);

            system.ProcessSkillDecayForTesting(1f);

            float after = system.GetSkillLevel("player", SkillSystem.SkillType.Driving);
            Assert.AreEqual(before, after, 0.001f, "Driving should not decay");
        }

        [Test]
        public void SkillDecay_CookingSkill_NeverDecays()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Cooking, 80f);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Cooking, TimeEnergySystem.Instance.GetCurrentTime().AddDays(-100));
            float before = system.GetSkillLevel("player", SkillSystem.SkillType.Cooking);

            system.ProcessSkillDecayForTesting(1f);

            float after = system.GetSkillLevel("player", SkillSystem.SkillType.Cooking);
            Assert.AreEqual(before, after, 0.001f, "Cooking should not decay");
        }

        [Test]
        public void SkillDecay_FiresOnSkillDecayedEvent()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Fitness, 80f);
            system.SetSkillLastUsedForTesting("player", SkillSystem.SkillType.Fitness, TimeEnergySystem.Instance.GetCurrentTime().AddDays(-35));

            bool fired = false;
            system.OnSkillDecayed += (pid, skill) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Fitness)
                {
                    fired = true;
                }
            };

            system.ProcessSkillDecayForTesting(1f);

            Assert.IsTrue(fired, "Decay should fire event");
        }

        [Test]
        public void OnSkillChanged_WhenModified_FiresWithCorrectParameters()
        {
            string capturedPlayer = null;
            SkillSystem.SkillType capturedSkill = SkillSystem.SkillType.Driving;
            float capturedOld = 0f;
            float capturedNew = 0f;

            system.OnSkillChanged += (pid, skill, oldLevel, newLevel) =>
            {
                capturedPlayer = pid;
                capturedSkill = skill;
                capturedOld = oldLevel;
                capturedNew = newLevel;
            };

            system.ModifySkill("player", SkillSystem.SkillType.Driving, 10f, "test");

            Assert.AreEqual("player", capturedPlayer, "Player id should match");
            Assert.AreEqual(SkillSystem.SkillType.Driving, capturedSkill, "Skill should match");
            Assert.AreEqual(0f, capturedOld, 0.001f, "Old level should match");
            Assert.AreEqual(10f, capturedNew, 0.001f, "New level should match");
        }

        [Test]
        public void OnSkillImproved_WhenUsed_FiresWithGainAmount()
        {
            float capturedGain = 0f;
            system.OnSkillImproved += (pid, skill, gain) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Trading)
                {
                    capturedGain = gain;
                }
            };

            system.ImproveSkillFromUse("player", SkillSystem.SkillType.Trading, 10f);

            Assert.AreEqual(10f, capturedGain, 0.01f, "Gain amount should match");
        }

        [Test]
        public void OnSkillMilestone_AtMilestone_FiresWithCorrectLevel()
        {
            system.SetSkillLevelForTesting("player", SkillSystem.SkillType.Trading, 24f);
            int milestone = 0;
            system.OnSkillMilestone += (pid, skill, value) =>
            {
                if (pid == "player" && skill == SkillSystem.SkillType.Trading)
                {
                    milestone = value;
                }
            };

            system.ModifySkill("player", SkillSystem.SkillType.Trading, 2f, "test");

            Assert.AreEqual(25, milestone, "Milestone should be 25");
        }

        private float CaptureImprovedGain(string playerId, SkillSystem.SkillType skill, float baseGain)
        {
            float captured = 0f;
            system.OnSkillImproved += (pid, s, gain) =>
            {
                if (pid == playerId && s == skill)
                {
                    captured = gain;
                }
            };

            system.ImproveSkillFromUse(playerId, skill, baseGain);
            return captured;
        }

        private SkillSystem.Skill GetSkill(string playerId, SkillSystem.SkillType skill)
        {
            system.GetSkillLevel(playerId, skill);
            Dictionary<string, SkillSystem.SkillProfile> profiles = GetPrivateField<Dictionary<string, SkillSystem.SkillProfile>>(system, "profiles");
            SkillSystem.SkillProfile profile = profiles[playerId];
            return profile.skills[skill];
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
