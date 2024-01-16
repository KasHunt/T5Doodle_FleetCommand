using System;
using System.Collections.Generic;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class RadioButtons : MonoBehaviour
    {
        public int initiallySelected;
        public List<LitButton> buttons;
        
        ///// Public for use in code /////
        
        public readonly NotifyingVariable<int> Selected = new(0);
        
        /////

        private int _lastSelected = -1;
        
        private void Start()
        {
            Selected.Value = initiallySelected;
            
            if (buttons.Count == 0) return;
            buttons[Math.Min(Selected.Value, buttons.Count - 1)].lightOn = true;
            
            // Subscribe for changes to buttons
            foreach (var litButton in buttons) litButton.OnClicked += LitButtonOnOnClicked;
        }

        private void OnDestroy()
        {
            // Unsubscribe for changes to buttons
            foreach (var litButton in buttons) litButton.OnClicked -= LitButtonOnOnClicked;
        }

        private void LitButtonOnOnClicked(LitButton button)
        {
            for (var i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == button) Selected.Value = i;
            }
        }

        private void Update()
        {
            if (_lastSelected == Selected.Value) return;
            _lastSelected = Selected.Value;
                
            for (var i = 0; i < buttons.Count; i++) buttons[i].lightOn = i == _lastSelected;
        }
    }
}
