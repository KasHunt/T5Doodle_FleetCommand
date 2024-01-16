using System;
using UnityEngine;

namespace Code.Scripts
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ConditionalShowAttribute : PropertyAttribute
    {
        public readonly string ConditionField;
        public readonly object ConditionValue;

        public ConditionalShowAttribute(string conditionField, object conditionValue = null)
        {
            ConditionField = conditionField;
            ConditionValue = conditionValue;
        }
    }

}