using UnityEngine;

namespace Amlos.AI.Navigation
{

    /// <summary>
    /// PathProvider to trace another transform
    /// </summary>
    public class Tracer : PathProvider
    {
        Transform target;
        /// <summary> the assumed final point (since target might move, this is the current expected final point) </summary>
        Vector2Int currentFinalPoint;
        float correctionDistance;


        private Vector2Int EntityCurrentPoint => Vector2Int.FloorToInt(entity.position);
        private Vector2Int TargetPoint => Vector2Int.FloorToInt(target.position);
        private float TargetMovement => (currentFinalPoint - (Vector2)target.position).magnitude;

        protected override Vector2Int ExpectedDestination => (currentFinalPoint = TargetPoint);



        public Tracer(Transform entity, Transform target, PathFinder pathfinder, float correctionDistance = 2f) : base(pathfinder)
        {
            this.pathFinder = pathfinder;
            base.entity = entity;
            this.target = target;
            this.correctionDistance = Mathf.Max(2, correctionDistance);
            GenerateNewPath();
        }





        private void GenerateNewPath()
        {
            cachedPath = pathFinder.FindPath(EntityCurrentPoint, ExpectedDestination);
            if (drawPath) DrawPath();
        }

        private void CheckFinderEnd()
        {
            //done
            if (EntityCurrentPoint == ExpectedDestination)
            {
                return;
            }
            //need to keep going
            GenerateNewPath();
        }

        public override void Reevaluate()
        {
            var distanceFromPath = DistanceToNextPoint;

            //offset too large, calculate a new path
            if (distanceFromPath >= correctionDistance)
            {
                GenerateNewPath();
                return;
            }

            var targetMovement = TargetMovement;
            //offset too large, calculate a new path
            if (targetMovement >= correctionDistance)
            {
                GenerateNewPath();
                return;
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
