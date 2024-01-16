using UnityEngine;

namespace Code.Scripts
{
    public class Vector2RangeAttribute : PropertyAttribute
    {
        public float Min { get; private set; }
        public float Max { get; private set; }

        public Vector2RangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
    
    public class Vector2IntRangeAttribute : PropertyAttribute
    {
        public int Min { get; private set; }
        public int Max { get; private set; }

        public Vector2IntRangeAttribute(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }
}