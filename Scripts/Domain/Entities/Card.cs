using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    /// <summary>
    /// 카드 원본 데이터
    /// </summary>
    public class Card
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
        public bool IsRoot { get; }
        public bool IsUniqueReveal { get; }
        public bool IsRepeatable { get; }
        public bool IsRevealImmediately { get; }
        public bool IsEcho { get; }
        public bool IsForceSelect { get; }
        public bool IsCrisis { get; } // [추가] 파괴 시 교역소로 가지 않고 제외되는 카드
        // [수정] 기본값 제거 — cardDB.json에서 모든 카드가 IsCollectible 을 명시해야 한다.
        //   기본값을 두면 디버그/시스템 카드(예: 기아) 가 컬렉션에 노출되는 사고가 생길 수 있다.
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