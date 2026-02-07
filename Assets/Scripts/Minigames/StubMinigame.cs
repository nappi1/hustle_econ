using UnityEngine;

namespace Minigames
{
    public class StubMinigame : Minigame
    {
        private float baseScore = 60f;
        private float variance = 0f;

        public StubMinigame(MinigameInstance instance) : base(instance)
        {
        }

        public override void Initialize()
        {
            baseScore = Random.Range(50f, 70f);
        }

        public override void UpdateLogic(float deltaTime)
        {
            variance += (Random.value - 0.5f) * deltaTime * 10f;
            variance = Mathf.Clamp(variance, -15f, 15f);
        }

        public override void HandleInput()
        {
        }

        public override float CalculatePerformance()
        {
            return Mathf.Clamp(baseScore + variance, 30f, 80f);
        }

        public override void Cleanup()
        {
        }
    }
}

