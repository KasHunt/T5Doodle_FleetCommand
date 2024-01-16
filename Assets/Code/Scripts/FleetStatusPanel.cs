using Code.Scripts.Panel;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Scripts
{
    public class FleetStatusPanel : MonoBehaviour
    {
        public StatusLight readyIndicator;
        public StatusLight attackIndicator;
        public GameObject fleetStrengthBar;
        
        ///////

        // 30 / 1024 (Segment width in pixels in the texture / texture width in pixels)
        private const float TEXTURE_STEP = 0.029296875f;
        private const int FLEET_SIZE = 17;
        
        private Material _fleetStrengthMaterial;
        
        private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        private static readonly int EmissiveColorMap = Shader.PropertyToID("_EmissiveColorMap");
        private static readonly int EmissiveColorLDR = Shader.PropertyToID("_EmissiveColorLDR");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Commander _commander;

        public SeaWar gameController;
        [CanBeNull] private NotifyingVariable<int> _fleetStrength;
        [CanBeNull] private NotifyingVariable<bool> _playing;

        private void Start()
        {
            gameController.AttackingCommander.GetAndSubscribe(OnAttackingCommanderUpdated);
        }

        private void OnDestroy()
        {
            _playing?.Unsubscribe(OnPlayingUpdated);
            _fleetStrength?.Unsubscribe(OnFleetStrengthUpdated);
            gameController.AttackingCommander.Unsubscribe(OnAttackingCommanderUpdated);
        }

        private void OnAttackingCommanderUpdated(Commander commander)
        {
            attackIndicator.lightOn = commander == _commander;
        }

        public void SetCommander(Commander commander, 
            NotifyingVariable<bool> playing,
            NotifyingVariable<int> fleetStrength)
        {
            // Store the commander and update the colors
            _commander = commander;
            UpdateColor();

            // Track the 'playing' state of the associated commander
            _playing?.Unsubscribe(OnPlayingUpdated);
            _playing = playing;
            playing.GetAndSubscribe(OnPlayingUpdated);
            
            // Track the fleet strength of the associated commander
            _fleetStrength?.Unsubscribe(OnFleetStrengthUpdated);
            _fleetStrength = fleetStrength;
            fleetStrength.GetAndSubscribe(OnFleetStrengthUpdated);
        }

        private void OnPlayingUpdated(bool playing) => readyIndicator.lightOn = playing;

        private void OnFleetStrengthUpdated(int strength) => SetFleetStrength(strength);

        private void EnsureMaterialInstantiated()
        {
            if (_fleetStrengthMaterial) return;
            
            var barRenderer = fleetStrengthBar.GetComponent<Renderer>();
            _fleetStrengthMaterial = Instantiate(barRenderer.material);
            barRenderer.material = _fleetStrengthMaterial;
        }
        
        private void UpdateColor()
        {
            var color = gameController.Commanders[_commander].TeamColor;
            
            readyIndicator.onColor = color;
            readyIndicator.offColor = color;
            
            attackIndicator.onColor = color;

            EnsureMaterialInstantiated();
            _fleetStrengthMaterial.SetColor(BaseColor, color);
            _fleetStrengthMaterial.SetColor(EmissiveColorLDR, color);
            ColorUtils.UpdateEmissiveColor(_fleetStrengthMaterial);
        }

        private static Vector2 ComputeOffsetFromStrength(int fleetStrength) =>
            new(Mathf.Clamp(FLEET_SIZE - fleetStrength, 0, FLEET_SIZE) * TEXTURE_STEP, 0f);
        
        private void SetFleetStrength(int strength)
        {
            var computedOffset = ComputeOffsetFromStrength(strength);

            // Adjust texture offsets
            EnsureMaterialInstantiated();
            _fleetStrengthMaterial.SetTextureOffset(BaseColorMap, computedOffset);
            _fleetStrengthMaterial.SetTextureOffset(EmissiveColorMap, computedOffset);
        }
    }
}
