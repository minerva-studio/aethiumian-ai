using UnityEngine;

namespace Amlos.AI.Navigation
{
    /// <summary>
    /// PathProvider to a point
    /// </summary>
    public class ToPosition : PathProvider
    {
        Vector2Int finalPoint;

        protected override Vector2Int ExpectedDestination => finalPoint;




        public ToPosition(Transform entity, Vector2Int finalPoint, PathFinder pathfinder, float arrivalErrorBound = 0.2f) : base(pathfinder, arrivalErrorBound)
        {
            base.entity = entity;
            this.finalPoint = finalPoint;
            GenerateNewPath();
        }





        private void GenerateNewPath()
        {
            cachedPath = pathFinder.FindPath(EntityCurrentPoint, finalPoint);
            if (drawPath) DrawPath();
        }


        private void CheckFinderEnd()
        {
            //done, entity is at  
            if ((EntityCurrentPoint - ExpectedDestination).magnitude < arrivalErrorBound)
            {
                cachedPath.Clear();
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
            return currentPathPoint = next;
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
