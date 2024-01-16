using Missiles.Implementations;
using UnityEngine;

namespace Missiles
{
    [CreateAssetMenu(fileName = "Missile", menuName = "Missiles/Missile")]

    public class Missile : ScriptableObject
    {
        public GameObject prefab;
    
        public MissileBase InstantiateMissile() =>
            Instantiate(prefab).GetComponentInChildren<MissileBase>();
    }
}
