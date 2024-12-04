using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using TiltFive;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using Input = TiltFive.Input;
using Random = UnityEngine.Random;

namespace Code.Scripts
{
    public class SeaWar : MonoBehaviour, Actuator.IActuatorMovableProvider
    {
        [Header("Ship Templates")]
        public GameObject aircraftCarrierTemplate;
        public GameObject battleshipTemplate;
        public GameObject destroyerTemplate;
        public GameObject lcsTemplate;
        public GameObject submarineTemplate;
        
        [Header("Lobby Ship Templates")]
        public GameObject aircraftCarrierLobbyTemplate;
        public GameObject battleshipLobbyTemplate;
        public GameObject destroyerLobbyTemplate;
        public GameObject lcsLobbyTemplate;
        public GameObject submarineLobbyTemplate;
        
        [Header("Grids")]
        public VisualEffect obscuredTileTemplate;
        public VisualEffect onFireTileTemplate;
        public Material gridMaterialTemplate;
        public Material gridSelectorMaterialTemplate;
        public Material lowPolyMaterial;
        public float gridPitch;
        public float gridOffset;
        public float lobbyGridScale = 0.9f;
        public float lobbyFleetLineWidth = 50f;
        public float lobbyVesselOffset = 120f;
        public Vector3 lowPolyPlaneOffset = new(0, 2.2f, 0);
        public Vector3 lowPolyPlaneScale = new(58, 1, 58);
        
        [Header("View Rotation")]
        public float viewRotationSnapAngle = 2f;
        public float viewRotationAcceleration = 5f;
        public float viewRotationMaxSpeed = 45f;
        public float viewScaleFactor = 0.4f;
        public float viewScaleOffset = 0.75f;
        public float viewZoomSpeed = 1f;

        [Header("Target Follow")]
        public float panToAttackOriginDuration = 2f;
        public float dwellOnAttackOriginDuration = 2f;
        public float followRotateToTargetDuration = 2f;
        public float followHoldOnTargetGridDuration = 2f;
        public float vesselViewAngle = 45f;
        
        [Header("Transition Effect")]
        public float transformEffectDuration = 3f;
        public float transformEffectRange = 4000f;
        public float islandBasedY = -162;
        public float islandTransitionHeight = 120;
        public float islandTransitionStart = 0.2f;
        public float islandTransitionDuration = 0.2f;
        public float materialTransitionStart = 0.2f;
        public float materialTransitionDuration = 0.6f;
        public float viewTransitionOutStart;
        public float viewTransitionOutDuration = 0.2f;
        public float viewTransitionInStart = 0.8f;
        public float viewTransitionInDuration = 0.2f;
        
        public List<Material> transitionEffectMaterials;
        public GameObject island;

        public GameObject gridSelectCube;
        public float gridHighlightHeight = 100;
        
        [FormerlySerializedAs("nameCheckInterval")] public float playerCheckInterval = 3;

        public AudioClip effectTriggerSound;

        [Vector2Range(0.5f, 10f)]
        public Vector2 aiDelay = new(2, 5);

        public VisualEffect fireworkTemplate;
        public AudioClip victorySound;
        public int islandFireworkCount = 12;
        public int islandFireworkDistance = 100;
        public Vector3 islandFireworkScale = Vector3.one;

        public Reticle reticle;
        public float reticleBase = 2f;
        public float reticleFadeUpFraction = 0.5f;
        public float reticleFadeUpDuration = 0.2f;
        public float reticleFadeDownFraction = 0.9f;
        public float reticleFadeDownDuration = 0.1f;

        public AudioClip launchWarningSound;

        public ControlPanel controlPanelTemplate;

        public TMP_FontAsset lobbyFleetLabelFont;
        public GameObject lobbyTitle; 
        
        [Header("Ammo/Explosion Pools")]
        public CannonShellPool cannonShellPool;
        public MissilePool tomahawkMissilePool;
        public MissilePool jsmMissilePool;
        
        [Header("Debug")]
        [Range(0, 1)]
        public float transitionEffectOverrideProgress;
        public bool transitionEffectOverride;
        public Gradient pdfGizmoColor;
        
        ////// FOLLOW RELATED
        [CanBeNull] private IGameboardFollowTarget _followTarget;
        private Vector3 _followTargetPosition;
        private FollowViewStage? _followTargetViewStage;
        private int _shotCounter;
        private float _followTargetStart;
        private float _followTargetImpactTime;

        private Fireworks _islandFireworks;

        private bool _transitionComplete;
        private float _transformEffectStartTime = float.NaN;
        private readonly Dictionary<LocalGrid, Dictionary<Vector2Int, int>> _lastPdfs = new();
        
        private readonly Dictionary<CombatVesselType, Queue<LobbyVessel>> _lobbyFleetCache = new();
        private readonly Dictionary<CombatVesselType, Queue<LobbyVessel>> _activeLobbyFleets = new();
        
        public readonly Dictionary<Commander, CommanderStatus> Commanders = new();

        private List<int> AvailableColors => Enumerable
            .Range(0, CommanderColors.Count)
            .Except(Commanders.Select(e => e.Value.TintColorIndex))
            .ToList();
        
        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
        
        public IEnumerable<KeyValuePair<Commander, CommanderStatus>> CombatCommanders => Commanders
            .Where(e => e.Value.Mode == CommanderMode.Combat);

        private enum TeamLayerVisibility
        {
            TeamOnly,       // Objects on this layer are only visible to a team
            TeamExcluded    // Objects on this layer are visible to everyone except a team
        }
        
        private class TeamInfo
        {
            public int PlayerCount;
            public Color TeamColor;
            public int TeamOnlyLayer; // Objects on this layer are only visible to this team  
            public int TeamExcludedLayer; // Objects on this layer are visible to everyone except this team
            public int CameraLayerMask;
        }
        
        private Dictionary<Color, TeamInfo> _teams = new();
            
        private IEnumerable<Color> ComputeUniqueCommanderColors() => Commanders
            .Select(cmdr => cmdr.Value) // Select statuses...
            .GroupBy(status => status.TeamColor)
            .Select(group => group.Key);

        private IEnumerable<LocalGrid> AllGrids =>
            Commanders.Values.Select(e => e.Grid).Where(grid => grid);
        private IEnumerable<ControlPanel> AllControlPanels =>
            Commanders.Values.Select(e => e.Cpl).Where(cpl => cpl);
        
        private Vector2Int _currentGameMode = Vector2Int.zero;
        
        private const int GRID_SIZE = 8;
        
        private readonly List<TextMeshPro> _cachedTexts = new();

        private class FleetBox
        {
            public GameObject FleetBoxGameObject;
            public Renderer FleetBoxRenderer;
            public LineRenderer FleetLineRenderer;
        }
        private readonly List<FleetBox> _lobbyFleetBoxes = new();
        
        public enum GameState
        {
            Lobby,
            Placing,
            Playing,
            Victory
        }

        public interface IGameboardFollowTarget
        {
            float GetFollowZoom();
            Vector3 GetPosition();
            float GetFlightFraction();
            int GetDistanceToTarget();
            float GetFollowFinishTime();
        }

        public class LauncherFollowTarget : MonoBehaviour, IGameboardFollowTarget
        {
            public float GetFollowZoom() => 1;
            public Vector3 GetPosition() => transform.position;
            public float GetFlightFraction() => 0;
            public int GetDistanceToTarget() => -1;
            public float GetFollowFinishTime() => float.MaxValue;
        }
        
        public class FollowTargetProxy : IGameboardFollowTarget
        {
            public FollowTargetProxy(IGameboardFollowTarget principal)
            {
                Principal = principal;
            }

            public IGameboardFollowTarget Principal;

            public float GetFollowZoom() => Principal.GetFollowZoom();

            public Vector3 GetPosition() => Principal.GetPosition();

            public int GetDistanceToTarget() => Principal.GetDistanceToTarget();

            public float GetFollowFinishTime() => Principal.GetFollowFinishTime();

            public float GetFlightFraction() => Principal.GetFlightFraction();
        }
        
        private float _lastEffectProgress = float.MaxValue;
        private float _transitionEffectViewProgress;

        public readonly NotifyingVariable<GameState> CurrentGameState = new(GameState.Lobby);
        
        private static readonly int EffectRange = Shader.PropertyToID("_Effect_Range");

        private static SeaWar _instance;
        private int _commanderPlayIndex;
        private List<Commander> _commandersPlayOrder;

        public readonly NotifyingVariable<Commander> AttackingCommander = new(null);
        private bool _commanderHasFired;
        private bool _launchWarningShown;
        private float _playerCheckTime;
        [CanBeNull] private Commander _commanderUnderAttack;

