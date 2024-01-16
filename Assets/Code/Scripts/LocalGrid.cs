using System;
using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace Code.Scripts
{
    public class LocalGrid : MonoBehaviour
    {
        public VisualEffect obscuredTileTemplate;
        public VisualEffect onFireTileTemplate;
        
        public Material gridMaterialTemplate;
        public Material gridSelectorMaterialTemplate;
        public Material lowPolyMaterial;
        
        public float gridPitch;
        
        public int width;
        public int height;
        
        public Vector3 gridPlaneHighOffset = new(0, 35f, 0);
        public Vector3 gridPlaneLowOffset = new(0, 2.21f, 0);
        
        public Vector3 lowPolyPlaneOffset = new(0, 2.2f, 0);
        public Vector3 lowPolyPlaneScale = new(100, 1, 100);
        public float selectorHeightFactor = 0.5f;
        
        public SeaWar gameController;
        
        public GameObject gridSelectCube;
        public float gridHighlightHeight = 100;

        public float gridAngle;
        
        private GameObject _plane;
        private GameObject _lowPolyPlane;
        private GameObject _placementCube;
        private Renderer _renderer;
        private Material _gridMaterial;

        private readonly Dictionary<Vector2Int, CombatVesselBase> _occupiedGrid = new();
        private readonly Dictionary<Vector2Int, CellState> _cellState = new();
        private readonly Dictionary<CombatVesselBase, List<Vector2Int>> _gridVessels = new();
        private readonly List<CombatVesselBase> _attackVessels = new();
        private readonly Dictionary<Commander, GameObject> _selectors = new();
        private readonly Dictionary<Commander, Vector3> _wandPositions = new();
        
        private static readonly int TexScaleOffset = Shader.PropertyToID("_UnlitColorMap_ST");

        private Vector3 _gridScale;
        
        private int _width;
        private int _height;
        private static readonly int UnlitColor = Shader.PropertyToID("_UnlitColor");
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");

        private int _attackVesselIndex;
        private Renderer _placementCubeRenderer;
        public Commander GridCommander;
        public Dictionary<CombatVesselBase, VesselState> CombatVessels = new();
        private readonly Dictionary<Vector2Int, VisualEffect> _fog = new();
        private SeaWar.GameState _currentGameState;
        
        private GameObject _attackSourceIndicator;
        private Renderer _gridHighlightRenderer;
        private Commander _attackingCommander;
        private const float FOG_DISSIPATION_DELAY = 10f;
        private Fireworks _fireworks;

        private void CreateLowPolyPlane()
        {
            _lowPolyPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _lowPolyPlane.name = "LocalGrid Low Poly Plane";
            _lowPolyPlane.transform.SetParent(transform, false);
            _lowPolyPlane.transform.localPosition = lowPolyPlaneOffset;
            _lowPolyPlane.transform.localScale = lowPolyPlaneScale;
            Destroy(_lowPolyPlane.GetComponent<Collider>());
            
            var lowPolyRenderer = _lowPolyPlane.GetComponent<Renderer>();
            lowPolyRenderer.material = lowPolyMaterial;
            lowPolyRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }
                
        private void CreateHighlightCube()
        {
            _placementCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _placementCube.name = "LocalGrid Placement Cube";
            _placementCube.transform.SetParent(transform, false);
            Destroy(_placementCube.GetComponent<Collider>());
            
            _placementCubeRenderer = _placementCube.GetComponent<Renderer>();
            _placementCubeRenderer.material = Instantiate(gridSelectorMaterialTemplate);
            _placementCubeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void SetPlacementCube(Vector2Int gridPosition, Vector2Int size, Color color)
        {
            _placementCube.SetActive(true);
            
            Vector2 floatGridPosition = gridPosition;
            if (size.x % 2 == 0) floatGridPosition.x += size.x > 0 ? -0.5f : 0.5f;
            if (size.y % 2 == 0) floatGridPosition.y += size.y > 0 ? -0.5f : 0.5f;
            
            var position = GetGridCenter(floatGridPosition, 0.1f);
            _placementCube.transform.localPosition = position;
            _placementCube.transform.localScale = new Vector3(size.x * gridPitch, 0.1f * gridPitch, size.y * gridPitch);
            
            _placementCubeRenderer.material.SetColor(UnlitColor, color);
            _placementCubeRenderer.material.SetColor(EmissiveColor, color);            
        }
        
        private void ClearPlacementCube()
        {
            _placementCube.SetActive(false);
        }
        
        private void CreatePlane()
        {
            if (_plane)
            {
                Debug.LogError("LocalGrid plane already created!");
                return;
            }
            
            if (width % 2 != 0 || height % 2 != 0)
            {
                Debug.LogError("LocalGrid width and height must be a multiple of 2!");
                return;
            }
            
            // Create a plane primitive at the world origin
            _plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _plane.name = "LocalGrid Plane";
            _plane.transform.SetParent(transform, false);
            _plane.transform.localPosition = gridPlaneLowOffset;
            Destroy(_plane.GetComponent<Collider>());
            
            _renderer = _plane.GetComponent<Renderer>();
            _renderer.material = Instantiate(gridMaterialTemplate);
            _renderer.shadowCastingMode = ShadowCastingMode.Off;

            _width = width / 2;
            _height = height / 2;

            CreateLowPolyPlane();
            CreateHighlightCube();
            CreateAttackSourceIndicator();

            // Add the fireworks
            _fireworks = gameObject.AddComponent<Fireworks>();
            _fireworks.fireworkTemplate = gameController.fireworkTemplate;
        }

        private void CreateAttackSourceIndicator()
        {
            _attackSourceIndicator = Instantiate(gridSelectCube, transform, false);
            _attackSourceIndicator.name = "Attack source indicator";
            _attackSourceIndicator.transform.localPosition = gridPlaneLowOffset;
            
            var selectorRenderer = _attackSourceIndicator.GetComponent<Renderer>();
            selectorRenderer.material = Instantiate(selectorRenderer.material);
            
            var color = gameController.Commanders[GridCommander].TeamColor;
            selectorRenderer.material.SetColor(UnlitColor, color);
            selectorRenderer.material.SetColor(EmissiveColor, color);
            
            _attackSourceIndicator.SetActive(false);
        }

        private void CreatePlayerSelector(Commander commander)
        {
            Renderer selectorRenderer;
            
            if (!_selectors.TryGetValue(commander, out var selector))
            {
                // Create a plane primitive at the world origin
                selector = Instantiate(gridSelectCube, transform, false);
                selector.name = $"Grid Selector ({commander}";
                selector.transform.localPosition = Vector3.zero;
                selector.transform.localEulerAngles = new Vector3(180, 0, 0);
                Destroy(selector.GetComponent<Collider>());
                
                _selectors[commander] = selector;
                
                selectorRenderer = selector.GetComponent<Renderer>();
                selectorRenderer.material = Instantiate(selectorRenderer.material);
                selectorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                selectorRenderer = selector.GetComponent<Renderer>();
            }
            
            // Set the colors
            var color = gameController.Commanders[commander].TeamColor;
            selectorRenderer.material.SetColor(UnlitColor, color);
            selectorRenderer.material.SetColor(EmissiveColor, color);
            
            selector.SetActive(false);
        }

        public void UpdateGridPosition(float progress)
        {
            if (_plane) _plane.transform.localPosition = Vector3.Lerp(gridPlaneLowOffset, gridPlaneHighOffset, progress);
        }
        
        private void UpdateGridSize()
        {
            _plane.transform.localScale = _gridScale;
            
            var scaleX = _gridScale.x / gridPitch;
            var scaleY = _gridScale.z / gridPitch;
            
            var offsetX = -0.5f * _gridScale.x % gridPitch / gridPitch;
            var offsetY = -0.5f * _gridScale.z % gridPitch / gridPitch;
            
            var tilingAndOffset = new Vector4(scaleX, scaleY, offsetX, offsetY);
            _renderer.material.SetVector(TexScaleOffset, tilingAndOffset);
            
            // Update the grid highlight cube
            var gridHighlightCubeScale = _gridScale;
            gridHighlightCubeScale.y = gridHighlightHeight;
            var yPosition = gridPlaneLowOffset.y + gridHighlightHeight / 2;
            
            _attackSourceIndicator.transform.localScale = gridHighlightCubeScale * 1.1f;
            _attackSourceIndicator.transform.localPosition = new Vector3(0, yPosition, 0);
            
        }

        private void UpdateSelectorsSize()
        {
            var scale = new Vector3(gridPitch, gridPitch * selectorHeightFactor, gridPitch);
            foreach (var selector in _selectors.Values) selector.transform.localScale = scale;
        }
        
        public Vector2Int GetGridForPosition(Vector3 position)
        {
            if (!_plane) return new Vector2Int(int.MaxValue, int.MaxValue);
            var localPoint = _plane.transform.InverseTransformPoint(position);
            
            return new Vector2Int
            {
                x = Mathf.RoundToInt((localPoint.x + 0.5f) * width - 0.5f),
                y = Mathf.RoundToInt((localPoint.z + 0.5f) * height - 0.5f)
            };
        }

        public Vector3 GetGridCenter(Vector2 gridPosition, float heightFraction)
        {
            return new Vector3(
                (gridPosition.x - (_width - 0.5f)) * gridPitch,
                gridPitch * heightFraction,
                (gridPosition.y - (_height - 0.5f)) * gridPitch  
            );
        }

        public Vector3 GetGridCenterWorld(Vector2 gridPosition, float heightFraction)
        {
            var target = GetGridCenter(gridPosition, heightFraction); 
            return transform.TransformPoint(target);
        }
        
        private void Start()
        {
            if (!_plane) CreatePlane();
            
            _gridScale = new Vector3(_width * 2 * gridPitch, 0, _height * 2 * gridPitch);
            
            gameController.CurrentGameState.GetAndSubscribe(OnGameStateChanged, NotifyingVariableBehaviour.ResendLast);
            gameController.AttackingCommander.GetAndSubscribe(OnCommanderChanged, NotifyingVariableBehaviour.ResendLast);
            
            UpdateGridSize();
            UpdateSelectorsSize();
            
            FillWithFog();
            ApplyVesselPositions();
        }

        private void ApplyVesselPositions()
        {
            foreach (var (vessel, _) in CombatVessels)
            {
                vessel.SetGridPosition(vessel.GetGridPosition(), 0);
            }
        }

        private void OnDestroy()
        {
            gameController.CurrentGameState.Unsubscribe(OnGameStateChanged);
            gameController.AttackingCommander.Unsubscribe(OnCommanderChanged);
        }

        private VisualEffect CreateFog(Vector2Int position)
        {
            var fx = Instantiate(obscuredTileTemplate, transform);
            fx.name = $"Obscured Tile ({position.x}\u00d7{position.y})";
            fx.gameObject.layer = gameController.LayerForTeam(GridCommander, false);
            fx.transform.localPosition = GetGridCenter(position, 0.5f);
            return fx;
        }
        
        private VisualEffect CreateFireFog(Vector2Int position)
        {
            var fx = Instantiate(onFireTileTemplate, transform);
            fx.name = $"Burning Tile ({position.x}\u00d7{position.y})";
            fx.gameObject.layer = gameController.LayerForTeam(GridCommander, false);
            fx.transform.localPosition = GetGridCenter(position, 0.5f);
            return fx;
        }
        
        private void FillWithFog()
        {
            _cellState.Clear();
            
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
            {
                for (position.y = 0; position.y < height; position.y++)
                {
                    _fog[position] = CreateFog(position);
                    _cellState[position] = CellState.Hidden;
                }                
            }
        }

        private void OnDrawGizmosSelected()
        {
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
            {
                for (position.y = 0; position.y < height; position.y++)
                {
                    Gizmos.color = _cellState[position] switch
                    {
                        CellState.Hidden => Color.green,
                        CellState.OnFire => Color.red,
                        CellState.Revealed => Color.cyan,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    
                    var pos = GetGridCenterWorld(position, 0);
                    Gizmos.DrawWireSphere(pos, 20);
                }                
            }
        }

        public void Reveal()
        {
            ClearFog();
            
            // Add all cells to the revealed cell list
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
                for (position.y = 0; position.y < height; position.y++)
                    _cellState[position] = CellState.Revealed;
            
            // Reveal all unrevealed vessels
            _attackVessels.ForEach(vessel => vessel.SetLayer(0));
        }
        
        private void ClearFog()
        {
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
                for (position.y = 0; position.y < height; position.y++)
                    ClearFog(position);
        }
        
        private void ClearFog(Vector2Int position)
        {
            if (!_fog[position]) return;
            _fog[position].SetFloat("Dissipate", -5);
            _fog[position].Stop();
            Destroy(_fog[position], FOG_DISSIPATION_DELAY);
            _cellState[position] = CellState.Revealed;
        }
        
        private void SetFogOnFire(Vector2Int position)
        {
            ClearFog(position);
            _fog[position] = CreateFireFog(position);
            _cellState[position] = CellState.OnFire;
        }
        
        public void CreatePlayerSelectors(List<Commander> commanders) =>
            commanders.ForEach(CreatePlayerSelector);

        private bool IsInGrid(Vector2Int gridPosition) => 
            gridPosition is { x: >= 0, y: >= 0 } && gridPosition.x < width && gridPosition.y < height;
        
        public void HandlePointer(Commander commander, Vector3 position)
        {
            _wandPositions[commander] = position;

            if (!_selectors.TryGetValue(commander, out var selector)) return;
            
            var gridPosition = GetGridForPosition(position);
            var isInGrid = IsInGrid(gridPosition);
            var ownGrid = commander == GridCommander;
            
            // Show the selector if the pointer is inside the grid, otherwise hide it
            if (selector.activeSelf != isInGrid && !ownGrid) selector.SetActive(isInGrid);
            
            // Only position the selector if we're inside the grid
            if (!isInGrid) return;

            SetSelectorPosition(commander, gridPosition);
        }

        public void SetSelectorPosition(Commander commander, Vector2Int gridPosition)
        {
            var selectorPosition = GetGridCenter(gridPosition, 0.5f + selectorHeightFactor / 2);
            _selectors[commander].transform.localPosition = selectorPosition;
        }
        
        private void OccupyGrid(CombatVesselBase vessel)
        {
            var cells = vessel.GetOccupiedCells();

            foreach (var cell in cells) _occupiedGrid[cell] = vessel;
            _gridVessels[vessel] = cells;
        }
        
        public bool PlaceVessel(CombatVesselBase vessel)
        {
            var isValid = ValidateVesselPlacement(vessel);
            if (isValid)
            {
                OccupyGrid(vessel);
                CombatVessels[vessel].Placed = true;
                _attackVessels.Add(vessel);
                gameController.Commanders[GridCommander].FleetStrength.Value = FleetStrength();
                if (PlacementComplete()) gameController.SetPlacementComplete(GridCommander, true);
            }
            
            ClearPlacementCube();
            return isValid;
        }

        public void TakeVessel(CombatVesselBase vessel)
        {
            // Remove the vessel from the grid if it's already present there
            if (!_gridVessels.TryGetValue(vessel, out var gridList)) return;
            foreach (var vector2Int in gridList) _occupiedGrid.Remove(vector2Int);
            _gridVessels.Remove(vessel);
            CombatVessels[vessel].Placed = false;
            
            gameController.Commanders[GridCommander].FleetStrength.Value = FleetStrength();
            _attackVessels.Remove(vessel);

            // Mark placement as incomplete while moving vessels
            gameController.SetPlacementComplete(GridCommander, false);
        }

        public void HandleTriggerPull(Commander commander)
        {
            if (_currentGameState != SeaWar.GameState.Playing) return;  // Only fire in 'playing' mode
            if (_attackingCommander != commander) return;               // Only fire when it's our turn

            // Don't fire on grids with the same color (team)
            var attackingColor = gameController.Commanders[_attackingCommander].TeamColor;
            var localGridColor = gameController.Commanders[GridCommander].TeamColor;
            if (attackingColor == localGridColor) return;
            
            // Don't fire if the target is outside the grid
            var gridPosition = GetGridForPosition(_wandPositions[commander]);
            if (!IsInGrid(gridPosition)) return;

            // Don't fire if the cell has already been revealed
            if (_cellState[gridPosition] != CellState.Hidden) return;
            
            if (!Attack(commander, gridPosition))
            {
                Debug.LogError($"Failed to attack : {commander}");
            }
        }

        public bool TryGetRandomAttackVessel(Commander commander, out CombatVesselBase.FireReservation fireReservation)
        {
            fireReservation = new CombatVesselBase.FireReservation();
            
            var commanderGrid = gameController.Commanders[commander].Grid;
            var initialLayer = gameController.LayerForTeam(commander, true);
            
            // Can't attack if the fleet has been destroyed
            if (commanderGrid._attackVessels.Count == 0) return false;

            var startingIndex = commanderGrid._attackVesselIndex;
            for (;;)
            {
                // Increment and get the next available attack vessel
                commanderGrid._attackVesselIndex =
                    (commanderGrid._attackVesselIndex + 1) % commanderGrid._attackVessels.Count;
                
                var vessel = commanderGrid._attackVessels[commanderGrid._attackVesselIndex];
                if (vessel.PrepareToFire(initialLayer, out fireReservation)) return true;
                
                // Abort if we loop through all vessels looking for a target
                if (commanderGrid._attackVesselIndex == startingIndex) return false;
            }
        }
        
        private bool Attack(Commander commander, Vector2Int attackCell)
        {
            if (!TryGetRandomAttackVessel(commander, out var fireReservation)) return false;
            
            var attackCoordinates = GetCellTarget(attackCell);
            gameController.Attack(commander, this, fireReservation, attackCell, attackCoordinates);
            return true;
        }

        // If the cell is occupied, get a position within the cell that will hit
        // the vessel (as declared by the vessel), otherwise return the center of the cell
        public Vector3 GetCellTarget(Vector2Int attackCell)
        {
            var center = GetGridCenterWorld(attackCell, 0);
            return center;
            
            //TODO (khunt): Use occupied position instead
            return _occupiedGrid.TryGetValue(attackCell, out var vessel) ? 
                center + vessel.GetTargetInCell(attackCell) : 
                center;
        }

        public bool IsCellOccupied(Vector2Int attackCell) =>
            _occupiedGrid.TryGetValue(attackCell, out _);
        
        public void HandleImpact(Vector2Int attackCell)
        {
            if (!_occupiedGrid.TryGetValue(attackCell, out var vessel))
            {
                ClearFog(attackCell);
                return;
            }

            // Skip the rest if the target cell is already on fire
            if (vessel.IsOnFire(attackCell)) return;
            
            SetFogOnFire(attackCell);
            vessel.SetOnFire(attackCell, true);
            if (--gameController.Commanders[GridCommander].FleetStrength.Value == 0) HandleElimination();

            // If we've hit a ship, check if we've also destroyed it
            if (!vessel.IsDestroyed()) return;
            var vesselCells = vessel.GetOccupiedCells();
            foreach (var vesselCell in vesselCells)
            {
                ClearFog(vesselCell);
            }
            vessel.SetLayer(0);
            
            _attackVessels.Remove(vessel);
        }

        private void HandleElimination()
        {
            gameController.EliminateCommander(GridCommander);
            ClearFog();
        }
        
        private bool CellInGrid(Vector2Int gridPosition) =>
            gridPosition is { x: >= 0, y: >= 0 } && gridPosition.x < width && gridPosition.y < height;

        private bool ValidateVesselPlacement(CombatVesselBase vessel) => 
            ValidateVesselPlacement(vessel, vessel.GetGridPosition(), vessel.GetDirection());
        
        private bool ValidateVesselPlacement(
            CombatVesselBase vessel, 
            Vector2Int position,
            CombatVesselBase.CardinalDirection direction) => 
            ValidateVesselPlacement(position, CombatVesselBase.GetSize(vessel.GetLength(), direction));
        
        private bool ValidateVesselPlacement(Vector2Int position, Vector2Int size)
        {
            var cells = CombatVesselBase.GetOccupiedCells(position, size);
            return cells.All(cell => CellInGrid(cell) && !_occupiedGrid.ContainsKey(cell));
        }

        private bool CanHideVessel(Vector2Int position, Vector2Int size, out List<Vector2Int> cells)
        {
            cells = CombatVesselBase.GetOccupiedCells(position, size);
            return cells.All(cell => CellInGrid(cell) && _cellState[cell] != CellState.Revealed);
        }
        
        public bool HighlightPlacement(CombatVesselBase vessel, Vector2Int gridPosition)
        {
            var isValid = ValidateVesselPlacement(vessel);
            var size = vessel.GetSize();
            
            SetPlacementCube(gridPosition, size, isValid ? Color.green : Color.red);
            
            return isValid;
        }

        public void RotateMovingVessel(int stickDirection)
        {
            var movingVessels = CombatVessels
                .Where(vessel => vessel.Key.IsMoving())
                .Select(vessel => vessel.Key);
            foreach (var vessel in movingVessels)
            {
                vessel.SetDirection(stickDirection switch
                {
                    > 0 => vessel.GetDirection().NextDirection(),
                    < 0 => vessel.GetDirection().PrevDirection(),
                    _ => vessel.GetDirection()
                });
            }
        }

        public IEnumerable<Actuator.IActuatorMovable> GetMovables() =>
            CombatVessels.Select(vessel => vessel.Key);

        private bool PlacementComplete() =>
            CombatVessels.All(e => e.Value.Placed);

        private int FleetStrength() =>
            CombatVessels.Sum(e => e.Value.Placed ? e.Key.GetLength() : 0);
        
        public void NotifyTransitionComplete()
        {
            foreach (var vessel in CombatVessels.Keys) vessel.Enable();
            
        }

        private void OnGameStateChanged(SeaWar.GameState newGameState)
        {
            if (_currentGameState == newGameState) return;
            
            switch (newGameState)
            {
                case SeaWar.GameState.Lobby:
                    _fireworks.Clear();
                    break;
                
                case SeaWar.GameState.Placing:
                    _fireworks.Clear();
                    break;
                
                case SeaWar.GameState.Playing:
                    _fireworks.Clear();
                    break;
                
                case SeaWar.GameState.Victory:
                    MaybeShowVictory();
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(newGameState), newGameState, null);
            }
            
            _currentGameState = newGameState;
        }

        private void OnCommanderChanged(Commander commander)
        {
            _attackingCommander = commander;
            _attackSourceIndicator.SetActive(commander == GridCommander);
        }

        private void MaybeShowVictory()
        {
            // Only show victory if the commander is still alive during victory
            var status = gameController.Commanders[GridCommander];
            if (!status.Playing.Value) return;

            // Spawn fireworks
            _fireworks.Spawn(status.TeamColor, _attackVessels.Select(vessel => vessel.transform.position));
            
            // Reveal any un-destroyed vessels
            _attackVessels.ForEach(vessel => vessel.SetLayer(0));
        }

        public void SelfDestruct() =>
            CombatVessels
                .SelectMany(e => e.Key.GetOccupiedCells())
                .ToList()
                .ForEach(HandleImpact);

        public Dictionary<Vector2Int, int> ComputeProbabilityDensityField()
        {
            // Clear the pdf
            var pdf = new Dictionary<Vector2Int, int>();
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
                for (position.y = 0; position.y < height; position.y++)
                    pdf[position] = 0;
            
            // If no cells are on fire, compute the PDF for all remaining combat vessels
            foreach (var vessel in _attackVessels) AddToPdf(ref pdf, vessel.GetLength());

            // If any cells are on fire, boost cells that are adjacent to the burning cells
            var burningAdjacent = GetBurningAdjacentCells().ToList();
            burningAdjacent.ToList().ForEach(c => pdf[c] *= 20);

            // For any cells that have already been hit, zero the PDF
            _cellState
                .Where(c => c.Value != CellState.Hidden)
                .ToList()
                .ForEach(c => pdf[c.Key] = 0); 
                
            return pdf;
        }

        private IEnumerable<Vector2Int> GetBurningAdjacentCells()
        {
            // Get all burning cells
            return _cellState
                .Where(e => e.Value == CellState.OnFire)
                .SelectMany(e => GetAdjacentCells(e.Key));
        }

        private bool CellInBounds(Vector2Int cell) =>
            cell is { x: >= 0, y: >= 0 } && cell.x < width && cell.y < height;
        
        private IEnumerable<Vector2Int> GetAdjacentCells(Vector2Int center) =>
            center.AdjacentCells().Where(CellInBounds);
        
        private void AddToPdf(
            ref Dictionary<Vector2Int, int> pdf, 
            int vesselLength)
        {
            var position = Vector2Int.zero;
            for (position.x = 0; position.x < width; position.x++)
            {
                for (position.y = 0; position.y < height; position.y++)
                {
                    var directions = new List<CombatVesselBase.CardinalDirection>
                    {
                        CombatVesselBase.CardinalDirection.North,
                        CombatVesselBase.CardinalDirection.East,
                        CombatVesselBase.CardinalDirection.South,
                        CombatVesselBase.CardinalDirection.West
                    };

                    foreach (var size in directions.Select(cardinalDirection => CombatVesselBase.GetSize(vesselLength, cardinalDirection)))
                    {
                        if (!CanHideVessel(position, size, out var cells)) continue;
                        foreach (var cell in cells) pdf[cell]++;
                    }
                }                
            }
        }
    }

    public enum CellState
    {
        Hidden,
        OnFire,
        Revealed
    }
    
    public class VesselState
    {
        public bool Placed;
    }
}
