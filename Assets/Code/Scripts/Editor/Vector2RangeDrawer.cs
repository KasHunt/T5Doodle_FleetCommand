using System;
using UnityEditor;
using UnityEngine;

namespace Code.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(Vector2RangeAttribute))]
    public class Vector2RangeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (Vector2RangeAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            // Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Calculate rects
            const float valueWidth = 50.0f;
            var sliderWidth = position.width - valueWidth * 2 - 4 * 2;  // -4 * 2 for some padding
            var minValueRect = new Rect(position.x, position.y, valueWidth, position.height);
            var sliderRect = new Rect(position.x + valueWidth + 4, position.y, sliderWidth, position.height);
            var maxValueRect = new Rect(position.x + valueWidth + sliderWidth + 8, position.y, valueWidth, position.height);

            // Store values in temporary variables
            var range = property.vector2Value;
        
            // Draw fields - pass GUIContent.none to each so they are drawn without labels
            range.x = EditorGUI.FloatField(minValueRect, range.x);
            EditorGUI.MinMaxSlider(sliderRect, ref range.x, ref range.y, attr.Min, attr.Max);
            range.y = EditorGUI.FloatField(maxValueRect, range.y);

            // Store the values back into the property
            property.vector2Value = range;

            EditorGUI.EndProperty();
        }
    }
    
    [CustomPropertyDrawer(typeof(Vector2IntRangeAttribute))]
    public class Vector2IntRangeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (Vector2IntRangeAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            // Label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Calculate rects
            const float valueWidth = 50.0f;
            var sliderWidth = position.width - valueWidth * 2 - 4 * 2;  // -4 * 2 for some padding
            var minValueRect = new Rect(position.x, position.y, valueWidth, position.height);
            var sliderRect = new Rect(position.x + valueWidth + 4, position.y, sliderWidth, position.height);
            var maxValueRect = new Rect(position.x + valueWidth + sliderWidth + 8, position.y, valueWidth, position.height);

            // Store values in temporary variables
            var range = property.vector2IntValue;
        
            // Draw fields - pass GUIContent.none to each so they are drawn without labels
            float minValue = range.x;
            float maxValue = range.y;
            
            minValue = EditorGUI.IntField(minValueRect, range.x);
            EditorGUI.MinMaxSlider(sliderRect, ref minValue, ref maxValue, attr.Min, attr.Max);
            maxValue = EditorGUI.IntField(maxValueRect, Mathf.RoundToInt(maxValue));
            
            range.x = Mathf.RoundToInt(Mathf.Clamp(minValue, attr.Min, attr.Max));
            range.y =  Mathf.RoundToInt(Mathf.Clamp(maxValue, attr.Min, attr.Max));

            // Store the values back into the property
            property.vector2IntValue = range;

            EditorGUI.EndProperty();
        }
    }
}
