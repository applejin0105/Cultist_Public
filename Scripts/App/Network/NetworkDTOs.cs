namespace App.Network
{
    public sealed class NetworkDTOs
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
    }
}