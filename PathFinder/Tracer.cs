using UnityEngine;

namespace Amlos.AI.PathFinder
{
    /// <summary>
    /// trace another transform or to a point
    /// </summary>
    public class Tracer : PathProvider
    {

        Transform target;
        Vector2Int currentFinalPoint;
        float correctionDistance;


        private bool IsDynamicDestination => target;
        private Vector2Int StartPoint => Vector2Int.FloorToInt(entity.position);
        private Vector2Int TargetPoint => Vector2Int.FloorToInt(target.position);
        private Vector2Int FinalPoint => IsDynamicDestination ? (currentFinalPoint = TargetPoint) : currentFinalPoint;
        private float CurrentOffsetToTarget => IsDynamicDestination ? (currentFinalPoint - (Vector2)target.position).magnitude : 0;





        public Tracer(Transform startPoint, Vector2Int finalPoint)
        {
            entity = startPoint;
            currentFinalPoint = finalPoint;
            correctionDistance = 0;
            GenerateNewPath();
        }

        public Tracer(Transform startPoint, Transform finalPoint, float correctionDistance = 2f)
        {
            entity = startPoint;
            target = finalPoint;
            this.correctionDistance = Mathf.Max(2, correctionDistance);
            GenerateNewPath();
        }





        private void GenerateNewPath()
        {
            aStar = new AStarPathFinder();
            cachePath = aStar.FindPath(StartPoint, FinalPoint);
            if (drawPath) DrawPath();
        }


        private void CheckFinderEnd()
        {
            //done
            if (StartPoint == FinalPoint)
            {
                return;
            }
            //need to keep going
            GenerateNewPath();
        }

        private void Reevaluate()
        {
            if (!IsDynamicDestination)
            {
                return;
            }

            var currentOffset = CurrentOffsetToTarget;
            //offset is not significant, ignore
            if (currentOffset < 1)
            {
                return;
            }
            //offset too large, calculate a new path
            if (currentOffset > correctionDistance)
            {
                GenerateNewPath();
            }
        }




        public override Vector2 Next()
        {
            var next = cachePath[0];
            cachePath.RemoveAt(0);
            Debug.Log(next);
            return next;
        }

        public override bool HasNext()
        {
            Reevaluate();
            if (cachePath?.Count == 0)
            {
                CheckFinderEnd();
            }
            Debug.Log(cachePath?.Count);
            return cachePath?.Count > 0;
        }
    }
}
