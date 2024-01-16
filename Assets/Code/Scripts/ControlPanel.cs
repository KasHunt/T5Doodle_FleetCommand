using System;
using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Panel;
using Code.Scripts.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Code.Scripts
{
    public class ControlPanel : MonoBehaviour
    {
        private enum ButtonMode
        {
            Play = 0,
            Config = 1,
            Help = 2,
            Exit = 3
        }

        private enum NetworkMode
        {
            Offline = 0,
            Host = 1,
            Client = 2
        }
        
        public enum StatusLightMode
        {
            AttackTarget,
            AttackTrack,
            Alert,
            Idle
        }
        
        [Header("Mode Buttons")]
        public RadioButtons modeRadioButtons;
        
        ////// Panel 1 ////// 
        
        [Header("Panel 1 : Blank")]
        public GameObject panel1Blank;
        
        [Header("Panel 1 : Help")]
        public GameObject panel1Help;
        public BistableRotation helpPanel1Cover;

        [Header("Panel 1 : Confirm Exit")]
        public GameObject panel1ConfirmExit;
        public LitButton confirmExitButton;
        
        [Header("Panel 1 : Simulation Mode")]
        public GameObject panel1SimulationMode;
        public ThumbWheel gameMode;
        public ThumbWheel gameSubMode;
        public LitButton showNetwork;
        
        [Header("Panel 1 : Network")]
        public GameObject panel1Network;
        public RadioButtons networkMode;
        public LedDisplay networkStatus;
        
        [Header("Panel 1 : Settings")]
        public GameObject panel1Settings;
        public ThumbWheel wandArc;
        public ThumbWheel soundEffectsVolume;
        public ThumbWheel backgroundSoundVolume;
        
        [Header("Panel 1 : Fleet Status")]
        public GameObject panel1FleetStatus;
        public FleetStatusPanel fleetStatusPanelTemplate;
        public GameObject fleetStatusPanelBlankTemplate;
        public List<GameObject> fleetStatusSlots;
        

        ////// Panel 2 //////
        
        [Header("Panel 2 : Blank")]
        public GameObject panel2Blank;
        
        [Header("Panel 2 : Help")]
        public GameObject panel2Help;
        public BistableRotation helpPanel2Cover;
        
        [Header("Panel 2 : Network Host")]
        public GameObject panel2NetworkHost;
        public LedDisplay networkHostCode;

        [Header("Panel 2 : Network Client")]
        public GameObject panel2NetworkClient;
        public DecimalThumbWheels networkJoinCode;
        public LitButton joinButton;
        
        [Header("Panel 2 : Commander Setup")]
        public GameObject panel2CommanderSetup;
        public LedDisplay commanderNameDisplay;
        public ThumbWheel commanderColor;
        public RotaryToggle commanderGameMode;
        
        [Header("Panel 2 : Commander Status")]
        public GameObject panel2CombatStatus;
        public BistableRotation cmdrPanelCover;
        
        public StatusLight cmdrStatusAttack;
        [FormerlySerializedAs("cmdrStatusDefend")] public StatusLight cmdrStatusAlert;
        public StatusLight cmdrStatusAttackTarget;
        public StatusLight cmdrStatusAttackTrack;
        public LedDisplay cmdrStatusDistanceToTarget;
        
        public ThumbWheel timeControl;
    
        ////// Panel 3 //////
        
        [Header("Panel 3 : Blank")]
        public GameObject panel3Blank;
        
        [Header("Panel 3 : Help")]
        public GameObject panel3Help;
        public LitButton panel3Dismiss;
        
        [Header("Panel 3 : Start")]
        public GameObject panel3Start;
        public CoveredToggle startSwitch;
        
        [Header("Panel 3 : Launch")]
        public GameObject panel3Launch;
        public BistableRotation fleetPlacedCover;
        public RotaryToggle launchSwitch;
        public SixteenSegController humanFleetCount;
        public SixteenSegController aiFleetCount;
        
        [Header("Panel 3 : SelfDestruct")]
        public GameObject panel3SelfDestruct;
        public CoveredButton selfDestructButton;

        [Header("Panel switch behavior")]
        public float panelAscendTime = 2f;
        public float panelDescendTime = 2f;
        public float panelDoorActuateTime = 2f;
        public float doorActuateDelay = 1f;
        public float doorActuateHold = 1f;
        
        public AudioClip panelDownSound;
        public AudioClip panelChangeSound;
        public AudioClip panelSlideSound;
        
        public Vector3 doorDownOffset = new(0, -0.5f,0);
        public Vector3 doorStowedOffset = new(0, -0.5f,-2f);
        public Vector3 panelDownOffset = new(0, -5f,0);

        [Header("Grid Status Text")]
        public TextMeshPro statusTextLeft;
        public TextMeshPro statusTextRight;
        [Min(0)] public float characterTypeDelay;
        [Min(1)] public int statusTextLines;
        
        [Header("Settings Related")]
        public float arcVelocityMultiplier = 10;
        
        public enum Panel1Mode
        {
            Blank,
            Help,
            GameMode,
            Network,
            FleetStatus,
            Settings,
            ConfirmExit
        }
        public Panel1Mode panel1Mode = Panel1Mode.Help;
        
        public enum Panel2Mode
        {
            Blank,
            Help,
            CommanderSetup,
            CombatStatus,
            NetworkHost,
            NetworkClient
        }
        public Panel2Mode panel2Mode = Panel2Mode.Help;
        
        public enum Panel3Mode
        {
            Blank,
            Dismiss,
            StartGame,
            Launch,
            SelfDestruct
        }
        public Panel3Mode panel3Mode = Panel3Mode.Dismiss;

        public SeaWar gameController;
        public Commander CommanderForPanel;
        
        private SeaWar.GameState _gameState;
        
        public StatusLightMode CurrentStatusLightMode
        {
            set
            {
                cmdrStatusAttack.lightOn = value is StatusLightMode.AttackTarget or StatusLightMode.AttackTrack;
                cmdrStatusAlert.lightOn = value == StatusLightMode.Alert;
                cmdrStatusAttackTarget.lightOn = value == StatusLightMode.AttackTarget;
                cmdrStatusAttackTrack.lightOn = value == StatusLightMode.AttackTrack;
            }
        }

        public int DistanceToTarget
        {
            set => cmdrStatusDistanceToTarget.text = value < 0 ? "------" : $"{value}";
        }
        
        ////////////////////////////////////////////////////////
        private readonly List<FleetStatusPanel> _fleetStatus = new();
        private readonly List<GameObject> _fleetStatusBlanks = new();

        private Panel1Mode _lastPanel1Mode = Panel1Mode.Help;
        private Panel2Mode _lastPanel2Mode = Panel2Mode.Help;
        private Panel3Mode _lastPanel3Mode = Panel3Mode.Dismiss;

        private readonly Dictionary<int, GameObject> _blankPanels = new();
        private readonly Dictionary<int, GameObject> _currentPanels = new();
        
        private void Start()
        {
            _leftTeletypeData = new TeletypeData(statusTextLines);
            _rightTeletypeData = new TeletypeData(statusTextLines);
            
            CurrentStatusLightMode = StatusLightMode.Idle;
            gameController.CurrentGameState.GetAndSubscribe(OnGameStateChanged, NotifyingVariableBehaviour.ResendLast);
            SubscribeInputEvents();
            
            PrepareFleetStrengths();
            SetInitialPanels();
        }

        private void OnGameStateChanged(SeaWar.GameState newState)
        {
            _gameState = newState;
            OnModeButtonChange(_lastMode);
        }

        private void OnDestroy()
        {
            gameController.CurrentGameState.Unsubscribe(OnGameStateChanged);
            UnsubscribeInputEvents();            
        } 

        private void SubscribeInputEvents()
        {
            // Mode buttons / exit
            modeRadioButtons.Selected.GetAndSubscribe(OnModeButtonChange, NotifyingVariableBehaviour.ResendLast);
            
            // Panel 1 : Confirm Exit
            confirmExitButton.OnClicked += ConfirmExitButtonClicked;
            
            // Panel 1 : Settings
            wandArc.Position.GetAndSubscribe(OnWandArcChanged);
            WandManager.Instance.ArcLaunchVelocity.GetAndSubscribe(OnWandArcChangedByManager, NotifyingVariableBehaviour.ResendLast);
            soundEffectsVolume.Position.GetAndSubscribe(OnSoundFxVolumeChanged);
            backgroundSoundVolume.Position.GetAndSubscribe(OnMusicVolumeChanged);
            SoundManager.Instance.EffectVolume.GetAndSubscribe(OnSoundFxVolumeChangedByManager, NotifyingVariableBehaviour.ResendLast);
            SoundManager.Instance.MusicVolume.GetAndSubscribe(OnMusicVolumeChangedByManager, NotifyingVariableBehaviour.ResendLast);

            // Panel 1 : Simulation Mode
            gameMode.Position.GetAndSubscribe(OnGameModeChanged);
            gameSubMode.Position.GetAndSubscribe(OnGameSubModeChanged);
            showNetwork.OnClicked += OnShowNetwork;
            joinButton.OnClicked += OnJoinNetworkClicked;
            
            // Panel 1 : Network
            networkMode.Selected.GetAndSubscribe(OnNetworkModeChange, NotifyingVariableBehaviour.ResendLast);
            
            // Panel 1 : Fleet Status
            // No watchable properties
            
            // Panel 2 : Commander Settings
            commanderColor.Position.GetAndSubscribe(OnCommanderColorChanged);
            commanderGameMode.SwitchOn.GetAndSubscribe(OnCommanderModeSwitch);
            
            // Panel 2 : Combat Status
            timeControl.Position.GetAndSubscribe(OnTimeControlChanged);
            
            // Panel 3 : Start Switch / Self Destruct / Launch
            startSwitch.ToggleOn.GetAndSubscribe(OnStartToggle);
            selfDestructButton.OnClicked += SelfDestruct;
            launchSwitch.SwitchOn.GetAndSubscribe(OnLaunchSwitch);
            
            // Panel 3: Help Panel
            panel3Dismiss.OnClicked += OnDismiss;
        }

        private void UnsubscribeInputEvents()
        {
            modeRadioButtons.Selected.Unsubscribe(OnModeButtonChange);
            
            confirmExitButton.OnClicked -= ConfirmExitButtonClicked;
            
            wandArc.Position.Unsubscribe(OnWandArcChanged);
            WandManager.Instance.ArcLaunchVelocity.Unsubscribe(OnWandArcChangedByManager);
            soundEffectsVolume.Position.Unsubscribe(OnSoundFxVolumeChanged);
            backgroundSoundVolume.Position.Unsubscribe(OnMusicVolumeChanged);
            SoundManager.Instance.EffectVolume.Unsubscribe(OnSoundFxVolumeChangedByManager);
            SoundManager.Instance.MusicVolume.Unsubscribe(OnMusicVolumeChangedByManager);
            
            gameMode.Position.Unsubscribe(OnGameModeChanged);
            gameSubMode.Position.Unsubscribe(OnGameSubModeChanged);
            showNetwork.OnClicked -= OnShowNetwork;
            joinButton.OnClicked -= OnJoinNetworkClicked;
            
            networkMode.Selected.Unsubscribe(OnNetworkModeChange);
            
            commanderColor.Position.Unsubscribe(OnCommanderColorChanged);
            commanderGameMode.SwitchOn.Unsubscribe(OnCommanderModeSwitch);
            
            timeControl.Position.Unsubscribe(OnTimeControlChanged);
            
            startSwitch.ToggleOn.Unsubscribe(OnStartToggle);
            selfDestructButton.OnClicked -= SelfDestruct;
            launchSwitch.SwitchOn.Unsubscribe(OnLaunchSwitch);
            
            panel3Dismiss.OnClicked -= OnDismiss;
        }
        
        private static void ConfirmExitButtonClicked(LitButton obj)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        private void OnWandArcChanged(int velocity) => WandManager.Instance.ArcLaunchVelocity.Value = velocity * arcVelocityMultiplier;
        private void OnWandArcChangedByManager(float value) => wandArc.Position.Value = Mathf.RoundToInt(value / arcVelocityMultiplier);
        
        private static void OnSoundFxVolumeChanged(int volume) => SoundManager.Instance.EffectVolume.Value = volume / 9f;
        private static void OnMusicVolumeChanged(int volume) => SoundManager.Instance.MusicVolume.Value = volume / 9f;
        private void OnSoundFxVolumeChangedByManager(float value) => soundEffectsVolume.Position.Value = Mathf.RoundToInt(value * 9f);
        private void OnMusicVolumeChangedByManager(float value) => backgroundSoundVolume.Position.Value = Mathf.RoundToInt(value * 9f);

        private void OnGameModeChanged(int mode) => gameController.SetGameMode(mode);
        private void OnGameSubModeChanged(int mode) => gameController.SetGameSubMode(mode);
        
        private static void OnTimeControlChanged(int index)
        {
            SeaWar.SetTimeMultiplier(index switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
            });
        }

        private void OnModeButtonChange(ButtonMode radioButtonIndex) =>
            OnModeButtonChange((int)radioButtonIndex);
        
        private void OnModeButtonChange(int radioButtonIndex)
        {
            var mode = (ButtonMode)radioButtonIndex;
            
            _lastMode = mode;

            if (_showNetwork)
            {
                panel1Mode = Panel1Mode.Network;
                panel2Mode = _currentNetworkMode switch
                {
                    NetworkMode.Offline => Panel2Mode.Blank,
                    NetworkMode.Client => Panel2Mode.NetworkClient,
                    NetworkMode.Host => Panel2Mode.NetworkHost,
                    _ => throw new ArgumentOutOfRangeException()
                };
                return;
            }
            
            panel1Mode = mode switch
            {
                ButtonMode.Exit => Panel1Mode.ConfirmExit,
                ButtonMode.Help => Panel1Mode.Help,
                ButtonMode.Config => Panel1Mode.Settings,
                ButtonMode.Play when _gameState == SeaWar.GameState.Lobby => Panel1Mode.GameMode,
                ButtonMode.Play => Panel1Mode.FleetStatus,
                _ => Panel1Mode.Blank
            };
            
            panel2Mode = mode switch
            {
                ButtonMode.Help => Panel2Mode.Help,
                ButtonMode.Play when _gameState == SeaWar.GameState.Lobby => Panel2Mode.CommanderSetup,
                ButtonMode.Play when _gameState == SeaWar.GameState.Playing => Panel2Mode.CombatStatus,
                _ => Panel2Mode.Blank
            };

            var isCombatCommander = gameController.Commanders[CommanderForPanel].Mode == SeaWar.CommanderMode.Combat;
            panel3Mode = mode switch
            {
                ButtonMode.Help => Panel3Mode.Dismiss,
                ButtonMode.Play when _gameState == SeaWar.GameState.Lobby => Panel3Mode.StartGame,
                ButtonMode.Play when _gameState == SeaWar.GameState.Victory => Panel3Mode.StartGame,
                ButtonMode.Play when !isCombatCommander => Panel3Mode.Blank,
                ButtonMode.Play when _gameState == SeaWar.GameState.Placing => Panel3Mode.Launch,
                ButtonMode.Play when _gameState == SeaWar.GameState.Playing => Panel3Mode.SelfDestruct,
                _ => Panel3Mode.Blank
            };
        }

        private void OnCommanderColorChanged(int index) => 
            gameController.SetCommanderColor(CommanderForPanel, index);
        
        private void OnCommanderModeSwitch(bool mode) => 
            gameController.SetCommanderMode(CommanderForPanel, mode ? 
                SeaWar.CommanderMode.Observe : SeaWar.CommanderMode.Combat);
        
                
        private void OnStartToggle(bool state)
        {
            switch (_gameState)
            {
                case SeaWar.GameState.Lobby when state:
                    gameController.StartPlacement();
                    break;
                
                case SeaWar.GameState.Victory when !state:
                    gameController.EndGame();
                    break;
                
                case SeaWar.GameState.Placing:
                case SeaWar.GameState.Playing:
                default:
                    break;
            }
        }

        public void SetLeftTeletype(string text) => _leftTeletypeData.SetTargetText(text);
        
        public void SetRightTeletype(string text) => _rightTeletypeData.SetTargetText(text);
        
        private void SelfDestruct(CoveredButton obj) => gameController.SelfDestruct(CommanderForPanel);
        
        private void OnLaunchSwitch(bool state) => gameController.MarkCommanderReady(CommanderForPanel, state);
        
        private void OnDismiss(LitButton _)
        {
            if (_lastMode == ButtonMode.Help)
            {
                // Help => Play
                OnModeButtonChange(ButtonMode.Play);
                modeRadioButtons.Selected.Value = 0;
            }
            else
            {
                // Play/Network => Play
                _showNetwork = false;
            }
        }

        private void OnShowNetwork(LitButton _)
        {
            // TODO (khunt): Finish networking support
            return;
            
            _showNetwork = true;
            OnModeButtonChange(_lastMode);
        }
        
        private void OnNetworkModeChange(int index)
        {
            _currentNetworkMode = (NetworkMode)index;

            switch (_currentNetworkMode)
            {
                case NetworkMode.Offline:
                    networkStatus.text = "Offline";
                    break;
                
                case NetworkMode.Host:
                    networkStatus.text = "Host";
                    break;
                
                case NetworkMode.Client:
                    networkStatus.text = "Client";
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            //TODO: IMPLEMENT
            networkHostCode.text = "23456";
            
            OnModeButtonChange(_lastMode);
        }
        
        private void OnJoinNetworkClicked(LitButton _)
        {
            var joinCode = networkJoinCode.Value.Value;
            networkStatus.text = $"JOIN: {joinCode}";
            //TODO: IMPLEMENT
        }
        
        private void SetInitialPanels()
        {
            // Set the blank panels as current
            _blankPanels[0] = panel1Blank;
            _blankPanels[1] = panel2Blank;
            _blankPanels[2] = panel3Blank;
            _currentPanels[0] = panel1Help;
            _currentPanels[1] = panel2Help;
            _currentPanels[2] = panel3Help;

            panel1Blank.transform.localPosition = doorStowedOffset;
            panel2Blank.transform.localPosition = doorStowedOffset;
            panel3Blank.transform.localPosition = doorStowedOffset;
            
            // Set initial panel visibility
            panel1Help.SetActive(true);
            panel1Blank.SetActive(true);
            panel1Settings.SetActive(false);
            panel1Network.SetActive(false);
            panel1ConfirmExit.SetActive(false);
            panel1FleetStatus.SetActive(false);
            panel1SimulationMode.SetActive(false);
            
            panel2Help.SetActive(true);
            panel2Blank.SetActive(true);
            panel2CombatStatus.SetActive(false);
            panel2CommanderSetup.SetActive(false);
            panel2NetworkClient.SetActive(false);
            panel2NetworkHost.SetActive(false);
            
            panel3Help.SetActive(true);
            panel3Blank.SetActive(true);
            panel3Launch.SetActive(false);
            panel3Start.SetActive(false);
            panel3SelfDestruct.SetActive(false);
        }
        
        private void Update()
        {
            MaybeStartPanel1Animation();
            MaybeStartPanel2Animation();
            MaybeStartPanel3Animation();
            AnimatePanels();

            UpdateStatusTexts();
        }

        private void UpdateStatusTexts()
        {
            // Wait for the correct time to type the character
            var now = Time.unscaledTime;
            if (now < _nextStatusCharTime) return;
            _nextStatusCharTime = now + characterTypeDelay;
            
            UpdatedStatusText(statusTextLeft, ref _leftTeletypeData);
            UpdatedStatusText(statusTextRight, ref _rightTeletypeData, false);
        }
        
        private static void UpdatedStatusText(TMP_Text statusText, ref TeletypeData teletypeData, bool flashingCarat = true)
        {
            var charToType = teletypeData.GetNextChar();
            
            // Add chars if necessary
            if (charToType == null)
            {
                if (statusText.text.Length <= 0 || !flashingCarat) return;
                statusText.text = statusText.text[..^1] + (Time.unscaledTime % 2 < 1 ? "_" : " ");
                return;
            }

            // Add the character
            if (flashingCarat)
            {
                statusText.text = (statusText.text.Length > 0 ? statusText.text[..^1] : "") + charToType + "_";                
            }
            else
            {
                statusText.text += charToType;
            }
            
            // Trim statusText to the correct number of lines
            var lines = statusText.text.Split('\n').ToList();
            while (lines.Count > teletypeData.GetMaxLines())
            {
                lines.RemoveAt(0);
            }
            statusText.text = string.Join("\n", lines);
        }

        public void SetCommanderName(string commanderName) =>
            commanderNameDisplay.text = commanderName;

        public void SetFleetCounts(int humans, int ais)
        {
            humanFleetCount.asciiChar = $"{humans}"[0];
            aiFleetCount.asciiChar = $"{ais}"[0];
        }

        public void SetLaunchEnabled(bool launchEnabled)
        {
            _launchEnabled = launchEnabled;
            launchSwitch.locked = !_launchEnabled;
            UpdateLaunchCover();
        }

        public void SetCommanderEnabled(bool commanderEnabled)
        {
            _commanderEnabled = commanderEnabled;
            UpdateCommanderCover();
        }

        private void UpdateLaunchCover()
        {
            fleetPlacedCover.Open.Value = _launchEnabled && panel3Mode == Panel3Mode.Launch;
        }
        
        private void UpdateCommanderCover()
        {
            cmdrPanelCover.Open.Value = _commanderEnabled && panel2Mode == Panel2Mode.CommanderSetup;
        }

        private void UpdateHelp1Cover()
        {
            helpPanel1Cover.Open.Value = panel1Mode == Panel1Mode.Help;
        }
        
        private void UpdateHelp2Cover()
        {
            helpPanel2Cover.Open.Value = panel2Mode == Panel2Mode.Help;
        }
        
        private GameObject GetPanelForMode(Panel1Mode mode)
        {
            return mode switch
            {
                Panel1Mode.Help => panel1Help,
                Panel1Mode.Blank => panel1Blank,
                Panel1Mode.GameMode => panel1SimulationMode,
                Panel1Mode.FleetStatus => panel1FleetStatus,
                Panel1Mode.Settings => panel1Settings,
                Panel1Mode.ConfirmExit => panel1ConfirmExit,
                Panel1Mode.Network => panel1Network,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
        
        private GameObject GetPanelForMode(Panel2Mode mode)
        {
            return mode switch
            {
                Panel2Mode.Help => panel2Help,
                Panel2Mode.Blank => panel2Blank,
                Panel2Mode.CommanderSetup => panel2CommanderSetup,
                Panel2Mode.CombatStatus => panel2CombatStatus,
                Panel2Mode.NetworkClient => panel2NetworkClient,
                Panel2Mode.NetworkHost => panel2NetworkHost,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private GameObject GetPanelForMode(Panel3Mode mode)
        {
            return mode switch
            {
                Panel3Mode.Dismiss => panel3Help,
                Panel3Mode.Blank => panel3Blank,
                Panel3Mode.StartGame => panel3Start,
                Panel3Mode.Launch => panel3Launch,
                Panel3Mode.SelfDestruct => panel3SelfDestruct,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private void MaybeStartPanel1Animation()
        {
            if (panel1Mode == _lastPanel1Mode) return;
            if (StartPanelAnimation(GetPanelForMode(panel1Mode), 0)) _lastPanel1Mode = panel1Mode;
        }
        
        private void MaybeStartPanel2Animation()
        {
            if (panel2Mode == _lastPanel2Mode) return;
            if (StartPanelAnimation(GetPanelForMode(panel2Mode), 1)) _lastPanel2Mode = panel2Mode;
        }
        
        private void MaybeStartPanel3Animation()
        {
            if (panel3Mode == _lastPanel3Mode) return;
            if (StartPanelAnimation(GetPanelForMode(panel3Mode), 2)) _lastPanel3Mode = panel3Mode;
        }

        private float GetPanelAnimationDelay(Object currentPanel)
        {
            if (currentPanel == panel2CommanderSetup && cmdrPanelCover.Open.Value)
            {
                UpdateCommanderCover();
                return 1f;
            }

            if (currentPanel == panel3Launch && fleetPlacedCover.Open.Value)
            {
                UpdateLaunchCover();
                return 1f;
            }

            if (currentPanel == panel1Help)
            {
                UpdateHelp1Cover();
                return 1f;
            }

            if (currentPanel == panel2Help)
            {
                UpdateHelp2Cover();
                return 1f;
            }

            return 0f;
        }
        
        private void AnimatePanelOut(GameObject currentPanel, GameObject blankPanel, Action onComplete)
        {
            // If the panel has an open door, close it before animating
            var delay = GetPanelAnimationDelay(currentPanel);
            
            // If the current panel /isn't/ blank, animate it down, and close the door
            AnimatePanel(currentPanel, new List<Vector3>
            {
                Vector3.zero, 
                panelDownOffset
            }, panelDescendTime, delay, () =>
            {
                if (panelDownSound) SoundManager.Instance.PlaySound(panelDownSound, 1f);
            }, null);

            // Animate the door closed
            AnimatePanel(blankPanel, new List<Vector3>
            {
                doorStowedOffset,
                doorDownOffset,
                Vector3.zero
            }, panelDoorActuateTime, delay + doorActuateDelay, () =>
            {
                if (panelSlideSound) SoundManager.Instance.PlaySound(panelSlideSound, 1f);
            }, () =>
            {
                // Play the 'whir' sound when to door closes
                if (panelChangeSound) SoundManager.Instance.PlaySound(panelChangeSound, 1f);
                onComplete?.Invoke();
            });
        }

        private void AnimatePanelIn(GameObject newPanel, GameObject blankPanel, float delay, Action onComplete)
        {
            // If the new panel is the blank panel, then we're done.
            if (newPanel == blankPanel)
            {
                onComplete?.Invoke();
                return;
            }
            
            // Otherwise, activate the new panel in it's 'down' position...
            newPanel.SetActive(true);
            newPanel.transform.localPosition = panelDownOffset;
            
            // ...open the door...
            AnimatePanel(blankPanel, new List<Vector3>
            {
                Vector3.zero,
                doorDownOffset,
                doorStowedOffset
            }, panelDoorActuateTime, delay, () =>
            {
                if (panelSlideSound) SoundManager.Instance.PlaySound(panelSlideSound, 1f);
            }, null);
            
            // ...and animate in the new panel
            delay += panelDoorActuateTime - doorActuateDelay;
            AnimatePanel(newPanel, new List<Vector3>
            {
                panelDownOffset, 
                Vector3.zero
            }, panelAscendTime, delay, () =>
            {
                if (panelDownSound) SoundManager.Instance.PlaySound(panelDownSound, 1f);
            }, onComplete);
        }
        
        private bool StartPanelAnimation(GameObject newPanel, int panelIndex)
        {
            // Return if we're already animating
            if (!_currentPanels[panelIndex]) return false;
            
            var blankPanel = _blankPanels[panelIndex];
            var currentPanel = _currentPanels[panelIndex];
            Action onComplete = () =>
            {
                _currentPanels[panelIndex] = newPanel;
                
                // Maybe open covers
                if (newPanel == panel3Launch) UpdateLaunchCover();
                if (newPanel == panel2CommanderSetup) UpdateCommanderCover();
                if (newPanel == panel1Help) UpdateHelp1Cover();
                if (newPanel == panel2Help) UpdateHelp2Cover();
            };
            
            // Clear the current panel (marking that we're animating it)
            _currentPanels[panelIndex] = null;
            
            if (currentPanel == blankPanel)
            {
                // If the current panel is the door, just animate in the new panel without delay
                AnimatePanelIn(newPanel, blankPanel, 0, onComplete);
            }
            else
            {
                // Otherwise, animate the current panel out, hold for a moment, then animate in the new panel
                AnimatePanelOut(currentPanel, blankPanel, () =>
                {
                    currentPanel.SetActive(false);
                    AnimatePanelIn(newPanel, blankPanel, doorActuateHold, onComplete);
                });   
            }

            return true;
        }
        
        private void PrepareFleetStrengths()
        {
            for (var i = 0; i < 6; i++)
            {
                var fleetStatus = Instantiate(fleetStatusPanelTemplate, fleetStatusPanelTemplate.transform.parent, false);
                var fleetStatusBlank = Instantiate(fleetStatusPanelBlankTemplate, fleetStatusPanelBlankTemplate.transform.parent, false);

                // Position the fleet status lines
                var blankPosition = fleetStatusSlots[i].transform.position;
                fleetStatus.gameController = gameController;
                fleetStatus.transform.position = blankPosition;
                fleetStatusBlank.transform.position = blankPosition;
                
                _fleetStatus.Add(fleetStatus);
                _fleetStatusBlanks.Add(fleetStatusBlank);
            }
            
            UpdateFleetStrengths();
        }

        public void UpdateFleetStrengths()
        {
            var commanderList = gameController.CombatCommanders.ToList();
            var count = commanderList.Count;
            for (var i = 0; i < 6; i++)
            {
                var active = i < count;
                _fleetStatus[i].gameObject.SetActive(active);
                _fleetStatusBlanks[i].SetActive(!active);
                
                if (active)
                {
                    _fleetStatus[i].SetCommander(commanderList[i].Key, commanderList[i].Value.Playing, commanderList[i].Value.FleetStrength);
                }
            }
        }

        private readonly List<PanelAnimation> _animatingPanels = new();
        private ButtonMode _lastMode;
        private bool _showNetwork;
        private NetworkMode _currentNetworkMode = NetworkMode.Offline;
        private bool _launchEnabled;
        private bool _commanderEnabled;

        private TeletypeData _leftTeletypeData;
        private TeletypeData _rightTeletypeData;
        private double _nextStatusCharTime;

        private struct TeletypeData
        {
            private string _targetText;
            private string _pendingText;
            private string _currentText;

            private readonly int _maxLines;

            public TeletypeData(int maxLines)
            {
                _maxLines = maxLines;
                _targetText = "";
                _pendingText = "";
                _currentText = "";
            }

            public void SetTargetText(string text)
            {
                _targetText = text;
                if (_currentText == _targetText) return;
                
                // If the target text has changed, create the necessary characters for typing
                _pendingText = new string('\n', _maxLines) + _targetText;
                _currentText = _targetText;
            }

            public char? GetNextChar()
            {
                // Return null if there is no next character
                if (_pendingText.Length == 0) return null;
                
                // Otherwise, take the next character and return it
                var charToType = _pendingText.First();
                _pendingText = _pendingText[1..];
                return charToType;
            }

            public int GetMaxLines() => _maxLines;
        }
        
        private void AnimatePanel(GameObject obj, List<Vector3> waypoints, float duration, float delay, Action onStart, Action onComplete)
        {
            _animatingPanels.Add(new PanelAnimation(obj, waypoints, Time.fixedTime + delay, duration, onStart, onComplete));
        }
        
        private void AnimatePanels()
        {
            var completed = new List<PanelAnimation>();
            
            // Trigger 'OnStart' as required
            var started = _animatingPanels.Where(obj => !obj.Started && Time.fixedTime > obj.StartTime);
            foreach (var start in started)
            {
                start.OnStart?.Invoke();
                start.Started = true;
            }
            
            // Iterate each running animation
            foreach (var animatingPanel in _animatingPanels)
            {
                var elapsed = Time.fixedTime - animatingPanel.StartTime;
                if (elapsed < 0) continue;
                
                // Fixed time is used so that panels (UI) animates at the same speed regardless of game time-warp
                var progress = elapsed / animatingPanel.Duration;
                var position = CalculatePosition(animatingPanel.Waypoints, Mathf.Clamp01(progress));
                
                animatingPanel.Object.transform.localPosition = position;
                
                // Store the completed animations for OnComplete execution and removal after we've finished iterating
                if (progress > 1) completed.Add(animatingPanel);
            }

            // Invoke OnComplete and remove any completed animations from the list
            foreach (var panelAnimation in completed)
            {
                _animatingPanels.Remove(panelAnimation);
                panelAnimation.OnComplete?.Invoke();
            }
        }

        // Interpolate between two waypoints given a global progress (with easing for between waypoints)
        private static Vector3 CalculatePosition(List<Vector3> waypoints, float progress)
        {
            if (waypoints == null || waypoints.Count == 0) return Vector3.zero;
            if (waypoints.Count == 1 || progress <= 0) return waypoints[0];
            if (progress >= 1) return waypoints[^1];

            float totalSegments = waypoints.Count - 1;
            var scaledProgress = progress * totalSegments;

            var startIndex = Mathf.FloorToInt(scaledProgress);
            var endIndex = Mathf.Min(startIndex + 1, waypoints.Count - 1);

            var segmentProgress = Easing.InOutQuad(scaledProgress - startIndex);

            return Vector3.Lerp(waypoints[startIndex], waypoints[endIndex], segmentProgress);
        }
    }

    public class PanelAnimation
    {
        public readonly GameObject Object;
        
        public readonly List<Vector3> Waypoints;
        public readonly float StartTime;
        public readonly float Duration;
        public readonly Action OnStart;
        public readonly Action OnComplete;

        public bool Started;

        public PanelAnimation(GameObject obj, List<Vector3> waypoints, float startTime, float duration, Action onStart, Action onComplete)
        {
            Object = obj;
            Waypoints = waypoints;
            StartTime = startTime;
            Duration = duration;
            OnComplete = onComplete;
            OnStart = onStart;
            
            Started = false;
        }
    }
}
