namespace App.Network
{
    public class NetworkDTOs
    {
        // 카드 상태 동기화용 구조체
        public struct CardNetData
        {
            public int InstanceId;
            public int CardId;
            public int OwnerSeat;
            public int Zone;
            public int Status;
            public bool IsReveal;

            public int ParentInstanceId;

            public int SiblingIndex;
        }

        public struct PlayerNetData
        {
            public int SeatIndex;
            public int LifeStatus;
            public int Cultist;
            public int[] Symbols;
        }
    }
}