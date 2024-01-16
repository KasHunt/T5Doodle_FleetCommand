using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TiltFive;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Scripts.Utils
{
    public static class LinqUtils
    {
        public static IEnumerable<T> TakeWhileInclusive<T>(
            this IEnumerable<T> source,
            Func<T, bool> predicate,
            Func<T, bool> includeLast = null)
        {
            foreach (var item in source)
            {
                var shouldInclude = predicate(item);
                if (shouldInclude || (includeLast != null && includeLast(item))) yield return item;
                if (!shouldInclude) break;
            }
        }
        
        public static float DecayingAverage<T>(this IEnumerable<T> sequence, 
            Func<T, float> valueSelector, float alpha) => sequence
                .Select(valueSelector)
                .Aggregate<float, float>(0, (current, value) => alpha * value + (1 - alpha) * current);
        
        public static bool TryFirstOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate, out T value)
        {
            using var iterator = source.GetEnumerator();
            while (iterator.MoveNext())
            {
                if (!predicate.Invoke(iterator.Current)) continue;
                value = iterator.Current;
                return true;
            } 
            
            // Fail -> Return false/default
            value = default;
            return false;
        }
    }
    
    public static class ColorUtils
    {
        private static readonly int EmissiveIntensity = Shader.PropertyToID("_EmissiveIntensity");
        private static readonly int EmissiveColorLDR = Shader.PropertyToID("_EmissiveColorLDR");
        private static readonly int EmissiveColor = Shader.PropertyToID("_EmissiveColor");
        
        public static Color ColorWithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
        
        public static void UpdateEmissiveColor(Material material)
        {
            if (!material.HasProperty(EmissiveColorLDR) || 
                !material.HasProperty(EmissiveIntensity) ||
                !material.HasProperty(EmissiveColor)) return;
            
            var emissiveColorLDR = material.GetColor(EmissiveColorLDR);
            var emissiveColorLDRLinear = new Color(
                Mathf.GammaToLinearSpace(emissiveColorLDR.r), 
                Mathf.GammaToLinearSpace(emissiveColorLDR.g), 
                Mathf.GammaToLinearSpace(emissiveColorLDR.b)
            );
            material.SetColor(EmissiveColor, emissiveColorLDRLinear * material.GetFloat(EmissiveIntensity));
        }
    }
    
    public static class ListUtils
    {
        public static Queue<T> ToQueue<T>(this IEnumerable<T> self) {
            var queue = new Queue<T>();
            foreach (var item in self) queue.Enqueue(item);
            return queue;
        }
        
        public static T RandomElement<T>(this List<T> self)
        {
            var index = Random.Range(0, self.Count);
            return self[index];
        }
        
        public static List<T> Shuffle<T>(this List<T> list, int? seed = null)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            var n = list.Count;
        
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }

            return list;
        }
    }
    
    public static class VectorUtils
    {
        public static void Jitter(this ref Vector3 self, Vector3 size)
        {
            self += RandomVector3(-size, size);
        }
        
        public static Vector3 RandomVector3(Vector3 minInclusive, Vector3 maxInclusive)
        {
            return new Vector3(
                Random.Range(minInclusive.x, maxInclusive.x),
                Random.Range(minInclusive.y, maxInclusive.y),
                Random.Range(minInclusive.z, maxInclusive.z)
            );
        }
        
        public static Vector3 RandomVector3(float minInclusive, float maxInclusive)
        {
            return new Vector3(
                Random.Range(minInclusive, maxInclusive),
                Random.Range(minInclusive, maxInclusive),
                Random.Range(minInclusive, maxInclusive)
            );
        }

        public static Vector3 LerpBezierArc(Vector3 start, Vector3 end, float apexHeight, float t)
        {
            var controlPoint = start;
            controlPoint.y += apexHeight;
            
            return new Vector3(
                Mathf.Lerp(start.x, end.x, t),
                Mathf.Pow(1 - t, 2) * start.y + 2 * (1 - t) * t * controlPoint.y + Mathf.Pow(t, 2) * end.y,
                Mathf.Lerp(start.z, end.z, t)
            );
        }

        public static int ManhattanDistance(this Vector2Int a, Vector2Int b) => 
            Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

        public static IEnumerable<Vector2Int> AdjacentCells(this Vector2Int self)
        {
            return new List<Vector2Int>
            {
                new(self.x+1, self.y),
                new(self.x-1, self.y),
                new(self.x, self.y+1),
                new(self.x, self.y-1)
            };
        }

        public static Vector3 Reciprocal(this Vector3 self) =>
            new(1 / self.x, 1 / self.y, 1 / self.z);

        public static Vector3 Multiply(this Vector3 self, Vector3 other) =>
            new(self.x * other.x, self.y * other.y, self.z * other.z);
    }

    public static class Multiplayer
    {
        public static void SetLayerInChildren(this Component root, int layer)
        {
            var children = root.GetComponentsInChildren<Transform>(includeInactive: true);
            foreach (var child in children) child.gameObject.layer = layer;
        }
    }
    
    public static class Easing
    {
        public static float InOutQuad(float t)
        {
            return t switch
            {
                < 0.5f => 2 * t * t,
                _ => 1 - Mathf.Pow(-2 * t + 2, 2) / 2
            };
        }
        
        public static float InQuad(float t)
        {
            return t * t;
        }
        
        public static float OutQuad(float t)
        {
            return 1 - (1 - t) * (1 - t);
        }
    }

    public static class AnimationUtils
    {
        public static float ComputeSubAnimationTime(float mainAnimationTime, float subAnimationStart, float subAnimationDuration) {
            // Clamp the main animation time to make sure it's within the sub-animation range
            var clampedTime = Mathf.Clamp(mainAnimationTime - subAnimationStart, 0.0f, subAnimationDuration);
    
            // Normalize the clamped time to get a 0-1 value representing the sub-animation progress
            var normalizedTime = clampedTime / subAnimationDuration;
    
            return normalizedTime;
        }
    }

    public static class CoroutineUtils
    {
        public static IEnumerator TimedCoroutine(float duration, float delay, 
            Action<(float progress, bool complete)> preAction, 
            Action<(float progress, bool complete)> loopAction, 
            Action<(float progress, bool complete)> postAction, 
            IEnumerator next = null)
        {
            // Wait before starting
            if (delay > 0) yield return new WaitForSeconds(delay);

            preAction?.Invoke((0, true));
            
            float elapsedTime = 0;
            while (elapsedTime < duration)
            {
                loopAction?.Invoke((elapsedTime / duration, false));

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            postAction?.Invoke((1, true));
            
            if (next != null) yield return next;
        }
        
        public static IEnumerator DelayedCoroutine(float delay, Action<object> action, object data = null, IEnumerator next = null)
        {
            // Wait before starting
            if (delay > 0) yield return new WaitForSeconds(delay);

            action?.Invoke(data);
            
            if (next != null) yield return next;
        }
    }

    public static class RotationUtils
    {
        public static Dictionary<GameObject, Quaternion> CaptureRotations(IEnumerable<List<GameObject>> targets)
        {
            Dictionary<GameObject, Quaternion> rotations = new();
            
            foreach (var obj in targets.SelectMany(objList => objList))
            {
                rotations[obj] = obj.transform.localRotation;
            }

            return rotations;
        }
        
        public static void SetLocalRotationX(GameObject obj, float rotation, Dictionary<GameObject, Quaternion> referenceRotations)
        {
            obj.transform.localRotation = referenceRotations[obj] * Quaternion.AngleAxis(rotation, Vector3.right);
        }
        
        public static void SetLocalRotationY(GameObject obj, float rotation, Dictionary<GameObject, Quaternion> referenceRotations)
        {
            obj.transform.localRotation = referenceRotations[obj] * Quaternion.AngleAxis(rotation, Vector3.up);
        }
            
        public static void SetLocalRotationZ(GameObject obj, float rotation, Dictionary<GameObject, Quaternion> referenceRotations)
        {
            obj.transform.localRotation = referenceRotations[obj] * Quaternion.AngleAxis(rotation, Vector3.forward);
        }

        public static float AddAndClamp360(this float current, float delta) => (current + delta) % 360f;
    }

    public static class GeometryUtils
    {
        public enum Axis
        {
            X,
            Y,
            Z
        }
        
        public static Vector3? PlanarImpactPoint(this List<Vector3> points, float yPosition)
        {
            if (points.Count < 2) return null;
            var pointAbove = points[^2];
            var pointBelow = points[^1];
            
            // The points are the same, so just take either one
            if (Math.Abs(pointAbove.y - pointBelow.y) < 0.001) return pointAbove;
            
            var t = (yPosition - pointBelow.y) / (pointAbove.y - pointBelow.y);
            return Vector3.Lerp(pointBelow, pointAbove, t);
        }
        
        public static Vector3 FindClosestPoint(this List<Vector3> linePoints, Vector3 point) => 
            FindClosestPointInternal(linePoints, point, out _);

        public static List<Vector3> TruncateToClosestPointOnLine(this List<Vector3> linePoints, Vector3 point)
        {
            var closestPoint = FindClosestPointInternal(linePoints, point, out var closestSegmentIndex);
            if (closestSegmentIndex == -1) return new List<Vector3> { closestPoint };

            var truncatedList = linePoints.GetRange(0, closestSegmentIndex + 1);
            truncatedList.Add(closestPoint);
            return truncatedList;
        }

        private static Vector3 FindClosestPointInternal(IReadOnlyList<Vector3> linePoints, Vector3 point, out int closestSegmentIndex)
        {
            closestSegmentIndex = -1;
            if (linePoints.Count < 2)
            {
                return linePoints.Count == 1 ? linePoints[0] : Vector3.zero;
            }

            var closestDistance = float.MaxValue;
            var closestPoint = Vector3.zero;

            for (var i = 0; i < linePoints.Count - 1; i++)
            {
                var pointOnSegment = ClosestPointOnSegment(linePoints[i], linePoints[i + 1], point);
                var distance = Vector3.Distance(point, pointOnSegment);

                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closestPoint = pointOnSegment;
                closestSegmentIndex = i;
            }

            return closestPoint;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            var ap = point - a;
        
            var t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);
            return t switch
            {
                < 0f => a,
                > 1f => b,
                _ => a + ab * t
            };
        }
        
        public static List<Vector3> ComputeArc(Vector3 currentPosition, Vector3 initialVelocity, float gravity, float yPlane, float arcTimeStep, int segmentLimit = 200)
        {
            // Prepare the points list
            var points = new List<Vector3> { currentPosition };

            // Add points to the list until we cross the Y=0 plane,
            // adjusting velocity to account for gravity
            var velocity = initialVelocity;
            while (currentPosition.y >= yPlane)
            {
                velocity.y += gravity * arcTimeStep;
                currentPosition += velocity * arcTimeStep;
                points.Add(currentPosition);
                
                // Continue, or Abort if we're producing an unreasonably long arc
                if (points.Count <= segmentLimit) continue;
                break;
            }

            // Refine the last impact point
            var impact = points.PlanarImpactPoint(yPlane);
            if (impact.HasValue) points[^1] = impact.Value; 
            
            return points;
        }
    }
    
    public static class DebugUtils
    {
        public static string PrintAncestors(Transform obj)
        {
            var path = "";
            while (obj != null)
            {
                path = $"[{obj.name}]" + (path != "" ? "\u2192" : "") + path;
                obj = obj.parent;
            }

            return path;
        }
        
        private static void DrawPlane(this Debug debug, Vector3 inNormal, Vector3 inPoint) {
            var planeColor = Color.yellow;
            var rayColor = Color.magenta;
            
            var offset = Vector3.Cross(inNormal, 
                    inNormal.normalized != Vector3.forward ? Vector3.forward : Vector3.up)
                .normalized * inNormal.magnitude;

            for (var i = 0; i <= 180; i += 20)
            {
                var rotatedOffset = Quaternion.AngleAxis(i, inNormal) * offset;
                Debug.DrawLine(inPoint + rotatedOffset, inPoint - rotatedOffset, planeColor);
            }
            Debug.DrawLine(inPoint, inPoint + inNormal, rayColor);
        }
    }

    // TODO: Maybe pull into T5 core
    public static class T5Utils
    {
        public enum CardinalDirection
        {
            North,
            East,
            South,
            West
        }

        public static bool TryGetPlayerDirection(PlayerIndex playerIndex, out CardinalDirection direction)
        {
            // TODO (khunt): Depends on modified Glasses.cs - If/when TryGetPhysicalPose()
            // is added upstream, this can be removed
            
            
            if (!Glasses.TryGetPhysicalPose(playerIndex, out var pose))
            //if (!Glasses.TryGetPose(playerIndex, out var pose))
            {
                direction = CardinalDirection.North;
                return false;
            }

            var closerToX = Mathf.Abs(pose.position.x) > Mathf.Abs(pose.position.z);
            direction = (pose.position.x, pose.position.z, closerToX) switch
            {
                (>=0, <0, true) => CardinalDirection.South,
                (>=0, >=0, true) => CardinalDirection.South,
                (>=0, <0, false) => CardinalDirection.West,
                (<0, <0, false) => CardinalDirection.West,
                (<0, >=0, true) => CardinalDirection.North,
                (<0, <0, true) => CardinalDirection.North,
                (<0, >=0, false) => CardinalDirection.East,
                (>=0, >=0, false) => CardinalDirection.East,
                _ => throw new ArgumentOutOfRangeException()
            };

            return true;
        }
        
        public static float RotationAngle(this CardinalDirection direction)
        {
            return direction switch
            {
                CardinalDirection.West => 0,
                CardinalDirection.North => 90,
                CardinalDirection.East => 180,
                CardinalDirection.South => 270,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        public static Quaternion Rotation(this CardinalDirection direction) =>
            Quaternion.Euler(0, direction.RotationAngle(), 0);
        
        public static PlayerSettings GetPlayerSettings(this TiltFiveManager2 t5Manager, PlayerIndex playerIndex)
        {
            return playerIndex switch
            {
                PlayerIndex.One => t5Manager.playerOneSettings,
                PlayerIndex.Two => t5Manager.playerTwoSettings,
                PlayerIndex.Three => t5Manager.playerThreeSettings,
                PlayerIndex.Four => t5Manager.playerFourSettings,
                PlayerIndex.None => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null),
                _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
            };
        }

        public static WandSettings GetWandSettings(this TiltFiveManager2 t5, PlayerIndex playerIndex, ControllerIndex hand)
        {
            return (playerIndex, hand) switch
            {
                (PlayerIndex.One, ControllerIndex.Left) => t5.playerOneSettings.leftWandSettings,
                (PlayerIndex.One, ControllerIndex.Right) => t5.playerOneSettings.rightWandSettings,
                (PlayerIndex.Two, ControllerIndex.Left) => t5.playerTwoSettings.leftWandSettings,
                (PlayerIndex.Two, ControllerIndex.Right) => t5.playerTwoSettings.rightWandSettings,
                (PlayerIndex.Three, ControllerIndex.Left) => t5.playerThreeSettings.leftWandSettings,
                (PlayerIndex.Three, ControllerIndex.Right) => t5.playerThreeSettings.rightWandSettings,
                (PlayerIndex.Four, ControllerIndex.Left) => t5.playerFourSettings.leftWandSettings,
                (PlayerIndex.Four, ControllerIndex.Right) => t5.playerFourSettings.rightWandSettings,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static IEnumerable<PlayerIndex> GetConnectedPlayers() =>
            Enum.GetValues(typeof(PlayerIndex))
                .Cast<PlayerIndex>()
                .Except(new[]{PlayerIndex.None})
                .Where(Player.IsConnected);
    }
}
