using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Navigation
{
    /// <summary>
    /// Base class of PathProviders
    /// <br/>
    /// PathProvider is type of classes that provide destinations of an auto-fixed path to <see cref="Nodes.Movement"/> class
    /// <br/>
    /// This class can provide points that line to the best path to the destination, calculations is done inside this class
    /// </summary>
    public abstract class PathProvider
    {
        protected const int CORRECTION_DISTANCE = 2;



        public static bool drawPath = false;
        public static Action<List<Vector2Int>> drawPathAction;


        protected Transform entity;
        protected List<Vector2Int> cachedPath = new List<Vector2Int>();
        protected readonly PathFinder pathFinder;
        protected readonly float arrivalErrorBound;

        /// <summary>
        /// The current point entity is moving to
        /// </summary>
        protected Vector2Int currentPathPoint;


        public Vector2 currentFootPosition
        {
            get
            {
                Vector2 position = (Vector2)entity.position;
                position.y -= pathFinder.ObjectSize.y / 2;
                return position;
            }
        }
        protected Vector2Int EntityCurrentPoint => Vector2Int.FloorToInt(currentFootPosition);
        /// <summary> Get the distance to next point </summary>
        protected float DistanceToNextPoint => (currentFootPosition - currentPathPoint).magnitude;
        /// <summary> Get the distance to final point </summary>
        public float CurrentEntityDistance => (currentFootPosition - ExpectedDestination).magnitude;
        /// <summary> The next point in the provider </summary>
        public Vector2Int NextPoint => cachedPath[0];
        public Vector2Int CurrentPathPoint => currentPathPoint;
        public List<Vector2Int> CachedPath => new(cachedPath);




        protected PathProvider(PathFinder pathFinder, float arrivalErrorBound = 0.2f)
        {
            this.pathFinder = pathFinder;
            this.arrivalErrorBound = arrivalErrorBound;
        }




        /// <summary>
        /// Expected Destination of a PathProvider
        /// </summary>
        protected abstract Vector2Int ExpectedDestination { get; }

        /// <summary> Check whether requires more move </summary>
        /// <returns></returns>
        public abstract bool HasNext();

        /// <summary> Get the next destination </summary>
        /// <returns> the next point </returns>
        public abstract Vector2 Next();

        /// <summary>
        /// Re-evaluate current path, if current path is not the best pathm generate an new path
        /// </summary>
        public abstract void Reevaluate();





        /// <summary> Consume given entrt count in the provider </summary>
        /// <returns> the next point </returns>
        public Vector2 Consume(int count)
        {
            Vector2 vector2 = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                vector2 = Next();
            }
            return vector2;
        }


        /// <summary>
        /// is the entity went further but still on the path
        /// </summary>
        /// <returns></returns>
        public bool IsFurtherOnPath()
        {
            if (cachedPath.Count == 0)
            {
                return false;
            }
            return (currentFootPosition - NextPoint).magnitude < (currentPathPoint - NextPoint).magnitude;
        }



        /// <summary>
        /// debug draw cached path
        /// </summary>
        protected virtual void DrawPath()
        {
            if (cachedPath != null)
            {
                Debug.Log(cachedPath.Count);
                drawPathAction?.Invoke(cachedPath);
            }
            else
            {
                Debug.Log($"No path {currentFootPosition} => {ExpectedDestination}");
            }
        }
    }
}
