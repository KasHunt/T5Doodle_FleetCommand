using System;
using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Utils;
using UnityEngine;
using UnityEngine.VFX;

namespace Code.Scripts
{
    public abstract class CombatVesselBase : MonoBehaviour, Actuator.IActuatorMovable
    {
        public class FireReservation
        {
            public readonly string FireOriginLabel;
            public readonly SeaWar.IGameboardFollowTarget FollowTarget;
            private readonly FireFunction _fireAtTarget;
            
            public FireReservation(){}

            protected FireReservation(string fireOriginLabel, FireFunction fireFunction, SeaWar.IGameboardFollowTarget followTarget)
            {
                FireOriginLabel = fireOriginLabel;
                _fireAtTarget = fireFunction;
                FollowTarget = followTarget;
            }

            public void Fire(Vector3 target, bool targetIsVessel, Action onImpactCallback) =>
                _fireAtTarget(this, target, targetIsVessel, onImpactCallback);
            
            public delegate void FireFunction(FireReservation reservation, Vector3 target,
                bool targetIsVessel, Action onImpactCallback);
        }
        
        public enum Axis
        {
            X,
            Y,
            Z
        }
        
        public enum CardinalDirection
        {
            North,
            East,
            South,
            West
        }

        public bool passive;
        
        public VisualEffect fireTemplate;
        public float fireScale = 1;
        public Vector3 fireJitter = new(5, 0, 5);

        public float positionResetDuration = 1;
        public float positionResetApex = 100;
        
        public float capsizeDuration = 4;
        public float capsizeAngle = 45;
        public float capsizeDepth = 5;
        public Axis capsizeAxis = Axis.Z;
        public List<AudioClip> capsizeSounds;
        public CardinalDirection initialDirection = CardinalDirection.East;

        public Material colorSplashMaterial;
        public List<GameObject> colorSplashElements;
        public Material lowPolyMaterial;
        public List<GameObject> lowPolyElements;
        
        protected abstract List<Vector3> CellTargetOffsets { get; }
        protected abstract string VesselClassName { get; }
        
        private float _capsizeProgress;
        private float _lastCapsizeProgress;
        private readonly Dictionary<int, bool> _cellOnFire = new();

        private Vector3 _capsizeStartPosition;
        private Quaternion _capsizeStartQuaternion;

        private readonly Dictionary<int, VisualEffect> _fires = new();
        private readonly Dictionary<int, Light> _lights = new();
        private float _lightIntensity;
        protected SeaWar GameController;
        protected LocalGrid LocalGrid;
        private CardinalDirection _direction;
        private bool _moving;
        private Vector3 _actuatorMoveStart;
        private Quaternion _actuatorRotationStart;
        private Vector3? _animatingPositionTo;
        private float _animatingStartTime;
        
        private SnappingAngleSlewController _shipRotationSlewController;
        private Quaternion _animatingRotationFrom;
        private Vector3 _animatingPositionFrom;
        private bool _positionIsInvalid;
        private CardinalDirection _actuatorDirectionStart;
        private Vector2Int _gridPosition;
        private int _layer;
        private SeaWar.GameState _currentGameState;

        
        private Material _lowPolyMaterial;
        private Material _colorSplashMaterial;
        private Color _colorSplash;
        private static readonly int BaseColor = Shader.PropertyToID("_Base_Color");

        public int GetLength() => CellTargetOffsets.Count;

        private int GridPositionToIndex(Vector2Int gridPosition)
        {
            var cells = GetOccupiedCells();
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == gridPosition) return i;
            }
            
