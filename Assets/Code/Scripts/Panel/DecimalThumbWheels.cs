using System.Collections.Generic;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class DecimalThumbWheels : MonoBehaviour
    {
        public List<ThumbWheel> wheels;
        
        public readonly NotifyingVariable<int> Value = new (0);

        private void Start()
        {
            foreach (var thumbWheel in wheels)
                thumbWheel.Position.GetAndSubscribe(UpdateThumbWheels, NotifyingVariableBehaviour.ResendLast);
        }

        private void UpdateThumbWheels(int _)
        {
            var newValue = 0;
            foreach (var t in wheels) newValue = 10 * newValue + t.Position.Value;
            Value.Value = newValue;
        }
    }
}