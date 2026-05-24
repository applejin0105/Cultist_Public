using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>
    /// 카드 원본 데이터
    /// </summary>
    public sealed class Card
    {
        public int Id { get; }
        public string Name { get; }

        private readonly int[] _symbolR;
        public IReadOnlyList<int> SymbolR => _symbolR;

        private readonly int[] _symbolG;
        public IReadOnlyList<int> SymbolG => _symbolG;

        public int Cultist { get; }
        public int Junction { get; }
        public string Effect { get; }
        public string Description { get; }
        // 루트 카드 여부 판단
        public bool IsRoot { get; }
        // 자신의 필드에서 '한 장'만 앞으로 존재할 수 있는지
        public bool IsUniqueReveal { get; }
        // 카드 효과 사용을 반복할 수 있는지
        public bool IsRepeatable { get; }
        // 내려놓자마자 발동되는 카드인지
        public bool IsRevealImmediately { get; }
        // 상대의 카드 파괴 효과를 받으면 파괴 되는 대신 뒤집힘
        public bool IsEcho { get; }
        // Hand에서 강제 선택
        public bool IsForceSelect { get; }
        // 파괴되는 대신 제외되는지(파괴되면 교역소에 추가됨)
        public bool IsCrisis { get; }
        // 인게임에서 추가 가능한 카드인지
        public bool IsCollectible { get; set; }

        private const int SymbolSize = 6;

        public Card(int id, string name, int[] symbolR, int[] symbolG, int cultist, int junction, string effect,
            string description, bool isRoot, bool isUniqueReveal, bool isRepeatable, bool isRevealImmediately,
            bool isEcho, bool isForceSelect, bool isCrisis = false)
        {
            if (symbolR == null) throw new ArgumentNullException(nameof(symbolR));
            if (symbolG == null) throw new ArgumentNullException(nameof(symbolG));

            if (symbolR.Length != SymbolSize) throw new Exception("symbolR.Length != SymbolSize");
            if (symbolG.Length != SymbolSize) throw new Exception("symbolG.Length != SymbolSize");

            Id = id;
            Name = name;
            _symbolR = (int[])symbolR.Clone();
            _symbolG = (int[])symbolG.Clone();
            Cultist = cultist;
            Junction = junction;
            Effect = effect;
            Description = description;
            IsRoot = isRoot;
            IsUniqueReveal = isUniqueReveal;
            IsRepeatable = isRepeatable;
            IsRevealImmediately = isRevealImmediately;
            IsEcho = isEcho;
            IsForceSelect = isForceSelect;
            IsCrisis = isCrisis;
        }
    }
}