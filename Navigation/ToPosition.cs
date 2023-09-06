using System;
using UnityEngine;
using static Codice.CM.Common.Merge.MergePathResolver;

namespace Amlos.AI.Nevigation
{
    /// <summary>
    /// PathProvider to a point
    /// </summary>
    public class ToPosition : PathProvider
    {
        Vector2Int finalPoint;

        private Vector2Int EntityCurrentPoint => Vector2Int.FloorToInt(entity.position);

        protected override Vector2Int ExpectedDestination => finalPoint;




        public ToPosition(Transform entity, Vector2Int finalPoint, PathFinder pathfinder) : base(pathfinder)
        {
            base.entity = entity;
            this.finalPoint = finalPoint;
            this.pathFinder = pathfinder;
            GenerateNewPath();
        }





        private void GenerateNewPath()
        {
            cachedPath = pathFinder.FindPath(EntityCurrentPoint, finalPoint);
            if (drawPath) DrawPath();
        }


        private void CheckFinderEnd()
        {
            //done, entity is at point
            if (EntityCurrentPoint == finalPoint)
            {
                return;
            }
            //need to keep going
            GenerateNewPath();
        }

        /// <summary>
        /// Reevaluate the path: whether the path is still usable
        /// </summary>
        public override void Reevaluate()
        {
            /* 
             * if entity is too far away from the next point, then recalculate the path
             */

            float currentOffset = DistanceToNextPoint;
            //offset is too large
            if (currentOffset >= CORRECTION_DISTANCE)
            {
                GenerateNewPath();
            }
        }




        public override Vector2 Next()
        {
            var next = cachedPath[0];
            cachedPath.RemoveAt(0);
            //Debug.Log(next);
            return currentPoint = next;
        }

        public override bool HasNext()
        {
            Reevaluate();
            if (cachedPath?.Count == 0)
            {
                CheckFinderEnd();
            }
            //Debug.Log(cachePath?.Count);
            return cachedPath?.Count > 0;
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