        private void Awake()
        {
            if (_instance != null)
            {
                Debug.LogError("Destroying duplicate SeaWar instance");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            PrepareLobbyCaches();
        }

        private void PrepareLobbyCaches()
        {
            foreach (CombatVesselType value in Enum.GetValues(typeof(CombatVesselType)))
            {
                _lobbyFleetCache[value] = new Queue<LobbyVessel>();
                _activeLobbyFleets[value] = new Queue<LobbyVessel>();
            }
        }

        // Start is called before the first frame update
        private void Start()
        {
            WandManager.Instance.MovableProvider = this;

            _islandFireworks = gameObject.AddComponent<Fireworks>();
            _islandFireworks.fireworkTemplate = fireworkTemplate;
            
            StartLobby();
        }

        private float GetEffectProgress()
        {
            if (transitionEffectOverride) return transitionEffectOverrideProgress;
            if (float.IsNaN(_transformEffectStartTime)) return 0;
            return Mathf.Clamp01((Time.time - _transformEffectStartTime) / transformEffectDuration);
        }

        private void StartTransitionEffect()
        {
            // Don't start the transition if we've already run it
            if (!float.IsNaN(_transformEffectStartTime))
            {
                OnTransitionComplete();
                return;
            }
            
            _transformEffectStartTime = Time.time;
            StartCoroutine(PlayTransitionSoundCoroutine());
            StartCoroutine(OnTransitionCompleteCoroutine());
        }

        private IEnumerator PlayTransitionSoundCoroutine()
        {
            yield return new WaitForSeconds(materialTransitionStart * transformEffectDuration);
            SoundManager.Instance.PlaySound(effectTriggerSound, 1);
        }
        
        private IEnumerator OnTransitionCompleteCoroutine()
        {
            yield return new WaitForSeconds(viewTransitionOutDuration * transformEffectDuration);
            OnTransitionComplete();
        }

        private void OnTransitionComplete()
        {
            _transitionComplete = true;
            foreach (var commander in CombatCommanders)
            {
                commander.Value.Grid.NotifyTransitionComplete();
            }
            SetHintTeletypes();
        }

        private void StartGame()
        {
            CurrentGameState.Value = GameState.Playing;
            SoundManager.Instance.StartAmbientAndMusic();
            StartTransitionEffect();
            ClearPlaceHintTeletypes();
            NextPlayer();
        }

        private void Update()
        {
            ApplyTransitionEffect();

            HandleWands();
            AnimateViewRotations();

            ProcessAiCommanders();

            CheckLaunchSwitches();

            PeriodicPlayerCheck();
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var (grid, pdf) in _lastPdfs)
            {
                float max = pdf.Max(e => e.Value);
                foreach (var (key, value) in pdf)
                {
                    var fraction = value / max;
                    var pos = grid.GetGridCenterWorld(key, 0);
                    Gizmos.color = pdfGizmoColor.Evaluate(fraction);

                    var size = new Vector3(10, fraction * 200, 10);
                    pos += new Vector3(0, size.y / 2, 0);
                    Gizmos.DrawWireCube(pos, size);
                }
            }
        }
        
        private void PeriodicPlayerCheck()
        {
            if (Time.fixedTime < _playerCheckTime) return;
            _playerCheckTime = Time.fixedTime + playerCheckInterval;
            
            UpdateConnectedPlayers();
            UpdateGlassesNames();
        }

        private void UpdateConnectedPlayers()
        {
            var knownLocalCommanders = Commanders
                .Where(e => e.Key.IsLocalCommander())
                .Select(e => e.Key)
                .ToList();

            var connectedPlayers = T5Utils.GetConnectedPlayers().ToList();
            var newlyConnectedCommanders = connectedPlayers
                .Where(idx => !knownLocalCommanders
                    .Any(known => known.IsLocalCommander(idx)));

            var playersChanged = false;
            
            // Add the new players
            newlyConnectedCommanders.ToList().ForEach(idx =>
            {
                // Add the new commander, ensuring that only players that are
                // replacing AIs can enter combat - IE don't exceed the player
                // count for the current game mode.
                var (_, aiCount) = PlayerCountsForMode();
                AddPlayerCommander(idx, aiCount > 0);
                playersChanged = true;
            });
            UpdateFleetCounts();

            // If we're in the lobby, remove any players that disconnect
            // (We don't do this during gameplay)
            if (CurrentGameState.Value != GameState.Lobby) return;
            var disconnectedCommanders = knownLocalCommanders
                .Where(c => !connectedPlayers.Contains(c.LocalPlayerIndex));
            foreach (var disconnected in disconnectedCommanders)
            {
                RemovePlayerCommander(disconnected);
                playersChanged = true;
            }
            
            if (playersChanged) UpdateLobbyFleets();
        }

        private void CheckLaunchSwitches()
        {
            if (CurrentGameState.Value != GameState.Placing) return;
            if (CombatCommanders.All(pair => pair.Value.Playing.Value)) StartGame();
        }

