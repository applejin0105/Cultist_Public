namespace Effects.Core
{
    /// <summary>
    /// 결정론(Replay/Server-authority)을 위해 RNG는 반드시 이 인터페이스를 거쳐 사용한다.
    /// </summary>
    public interface IRandomSource
    {
        int Next();
        int Next(int maxExclusive);
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _rng;

        public SystemRandomSource(System.Random rng)
        {
            _rng = rng;
        }

        public int Next() => _rng.Next();
        public int Next(int maxExclusive) => _rng.Next(maxExclusive);
        public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
    }
}
