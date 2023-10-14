namespace StacklandsRandomizerNS {
    public class Settings {
        public readonly GoalType goal;

        public Settings(GoalType goal)
        {
            this.goal = goal;
        }
        public Settings()
        {
            this.goal = GoalType.KillDemonLord;
        }
        public enum GoalType
        {
            KillDemonLord = 0,
        }
    }
}
