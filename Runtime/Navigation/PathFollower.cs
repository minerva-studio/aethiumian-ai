using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Navigation
{
    /// <summary>
    /// follows the given path
    /// </summary>
    public class PathFollower : PathProvider
    {
        Queue<Vector2Int> midpoints;
        Vector2Int finalPoint;

        Vector2Int currentFinalPoint;

        protected override Vector2Int ExpectedDestination => finalPoint;





        public PathFollower(Transform startPoint, PathFinder pathFinder, params Vector2Int[] points) : base(pathFinder)
        {
            if (points.Length == 0)
            {
                throw new InvalidOperationException("Cannot create a path follower with no mid points");
            }

            this.entity = startPoint;
            this.midpoints = new Queue<Vector2Int>(points);
            finalPoint = points[^1];
            GenerateNewPath();
        }


        private void GenerateNewPath()
        {
            currentFinalPoint = midpoints.Dequeue();
            cachedPath = pathFinder.FindPath(EntityCurrentPoint, currentFinalPoint);

            if (drawPath) DrawPath();
        }

        private void CheckFinderEnd()
        {
            //there are path exist
            if (cachedPath?.Count != 0) return;
            //done
            if (midpoints.Count == 0 && EntityCurrentPoint == currentFinalPoint) return;

            //need to keep going
            currentFinalPoint = midpoints.Dequeue();
            GenerateNewPath();
        }

        public override Vector2 Peek()
        {
            return cachedPath[0];
        }

        public override Vector2 Next()
        {
            var next = cachedPath[0];
            cachedPath.RemoveAt(0);
            return currentPathPoint = next;
        }

        public override bool HasNext()
        {
            if (cachedPath?.Count == 0)
            {
                CheckFinderEnd();
            }

            return cachedPath?.Count > 0;
        }

        public override void Reevaluate()
        {
            float currentOffset = DistanceToNextPoint;
            //offset is too large
            if (currentOffset >= CORRECTION_DISTANCE)
            {
                GenerateNewPath();
            }
        }

        /// <summary>
        /// debug draw cached path
        /// </summary>
        //protected override void DrawPath()
        //{
        //    Color black = new Color(Random.value, Random.value, Random.value);
        //    foreach (var item in cachePath)
        //    {
        //        Map.Instance.SetColor((Vector3Int)item, black, Levels.TilemapLayer.background);
        //    }
        //    Debug.Log(cachePath.Count);
        //}
    }
}
