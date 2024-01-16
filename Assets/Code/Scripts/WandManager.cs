using System;
using TiltFive;
using UnityEngine;
using System.Collections.Generic;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using TiltFive.Logging;

namespace Code.Scripts
{
    public interface IWandActuator
    {
        public void SetPlayerIndex(PlayerIndex playerIndex);

        public PlayerIndex GetPlayerIndex();
    }

    public interface IWandArcCollider
    {
        public void OnWandArcEnter(Wand wand);
        public void OnWandArcExit(Wand wand);
        public void OnTriggerPull(Wand wand);
    }
    
    public class WandManager : MonoBehaviour
    {
        [Header("Arc")]
        [Range(0.05f, 100f)]
        public float arcWidth = 0.1f;
        [Range(0.05f, 1f)]
        public float arcTimeStep = 0.1f;
        public Material arcMaterial;
        [Layer] public bool arcVisibleToAll;
        public float yPlane;
        public float actuatorDefaultHeight = 1f;
        
        public NotifyingVariable<float> ArcLaunchVelocity;
        
        [Header("Actuators")]
        public bool enableLeftWand;
        [ConditionalShow("enableLeftWand")] public GameObject leftActuatorObject;
        [ConditionalShow("enableLeftWand")] public bool leftActuatorVisibleToAll;
        public bool enableRightWand;
        [ConditionalShow("enableRightWand")] public GameObject rightActuatorObject;
        [ConditionalShow("enableRightWand")] public bool rightActuatorVisibleToAll;
        
        [Header("Canvas")]
        public GameObject canvasCursorObject;
        public GameObject canvasOtherCursorObject;
        
        [Layer] public int playerOneLayer;
        [Layer] public int playerTwoLayer;
        [Layer] public int playerThreeLayer;
        [Layer] public int playerFourLayer;
        
        public float prefSaveDelay = 3f;
        
        public static WandManager Instance { get; private set; }
        
        [CanBeNull] public Actuator.IActuatorMovableProvider MovableProvider;
        private readonly Dictionary<Transform, IWandArcCollider> _arcColliders = new();
        
        private float _prefsSaveTime = float.MaxValue;
        
        private const string WND_LAUNCH_VELOCITY = "WND_Launch_Velocity";
        
        private static void SetWandObjectsForPlayer(PlayerIndex playerIndex, 
            ControllerIndex hand,
            [CanBeNull] GameObject aimObject = null, 
            [CanBeNull] GameObject gripObject = null, 
            [CanBeNull] GameObject fingertipObject = null)
        {
            var settings = TiltFiveManager2.Instance.GetWandSettings(playerIndex, hand);
            settings.AimPoint = aimObject;
            settings.GripPoint = gripObject;
            settings.FingertipPoint = fingertipObject;
        }

        private static void CheckActuator(GameObject actuatorObject)
        {
            if (!actuatorObject) Log.Warn("Wand Actuator GameObject not specified");

            var wandActuator = actuatorObject.GetComponent<IWandActuator>();
            if (wandActuator == null)
            {
                Log.Warn("Wand Actuator GameObject does not include a component that " +
                         "implements IWandActuator - Actuator won't be attached");
            }
        }

        private void CreateWand(PlayerIndex playerIndex, 
                                ControllerIndex controllerIndex)
        {
            var namePrefix = $"Wand_{playerIndex}_{controllerIndex}";
            
            var wand = new GameObject(namePrefix);
            wand.transform.SetParent(transform);
            
            var aimObject = new GameObject($"{namePrefix}_Aim");
            SetWandObjectsForPlayer(playerIndex, controllerIndex, aimObject: aimObject);
            aimObject.transform.SetParent(wand.transform);
                
            var wandBehaviour = aimObject.AddComponent<Wand>();
            wandBehaviour.playerIndex = playerIndex;
            wandBehaviour.controllerIndex = controllerIndex;
        }
        
