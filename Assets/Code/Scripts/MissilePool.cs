using System.Collections.Generic;
using Missiles;
using Missiles.Implementations;
using UnityEngine;

namespace Code.Scripts
{
    public class MissilePool : MonoBehaviour
    {
        [Header("Pool Properties")]
        public int poolSize = 16;
        public Missile missileTemplate;
        
        private readonly Queue<MissileBase> _available = new();

        private MissileBase MakeMissile()
        {
            var instance = missileTemplate.InstantiateMissile();
            instance.gameObject.SetActive(false);
            return instance;
        }
        
        private void Awake()
        {
            for (var i = 0; i < poolSize; i++) _available.Enqueue(MakeMissile());
        }
        
        public void ReturnToPool(MissileBase missile) => _available.Enqueue(missile);
        
        public MissileBase TakeFromPool()
        {
            if (!_available.TryDequeue(out var missile)) missile = MakeMissile();
            return missile;
        }
    }
}