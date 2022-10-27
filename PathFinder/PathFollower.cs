using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.PathFinder
{
    /// <summary>
    /// follows the given path
    /// </summary>
    public class PathFollower : PathProvider
    {
        Queue<Vector2Int> points;
        Vector2Int currentFinalPoint;

        private Vector2Int CurrentLocation => Vector2Int.FloorToInt(entity.position);





        public PathFollower(Transform startPoint, params Vector2Int[] points)
        {
            if (points.Length == 0)
            {
                throw new InvalidOperationException("Cannot create a path follower with no mid points");
            }

            entity = startPoint;
            this.points = new Queue<Vector2Int>(points);
            GenerateNewPath();
        }


        private void GenerateNewPath()
        {
            aStar = new AStarPathFinder();
            currentFinalPoint = points.Dequeue();
            cachePath = aStar.FindPath(CurrentLocation, currentFinalPoint);

            if (drawPath) DrawPath();
        }

        private void CheckFinderEnd()
        {
            //there are path exist
            if (cachePath?.Count != 0) return;
            //done
            if (points.Count == 0 && CurrentLocation == currentFinalPoint) return;

            //need to keep going
            currentFinalPoint = points.Dequeue();
            GenerateNewPath();
        }


        public override Vector2 Next()
        {
            var next = cachePath[0];
            cachePath.RemoveAt(0);
            return next;
        }

        public override bool HasNext()
        {
            if (cachePath?.Count == 0)
            {
                CheckFinderEnd();
            }

            return cachePath?.Count > 0;
        }
    }
}