        private void Awake()
        {
            // Check for existing instances and destroy this instance if we've already got one
            if (Instance != null)
            {
                Log.Warn("Destroying duplicate WandManager");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            PlayerIndex[] players =
            {
                PlayerIndex.One, 
                PlayerIndex.Two,
                PlayerIndex.Three,
                PlayerIndex.Four
            };

            // Check the actuator is valid
            if (enableLeftWand) CheckActuator(leftActuatorObject);
            if (enableRightWand) CheckActuator(rightActuatorObject);

            // Create the wands
            foreach (var playerIndex in players)
            {
                if (enableLeftWand) CreateWand(playerIndex, ControllerIndex.Left);
                if (enableRightWand) CreateWand(playerIndex, ControllerIndex.Right);
            }
            
            ArcLaunchVelocity = new NotifyingVariable<float>(PlayerPrefs.GetFloat(WND_LAUNCH_VELOCITY, 50f));
            ArcLaunchVelocity.GetAndSubscribe(UpdateLaunchVelocity);
        }

        private void Update()
        {
            MaybeSavePreferences();
        }

        private void OnDestroy()
        {
            ArcLaunchVelocity.Unsubscribe(UpdateLaunchVelocity);
        }

        private void UpdateLaunchVelocity(float volume)
        {
            PlayerPrefs.SetFloat(WND_LAUNCH_VELOCITY, volume);
            _prefsSaveTime = Time.unscaledTime + prefSaveDelay;
        }
        
        private void MaybeSavePreferences()
        {
            var now = Time.unscaledTime;
            if (now < _prefsSaveTime) return;
            _prefsSaveTime = float.MaxValue;
            PlayerPrefs.Save();
        }
        
        public int LayerForPlayer(PlayerIndex index)
        {
            return index switch
            {
                PlayerIndex.One => playerOneLayer,
                PlayerIndex.Two => playerTwoLayer,
                PlayerIndex.Three => playerThreeLayer,
                PlayerIndex.Four => playerFourLayer,
                PlayerIndex.None => throw new ArgumentOutOfRangeException(nameof(index), index, null),
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
            };
        }

        public void RegisterArcCollider(MonoBehaviour arcColliderGameObject)
        {
            if (!arcColliderGameObject.GetComponent<Collider>())
            {
                Debug.LogWarning($"'{arcColliderGameObject.name}' has no collider - wand arc interaction disabled");
                return;
            }
            _arcColliders[arcColliderGameObject.transform] = arcColliderGameObject.GetComponent<IWandArcCollider>();
        }
        
        public void RegisterArcCollider(Transform arcColliderTransform, IWandArcCollider arcCollider) =>
            _arcColliders[arcColliderTransform] = arcCollider;
        
        public void DeregisterArcCollider(MonoBehaviour arcColliderGameObject) =>
            _arcColliders.Remove(arcColliderGameObject.transform);
        
        public void DeregisterArcCollider(Transform arcColliderTransform) =>
            _arcColliders.Remove(arcColliderTransform);

        [CanBeNull]
        public IWandArcCollider CheckArcCollider(Transform obj) =>
            _arcColliders.TryGetValue(obj, out var arcCollider) ? arcCollider : null;
    }
    
    public class Wand : MonoBehaviour, IGameboardCanvasPointer
    {
        public PlayerIndex playerIndex;
        public ControllerIndex controllerIndex;
        
        private Stack<GameObject> _canvasOtherCursorObjects = new();
        private LineRenderer _lineRenderer;
        private List<Vector3> _points = new();
        private bool _canvasCursorActive;
        private Vector3? _arcObjectImpact;
        private bool _wandObserved;
        private bool _triggerWasPulled;
        private int _otherCursorInstanceCount;
        
        private GameObject _actuator;
        private GameObject _canvasCursor;
        private GameObject _canvasOtherCursorTemplate;
        
        private int PlayerLayer => WandManager.Instance.LayerForPlayer(playerIndex);
        private int _arcLayer;

