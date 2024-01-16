using Missiles.Implementations;
using UnityEngine;

namespace Code.Scripts
{
    public enum FuseResult
    {
        NoAction,
        Detonate,
        Terminate,
        Splash
    }
    
    public delegate FuseResult ShellFuse(CannonShell shell, Collision other);
    public delegate FuseResult MissileFuse(MissileBase missile, Collision other);
}