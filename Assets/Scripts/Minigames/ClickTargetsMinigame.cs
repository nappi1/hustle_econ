using System.Collections.Generic;
using Minigames;
using UnityEngine;

namespace Minigames
{
    public class ClickTargetsMinigame : Minigame
    {
        private class Target
        {
            public Vector2 position;
            public float lifetime;
        }

        private readonly List<Target> activeTargets = new List<Target>();
        private float timeSinceLastSpawn = 0f;
        private float spawnInterval = 2f;

        public ClickTargetsMinigame(MinigameInstance instance) : base(instance)
        {
        }

        public override void Initialize()
        {
            SpawnTarget();
        }

        public override void UpdateLogic(float deltaTime)
        {
            timeSinceLastSpawn += deltaTime;
            if (timeSinceLastSpawn >= spawnInterval)
            {
                SpawnTarget();
                timeSinceLastSpawn = 0f;
            }

            for (int i = activeTargets.Count - 1; i >= 0; i--)
            {
                Target target = activeTargets[i];
                target.lifetime -= deltaTime;
                if (target.lifetime <= 0f)
                {
                    instance.failedActions++;
                    instance.totalActions++;
                    activeTargets.RemoveAt(i);
                }
            }

            spawnInterval = Mathf.Max(0.2f, 2f / Mathf.Max(0.1f, instance.difficulty));
        }

        public override void HandleInput()
        {
            if (!IsLegacyInputAvailable())
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Vector2 mousePos = Input.mousePosition;
            for (int i = 0; i < activeTargets.Count; i++)
            {
                Target target = activeTargets[i];
                if (IsClickOnTarget(mousePos, target))
                {
                    instance.successfulActions++;
                    instance.totalActions++;
                    activeTargets.RemoveAt(i);
                    SpawnTarget();
                    break;
                }
            }
        }

        public override float CalculatePerformance()
        {
            float accuracy = CalculateAccuracyScore();
            float actionsPerMinute = instance.elapsedTime > 0f
                ? instance.totalActions / (instance.elapsedTime / 60f)
                : 0f;
            float speedBonus = Mathf.Min(20f, actionsPerMinute * 2f);
            return Mathf.Clamp(accuracy + speedBonus - 20f, 0f, 100f);
        }

        public override void Cleanup()
        {
            activeTargets.Clear();
        }

        public void SimulateClickForTesting(Vector2 clickPos)
        {
            for (int i = 0; i < activeTargets.Count; i++)
            {
                Target target = activeTargets[i];
                if (IsClickOnTarget(clickPos, target))
                {
                    instance.successfulActions++;
                    instance.totalActions++;
                    activeTargets.RemoveAt(i);
                    SpawnTarget();
                    break;
                }
            }
        }

        public int GetActiveTargetCountForTesting()
        {
            return activeTargets.Count;
        }

        private void SpawnTarget()
        {
            Target target = new Target
            {
                position = GetRandomScreenPosition(),
                lifetime = 3f
            };
            activeTargets.Add(target);
        }

        private Vector2 GetRandomScreenPosition()
        {
            float x = Random.Range(0.1f, 0.9f);
            float y = Random.Range(0.1f, 0.9f);
            return new Vector2(x * 1920f, y * 1080f);
        }

        private bool IsClickOnTarget(Vector2 clickPos, Target target)
        {
            float distance = Vector2.Distance(clickPos, target.position);
            return distance < 50f;
        }

        private static bool IsLegacyInputAvailable()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return true;
#else
            return false;
#endif
        }
    }
}