            return -1;
        }

        public List<Vector2Int> GetOccupiedCells() => GetOccupiedCells(_gridPosition, GetSize());
        
        public static List<Vector2Int> GetOccupiedCells(Vector2Int position, Vector2Int size)
        {
            var reverse = true;
            
            if (size.x < 0)
            {
                size.x *= -1;
                if (size.x % 2 == 0) position.x++;
                reverse = false;
            }

            if (size.y < 0)
            {
                size.y *= -1;
                if (size.y % 2 == 0) position.y++;
                reverse = false;
            }

            var cellList = new List<Vector2Int>();

            // Calculate half sizes, rounding down for odd-sized regions
            var halfSizeX = size.x / 2;
            var halfSizeY = size.y / 2;

            // Loop to populate the cells
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var offsetX = x - halfSizeX;
                    var offsetY = y - halfSizeY;
                    var occupiedCell = new Vector2Int(position.x + offsetX, position.y + offsetY);
                    cellList.Add(occupiedCell);
                }
            }

            if (reverse) cellList.Reverse();

            return cellList;
        }

        public Vector2Int GetSize() => GetSize(GetLength(), GetDirection());
        
        public static Vector2Int GetSize(int length, CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.North => new Vector2Int(-length, 1),
                CardinalDirection.East => new Vector2Int(1, length),
                CardinalDirection.South => new Vector2Int(length, 1),
                CardinalDirection.West => new Vector2Int(1, -length),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public bool IsOnFire(Vector2Int gridPosition) => 
            _cellOnFire.TryGetValue(GridPositionToIndex(gridPosition), out var onFire) && onFire;  

        public void SetOnFire(Vector2Int gridPosition, bool onFire)
        {
            if (IsOnFire(gridPosition) == onFire) return;

            var cellIndex = GridPositionToIndex(gridPosition);
            if (cellIndex == -1) return;
            
            var wasDestroyed = IsDestroyed();
            _cellOnFire[cellIndex] = onFire;
            var isDestroyed = IsDestroyed();
            
            if (onFire)
            {
                if (_fires.TryGetValue(cellIndex, out var effect)) effect.Play();
                if (_lights.TryGetValue(cellIndex, out var fireLight)) fireLight.enabled = true;
            }
            else
            {
                if (_fires.TryGetValue(cellIndex, out var effect)) effect.Stop();
                if (_lights.TryGetValue(cellIndex, out var fireLight)) fireLight.enabled = false;
            }

            // Get the initial capsize position if we've started capsizing
            var capsizeStart = isDestroyed && !wasDestroyed;
            if (!capsizeStart) return;
            
            // Play the capsize sound
            SoundManager.Instance.PlaySound(capsizeSounds.RandomElement(), 1);
            
            CapturePosition();
        }

        private void CapturePosition()
        {
            var vesselTransform = transform;
            _capsizeStartPosition = vesselTransform.position;
            _capsizeStartQuaternion = vesselTransform.rotation;
        }
    
        public bool IsDestroyed() => _cellOnFire.All(cell => cell.Value);
    
        public Vector3 GetTargetInCell(Vector2Int gridPosition) => CellTargetOffsets[GridPositionToIndex(gridPosition)];

        private static float RotationForDirection(CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.North => 270,
                CardinalDirection.East => 0,
                CardinalDirection.South => 90,
                CardinalDirection.West => 180,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public void SetGridPosition(Vector2Int gridPosition, float heightFraction)
        {
            _gridPosition = gridPosition;
            transform.localPosition = LocalGrid.GetGridCenter(gridPosition, heightFraction);
        }

        public Vector2Int GetGridPosition() => _gridPosition;
        
        public void SetDirection(CardinalDirection direction)
        {
            _direction = direction;
            _shipRotationSlewController.Target = RotationForDirection(direction);
        }

        public bool IsMoving() => _moving;

        public abstract void Enable();

        public CardinalDirection GetDirection() => _direction;

        public void SetLocalGrid(LocalGrid localGrid)
        {
            LocalGrid = localGrid;
            transform.SetParent(localGrid.transform);
        }
        
        public abstract bool PrepareToFire(int initialLayer, out FireReservation reservation);

        private void PrepareFires()
        {
            for (var i = 0; i < CellTargetOffsets.Count; i++)
            {
                var fire = Instantiate(fireTemplate, GetFireParent(i).transform);
                fire.gameObject.layer = _layer;
                
                var jitter = VectorUtils.RandomVector3(-fireJitter, fireJitter);
                var fireTransform = fire.transform;
                fireTransform.localPosition = CellTargetOffsets[i] + jitter;
                fireTransform.localScale *= fireScale;
                fire.Stop();
                
                _fires[i] = fire;
                
                _lights[i] = fire.GetComponentInChildren<Light>();
                _lights[i].enabled = false;

                _cellOnFire[i] = false;
            }
            
            if (_lights.Count > 0) _lightIntensity = _lights[0].intensity;
        }

        protected virtual void Start()
        {
            PrepareColorSplashes();
            if (passive) return;
            
            CapturePosition();
            PrepareFires();

            _shipRotationSlewController = new SnappingAngleSlewController(
                initialAngle: RotationForDirection(initialDirection),
                snapAngle: 0.1f,
                acceleration: 720f,
                maxSpeed: 720f
            );
            
            _currentGameState = GameController.CurrentGameState.GetAndSubscribe(UpdateCurrentGameState);
            _direction = initialDirection;
        }

        private void PrepareColorSplashes()
        {
            _colorSplashMaterial = Instantiate(colorSplashMaterial);
            GameController.RegisterTransitionEffectMaterial(_colorSplashMaterial);

            _lowPolyMaterial = Instantiate(lowPolyMaterial);
            GameController.RegisterTransitionEffectMaterial(_lowPolyMaterial);

            colorSplashElements
                .Select(obj => obj.GetComponent<Renderer>())
                .ToList()
                .ForEach(rend => rend.material = _colorSplashMaterial);
            
            lowPolyElements
                .Select(obj => obj.GetComponent<Renderer>())
                .ToList()
                .ForEach(rend => rend.material = _lowPolyMaterial);
            
            ApplyColorToMaterial(_colorSplash);
        }

        private void UpdateCurrentGameState(SeaWar.GameState state) => _currentGameState = state;
        
        private void AnimatePlacement(Transform vesselTransform)
        {
            var placementAngle = _shipRotationSlewController.Update(Time.deltaTime);
            var placementRotation = Quaternion.Euler(0, placementAngle, 0);
            vesselTransform.localRotation = placementRotation;
        }
        
        protected virtual void Update()
        {
            var vesselTransform = transform;

            if (_currentGameState == SeaWar.GameState.Placing)
            {
                AnimatePlacement(vesselTransform);
                InvalidPositionJiggle(vesselTransform);
                AnimateVesselPosition(vesselTransform);   
            }

            if (_currentGameState is SeaWar.GameState.Playing or SeaWar.GameState.Victory)
            {
                AnimateCapsize(vesselTransform);                
            }
        }

        private void InvalidPositionJiggle(Transform vesselTransform)
        {
            if (!_positionIsInvalid) return;
            vesselTransform.Rotate(new Vector3(0, 0, 1), 10 * Mathf.Sin(Time.time * 20));
        }
        
        private void AnimateVesselPosition(Transform vesselTransform)
        {
            var toPosition = _animatingPositionTo;
            if (!toPosition.HasValue) return;
            
            var elapsed = Time.time - _animatingStartTime;
            var progressUnclamped = elapsed / positionResetDuration;
            var progress = Mathf.Clamp01(progressUnclamped);
            var positionProgress = Easing.InOutQuad(progress);
            var rotationProgress = Easing.InOutQuad(progress);

            var position = VectorUtils.LerpBezierArc(_animatingPositionFrom, toPosition.Value, positionResetApex, positionProgress);
            var rotation = Quaternion.Slerp(_animatingRotationFrom, _actuatorRotationStart, rotationProgress);
            
            vesselTransform.position = position;
            vesselTransform.rotation = rotation;

            // Check for completion
            if (!(progressUnclamped > 1)) return;
            _animatingPositionTo = null;
            _shipRotationSlewController.Reset(vesselTransform.rotation.eulerAngles.y);
            _direction = _actuatorDirectionStart;
        }

        private void AnimateCapsize(Transform vesselTransform)
        {
            // Return early if we're not capsizing
            _capsizeProgress += Time.deltaTime / capsizeDuration * (IsDestroyed() ? 1: -1);
            _capsizeProgress = Mathf.Clamp01(_capsizeProgress);
            if (Math.Abs(_capsizeProgress - _lastCapsizeProgress) < 0.001f) return;
            _lastCapsizeProgress = _capsizeProgress;

            var depth = _capsizeProgress * capsizeDepth;
            var angle = _capsizeProgress * capsizeAngle;

            var rotation = capsizeAxis switch
            {
                Axis.X => Quaternion.Euler(angle,0,0),
                Axis.Y => Quaternion.Euler(0,angle,0),
                Axis.Z => Quaternion.Euler(0,0,angle),
                _ => throw new ArgumentOutOfRangeException()
            };
            vesselTransform.rotation = _capsizeStartQuaternion * rotation;
            vesselTransform.position = _capsizeStartPosition + new Vector3(0, depth, 0);
            
            foreach (var (_, fireLight) in _lights)
                fireLight.intensity = _capsizeProgress * _lightIntensity;
            
            ApplyCapsizeEffect(_capsizeProgress, IsDestroyed());
        }

        protected virtual void ApplyCapsizeEffect(float progress, bool isCapsizing) {}

        protected virtual GameObject GetFireParent(int index) => gameObject;

        public float GetSquaredDistanceFromActuator(Vector3 actuatorPosition)
        {
            // If we're animating our position, return an impossibly large distance, so we're not selected for movement
            return _animatingPositionTo.HasValue ? float.MaxValue : (actuatorPosition - transform.position).sqrMagnitude;
        }

        public Vector3 GetPositionForActuator() => transform.position;

        public void SetActuatorSelected(bool selected, Actuator actuator) {}    // No action?

        public void BeginActuatorMove(Actuator actuator)
        {
            var vesselTransform = transform;
            _actuatorMoveStart = vesselTransform.position;
            _actuatorRotationStart = vesselTransform.rotation;
            _actuatorDirectionStart = _direction;
            _moving = true;

            LocalGrid.TakeVessel(this);
        }

        private void MoveShip(Vector3 position, float heightFraction)
        {
            var gridPosition = LocalGrid.GetGridForPosition(position);
            SetGridPosition(gridPosition, heightFraction);
            
            _positionIsInvalid = !LocalGrid.HighlightPlacement(this, gridPosition);
        }
        
        public void ActuatorMove(Actuator actuator, Vector3 position, int movingObjectsCount)
        {
            MoveShip(position, 1f);
        }

        public void EndActuatorMove(Actuator actuator, Vector3 position, int movingObjectsCount)
        {
            var vesselTransform = transform;
            var fromPosition = vesselTransform.position;
            MoveShip(position, 0f);
            
            _moving = false;
            _positionIsInvalid = false;
            
            var moveValid = LocalGrid.PlaceVessel(this);
            
            // If the move is invalid, animate the ship back to it's starting point before the actuator drag
            if (moveValid) return;
            _animatingPositionFrom = fromPosition;
            _animatingRotationFrom = vesselTransform.rotation;
            _animatingPositionTo = _actuatorMoveStart;
            _animatingStartTime = Time.time;
        }

        public void SetGameController(SeaWar gameController)
        {
            GameController = gameController;
        }

        public void SetLayer(int layer)
        {
            _layer = layer;
            gameObject.layer = layer;
            transform.SetLayerInChildren(layer);
        }

        protected void ApplyLayerToChildren(Transform t) =>
            t.SetLayerInChildren(_layer);
        
        public void SetColorSplash(Color color)
        {
            _colorSplash = color;
            ApplyColorToMaterial(color);
        }

        private void ApplyColorToMaterial(Color color)
        {
            if (_colorSplashMaterial) _colorSplashMaterial.SetColor(BaseColor, color);
            if (_lowPolyMaterial) _lowPolyMaterial.SetColor(BaseColor, color);
        }

        public string GetIdent()
        {
            return $"{name}\nCLASS: {VesselClassName}";
        }

        public abstract void Terminate();
    }
}
