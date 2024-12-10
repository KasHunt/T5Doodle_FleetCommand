using System;
using System.Collections.Generic;

namespace Code.Scripts.Utils
{
    public enum NotifyingVariableBehaviour
    {
        NoAction,
        ResendLast
        
    }
    
    public class NotifyingVariable<T>
    {
        private event Action<T> OnValueChanged;
        
        private T _value;

        public T Value
        {
            get => _value;
            set => SetValue(value);
        }

        public NotifyingVariable(T initialValue)
        {
            Value = initialValue;
        }

        public void SetValue(T value)
        {
            if (EqualityComparer<T>.Default.Equals(Value, value)) return;
            
            _value = value;
            OnValueChanged?.Invoke(value);
        }
        
        public T GetAndSubscribe(Action<T> action, NotifyingVariableBehaviour behaviour = NotifyingVariableBehaviour.NoAction)
        {
            OnValueChanged += action;
            if (behaviour == NotifyingVariableBehaviour.ResendLast) action?.Invoke(_value);
            return Value;
        }
        
        public void Unsubscribe(Action<T> action)
        {
            OnValueChanged -= action;
        }
    }
}