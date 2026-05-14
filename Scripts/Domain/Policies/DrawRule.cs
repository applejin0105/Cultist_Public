namespace Domain.Policies
{
    public enum DrawType
    {
        Simple, // 가져온 카드를 모두 핸드에 넣음
        Draft // 가져온 카드 중 1장을 선택하고 나머지는 교역소
    }

    public enum DrawPhaseAction
    {
        None,
        Draw,
        Trade
    }

    public class DrawRule
    {
        public DrawType Type { get; set; } = DrawType.Simple;
        public bool SkipSelection { get; set; }
        public int Amount { get; set; }
        public Newtonsoft.Json.Linq.JObject CardCondition { get; set; }

        public static DrawRule Standard => new DrawRule
        {
            Type = DrawType.Draft,
            SkipSelection = false,
            Amount = 3,
            CardCondition = null
        };
    }
}