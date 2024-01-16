using System;
using TiltFive;
using UnityEngine;

namespace Code.Scripts
{
    public class Commander
    {
        public enum CommanderType
        {
            Local,
            Remote,
            Ai
        }

        public readonly CommanderType Type;
        public readonly PlayerIndex LocalPlayerIndex;
        public readonly int AiPlayerIndex;
        
        public readonly int SoloLayer;
        
        private Commander(CommanderType type, PlayerIndex localPlayerIndex, int aiPlayerIndex)
        {
            Type = type;
            LocalPlayerIndex = localPlayerIndex;
            AiPlayerIndex = aiPlayerIndex;
            
            SoloLayer = LayerForCommander(this);
        }

        public static Commander MakeLocalCommander(PlayerIndex localPlayerIndex)
        {
            return new Commander(CommanderType.Local, localPlayerIndex, 0);
        }

        public static Commander MakeAiCommander(int index)
        {
            return new Commander(CommanderType.Ai, PlayerIndex.None, index);
        }
        
        private static int LayerForCommander(Commander commander) => 
            !commander.IsLocalCommander() ? 0 : LayerMask.NameToLayer($"Player {(int)commander.LocalPlayerIndex} Only");
        
        public bool IsAiCommander() => Type == CommanderType.Ai;
        
        public bool IsLocalCommander() => Type == CommanderType.Local;
        
        public bool IsLocalCommander(PlayerIndex localPlayerIndex) => 
            Type == CommanderType.Local && LocalPlayerIndex == localPlayerIndex;

        public static bool operator==(Commander lhs, Commander rhs)
        {
            var lhsNull = ReferenceEquals(lhs, null);
            var rhsNull =  ReferenceEquals(rhs, null);
            
            if (lhsNull && rhsNull) return true;    // Both are null
            if (lhsNull || rhsNull) return false;   // One null
            return lhs.Equals(rhs);                 // Both non-null => Equals()
        }
        
        public static bool operator!=(Commander lhs, Commander rhs) => !(lhs == rhs);
        
        private bool MemberEquals(Commander other)
        {
            return Type == other.Type && 
                   LocalPlayerIndex == other.LocalPlayerIndex && 
                   AiPlayerIndex == other.AiPlayerIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null)) return false;
            if (ReferenceEquals(obj, this)) return true;
            if (obj.GetType() != GetType()) return false;
            return MemberEquals((Commander)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Type, (int)LocalPlayerIndex, AiPlayerIndex);
        }

        public override string ToString()
        {
            return Type switch
            {
                CommanderType.Ai => $"Commander (AI:{AiPlayerIndex})",
                CommanderType.Local => $"Commander (LOCAL:{LocalPlayerIndex})",
                CommanderType.Remote => "Commander (REMOTE)",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