        private void ProcessAiCommanders()
        {
            var ais = Commanders.Where(commander => commander.Key.IsAiCommander());
            foreach (var (commander, commanderStatus) in ais)
            {
                // Check if a move is due for this AI
                var isMoveDue = Time.time > commanderStatus.AiNextActionTime;
                if (!isMoveDue) continue;
                commanderStatus.AiNextActionTime = Time.time + Random.Range(aiDelay.x, aiDelay.y);
                
                switch (CurrentGameState.Value)
                {
                    case GameState.Lobby:
                        // No actions in the lobby
                        break;
                    
                    case GameState.Placing:
                        // If placement isn't complete - perform a placement
                        if (!commanderStatus.Playing.Value) ProcessAiPlace(commander);
                        break;
                    
                    case GameState.Playing:
                        if (commander != AttackingCommander.Value || _commanderHasFired) continue;
                        
                        // Perform the attack, setting the 'next try' time to 1 second if attack failed
                        if (!ProcessAiAttack(commander)) commanderStatus.AiNextActionTime = Time.time + 1;
                        break;
                    
                    case GameState.Victory:
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void ProcessAiPlace(Commander commander)
        {
            // Get the unplaced vessels
            var unplacedVessels = Commanders[commander].Grid.CombatVessels
                .Where(e => !e.Value.Placed)
                .ToList();
            
            // If no vessels are unplaced, mark as ready and return
            if (unplacedVessels.Count == 0)
            {
                Commanders[commander].Playing.Value = true;
                return;
            }
            
            var vessel = unplacedVessels.Shuffle().Select(ele => ele.Key).First();
            
            var directions = new List<CombatVesselBase.CardinalDirection>();
            directions.AddRange(Enum.GetValues(typeof(CombatVesselBase.CardinalDirection)));
            
            // Randomly place the vessel
            for (;;)
            {
                var randomDirection = directions.RandomElement();
                var randomPosition = new Vector2Int(Random.Range(0, GRID_SIZE), Random.Range(0, GRID_SIZE));
                
                vessel.SetDirection(randomDirection);
                vessel.SetGridPosition(randomPosition, 0f);
                var valid = Commanders[commander].Grid.PlaceVessel(vessel);
                
                if (valid) break;
            }
        }
        
        private float ComputeViewTransitionProgress(float effectProgress)
        {
            return effectProgress < viewTransitionInStart ? 
                AnimationUtils.ComputeSubAnimationTime(effectProgress, viewTransitionOutStart, viewTransitionOutDuration) : 
                1 - AnimationUtils.ComputeSubAnimationTime(effectProgress, viewTransitionInStart, viewTransitionInDuration);
        }

        private void ApplyTransitionEffect(bool force = false) =>
            ApplyTransitionEffect(GetEffectProgress(), force);
        
        private void ApplyTransitionEffect(float effectProgress, bool force)
        {
            if (!force && Math.Abs(_lastEffectProgress - effectProgress) < 0.0001f) return;
            _lastEffectProgress = effectProgress;
            
            // Compute progress for various parts
            var islandProgress = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(effectProgress, islandTransitionStart, islandTransitionDuration));
            var materialProgress = AnimationUtils.ComputeSubAnimationTime(effectProgress, materialTransitionStart, materialTransitionDuration);
            var viewProgress = Easing.InOutQuad(ComputeViewTransitionProgress(effectProgress));
            
            // Apply the island position
            var currentIslandPosition = island.transform.position;
            currentIslandPosition.y = islandBasedY + islandProgress * islandTransitionHeight;
            island.transform.position = currentIslandPosition;
            
            // Update the grid position
            AllGrids.ToList().ForEach(grid => grid.UpdateGridPosition(viewProgress));
            
            // Update the transition materials
            UpdateTransitionMaterials(materialProgress);

            _transitionEffectViewProgress = viewProgress;
        }

        private (Vector3 position, Quaternion rotation) GetGridOrientationForIndex(float  angle)
        {
            var rotation = Quaternion.Euler(0, angle, 0);
            var position = rotation * new Vector3(gridOffset, 0, 0);
            
            return (position, rotation);
        }
                
        private Dictionary<CombatVesselBase, VesselState> CreateFleet(LocalGrid grid, Commander commander, Queue<string> names)
        {
            Dictionary<CombatVesselBase, VesselState> combatVessels = new();
            foreach (var typeAndPosition in FleetTemplateForMode())
            {
                var vessel = InstantiateVesselType(typeAndPosition.Type, grid, commander);
                vessel.name = names.Dequeue();
                vessel.SetGridPosition(typeAndPosition.StartingPosition, 0);
                combatVessels[vessel] = new VesselState();
            }

            return combatVessels;
        }

        private void PrepareTeams()
        {
            _teams = ComputeUniqueCommanderColors().Select((color, idx) => new TeamInfo
            {
                PlayerCount = 0,
                TeamColor = color,
                TeamOnlyLayer = LayerMask.NameToLayer(LayerNameForTeam(idx, TeamLayerVisibility.TeamOnly)),
                TeamExcludedLayer = LayerMask.NameToLayer(LayerNameForTeam(idx, TeamLayerVisibility.TeamExcluded)),
                CameraLayerMask = LayerMaskForCamera(idx)
            }).ToDictionary(e => e.TeamColor, e => e);
        }

        private static LayerMask LayerMaskForCamera(int teamIndex)
        {
            var layers = new List<string> { "Default" };
            for (var i = 0; i < 4; i++)
                layers.Add(LayerNameForTeam(i, 
                    i == teamIndex ? TeamLayerVisibility.TeamOnly : TeamLayerVisibility.TeamExcluded));
            
            return LayerMask.GetMask(layers.ToArray());
        }

        private void SetCameraVisibility(Commander commander)
        {
            var mask = _teams[Commanders[commander].TeamColor].CameraLayerMask | (1 << commander.SoloLayer);
            
            var playerSettings = TiltFiveManager2.Instance.GetPlayerSettings(commander.LocalPlayerIndex);
            playerSettings.glassesSettings.cullingMask = mask;
        }
        
        public void StartPlacement()
        {
            // Hide the title when we start placement
            lobbyTitle.SetActive(false);
            
            // Hide the lobby fleets
            DisposeLobbyFleets();
            RemoveLobbyFleetLabels();
            RemoveLobbyFleetBoxes();
            
            // Create the AI commanders
            var (_, aiCount) = PlayerCountsForMode();
            
            for (var i = 0; i < aiCount; i++) AddAiCommander(i);
         
            PrepareTeams();
            
            // Randomly select a commander to attack
            _commandersPlayOrder = Commanders.Keys.ToList().Shuffle();
            
            // Create grids for commanders participating in combat
            var commanderList = Commanders.ToList();
            var defaultViewGrid = CreateCommanderGrids(commanderList);
            
            // Set commander type specific properties
            foreach (var (commander, status) in commanderList)
            {
                switch (commander.Type)
                {
                    case Commander.CommanderType.Local:
                        status.Cpl.UpdateFleetStrengths();
                        status.SlewController = new SnappingAngleSlewController(
                            initialAngle: -90f,
                            snapAngle: viewRotationSnapAngle, 
                            acceleration: viewRotationAcceleration,
                            maxSpeed: viewRotationMaxSpeed
                        );
                        status.ZoomLevel = 1;   // Zoom into the fleet
                        if (!status.ViewGrid) status.ViewGrid = defaultViewGrid;
                        SetCameraVisibility(commander);
                        SetPlaceHintTeletype(commander);
                        break;
                    
                    case Commander.CommanderType.Ai:
                        status.AiNextActionTime = Time.time + Random.Range(2, 5);
                        break;
                    
                    case Commander.CommanderType.Remote:
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            // Apply the transition effect to the newly created vessels (and their material instances)
            ApplyTransitionEffect(true);
            
            CurrentGameState.Value = GameState.Placing;
        }

        private LocalGrid CreateCommanderGrids(IReadOnlyList<KeyValuePair<Commander, CommanderStatus>> commanderList)
        {
            var fleets = RandomizeFleets().ToList();
            
            LocalGrid defaultViewGrid = null;
            for (var commanderIndex = 0; commanderIndex < commanderList.Count; commanderIndex++)
            {
                var (commander, status) = commanderList[commanderIndex];
                if (status.Mode != CommanderMode.Combat) continue;
                
                var color = Commanders[commander].TeamColor;
                var grid = CreateGridForCommander(commander, commanderIndex);
                var fleetNames = RandomizeVesselNames(fleets[commanderIndex]).ToQueue();
                
                grid.CombatVessels = CreateFleet(grid, commander, fleetNames);
                grid.CreatePlayerSelectors(Commanders.Keys.ToList());
                status.Grid = grid;
                status.ViewGrid = grid;
                status.TeamName = fleets[commanderIndex].Faction;
                if (!defaultViewGrid) defaultViewGrid = grid;

                _teams[color].PlayerCount++;
            }

            return defaultViewGrid;
        }

        public void EndGame()
        {
            SoundManager.Instance.StopAmbientAndMusic();
            
            CurrentGameState.Value = GameState.Lobby;
            
            // Destroy grids
            Commanders.Select(e => e.Value).ToList().ForEach(status =>
            {
                status.Grid.CombatVessels.ToList().ForEach(vessel =>
                {
                    vessel.Key.Terminate();
                    Destroy(vessel.Key.gameObject);
                });
                
                Destroy(status.Grid.gameObject);
                status.Grid = null;
                status.ViewGrid = null;
                status.ZoomLevel = 0;
                status.ZoomProgress = 0;
                status.FleetStrength.Value = 0;
                status.PlacementComplete = false;
            });
            
            // Remove AI commanders
            var aiCommanders = Commanders.Select(e => e.Key).Where(e => e.IsAiCommander()).ToList();
            aiCommanders.ForEach(aiCommander => Commanders.Remove(aiCommander));
         
            // Update human control panels
            Commanders.Select(e => e.Value).ToList().ForEach(status =>
            {
                status.Cpl.SetLaunchEnabled(false);
                status.Cpl.SetLeftTeletype("");
            });
            
            // Destroy fireworks
            _islandFireworks.Clear();
            
            // Recreate the lobby fleets
            UpdateLobbyFleets();
        }

        private void AddAiCommander(int aiIndex)
        {
            Commanders[Commander.MakeAiCommander(aiIndex)] = new CommanderStatus
            {
                TintColorIndex = AvailableColors[aiIndex],
                Name = $"AI {aiIndex}"
            };
        }
        
        private void AddPlayerCommander(PlayerIndex localPlayerIndex, bool enabledForCombat)
        {
            var commander = Commander.MakeLocalCommander(localPlayerIndex);
            var status = new CommanderStatus();
            
            var settings = TiltFiveManager2.Instance.GetPlayerSettings(localPlayerIndex);
            status.GameboardTransform = settings.gameboardSettings.currentGameBoard.transform;
            status.Mode = enabledForCombat ? CommanderMode.Combat : CommanderMode.Observe;
            
            // Create the control panel
            status.Cpl = CreateControlPanelForCommander(commander);
            status.Cpl.SetCommanderEnabled(enabledForCombat);
            
            // Store the initial transform
            var controlPanelTransform = status.Cpl.transform;
            status.CplInitialView = new ViewParameters(
                controlPanelTransform.localRotation,
                controlPanelTransform.localScale,
                controlPanelTransform.localPosition
            );
            
            Commanders[commander] = status;
        }

        private void RemovePlayerCommander(Commander commander)
        {
            var status = Commanders[commander];
            if (status.Cpl) Destroy(status.Cpl.gameObject);

            Commanders.Remove(commander);
        }
        
        private void StartLobby()
        {
            Commanders.Clear();
            _islandFireworks.Clear();
            
            UpdateFleetCounts();

            UpdateLobbyFleets();
        }

        private CombatVesselBase InstantiateVessel(GameObject template, LocalGrid localGrid, Commander commander)
        {
            var vesselInstance = Instantiate(template);
            var vessel = vesselInstance.GetComponent<CombatVesselBase>();
            vessel.SetGameController(this);
            vessel.initialDirection = CombatVesselBase.CardinalDirection.North;
            
            if (localGrid) vessel.SetLocalGrid(localGrid);
            
            // Set commander specific properties
            if (commander == null) return vessel;
            vessel.SetLayer(_teams[Commanders[commander].TeamColor].TeamOnlyLayer);
            vessel.SetColorSplash(Commanders[commander].TeamColor);
            return vessel;
        }

        private CombatVesselBase InstantiateVesselType(CombatVesselType type, LocalGrid localGrid, Commander commander)
        {
            var vessel = type switch
            {
                CombatVesselType.AircraftCarrier => InstantiateVessel(aircraftCarrierTemplate, localGrid, commander),
                CombatVesselType.Battleship => InstantiateVessel(battleshipTemplate, localGrid, commander),
                CombatVesselType.Destroyer => InstantiateVessel(destroyerTemplate, localGrid, commander),
                CombatVesselType.Submarine => InstantiateVessel(submarineTemplate, localGrid, commander),
                CombatVesselType.LittoralCombatShip => InstantiateVessel(lcsTemplate, localGrid, commander),
                _ => throw new ArgumentOutOfRangeException()
            };

            // Special handling
            switch (type)
            {
                case CombatVesselType.Battleship:
                    (vessel as Battleship)!.cannonShellPool = cannonShellPool;
                    break;
                
                case CombatVesselType.LittoralCombatShip:
                    (vessel as Lcs)!.cannonShellPool = cannonShellPool;
                    break;

                case CombatVesselType.Destroyer:
                    (vessel as Destroyer)!.missilePool = tomahawkMissilePool;
                    break;
                    
                case CombatVesselType.AircraftCarrier:
                    (vessel as AircraftCarrier)!.missilePool = jsmMissilePool;
                    break;
                
                case CombatVesselType.Submarine:
                    (vessel as Submarine)!.missilePool = tomahawkMissilePool;
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            
            return vessel;
        }

        private struct LobbyVessel
        {
            public GameObject VesselGameObject;
            public Material VesselMaterial;
        }

        private LobbyVessel InstantiateLobbyVesselType(CombatVesselType type)
        {
            var vessel = type switch {
                CombatVesselType.AircraftCarrier => Instantiate(aircraftCarrierLobbyTemplate),
                CombatVesselType.Battleship => Instantiate(battleshipLobbyTemplate),
                CombatVesselType.Destroyer => Instantiate(destroyerLobbyTemplate),
                CombatVesselType.Submarine => Instantiate(submarineLobbyTemplate),
                CombatVesselType.LittoralCombatShip => Instantiate(lcsLobbyTemplate),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            // Get the renderer
            var vesselRenderer = vessel.GetComponent<Renderer>();
            var material = vesselRenderer.material = Instantiate(vesselRenderer.material); 
            return new LobbyVessel{VesselGameObject = vessel, VesselMaterial = material};
        }
        
        private List<LobbyVessel> GetLobbyFleet(Vector2Int gameMode)
        {
            var vessels = new List<LobbyVessel>();
            
            var fleetTemplate = FleetTemplateForMode(gameMode);
            fleetTemplate.ForEach(typeAndPosition =>
            {
                if (!_lobbyFleetCache[typeAndPosition.Type].TryDequeue(out var vessel))
                {
                    vessel = InstantiateLobbyVesselType(typeAndPosition.Type);
                }
                _activeLobbyFleets[typeAndPosition.Type].Enqueue(vessel);
                vessel.VesselGameObject.gameObject.SetActive(true);
                vessels.Add(vessel);
            });
            
            ApplyTransitionEffect(true);

            return vessels;
        }
        
        private void DisposeLobbyFleets()
        {
            _activeLobbyFleets.ToList().ForEach(e =>
            {
                while (e.Value.TryDequeue(out var vessel))
                {
                    vessel.VesselGameObject.SetActive(false);
                    _lobbyFleetCache[e.Key].Enqueue(vessel);
                }
            });
        }

        private static void SetLobbyFleetColor(List<LobbyVessel> vessels, Color color)
        {
            vessels.ForEach(vessel => vessel.VesselMaterial.color = color);
        }
        
        private void UpdateLobbyFleets()
        {
            DisposeLobbyFleets();
            
            var gameMode = _currentGameMode;
            var (humans, ais) = PlayerCountsForMode(gameMode);
            var totalFleets = humans + ais;
            
            var namesAndColors = CombatCommanders
                .Select(c => (
                    c.Value.Name, 
                    TintIndex: c.Value.TintColorIndex, 
                    TintColor: c.Value.TeamColor))
                .ToList();

            MakeUpLobbyAiFleets(namesAndColors, totalFleets);

            // Sort the fleets such that fleets on the same team are adjacent
            namesAndColors.Sort((a, b) => a.TintIndex - b.TintIndex);

            // Prepare team connection lines
            var teamLines = new List<int>();
            for (var i = 1; i < namesAndColors.Count; i++)
                if (namesAndColors[i - 1].TintIndex == namesAndColors[i].TintIndex) teamLines.Add(i);
            
            RemoveLobbyFleetLabels();
            RemoveLobbyFleetBoxes();
            
            for (var i = 0; i < totalFleets; i++)
            {
                ComputeLobbyFleetGeometry(i, totalFleets, out var position, out var rotation);

                // Create and position the fleet
                var fleet = GetLobbyFleet(gameMode);
                SetLobbyFleetColor(fleet, namesAndColors[i].TintColor);
                PositionLobbyFleet(fleet, position, rotation);

                // Position the label
                var textPosition = rotation * new Vector3(gridOffset, 2, 0);
                PositionLobbyFleetLabel(i, totalFleets, namesAndColors[i].Name, namesAndColors[i].TintColor, textPosition, rotation);
                PositionLobbyFleetBox(i, totalFleets, namesAndColors[i].TintColor, position, rotation, teamLines.Contains(i));
            }
        }

        private void ComputeLobbyFleetGeometry(int i, int totalFleets, out Vector3 position, out Quaternion rotation)
        {
            var angle = i / (totalFleets + 0f);
            rotation = Quaternion.Euler(0, angle * 360, 0);
            position = rotation * new Vector3(gridOffset, 20, 0);
        }

        private void MakeUpLobbyAiFleets(ICollection<(string Name, int TintIndex, Color TintColor)> namesAndColors, int totalFleets)
        {
            // Make up the remaining fleets required for the current game mode with AIs
            var aiIndex = 0;
            while (namesAndColors.Count < totalFleets)
            {
                var colorIndex = AvailableColors[aiIndex++];
                
                namesAndColors.Add((
                    "AI",
                    TintIndex: colorIndex,
                    TintColor: CommanderColors[colorIndex]));
            }
        }

        private void PositionLobbyFleet(List<LobbyVessel> fleet, Vector3 position, Quaternion rotation)
        {
            var vesselRotation = Quaternion.Euler(0, -90, 0);
            var vesselScale = Vector3.one * 1.5f;
            var vesselOffset = new Vector3(0, 0, lobbyVesselOffset);
            var rotatedOffset = rotation * vesselOffset;
            
            var vesselPositionIndex = (fleet.Count - 1) / -2f;
            fleet.ForEach(vessel =>
            {
                var vesselTransform = vessel.VesselGameObject.transform;
                vesselTransform.position = position + rotatedOffset * vesselPositionIndex;

                vesselTransform.localRotation = vesselRotation * rotation;
                vesselTransform.localScale = vesselScale;
                vesselPositionIndex++;
            });
        }

        private void RemoveLobbyFleetLabels()
        {
            _cachedTexts.ForEach(text => text.gameObject.SetActive(false));
        }

        private void PositionLobbyFleetLabel(int i, int totalFleets, string label, Color tintColor, Vector3 position, Quaternion rotation)
        {
            // Position the label
            TextMeshPro textMeshPro;

            // Reuse or create TextMeshPro instance
            if (i < _cachedTexts.Count)
            {
                textMeshPro = _cachedTexts[i];
            }
            else
            {
                textMeshPro = new GameObject($"Lobby Fleet Label ({i + 1}/{totalFleets})").AddComponent<TextMeshPro>();
                _cachedTexts.Add(textMeshPro);
            }
            textMeshPro.gameObject.SetActive(true);

            // Update TextMeshPro instance
            textMeshPro.text = label;
            textMeshPro.color = tintColor;
            textMeshPro.alignment = TextAlignmentOptions.Center;
            textMeshPro.fontStyle = FontStyles.UpperCase;
            textMeshPro.font = lobbyFleetLabelFont;
            textMeshPro.characterSpacing = 8;
            textMeshPro.transform.localScale = new Vector3(40, 40, 40);
            textMeshPro.transform.position = position + rotation * new Vector3(-350, 0, 0);
            textMeshPro.transform.rotation = rotation * Quaternion.Euler(90, -90, 0);
        }
        
        private void RemoveLobbyFleetBoxes()
        {
            _lobbyFleetBoxes.ForEach(box =>
            {
                box.FleetLineRenderer.enabled = false;
                box.FleetBoxGameObject.SetActive(false);
            });
        }
        
        private void PositionLobbyFleetBox(int i, int totalFleets, Color tintColor, Vector3 position, Quaternion rotation, bool drawLine)
        {
            FleetBox lobbyFleetBox;
            if (i < _lobbyFleetBoxes.Count)
            {
                lobbyFleetBox = _lobbyFleetBoxes[i];
            }
            else
            {
                var box = Instantiate(gridSelectCube);
                box.name = $"Lobby Fleet Box ({i + 1}/{totalFleets})";
                
                lobbyFleetBox = new FleetBox
                {
                    FleetBoxGameObject = box,
                    FleetBoxRenderer = box.GetComponent<Renderer>(),
                    FleetLineRenderer = box.AddComponent<LineRenderer>()
                };

                // Prepare the line renderer
                lobbyFleetBox.FleetLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lobbyFleetBox.FleetLineRenderer.startWidth = lobbyFleetLineWidth;
                lobbyFleetBox.FleetLineRenderer.endWidth = lobbyFleetLineWidth;
                lobbyFleetBox.FleetLineRenderer.numCapVertices = 2;
                lobbyFleetBox.FleetLineRenderer.positionCount = 2;
                _lobbyFleetBoxes.Add(lobbyFleetBox);
            }
            lobbyFleetBox.FleetBoxGameObject.SetActive(true);
            
            // Position, rotate and scale the lobby fleet box
            var gridHalfSide = GRID_SIZE * gridPitch * lobbyGridScale; 
            var gridScale = new Vector3(gridHalfSide * 2, gridHighlightHeight, gridHalfSide * 2);
            lobbyFleetBox.FleetBoxGameObject.transform.localScale = gridScale;
            lobbyFleetBox.FleetBoxGameObject.transform.position = new Vector3(position.x, gridHighlightHeight / 2, position.z);
            lobbyFleetBox.FleetBoxGameObject.transform.rotation = rotation;
            lobbyFleetBox.FleetBoxRenderer.material.SetColor(UnlitColor, tintColor);
            lobbyFleetBox.FleetBoxRenderer.material.SetColor(EmissiveColor, tintColor);

            // Return if no team line is required
            if (!drawLine) return;
            
            // Draw the team line
            ComputeLobbyFleetGeometry(i - 1, totalFleets, out var fromPosition, out var fromRotation);

            var lineOffset = new Vector3(0, 0, -gridHalfSide);
            lobbyFleetBox.FleetLineRenderer.SetPosition(0, fromPosition + fromRotation * lineOffset);
            lobbyFleetBox.FleetLineRenderer.SetPosition(1, position + rotation * -lineOffset);
            
            lobbyFleetBox.FleetLineRenderer.startColor = tintColor;
            lobbyFleetBox.FleetLineRenderer.endColor = tintColor;
            lobbyFleetBox.FleetLineRenderer.enabled = true;
        }
        
        private ControlPanel CreateControlPanelForCommander(Commander commander)
        {
            var obj = Instantiate(controlPanelTemplate);
            obj.gameController = this;
            obj.CommanderForPanel = commander;
            obj.SetLayerInChildren(commander.SoloLayer);
            return obj;
        }

        private Commander IncrementAttackingCommander()
        {
            _commanderPlayIndex = (_commanderPlayIndex + 1) % _commandersPlayOrder.Count;
            AttackingCommander.Value = _commandersPlayOrder[_commanderPlayIndex];
            return AttackingCommander.Value;
        }

        private void UpdateTransitionMaterials(float effectProgress)
        {
            var range = effectProgress * transformEffectRange;
            foreach (var mat in transitionEffectMaterials) mat.SetFloat(EffectRange, range);
        }

        public void RegisterTransitionEffectMaterial(Material material)
        {
            transitionEffectMaterials.Add(material);
        }
        
        private LocalGrid CreateGridForCommander(Commander commander, int commanderIndex)
        {
            // Compute the angle of the grid (360 / number of commanders engaging in combat)
            var playerAngleOffset = 360f / CombatCommanders.Count();
            var gridAngle = commanderIndex * playerAngleOffset;
            
            var (position, rotation) = GetGridOrientationForIndex(gridAngle);
            
            var obj = new GameObject
            {
                transform =
                {
                    position = position,
                    rotation = rotation
                }
            };
            obj.transform.SetParent(transform);
            obj.name = $"Home Game Grid ({commander})";
            
            var grid = obj.AddComponent<LocalGrid>();
            
            grid.obscuredTileTemplate = obscuredTileTemplate;
            grid.onFireTileTemplate = onFireTileTemplate;
            grid.gridMaterialTemplate = gridMaterialTemplate;
            grid.gridSelectorMaterialTemplate = gridSelectorMaterialTemplate;
            grid.lowPolyMaterial = lowPolyMaterial;
            grid.gridPitch = gridPitch;
            grid.lowPolyPlaneOffset = lowPolyPlaneOffset;
            grid.lowPolyPlaneScale = lowPolyPlaneScale;
            grid.gridSelectCube = gridSelectCube;
            grid.gridHighlightHeight = gridHighlightHeight;

            grid.width = GRID_SIZE;
            grid.height = GRID_SIZE;

            grid.GridCommander = commander;
            grid.gameController = this;
            grid.gridAngle = gridAngle;
            
            return grid;
        }

        public IEnumerable<Actuator.IActuatorMovable> GetMovables(PlayerIndex playerIndex)
        {
            // Only expose movables when placing ships
            if (CurrentGameState.Value != GameState.Placing) return new List<Actuator.IActuatorMovable>();
            
            return CombatCommanders.TryFirstOrDefault(c => c.Key.IsLocalCommander(playerIndex), out var ele) ? 
                ele.Value.Grid.GetMovables() : 
                new List<Actuator.IActuatorMovable>();
        }

        public void PostProcessMovablesList(List<Actuator.IActuatorMovable> inRange, 
            List<Actuator.IActuatorMovable> notInRange) => Actuator.ReduceToSingle(inRange, notInRange);
        
        public void DispatchPointerPosition(Actuator actuator, Vector3 position)
        {
            var playerIndex = actuator.GetPlayerIndex();
            if (!Commanders
                    .Select(c => c.Key)
                    .TryFirstOrDefault(c => c.IsLocalCommander(playerIndex), out var commander))
                return;

            LocalGrid closestGrid = null;
            var closestDistance = float.MaxValue;
            AllGrids.ToList().ForEach(grid =>
            {
                grid.HandlePointer(commander, position);
                
                // Find the closest grid
                var distance = (grid.transform.position - position).sqrMagnitude;
                if (distance >= closestDistance) return;
                closestDistance = distance;
                closestGrid = grid;
            });
            Commanders[commander].ClosestGridToPointer = closestGrid;
            
            // Within the closest grid, find the closest vessel
            if (!closestGrid) return;
            CombatVesselBase closestVessel = null;
            closestDistance = float.MaxValue;
            closestGrid.CombatVessels.ToList().ForEach(vessel =>
            {
                // Don't zoom in to vessels that don't belong to the commander unless they're destroyed
                if (closestGrid.GridCommander != commander && 
                    !vessel.Key.IsDestroyed() && 
                    CurrentGameState.Value != GameState.Victory) return;
                
                var distance = (vessel.Key.transform.position - position).sqrMagnitude;
                if (distance >= closestDistance) return;
                closestDistance = distance;
                closestVessel = vessel.Key;
            });
            Commanders[commander].ClosestVesselToPointer = closestVessel;
        }

        private void HandleWands()
        {
            foreach (var commander in Commanders
                         .Where(commander => commander.Key.IsLocalCommander())) HandleWand(commander.Key);            
        }
        
        private void HandleWand(Commander commander)
        {
            HandleWandStick(commander);
            HandleWandTrigger(commander);
            HandleWandButtons(commander);
        }

        private static int StickPositionToInt(float position) => position switch
            {
                > 0.5f => 1,
                < -0.5f => -1,
                _ => 0
            };

        private static Vector2Int QuantizeStick(Vector2 stick) => new(StickPositionToInt(stick.x), StickPositionToInt(stick.y));

        private void HandleWandStick(Commander commander)
        {
            var playerIndex = commander.LocalPlayerIndex;
            var stickTilt = Input.GetStickTilt(playerIndex: playerIndex);
            
            var quantizedStick = QuantizeStick(stickTilt);
            var lastStick = Commanders[commander].StickPosition;
            Commanders[commander].StickPosition = quantizedStick;

            if (quantizedStick.x != lastStick.x) HandleThumbstickHorizontal(commander, quantizedStick.x);
            if (quantizedStick.y != lastStick.y) HandleThumbstickVertical(commander, quantizedStick.y);
        }
        
        private void HandleThumbstickHorizontal(Commander commander, int direction)
        {
            switch (CurrentGameState.Value)
            {
                case GameState.Lobby:
                    break;
                
                case GameState.Placing:
                    HandleThumbstickHorizontalPlacing(commander, direction);
                    break;
                
                case GameState.Playing:
                case GameState.Victory:
                    HandleThumbstickHorizontalPlaying(commander, direction);
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void HandleThumbstickVertical(Commander commander, int direction)
        {
            switch (CurrentGameState.Value)
            {
                case GameState.Lobby:
                case GameState.Placing:
                    break;
                
                case GameState.Playing:
                case GameState.Victory:
                    HandleThumbstickVerticalPlaying(commander, direction);
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void HandleThumbstickHorizontalPlacing(Commander commander, int stickDirection) =>
            Commanders[commander].Grid.RotateMovingVessel(stickDirection);
        
        private void HandleThumbstickHorizontalPlaying(Commander commander, int stickDirection)
        {
            var slewController = Commanders[commander].SlewController;
            
            slewController.Target = stickDirection switch
            {
                > 0 => slewController.Target.AddAndClamp360(-90),
                < 0 => slewController.Target.AddAndClamp360(90),
                _ => slewController.Target
            };
        }
        
        private void HandleThumbstickVerticalPlaying(Commander commander, int stickDirection)
        {
            var closestGrid = Commanders[commander].ClosestGridToPointer;
            var closestVessel = Commanders[commander].ClosestVesselToPointer;
            
            if (stickDirection > 0)
            {
                // Store the closest grid if we're zooming in to grid level
                if (Commanders[commander].ZoomLevel == 0)
                {
                    Commanders[commander].ViewGrid = closestGrid;
                }

                // Store the closest ship if we're zooming in to ship level (And abort the zoom if no ship is close)
                if (Commanders[commander].ZoomLevel == 1)
                {
                    if (!closestVessel) return;
                    Commanders[commander].ViewCellOffset = closestVessel.transform.position - closestGrid.transform.position;
                }
            }
            
            Commanders[commander].ZoomLevel = Mathf.Clamp(Commanders[commander].ZoomLevel + Math.Sign(stickDirection), 0, 2);
            SetControlPanelText(commander);
            SetHintTeletype(commander);
        }

        private void SetControlPanelText(Commander commander)
        {
            var closestGrid = Commanders[commander].ClosestGridToPointer;
            if (!closestGrid) return;
            
            var gridCommander = closestGrid.GridCommander;
            var gridCommanderStatus = Commanders[gridCommander];
            switch (Commanders[commander].ZoomLevel)
            {
                case 1:
                    Commanders[commander].Cpl.SetLeftTeletype($"{gridCommanderStatus.Name}\nFLEET: {gridCommanderStatus.TeamName}");
                    break;
                
                case 2:
                    Commanders[commander].Cpl.SetLeftTeletype(ControlPanelVesselText(gridCommander, Commanders[commander].ClosestVesselToPointer));
                    break;
                
                default:
                    Commanders[commander].Cpl.SetLeftTeletype("");
                    break;
            }
        }

        private string ControlPanelFireOriginText(Commander vesselCommander, string fireOriginLabel) => 
            $"{fireOriginLabel}\nFLEET: {Commanders[vesselCommander].TeamName}";
        
        private string ControlPanelVesselText(Commander vesselCommander, CombatVesselBase vessel) => 
            $"{vessel.GetIdent()}\nFLEET: {Commanders[vesselCommander].TeamName}";
        
        private void HandleWandTrigger(Commander commander)
        {
            var trigger = Input.GetTrigger(playerIndex: commander.LocalPlayerIndex);
            switch (trigger)
            {
                case >= 0.5f when !Commanders[commander].TriggerDown:
                {
                    Commanders[commander].TriggerDown = true;
                    AllGrids.ToList().ForEach(grid => grid.HandleTriggerPull(commander));
                    break;
                }
                case < 0.5f:
                    Commanders[commander].TriggerDown = false;
                    break;
            }
        }

        private void HandleWandButtons(Commander commander)
        {
            var playerIndex = commander.LocalPlayerIndex;

            if (!Input.TryGetButtonUp(Input.WandButton.Two, out var buttonUp,
                    ControllerIndex.Right, playerIndex) || !buttonUp) return;
            
            if (Commanders[commander].FollowEnabled)
                Commanders[commander].Following = !Commanders[commander].Following;                    
        }

        private void AnimateViewRotations()
        {
            foreach (var (commander, status) in Commanders
                         .Where(ele =>
                             ele.Key.IsLocalCommander()))
            {
                var view = GetCurrentView(commander);
                ApplyView(status.GameboardTransform, view);
                
                // Apply the reciprocal scale to the control panel
                var initial = status.CplInitialView;
                
                var scale = initial.Scale.Multiply(view.Scale.Reciprocal());

                var cplRotation = view.Rotation;
                if (T5Utils.TryGetPlayerDirection(commander.LocalPlayerIndex, out var direction))
                    cplRotation *= direction.Rotation();
                
                var scaledPosition = (cplRotation * initial.Position).Multiply(scale);
                
                var cplTransform = status.Cpl.transform;
                cplTransform.localScale = scale;
                cplTransform.localPosition = view.Position + scaledPosition;
                cplTransform.localRotation = cplRotation;
            }

            PositionReticle();
        }

        private void PositionReticle()
        {
            if (_followTarget == null) return;
            
            if (!reticle.gameObject.activeSelf) reticle.gameObject.SetActive(true);
            
            var followTarget = _followTarget.GetPosition();
            followTarget.y = reticleBase;
            reticle.transform.position = followTarget;

            // Compute reticle opacity
            var flightFraction = _followTarget.GetFlightFraction();
            var opacity = flightFraction < reticleFadeDownFraction ? 
                AnimationUtils.ComputeSubAnimationTime(flightFraction, reticleFadeUpFraction, reticleFadeUpDuration) : 
                1 - AnimationUtils.ComputeSubAnimationTime(flightFraction, reticleFadeDownFraction, reticleFadeDownDuration);
            reticle.SetOpacity(opacity);

            // Update distance to target for the attacking commander
            var distanceToTarget = _followTarget.GetDistanceToTarget();
            MaybeSetDistanceToTarget(AttackingCommander.Value, distanceToTarget);
            
            // Update distance to target and show warning for the attacked commander if we're in warning range
            var revealInboundAttack = float.IsNormal(flightFraction) && flightFraction > reticleFadeUpFraction;
            if (!revealInboundAttack) return;
            MaybeShowLaunchWarning();
            MaybeSetDistanceToTarget(_commanderUnderAttack, distanceToTarget);
        }

        private void MaybeSetDistanceToTarget(Commander commander, int distanceToTarget)
        {
            if (commander == null) return;
            if (commander.IsLocalCommander()) Commanders[commander].Cpl.DistanceToTarget = distanceToTarget;
        }

        private static void ApplyView(Transform gameboardTransform, ViewParameters parameters)
        {
            gameboardTransform.localRotation = parameters.Rotation;
            gameboardTransform.localScale = parameters.Scale;
            gameboardTransform.localPosition = parameters.Position;
        }

        private float UpdateAndGetZoom(Commander commander)
        {
            var zoomDelta = Mathf.Abs(Commanders[commander].ZoomLevel - Commanders[commander].ZoomProgress);
            var zoomDirection = Commanders[commander].ZoomLevel > Commanders[commander].ZoomProgress ? 1 : -1;
            var zoomStep = viewZoomSpeed * Time.deltaTime * zoomDirection;
            if (Mathf.Abs(zoomStep) < zoomDelta)
            {
                Commanders[commander].ZoomProgress = Mathf.Clamp(Commanders[commander].ZoomProgress + zoomStep, 0, 2);
            }
            else
            {
                Commanders[commander].ZoomProgress = Commanders[commander].ZoomLevel;
            }
            return Commanders[commander].ZoomProgress;
        }

        private float GetFollowZoom(float stageProgress)
        {
            var followTarget = _followTarget;
            if (followTarget == null) return 1;
            
            // Get the requested zoom for the follow target,
            // and adjust so zoom is in the range 1-<requested zoom>
            var followZoom = followTarget.GetFollowZoom() - 1;
            return 1 + Mathf.Clamp01(_followTargetViewStage switch
            {
                FollowViewStage.PanToAttackOrigin => stageProgress,
                FollowViewStage.DwellOnAttackOrigin => 1,
                FollowViewStage.Follow => 1 - stageProgress,
                _ => 0
            }) * followZoom;
        }
        
        private ViewParameters GetCurrentView(Commander commander)
        {
            // Update zoom
            var zoom = _transitionEffectViewProgress != 0 ? 1 - _transitionEffectViewProgress : UpdateAndGetZoom(commander);
            
            // Update rotation
            var gridAngle = Commanders[commander].SlewController?.Update(Time.deltaTime) ?? 0;
            
            // If we're not following a target, return the user controller view
            if (_followTargetViewStage == null ||
                !Commanders[commander].FollowEnabled ||
                !Commanders[commander].Following) return ComputeView(commander, gridAngle, zoom);
            
            // Otherwise, lerp and return the follow view
            var panAndDwellDuration = panToAttackOriginDuration + dwellOnAttackOriginDuration;
            var elapsed = Time.time - _followTargetStart;
            var followStageProgress = Mathf.Clamp01(_followTargetViewStage switch
            {
                FollowViewStage.PanToAttackOrigin => 
                    elapsed / panToAttackOriginDuration,
                FollowViewStage.DwellOnAttackOrigin => 
                    (elapsed - panToAttackOriginDuration) / dwellOnAttackOriginDuration,
                FollowViewStage.Follow => 
                    (elapsed - panAndDwellDuration) / followRotateToTargetDuration,
                FollowViewStage.TargetGrid => 
                    (Time.time - _followTargetImpactTime) / followHoldOnTargetGridDuration,
                _ => throw new ArgumentOutOfRangeException()
            });
            
            var easedProgress = Easing.InOutQuad(followStageProgress);
            var view = ComputedBlendedView(commander, easedProgress, gridAngle, zoom * GetFollowZoom(easedProgress));
            
            return view;
        }

        private ViewParameters ComputeView(Commander commander, float gridAngle, float zoom)
        {
            zoom = zoom <= 1 ? zoom : 1 + (zoom - 1) * 2;
            var l2ZoomProgress = Mathf.Clamp01((zoom - 1) / 2);
            
            // Update scale
            var scale = 1 + zoom * viewScaleFactor + viewScaleOffset;
            if (scale < 0.01f) scale = 0.01f;
            
            // Update position
            var viewGridAngle = Commanders[commander].ViewGrid ? Commanders[commander].ViewGrid.gridAngle : 0;
            var viewGridRotation = Quaternion.Euler(0, viewGridAngle, 0);
            var cellOffset = Commanders[commander].ViewCellOffset * l2ZoomProgress;
            var yOffset = l2ZoomProgress * 50f;
            var position = viewGridRotation * new Vector3(gridOffset * Mathf.Clamp01(zoom), yOffset, 0) + cellOffset;

            // Update rotation
            var currentRotationEuler = Quaternion.Euler(0, gridAngle, 0);
            
            return new ViewParameters(currentRotationEuler, new Vector3(scale, scale, scale), position);
        }
        
        private ViewParameters ComputeFollowView(IGameboardFollowTarget followTarget, float gridAngle, float zoom)
        {
            // Update position (and store for blending)
            // If following has finished, use the blend (last stored) position instead
            if (followTarget != null && followTarget.GetFollowFinishTime() > Time.time)
                _followTargetPosition = followTarget.GetPosition();

            // Update rotation
            var currentRotation = Quaternion.Euler(0, gridAngle, 0);
            
            // Update scale
            var scale = 1 + zoom + viewScaleOffset;
            if (scale < 0.01f) scale = 0.01f;
            scale *= 3; // Add a zoom effect to follow targets (ToDo: Fold into the zoom parameter?)
            
            // Offset so we can see the missile
            var viewOffset = new Vector3(0, 20, 0);
            
            return new ViewParameters(
                currentRotation, 
                new Vector3(scale, scale, scale), 
                _followTargetPosition + viewOffset
            );
        }
        
        public void Attack(Commander commander, LocalGrid toGrid, 
            CombatVesselBase.FireReservation fireReservation, Vector2Int attackCell, Vector3 attackCoordinates)
        {
            // Discard attacks if the commander isn't attacking
            if (_commanderHasFired || commander != AttackingCommander.Value) return;

            // Mark hints as complete after the first attack
            if (!Commanders[commander].HintsShown )
            {
                Commanders[commander].HintsShown = true;
                if (Commanders[commander].Cpl) Commanders[commander].Cpl.SetRightTeletype("");
            }
            
            _commanderHasFired = true;
            _commanderUnderAttack = toGrid.GridCommander;
            Commanders[commander].FollowEnabled = true;
            Commanders[commander].Following = true;
            
            SetStatusLights();
            
            StartCoroutine(AttackViewCoroutine(commander, toGrid, fireReservation, attackCell, attackCoordinates));
        }

        private void NextPlayer()
        {
            // Obtain the next commander who's still playing
            Commander commander;
            int attempts = 0; // Add a counter to prevent infinite loop
            while (!Commanders[commander = IncrementAttackingCommander()].Playing.Value && attempts < Commanders.Count)
            {
                attempts++;
            }

            if (attempts == Commanders.Count)
            {
                Debug.LogWarning("No commanders are currently playing.");
                // todo (sclokey): Figure out what to do to in this case where all players are
                // somehow null. Also figure out why any of the players are showing up null when
                // the glasses are still plugged in.
                return;
            }
            
            _launchWarningShown = false;
            _commanderUnderAttack = null;
            _commanderHasFired = false;
            
            SetStatusLights();
            ClearDistancesToTarget();
            
            // Initially disable the reticle - we'll display it as appropriate during flight
            reticle.gameObject.SetActive(false);
            
            if (commander.IsAiCommander())
            {
                // If we've switch to an AI commander, add a random delay before they make their attack
                Commanders[commander].AiNextActionTime = Time.time + Random.Range(aiDelay.x, aiDelay.y);
            }

            // If transition is complete, show the hint.
            // It'll be shown automatically when the transition completes.
            if (_transitionComplete) SetHintTeletypes();
        }

        private void SetPlaceHintTeletype(Commander commander)
        {
            var status = Commanders[commander];
            status.Cpl.SetLeftTeletype(status.PlacementComplete
                ? "TURN LAUNCH SWITCH WHEN READY"
                : "HOLD TRIGGER AND DRAG TO PLACE VESSELS");
        }

        private void ClearPlaceHintTeletypes()
        {
            foreach (var (commander, _) in Commanders)
            {
                // Only set text for local players
                if (!commander.IsLocalCommander()) continue;
                Commanders[commander].Cpl.SetLeftTeletype("");
            }
        }

        private void SetHintTeletypes()
        {
            foreach (var (commander, _) in Commanders)
            {
                SetHintTeletype(commander);
            }
        }
        
        private void SetHintTeletype(Commander commander)
        {
            // Only set text for local players
            if (!commander.IsLocalCommander()) return;

            var status = Commanders[commander];

            // Don't show hints if the hints have been completed
            if (status.HintsShown) return;
            
            // Set 'standby' text if we're not attacking
            if (commander != AttackingCommander.Value)
            {
                var attackerName = Commanders[AttackingCommander.Value].Name;
                status.Cpl.SetRightTeletype($"{attackerName} is commanding\nStandby");
                return;
            }

            var closestGrid = status.ClosestGridToPointer;
            var hintText = status.ZoomLevel switch
            {
                1 or 2 => closestGrid && closestGrid.GridCommander == commander
                    ? "Zoom out to see opponents"
                    : "Aim and pull trigger",
                _ => "Zoom in to select target",
            };
            
            status.Cpl.SetRightTeletype(hintText);
        }
        
        private static string LayerNameForTeam(int team, TeamLayerVisibility visibility) => 
            $"Team {team + 1} {(visibility == TeamLayerVisibility.TeamOnly ? "Only" : "Excluded")}";
        
        public int LayerForTeam(Commander commander, bool visible)
        {
            var team = _teams[Commanders[commander].TeamColor];
            return visible ? team.TeamOnlyLayer : team.TeamExcludedLayer;
        }
        
        private void ClearDistancesToTarget()
        {
            AllControlPanels.ToList().ForEach(cpl => cpl.DistanceToTarget = -1);
        }
        
        private bool ProcessAiAttack(Commander commander)
        {
            // Get a vessel to attack with
            var fromGrid = Commanders[commander].Grid;

            // Get an attack vessel, or return with a failure if we couldn't select a vessel to attack with
            if (!fromGrid.TryGetRandomAttackVessel(commander, out var fireReservation)) return false;
            
            // Select the target grid as the strongest commander (that isn't itself)
            var commanders = CombatCommanders.Where(c => c.Key != commander).ToList();
            var strongestCommander = commanders.Aggregate(commanders.First(), (agg, next) =>
                next.Value.FleetStrength.Value > agg.Value.FleetStrength.Value ? next : agg);
            
            var targetGrid = strongestCommander.Value.Grid;

            // Select the target location (based on the highest probability value)
            var pdf = targetGrid.ComputeProbabilityDensityField();
            _lastPdfs[targetGrid] = pdf;
            
            var mostProbable = pdf.Aggregate(pdf.First(), (agg, next) => agg.Value > next.Value ? agg : next);
            var targetLocation = mostProbable.Key;
            
            // Highlight the attack position
            targetGrid.SetSelectorPosition(commander, targetLocation);
            
            var attackCoordinates = targetGrid.GetCellTarget(targetLocation);
            Attack(commander, targetGrid, fireReservation, targetLocation, attackCoordinates);

            return true;
        }
        
        private ViewParameters ComputedBlendedView(
            Commander commander, 
            float progress, 
            float currentGridAngle,
            float zoom)
        {
            return _followTargetViewStage switch
            {
                FollowViewStage.PanToAttackOrigin => ViewParameters.Lerp(
                    ComputeView(commander, currentGridAngle, zoom),
                    ComputeFollowView(
                        _followTarget, 
                        currentGridAngle + progress * vesselViewAngle, 
                        zoom),
                    progress),
                FollowViewStage.DwellOnAttackOrigin =>
                    ComputeFollowView(
                        _followTarget, 
                        currentGridAngle + vesselViewAngle,
                        zoom),
                FollowViewStage.Follow =>
                    ComputeFollowView(
                        _followTarget, 
                        currentGridAngle + (1 - progress) * vesselViewAngle, 
                        zoom),
                FollowViewStage.TargetGrid => ViewParameters.Lerp(
                    ComputeFollowView(_followTarget, currentGridAngle, zoom), 
                    ComputeView(commander, currentGridAngle, zoom), 
                    progress),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void MaybeShowLaunchWarning()
        {
            if (_launchWarningShown) return;
            _launchWarningShown = true;
            
            SoundManager.Instance.PlaySound(launchWarningSound, 1);

            SetStatusLights();
        }

        private void SetStatusLights()
        {
            foreach (var (commander, _) in Commanders) SetStatusLights(commander);
        }
        
        private void SetStatusLights(Commander commander)
        {
            if (!commander.IsLocalCommander()) return;
            
            ControlPanel.StatusLightMode mode;
            if (commander == AttackingCommander.Value)
            {
                mode = _commanderHasFired
                    ? ControlPanel.StatusLightMode.AttackTrack
                    : ControlPanel.StatusLightMode.AttackTarget;
            }
            else if (commander == _commanderUnderAttack && _launchWarningShown)
            {
                mode = ControlPanel.StatusLightMode.Alert;
            }
            else
            {
                mode = ControlPanel.StatusLightMode.Idle;
            }
            
            Commanders[commander].Cpl.CurrentStatusLightMode = mode;
        }
        
        private IEnumerator AttackViewCoroutine(Commander commander, LocalGrid toGrid,
            CombatVesselBase.FireReservation fireReservation, Vector2Int attackCell,
            Vector3 attackCoordinates)
        {
            _followTarget = fireReservation.FollowTarget;
            _followTargetStart = Time.time;
            
            if (commander.IsLocalCommander())
            {
                Commanders[commander].Cpl.SetLeftTeletype(ControlPanelFireOriginText(commander, fireReservation.FireOriginLabel));
                
                // Pan to the attack origin (for local players, not for AI players)
                _followTargetViewStage = FollowViewStage.PanToAttackOrigin;
                yield return new WaitForSeconds(panToAttackOriginDuration);
                
                // Dwell on the attack origin before firing
                _followTargetViewStage = FollowViewStage.DwellOnAttackOrigin;
                yield return new WaitForSeconds(dwellOnAttackOriginDuration);
            }
            
            // Fire at target
            _followTargetViewStage = FollowViewStage.Follow;
            var shotCount = _shotCounter;
            fireReservation.Fire(attackCoordinates,toGrid.IsCellOccupied(attackCell), () =>
            {
                // Some weapons may register multiple impacts.
                // Only count the first impact for any given attack.
                if (shotCount != _shotCounter++) return;
                
                _followTargetViewStage = FollowViewStage.TargetGrid;
                _followTargetImpactTime = Time.time;
                toGrid.HandleImpact(attackCell);
                CheckForVictory();
            });
            
            // Wait until we reach the target grid
            while (_followTargetViewStage != FollowViewStage.TargetGrid) yield return new WaitForSeconds(0.1f);
            
            // Hold on the impact for local players
            if (commander.IsLocalCommander())
            {
                yield return new WaitForSeconds(followHoldOnTargetGridDuration);                
            }

            _followTargetViewStage = null;
            _followTarget = null;
            
            Commanders[commander].FollowEnabled = false;

            if (commander.IsLocalCommander()) SetControlPanelText(commander);
            
            NextPlayer();
        }
        
        [CanBeNull]
        private TeamInfo GetVictor()
        {
            TeamInfo victor = null;
            foreach (var (_, value) in _teams)
            {
                if (value.PlayerCount == 0) continue; // Team is dead, continue searching
                if (victor != null) return null;      // More than one team still alive - no victors
                victor = value;                       // Store the alive team as the victor
            }

            return victor;
        }
        
        private void CheckForVictory()
        {
            // Get the victor
            var victor = GetVictor();
            if (victor == null) return;
            
            // Set the victory GameState
            CurrentGameState.Value = GameState.Victory;

            // Play the victory sound effect
            if (victorySound) SoundManager.Instance.PlaySound(victorySound, 1);
                
            // Set off island fireworks
            var islandFireworkPositions = new List<Vector3>();
            for (var i = 0; i < islandFireworkCount; i++)
                islandFireworkPositions.Add(VectorUtils.RandomVector3(-islandFireworkDistance, islandFireworkDistance));
            
            _islandFireworks.Spawn(victor.TeamColor, islandFireworkPositions, islandFireworkScale);
            
            // Reveal all grids
            AllGrids.ToList().ForEach(grid => grid.Reveal());
        }

        public enum CommanderMode
        {
            Observe,
            Combat
        }

        private void UpdateGlassesNames()
        {
            foreach (var (commander, info) in Commanders)
            {
                if (!commander.IsLocalCommander()) continue;
                
                var glassesName = GetGlassesName(commander.LocalPlayerIndex);

                if (glassesName == info.Name) continue;
                info.Name = glassesName;
                info.Cpl.SetCommanderName(info.Name);
                UpdateLobbyFleets();
            }
        }
        
        private static string GetGlassesName(PlayerIndex playerIndex)
        {
            var playerSettings = TiltFiveManager2.Instance.GetPlayerSettings(playerIndex);
            var commanderName = playerSettings.glassesSettings.friendlyName;
            if (commanderName == GlassesSettings.DEFAULT_FRIENDLY_NAME || commanderName == "") commanderName = "Unnamed";
            return commanderName;
        }

        private void UpdateFleetCounts()
        {
            var (humans, ais) = PlayerCountsForMode();
            AllControlPanels.ToList().ForEach(cpl => cpl.SetFleetCounts(humans, ais));
        }

        private (int humans, int ais) PlayerCountsForMode() => PlayerCountsForMode(_currentGameMode);
        
        private (int humans, int ais) PlayerCountsForMode(Vector2Int gameMode)
        {
            var mode = GameModes[gameMode];
            var humans = CombatCommanders.Count(e => !e.Key.IsAiCommander());
            var ais = mode.Players - humans;

            return (humans, ais);
        }

        private List<VesselTypeAndStartingPosition> FleetTemplateForMode() => FleetTemplateForMode(_currentGameMode);
        private static List<VesselTypeAndStartingPosition> FleetTemplateForMode(Vector2Int mode) => GameModes[mode].FleetTemplate;
        
        public void SetGameMode(int mode)
        {
            _currentGameMode.x = mode;
            UpdateGameMode();
        }

        public void SetGameSubMode(int subMode)
        {
            _currentGameMode.y = subMode;
            UpdateGameMode();
        }

        private void UpdateGameMode()
        {
            UpdateFleetCounts();
            UpdateLobbyFleets();
        }

        public void SelfDestruct(Commander commander)
        {
            Commanders[commander].Grid.SelfDestruct();
            CheckForVictory();
            if (commander == AttackingCommander.Value && !_commanderHasFired) NextPlayer();
        } 
        
        public static void SetTimeMultiplier(float timeMultiplier) => Time.timeScale = timeMultiplier;

        public void SetPlacementComplete(Commander commander, bool complete)
        {
            Commanders[commander].PlacementComplete = complete;
            
            // For local commanders, also set the launch switch enablement and hints
            if (!commander.IsLocalCommander()) return;
            Commanders[commander].Cpl.SetLaunchEnabled(complete);
            SetPlaceHintTeletype(commander);
        }
        
        public void MarkCommanderReady(Commander commander, bool state)
        {
            Commanders[commander].Playing.Value = state;
            CheckLaunchSwitches();
        }

        public static readonly List<Color> CommanderColors = new List<Color>
        {
            new(1.0f, 0.0f, 0.0f),
            new(0.93f, 0.34f, 0.12f),
            new(1.0f, 1.0f, 0.0f),
            new(0.0f, 1.0f, 0.0f),
            new(0.0f, 1.0f, 1.0f),
            new(0.0f, 0.0f, 1.0f),
            new(1.0f, 0.0f, 1.0f),
            new(0.0f, 0.0f, 0.0f),
            new(0.5f, 0.5f, 0.5f),
            new(1.0f, 1.0f, 1.0f)
        };

        private struct FleetNames
        {
            public string Prefix;
            public string Faction;
            public Queue<string> VesselNames;
        }
        
        private static readonly List<FleetNames> NavalVesselNames = new List<FleetNames>
        {
            new()
            {
                Prefix = "FSS", // Federation Sea Ship
                Faction = "Federation",
                VesselNames = new Queue<string>(new [] {
                    "Intrepid", "Starlight", "Valkyrie", "Orpheus", "Prometheus",
                    "Sovereign", "Galactus", "Poseidon", "Olympus", "Nova",
                    "Infinity", "Nebula", "Chronos", "Quasar", "Pegasus",
                    "Atlas", "Titan", "Voyager", "Aether", "Sirius"
                })
            },
            
            new()
            {
                Prefix = "GDN", // Galactic Dominion Navy
                Faction = "Dominion",
                VesselNames = new Queue<string>(new [] {
                    "Celestial", "Nebulous", "Zodiac", "Solstice", "Quantum",
                    "Orion", "Eclipse", "Gemini", "Polaris", "Horizon",
                    "Andromeda", "Specter", "Enigma", "Draco", "Exodus",
                    "Pulsar", "Aurora", "Maelstrom", "Arcane", "Mystic"
                })
            },
            
            new()
            {
                Prefix = "QES", // Quantum Empire Ship
                Faction = "Empire",
                VesselNames = new Queue<string>(new [] {
                    "Quark", "Electron", "Neutron", "Boson", "Lepton",
                    "Photon", "Fermion", "Proton", "Higgs", "Gluon",
                    "Graviton", "Scalar", "Hadron", "Meson", "Baryon",
                    "Axion", "Wimp", "Chiral", "Vortex", "Zephyr"
                })
            },

            new()
            {
                Prefix = "DRN", // Digital Republic Navy
                Faction = "Republic",
                VesselNames = new Queue<string>(new [] {
                    "Cyberspace", "Firewall", "Kernel", "Byte", "Nexus",
                    "QuantumBit", "Protocol", "Network", "Cipher", "Pixel",
                    "Dataflow", "Algorithm", "Cache", "Socket", "Switch",
                    "Gateway", "Port", "Stack", "Thread", "Vector"
                })
            }
        };
        
        private static IEnumerable<string> RandomizeVesselNames(FleetNames fleet)
        {
            var rng = new System.Random();
            return fleet.VesselNames.OrderBy(_ => rng.Next()).Select(e => $"{fleet.Prefix} {e}");
        }
        
        private static IEnumerable<FleetNames> RandomizeFleets()
        {
            var rng = new System.Random();
            return NavalVesselNames.OrderBy(_ => rng.Next()).ToList();
        }

        private static readonly List<VesselTypeAndStartingPosition> StandardFleet = new List<VesselTypeAndStartingPosition>
        {
            new(){Type = CombatVesselType.Destroyer, StartingPosition = new Vector2Int(6, 8)},
            new(){Type = CombatVesselType.Battleship, StartingPosition = new Vector2Int(5, -1)},
            new(){Type = CombatVesselType.AircraftCarrier, StartingPosition = new Vector2Int(1, -1)},
            new(){Type = CombatVesselType.Submarine, StartingPosition = new Vector2Int(3, 8)},
            new(){Type = CombatVesselType.LittoralCombatShip, StartingPosition = new Vector2Int(0, 8)}
        };
        
        private static readonly List<VesselTypeAndStartingPosition> LoneWolfFleet = new List<VesselTypeAndStartingPosition>
        {
            new(){Type = CombatVesselType.Submarine, StartingPosition = new Vector2Int(3, 3)}
        };

        private static readonly Dictionary<Vector2Int, GameMode> GameModes = new Dictionary<Vector2Int, GameMode>
        {
            { new Vector2Int(0, 0), new GameMode {Players = 2, FleetTemplate = StandardFleet} },
            { new Vector2Int(0, 1), new GameMode {Players = 3, FleetTemplate = StandardFleet} },
            { new Vector2Int(0, 2), new GameMode {Players = 4, FleetTemplate = StandardFleet} },
            
            { new Vector2Int(1, 0), new GameMode {Players = 2, FleetTemplate = LoneWolfFleet} },
            { new Vector2Int(1, 1), new GameMode {Players = 3, FleetTemplate = LoneWolfFleet} },
            { new Vector2Int(1, 2), new GameMode {Players = 4, FleetTemplate = LoneWolfFleet} }
        };

        public void SetCommanderColor(Commander commander, int index)
        {
            Commanders[commander].TintColorIndex = (10 - index) % 10;
            UpdateLobbyFleets();
        }
                
        public void SetCommanderMode(Commander commander, CommanderMode mode)
        {
            Commanders[commander].Mode = mode;
            UpdateFleetCounts();
            UpdateLobbyFleets();
        }

        public void EliminateCommander(Commander commander)
        {
            var status = Commanders[commander];
            status.Playing.Value = false;
            _teams[status.TeamColor].PlayerCount--;
        }
    }
    
    public struct ViewParameters
    {
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly Vector3 Position;

        public ViewParameters(Quaternion rotation, Vector3 scale, Vector3 position)
        {
            Rotation = rotation;
            Scale = scale;
            Position = position;
        }

        public static ViewParameters Lerp(ViewParameters a, ViewParameters b, float t) =>
            new(
                Quaternion.Slerp(a.Rotation, b.Rotation, t), 
                Vector3.Lerp(a.Scale, b.Scale, t), 
                Vector3.Lerp(a.Position, b.Position, t)
            );
    }

    public enum FollowViewStage
    {
        PanToAttackOrigin,
        DwellOnAttackOrigin,
        Follow,
        TargetGrid
    }
    
    public class CommanderStatus
    {
        // Common
        public readonly NotifyingVariable<bool> Playing = new(false);
        public readonly NotifyingVariable<int> FleetStrength = new(0);
        public bool PlacementComplete;
        public string Name = "";
        public SeaWar.CommanderMode Mode = SeaWar.CommanderMode.Combat;
        public LocalGrid Grid;
        public int TintColorIndex;
        public Color TeamColor => SeaWar.CommanderColors[TintColorIndex];

        public string TeamName;

        // AI Related
        public float AiNextActionTime;
        
        // Local Human Related [Objects]
        public ControlPanel Cpl;
        
        // Local Human Related [View]
        public LocalGrid ViewGrid;
        public Vector3 ViewCellOffset = Vector3.zero;
        public int ZoomLevel;
        public float ZoomProgress;
        public bool Following;
        public bool FollowEnabled;
        public SnappingAngleSlewController SlewController;
        public ViewParameters CplInitialView;
        public bool HintsShown;
        
        // Local Human Related [Wand]
        public Vector2Int StickPosition;
        public bool TriggerDown;
        
        public Transform GameboardTransform;
        public LocalGrid ClosestGridToPointer;
        public CombatVesselBase ClosestVesselToPointer;
    }

    public class GameMode
    {
        public int Players;
        public List<VesselTypeAndStartingPosition> FleetTemplate;
    }

    public struct VesselTypeAndStartingPosition
    {
        public CombatVesselType Type;
        public Vector2Int StartingPosition;
    }
    
    public enum CombatVesselType
    {
        LittoralCombatShip,
        Destroyer,
        AircraftCarrier,
        Battleship,
        Submarine
    }
    
    public static class DirectionUtils
    {
        public static CombatVesselBase.CardinalDirection PrevDirection(this CombatVesselBase.CardinalDirection dir) => dir switch
        {
            CombatVesselBase.CardinalDirection.North => CombatVesselBase.CardinalDirection.West,
            CombatVesselBase.CardinalDirection.West => CombatVesselBase.CardinalDirection.South,
            CombatVesselBase.CardinalDirection.South => CombatVesselBase.CardinalDirection.East,
            CombatVesselBase.CardinalDirection.East => CombatVesselBase.CardinalDirection.North,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
        
        public static CombatVesselBase.CardinalDirection NextDirection(this CombatVesselBase.CardinalDirection dir) => dir switch
        {
            CombatVesselBase.CardinalDirection.North => CombatVesselBase.CardinalDirection.East,
            CombatVesselBase.CardinalDirection.East => CombatVesselBase.CardinalDirection.South,
            CombatVesselBase.CardinalDirection.South => CombatVesselBase.CardinalDirection.West,
            CombatVesselBase.CardinalDirection.West => CombatVesselBase.CardinalDirection.North,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }
}
