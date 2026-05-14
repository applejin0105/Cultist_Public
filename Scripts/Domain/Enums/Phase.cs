namespace Domain.Enums
{
    public static class Phase
    {
        public enum Main
        {
            StandBy,
            Draw,
            Play,
        }

        public enum Draw
        {
            StandBy,
            Draw,
            Trade
        }

        public enum Play
        {
            Play
        }
    }
}