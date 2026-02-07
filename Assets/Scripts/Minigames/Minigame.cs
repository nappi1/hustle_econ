using Minigames;

namespace HustleEconomy.Minigames
{
    public abstract class Minigame
    {
        protected MinigameInstance instance;

        protected Minigame(MinigameInstance instance)
        {
            this.instance = instance;
        }

        public abstract void Initialize();
        public abstract void UpdateLogic(float deltaTime);
        public abstract void HandleInput();
        public abstract float CalculatePerformance();
        public abstract void Cleanup();

        protected float CalculateAccuracyScore()
        {
            int total = instance.successfulActions + instance.failedActions;
            if (total == 0)
            {
                return 50f;
            }

            return (instance.successfulActions / (float)total) * 100f;
        }
    }
}