        public enum ImpactState
        {
            Plane,
            Object
        }
        public ImpactState CurrentImpactState => _arcObjectImpact.HasValue ? ImpactState.Object : ImpactState.Plane;

        private string NamePrefix => $"Wand_{playerIndex}_{controllerIndex}";
        
        private void Start()
        {
            var wandManager = WandManager.Instance;
            
            // Create the arc
            _arcLayer = wandManager.arcVisibleToAll ? 0 : PlayerLayer;
            var arc = new GameObject("WandArc")
            {
                layer = _arcLayer
            };

            // Add and configure the LineRenderer
            _lineRenderer = arc.AddComponent<LineRenderer>();
            _lineRenderer.transform.SetParent(transform);
            _lineRenderer.material = wandManager.arcMaterial;
            _lineRenderer.widthCurve = AnimationCurve.Linear(0, 0, 1, wandManager.arcWidth);
            
            // Create and attach an actuator
            var actuatorObject = controllerIndex == ControllerIndex.Left
                ? wandManager.leftActuatorObject : wandManager.rightActuatorObject;
            if (actuatorObject) CreateActuator(actuatorObject, wandManager);

            // Create and attach the canvas cursors
            if (wandManager.canvasCursorObject) CreateCanvasCursor(wandManager.canvasCursorObject);
            if (wandManager.canvasOtherCursorObject) CreateOtherCursor(wandManager.canvasOtherCursorObject);
            
            // Register the pointer with the gameboard manager
            GameboardCanvas.AddGameboardCanvasPointer(this, playerIndex, controllerIndex);
        }

        private void CreateCanvasCursor(GameObject actuatorObject)
        {
            _canvasCursor = Instantiate(actuatorObject, transform, true);
            _canvasCursor.name = $"{NamePrefix}_CanvasCursor";
            _canvasCursor.layer = PlayerLayer;
        }

        private void CreateOtherCursor(GameObject canvasOtherCursorObject)
        {
            _canvasOtherCursorTemplate = Instantiate(canvasOtherCursorObject, transform, true);
            _canvasOtherCursorTemplate.name = $"{NamePrefix}_CanvasOtherCursor";
            _canvasOtherCursorTemplate.layer = PlayerLayer;
        }
        
        private void CreateActuator(GameObject actuatorObject, WandManager wandManager)
        {
            _actuator = Instantiate(actuatorObject, transform, true);
            _actuator.name = $"{NamePrefix}_Actuator";

            // Set the actuator GameObject layer to the player layer if it's not 'visible to all'
            var actuatorVisibleToAll = controllerIndex == ControllerIndex.Left
                ? wandManager.leftActuatorVisibleToAll
                : wandManager.rightActuatorVisibleToAll;
            if (!actuatorVisibleToAll) _actuator.layer = PlayerLayer;

            // Assign the playerIndex to the actuator (via its interface)
            var wandActuator = _actuator.GetComponent<IWandActuator>();
            wandActuator.SetPlayerIndex(playerIndex);

            // Actuator is initially disabled - we'll enable it when we first detect the wand
            _actuator.SetActive(false);
        }

        private void OnDestroy()
        {
            GameboardCanvas.RemoveGameboardCanvasPointer(this);
        }

        private void Update()
        {
            DrawArc();
            HandleWandTrigger();

            // If we've not observed the wand yet, try to detect it
            if (!_wandObserved)
            {
                TiltFive.Wand.TryCheckConnected(out var connected, playerIndex, controllerIndex);
                _wandObserved = connected;

                if (!_wandObserved) return;
            }

            // Ensure the actuator is active if it should be, or inactive if it shouldn't be
            var shouldBeActive = CurrentImpactState == ImpactState.Plane;
            if (_actuator.activeSelf != shouldBeActive) _actuator.SetActive(shouldBeActive);
        }

