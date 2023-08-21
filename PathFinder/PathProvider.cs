using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.PathFinder
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


        protected Type pathFinderType;
        protected Transform entity;
        protected List<Vector2Int> cachedPath = new List<Vector2Int>();
        protected PathFinder pathFinder;

        /// <summary>
        /// The current point entity is moving to
        /// </summary>
        protected Vector2Int currentPoint;


        /// <summary> Get the distance to next point </summary>
        protected float DistanceToNextPoint => ((Vector2)entity.position - currentPoint).magnitude;
        /// <summary> Get the distance to final point </summary>
        public float CurrentEntityDistance => ((Vector2)entity.position - ExpectedDestination).magnitude;
        /// <summary> Get the distance to final point </summary>
        public float CurrentPointDistance => (currentPoint - ExpectedDestination).magnitude;
        /// <summary> The next point in the provider </summary>
        public Vector2Int NextPoint => cachedPath[0];
        public Vector2Int CurrentPoint => currentPoint;
        public List<Vector2Int> CachedPath => new(cachedPath);


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
            return ((Vector2)entity.position - NextPoint).magnitude < (currentPoint - NextPoint).magnitude;
        }

        /// <summary>
        /// Get the instance of a pathfinder
        /// </summary>
        /// <returns></returns>
        protected PathFinder GetPathFinder()
        {
            return pathFinderType != null ? Activator.CreateInstance(pathFinderType) as PathFinder : PathFinder.CreateInstance();
        }



        /// <summary>
        /// debug draw cached path
        /// </summary>
        protected virtual void DrawPath()
        {
            Debug.Log(cachedPath.Count);
            drawPathAction?.Invoke(cachedPath);
        }
    }
}
