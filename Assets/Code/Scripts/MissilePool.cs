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

        public void ReturnToPool(MissileBase missile)
        {
            Debug.Log("missile pool count before enqueue: " + _available.Count.ToString());
            _available.Enqueue(missile);
            Debug.Log($"Returned missile to pool: {missile.name}");
            Debug.Log("missile pool count after enqueue: " + _available.Count.ToString());
        }
        
        public MissileBase TakeFromPool()
        {
            Debug.Log("missile pool count before dequeue attempt: " + _available.Count.ToString());

            if (!_available.TryDequeue(out var missile))
            {
                Debug.LogWarning("No available missiles in the pool, creating a new one.");
                missile = MakeMissile();
            }
            else
            {
                Debug.Log($"Took missile from pool: {missile.name}");
            }

            Debug.Log("missile pool count after dequeue attempt: " + _available.Count.ToString());

            return missile;
        }
    }
}