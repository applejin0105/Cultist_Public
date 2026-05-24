using System;
using Domain.Enums;

namespace Domain.State
{
    public readonly struct PhaseState : IEquatable<PhaseState>
    {
        public readonly Phase.Main Main;
        public readonly int Sub;

        private PhaseState(Phase.Main main, int sub)
        {
            Main = main;
            Sub = sub;
        }

        public static PhaseState StandBy
            => new PhaseState(Phase.Main.StandBy, -1);

        public static PhaseState From(Phase.Draw step)
            => new PhaseState(Phase.Main.Draw, (int)step);

        public static PhaseState From(Phase.Play step)
            => new PhaseState(Phase.Main.Play, (int)step);
        
        public bool Equals(PhaseState other)
        {
            return Main == other.Main && Sub == other.Sub;
        }

        public override bool Equals(object obj)
        {
            return obj is PhaseState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Main, Sub);
        }

        public static bool operator ==(PhaseState left, PhaseState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhaseState left, PhaseState right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            if (Sub == -1) return $"[{Main}]";
            return $"[{Main} : {Sub}]";
        }
    }
}