        private void HandleWandTrigger()
        {
            // Trigger grabs the intersected pieces
            switch (TiltFive.Input.GetTrigger(playerIndex: playerIndex), _triggerWasPulled)
            {
                case (> 0.6f, false):
                    _triggerWasPulled = true;
                    _currentArcCollider?.OnTriggerPull(this);
                    break;
                
                case (< 0.4f, true):
                    _triggerWasPulled = false;
                    break;
            }
        }
        
        private Quaternion ComputeWandRotation()
        {
            var forward = transform.forward;
            var horizontalDirection = new Vector3(forward.x, 0, forward.z).normalized;
            var azimuth = Mathf.Atan2(horizontalDirection.z, horizontalDirection.x) * Mathf.Rad2Deg - 90;
            return Quaternion.Euler(0, -azimuth, 0);
        }
        
        private float ComputeWandElevation()
        {
            var forward = transform.forward;
            return Mathf.Atan2(forward.y, new Vector3(forward.x, 0, forward.z).magnitude) * Mathf.Rad2Deg;
        }
        
        private Vector3 ComputeInitialVelocity(Quaternion wandRotation, float elevation)
        {
            // Compute the initial velocity, rotated into the azimuth of the wand
            var radianAngle = Mathf.Deg2Rad * elevation;
            var velocity = wandRotation * new Vector3(
                0, 
                WandManager.Instance.ArcLaunchVelocity.Value * Mathf.Sin(radianAngle),
                WandManager.Instance.ArcLaunchVelocity.Value * Mathf.Cos(radianAngle)
            );

            var settings = TiltFiveManager2.Instance.GetPlayerSettings(playerIndex);
            velocity.Scale(Vector3.one + settings.gameboardSettings.currentGameBoard.transform.localScale.Reciprocal());
            
            return velocity;
        }

        private void DrawArc()
        {
            // Compute the Arc
            var wandRotation = ComputeWandRotation();
            var wandElevation = ComputeWandElevation();
            var velocity = ComputeInitialVelocity(wandRotation, wandElevation);
            var arcTimeStep = WandManager.Instance.arcTimeStep;
            var yPlane = WandManager.Instance.yPlane;
            _points = GeometryUtils.ComputeArc(transform.position, velocity, Physics.gravity.y, yPlane, arcTimeStep);

            // Set the line renderer points, truncating at the impact point if given
            var points = RaycastArc(_points, out var collision) ? 
                _points.TruncateToClosestPointOnLine(collision).ToArray() : _points.ToArray();
            
            _lineRenderer.positionCount = points.Length;
            _lineRenderer.SetPositions(points);
            
            // Return if there's no actuator
            if (!_actuator) return;
            
            // Compute the plane impact point, and move the actuator there
            var impactPoint = _points.PlanarImpactPoint(yPlane);
            if (!impactPoint.HasValue) return;
            _actuator.transform.position = impactPoint.Value;
            _actuator.transform.rotation = wandRotation;
        }

        [CanBeNull] private IWandArcCollider _currentArcCollider;
        
        private bool RaycastArc(IReadOnlyList<Vector3> points, out Vector3 collision)
        {
            var wandManager = WandManager.Instance;
            collision = Vector3.zero;
            
            for (var i = 0; i < points.Count - 1; i++)
            {
                var startPoint = points[i];
                var endPoint = points[i + 1];
                var direction = endPoint - startPoint;
                var distance = Vector3.Distance(startPoint, endPoint);

                var layerMask = (1 << _arcLayer) | 1; // Mask to the players layer, and the default layer
                if (!Physics.Raycast(startPoint, direction, out var hit, distance, layerMask)) continue;
                
                // If we've hit, check if it's an arc collider
                var arcCollider = wandManager.CheckArcCollider(hit.transform);
                if (arcCollider == null) continue;
                
                // Notify the new arc collider, set the collision point and return
                NotifyArcCollider(_currentArcCollider, arcCollider);
                _currentArcCollider = arcCollider;

                collision = hit.point;
                return true;
            }

            NotifyArcCollider(_currentArcCollider, null);
            _currentArcCollider = null;
            return false;
        }

