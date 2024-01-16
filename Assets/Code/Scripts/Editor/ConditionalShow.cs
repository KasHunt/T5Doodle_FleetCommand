using UnityEditor;
using UnityEngine;

namespace Code.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(ConditionalShowAttribute))]
    public class ConditionalShowPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var conditionalShow = (ConditionalShowAttribute)attribute;
            var conditionProperty = property.serializedObject.FindProperty(conditionalShow.ConditionField);
        
            var shouldShow = false;
            if (conditionProperty != null)
            {
                if (conditionalShow.ConditionValue == null)
                {
                    shouldShow = conditionProperty.boolValue;
                }
                else
                {
                    shouldShow = conditionProperty.propertyType switch
                    {
                        SerializedPropertyType.Boolean => (bool)conditionalShow.ConditionValue ==
                                                          conditionProperty.boolValue,
                        SerializedPropertyType.Enum => (int)conditionalShow.ConditionValue ==
                                                       conditionProperty.enumValueIndex,
                        _ => false
                    };
                }
            }

            var wasEnabled = GUI.enabled;
            GUI.enabled = shouldShow;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = wasEnabled;
        }
    }
}