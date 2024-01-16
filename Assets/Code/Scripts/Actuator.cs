using System.Collections.Generic;
using System.Linq;
using TiltFive;
using UnityEngine;

namespace Code.Scripts
{
    public class Actuator : MonoBehaviour, IWandActuator
    {
        public interface IActuatorMovable
        {
            public float GetSquaredDistanceFromActuator(Vector3 actuatorPosition);
            public Vector3 GetPositionForActuator();
            public void SetActuatorSelected(bool selected, Actuator actuator);
            public void BeginActuatorMove(Actuator actuator);
            public void ActuatorMove(Actuator actuator, Vector3 position, int movingObjectsCount);
            public void EndActuatorMove(Actuator actuator, Vector3 position, int movingObjectsCount);
        }

        public interface IActuatorMovableProvider
        {
            public IEnumerable<IActuatorMovable> GetMovables(PlayerIndex playerIndex);
            public void DispatchPointerPosition(Actuator actuator, Vector3 position);
            public void PostProcessMovablesList(List<IActuatorMovable> inRange, List<IActuatorMovable> notInRange);
        }
        
        public float selectionRange;
        
        private PlayerIndex _playerIndex;
        private bool _triggerDown;
        private bool _moving;
        private readonly Dictionary<IActuatorMovable, Vector3> _movingPieces = new();
        private Vector3 _selectionStartPosition;

        public void SetPlayerIndex(PlayerIndex playerIndex)
        {
            _playerIndex = playerIndex;
        }

        public PlayerIndex GetPlayerIndex() => _playerIndex;
        
        private static void GetPiecesInSelectionRange(Vector3 actuatorPosition, IEnumerable<IActuatorMovable> movables,
            float range, out IEnumerable<IActuatorMovable> inRange, out List<IActuatorMovable> notInRange)
        {
            var squaredSelectionRadius = range * range;

            var orderedInRange = new Dictionary<IActuatorMovable, float>();
            notInRange = new List<IActuatorMovable>();
            foreach (var movable in movables)
            {
                var squaredDistance = movable.GetSquaredDistanceFromActuator(actuatorPosition);
                if (squaredDistance < squaredSelectionRadius)
                {
                    orderedInRange.Add(movable, squaredDistance);
                }
                else
                {
                    notInRange.Add(movable);
                }
            }

            inRange = orderedInRange.OrderBy(pair => pair.Value).Select(pair => pair.Key);
        }

        private void UpdateSelectedPieces(IEnumerable<IActuatorMovable> inRange, List<IActuatorMovable> notInRange)
        {
            // Deselect pieces that are no longer in range and select pieces that are now in range 
            foreach (var piece in notInRange) piece.SetActuatorSelected(false, this);
            foreach (var piece in inRange) piece.SetActuatorSelected(true, this);
        }
        
        private void BeginMove(IEnumerable<IActuatorMovable> inRange)
        {
            // If we've just started moving, flag it as such and get the relative positions of moving objects
            _moving = true;
            var position = transform.position;
            _selectionStartPosition = new Vector3(position.x, 
                position.y - WandManager.Instance.actuatorDefaultHeight, position.z);
            foreach (var piece in inRange)
            {
                _movingPieces.Add(piece, piece.GetPositionForActuator());
                piece.BeginActuatorMove(this);
            }
        }

        private void Move()
        {
            // If we're already moving, compute the total move position,
            // and set the moving pieces to that plus their original positions
            var movementDelta = transform.position - _selectionStartPosition;
            foreach (var (piece, initialPosition) in _movingPieces)
            {
                piece.ActuatorMove(this, initialPosition + movementDelta, _movingPieces.Count);
            }
        }

        private void EndMove()
        {
            _moving = false;
            var movementDelta = transform.position - _selectionStartPosition;
            foreach (var (piece, initialPosition) in _movingPieces)
            {
                piece.EndActuatorMove(this, initialPosition + movementDelta, _movingPieces.Count);
            }
            _movingPieces.Clear();
        }
        
        private void HandleWandTrigger(IEnumerable<IActuatorMovable> inRange)
        {
            // Trigger grabs the intersected pieces
            switch (TiltFive.Input.GetTrigger(playerIndex: _playerIndex))
            {
                case > 0.5f when !_moving:
                    BeginMove(inRange);
                    break;
                case > 0.5f when _moving:
                    Move();
                    break;
                case < 0.5f when _moving:
                    EndMove();
                    break;
            }
        }

        public static void ReduceToSingle(List<IActuatorMovable> inRange, List<IActuatorMovable> notInRange)
        {
            if (inRange.Count <= 0) return;
            
            var singleSelect = inRange.First();
            if (inRange.Count > 1) notInRange.AddRange((inRange.GetRange(1, inRange.Count - 1)));
            inRange.Clear();
            inRange.Add(singleSelect);
        }

        private void Update()
        {
            // Notify listeners about the pointer position
            var movableProvider = WandManager.Instance.MovableProvider;
            movableProvider?.DispatchPointerPosition(this, transform.position);
            
            // Get and dispatch movables
            var movables = movableProvider?.GetMovables(_playerIndex) ?? new List<IActuatorMovable>();
            GetPiecesInSelectionRange(transform.position, movables, selectionRange,
                out var inRange, out var notInRange);
            var actuatorMovables = inRange.ToList();
            movableProvider?.PostProcessMovablesList(actuatorMovables, notInRange);
            
            // Only update the selected pieces if we're not moving, so we don't hoover up all pieces as we pass them
            if (!_moving) UpdateSelectedPieces(actuatorMovables, notInRange);
            
            // Handle start and stop of movables
            HandleWandTrigger(actuatorMovables);
        }
    }
}