        private void NotifyArcCollider(IWandArcCollider currentArcCollider, IWandArcCollider newArcCollider)
        {
            if (currentArcCollider == newArcCollider) return;
            
            currentArcCollider?.OnWandArcExit(this);
            newArcCollider?.OnWandArcEnter(this);
        }
        
        public bool ProcessGameboardCanvasPointer(
            Plane canvasPlane, 
            out Vector3 intersection, 
            out IGameboardCanvasPointer.ButtonState buttonState,
            out object data) {
            // Set the trivial `out` arguments
            buttonState = new IGameboardCanvasPointer.ButtonState
            {
                TriggerDown = TiltFive.Input.GetTrigger(playerIndex: playerIndex) > 0.5
            };
            
            for (var i = 0; i < _points.Count - 1; i++)
            {
                var lineStart = _points[i];
                var lineEnd = _points[i + 1];
                var lineLengthSquared = (lineEnd - lineStart).sqrMagnitude;
                
                var segmentRay = new Ray(lineStart, lineEnd - lineStart);
                if (!canvasPlane.Raycast(segmentRay, out var enter)) continue;
                
                // Raycast assumes an infinite line, be we only want to return the
                // intersection if it's within the line segment, so ensure the `enter` value
                // (the distance along the ray of the intersection) is less than the
                // length of the ray
                var enterSquared = enter * enter;
                if (!(enterSquared <= lineLengthSquared)) continue;
                
                intersection = segmentRay.GetPoint(enter); 
                data = i;
                return true;
            }

            intersection = Vector3.zero;
            data = null;
            return false;
        }
        
        public void SetCanvasPointerImpacts(IGameboardCanvasPointer.PointerImpact selfImpact,
            List<IGameboardCanvasPointer.PointerImpact> otherImpacts)
        {
            SetSelfCanvasCursor(selfImpact);
            SetOtherCanvasCursors(otherImpacts);
        }
        
        private void SetSelfCanvasCursor([CanBeNull] IGameboardCanvasPointer.PointerImpact pointerImpact)
        {
            if (pointerImpact != null)
            {
                _arcObjectImpact = pointerImpact.Position;
                if (!_canvasCursor) return;
                _canvasCursor.transform.position = pointerImpact.Position;
                
                if (_canvasCursorActive) return;
                _canvasCursorActive = true;
                _canvasCursor.SetActive(true);
            }
            else
            {
                _arcObjectImpact = null;
                if (!_canvasCursor || !_canvasCursorActive) return;
                _canvasCursorActive = false;
                _canvasCursor.SetActive(false);
            }
        }

        private void SetOtherCanvasCursors(List<IGameboardCanvasPointer.PointerImpact> impacts)
        {
            if (!_canvasOtherCursorTemplate) return;
            
            var newActiveCursors = new Stack<GameObject>();
            
            // Recycle existing 'other' canvas cursors if possible, or instantiate if needed
            foreach (var impact in impacts)
            {
                var recycled = _canvasOtherCursorObjects.TryPop(out var cursor);
                if (!recycled)
                {
                    cursor = Instantiate(_canvasOtherCursorTemplate, transform, true);
                    cursor.name = _canvasOtherCursorTemplate.name + "(" + _otherCursorInstanceCount++ + ")";
                }
                
                cursor.transform.position = impact.Position;
                if (!cursor.activeSelf) cursor.SetActive(true);
                newActiveCursors.Push(cursor);
            }

            // Deactivate and cache any unused cursors this frame
            while (_canvasOtherCursorObjects.TryPop(out var cursor))
            {
                if (cursor.activeSelf) cursor.SetActive(false);
                newActiveCursors.Push(cursor);
            }

            _canvasOtherCursorObjects = newActiveCursors;
        }
    }
}
