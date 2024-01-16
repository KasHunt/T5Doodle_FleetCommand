using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public enum LedDisplayMode
    {
        Truncate,
        Scroll
    }

    public enum LedDisplayAlignment
    {
        Left,
        Center,
        Right
    }

    public class LedDisplay : MonoBehaviour
    {
        public string text = "Test";
        public LedDisplayMode mode;
        public bool showSpinner;
        public float spinnerStepInterval = 0.2f;
        
        [ConditionalShow("mode", LedDisplayMode.Truncate)]
        public LedDisplayAlignment alignment;
        
        [ConditionalShow("mode", LedDisplayMode.Scroll)]
        [Range(0, 6)]
        public float scrollSpeedCharactersPerSecond = 1;
        
        [ConditionalShow("mode", LedDisplayMode.Scroll)]
        [Range(0, 10)]
        public int scrollSpacing;
        
        public List<SixteenSegController> displayModules;

        private string _lastText;
        private float _scrollPosition;
        private int _spinnerProgress;
        private float _spinnerLastStepTime;

        private static char GetSoloProgressGlyph(int progress)
        {
            // Solo : 0, 1, 2, 3
            int[] list = {0, 1, 2, 3};
            return (char)(128 + list[progress % 4]);
        }

        private static char GetInnerProgressGlyph(int index, int progress)
        {
            // Inner : 0, 10, 12, 11
            int[] list = {0, 10, 12, 11};
            var indexOffset = (index - 1) % 2 * 2;
            return (char)(128 + list[(progress + indexOffset) % 4]);
        }

        private static char GetSideProgressGlyph(int index, int segmentCount, int progress)
        {
            if (index == 0)
            {
                // Outer(left) : 4, 5, 0, 8
                int[] leftList = {4, 5, 0, 8};
                return (char)(128 + leftList[progress % 4]);
            }

            // Outer(right) : 7, 9, 0, 6
            int[] rightList = {7, 9, 0, 6};
            var indexOffset = (segmentCount - 1) % 2 * 2;
            return (char)(128 + rightList[(progress + indexOffset) % 4]);
        }
        
        private static char GetProgressGlyph(int index, int segmentCount, int progress)
        {
            if (segmentCount == 1) return GetSoloProgressGlyph(progress);
            if (index == 0) return GetSideProgressGlyph(0, segmentCount, progress);
            return index == segmentCount - 1 ? GetSideProgressGlyph(index, segmentCount, progress) : GetInnerProgressGlyph(index, progress);
        }

        private void UpdateSpinner()
        {
            var displaySize = displayModules.Count;
            
            for (var i = 0; i < displaySize; i++)
                displayModules[i].asciiChar = GetProgressGlyph(i, displaySize, _spinnerProgress);
            
            var now = Time.time;
            if (!(now > _spinnerLastStepTime + spinnerStepInterval)) return;
            _spinnerProgress++;
            _spinnerLastStepTime = now;
        }
        
        private void Update()
        {
            if (showSpinner)
            {
                UpdateSpinner();
                return;
            }
            
            if (text != _lastText)
            {
                _lastText = text;
                _scrollPosition = 0; // Reset scroll position when the text changes
            }

            var displaySize = displayModules.Count;
            var displayText = text;

            // Handle Scroll Mode
            if (mode == LedDisplayMode.Scroll && text.Length > displaySize)
            {
                var scrollText = displayText.PadRight(text.Length + scrollSpacing, ' ');
                
                var startPos = Mathf.FloorToInt(_scrollPosition);
                displayText = scrollText.Substring(startPos, Mathf.Min(displaySize, scrollText.Length - startPos)) +
                              scrollText;

                _scrollPosition += Time.deltaTime * scrollSpeedCharactersPerSecond;
                _scrollPosition %= scrollText.Length;
            }

            // Handle Alignment
            if (mode != LedDisplayMode.Scroll)
            {
                switch (alignment)
                {
                    case LedDisplayAlignment.Center:
                        var paddingSize = Mathf.Max((displaySize - displayText.Length) / 2, 0);
                        displayText = displayText.PadLeft(paddingSize + displayText.Length, ' ').PadRight(displaySize, ' ');
                        break;

                    case LedDisplayAlignment.Right:
                        displayText = displayText.PadLeft(displaySize, ' ');
                        break;

                    case LedDisplayAlignment.Left:
                        displayText = displayText.PadRight(displaySize, ' ');
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Handle Truncate Mode
            if (mode == LedDisplayMode.Truncate)
            {
                switch (alignment)
                {
                    case LedDisplayAlignment.Left:
                        displayText = displayText[..Mathf.Min(displaySize, displayText.Length)];
                        break;
                    
                    case LedDisplayAlignment.Center:
                        var centerStart = Math.Max((displayText.Length - displaySize) / 2, 0);
                        var centerEnd = Math.Min(centerStart + displaySize, displayText.Length);
                        displayText = displayText.Substring(centerStart, centerEnd - centerStart);
                        break;
        
                    case LedDisplayAlignment.Right:
                        var rightStart = Math.Max(displayText.Length - displaySize, 0);
                        displayText = displayText[rightStart..];
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // If for any reason, the string is shorter than the display,
            // pad with spaces to ensure we don't try to index nonexistent modules
            if (displayText.Length < displaySize) displayText = displayText.PadRight(displaySize, ' ');
            
            // Update Individual Display Modules
            for (var i = 0; i < displaySize; i++) displayModules[i].asciiChar = displayText[i];
        }
    }
}
