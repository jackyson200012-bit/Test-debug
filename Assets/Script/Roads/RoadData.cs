using System.Collections.Generic;
using UnityEngine;

namespace JayFos.Roads
{
    [System.Serializable]
    public struct NavigationWaypoint
    {
        public Vector3 Position;
        public Vector3 Direction;
        public float Width;
        public float Influence;
        public NavigationWaypointType WaypointType;
    }

    public enum NavigationWaypointType
    {
        Road,
        Intersection,
        Start,
        End
    }

    [System.Serializable]
    public class RoadSegment
    {
        public Vector3 start;
        public Vector3 end;
        public float width;
        public float influence;

        public RoadSegment(Vector3 start, Vector3 end, float width, float influence)
        {
            this.start = start;
            this.end = end;
            this.width = width;
            this.influence = influence;
        }

        public float Length => Vector3.Distance(start, end);

        public Vector3 GetPointAt(float t)
        {
            return Vector3.Lerp(start, end, Mathf.Clamp01(t));
        }

        public Vector3 GetDirection()
        {
            return (end - start).normalized;
        }
    }

    [System.Serializable]
    public class RoadData
    {
        public List<RoadSegment> segments = new List<RoadSegment>();
        public List<Vector3> waypoints = new List<Vector3>();

        public void AddSegment(Vector3 start, Vector3 end, float width, float influence)
        {
            segments.Add(new RoadSegment(start, end, width, influence));
        }

        public void AddWaypoint(Vector3 position)
        {
            waypoints.Add(position);
        }

        public RoadSegment FindNearestSegment(Vector3 position)
        {
            RoadSegment nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < segments.Count; i++)
            {
                RoadSegment seg = segments[i];
                Vector3 closest = FindClosestPointOnSegment(position, seg);
                float dist = Vector3.Distance(position, closest);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = seg;
                }
            }

            return nearest;
        }

        public List<Vector3> GetWaypointsInRange(Vector3 center, float range)
        {
            List<Vector3> result = new List<Vector3>();
            float rangeSq = range * range;

            for (int i = 0; i < waypoints.Count; i++)
            {
                if ((waypoints[i] - center).sqrMagnitude <= rangeSq)
                {
                    result.Add(waypoints[i]);
                }
            }

            return result;
        }

        private Vector3 FindClosestPointOnSegment(Vector3 point, RoadSegment segment)
        {
            Vector3 ab = segment.end - segment.start;
            float t = Vector3.Dot(point - segment.start, ab) / Vector3.Dot(ab, ab);
            t = Mathf.Clamp01(t);
            return segment.start + ab * t;
        }

        public void Clear()
        {
            segments.Clear();
            waypoints.Clear();
        }

        public List<NavigationWaypoint> ExtractNavigationData(RoadFieldGrid roadGrid, Vector3 worldPosition)
        {
            // Find nearest road segment
            RoadSegment nearest = roadGrid.FindNearestSegment(worldPosition);

            if (nearest == null)
                return new List<NavigationWaypoint>();

            // Get road center and width
            Vector3 roadCenter = nearest.start + (nearest.end - nearest.start) * 0.5f;
            float roadWidth = nearest.width;

            // Get navigation data
            Vector3 direction = nearest.end - nearest.start;
            Vector3 roadNormal = Vector3.Perpendicular(direction);

            return new List<NavigationWaypoint>
            {
                new NavigationWaypoint
                {
                    Position = roadCenter,
                    Direction = direction.normalized,
                    Width = roadWidth,
                    Influence = nearest.influence,
                    WaypointType = NavigationWaypointType.Road
                }
            };
        }
    }
}