using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.PathFinder
{

    /// <summary>
    /// Base class of PathProviders
    /// <br/>
    /// PathProvider is type of classes that provide destinations of an auto-fixed path to <see cref="Movement"/> class
    /// <br/>
    /// This class can provide points that line to the best path to the destination, calculations is done inside this class
    /// </summary>
    public abstract class PathProvider
    {
        protected const int CORRECTION_DISTANCE = 2;


        public static bool drawPath = false;


        protected Transform entity;
        protected List<Vector2Int> cachePath = new List<Vector2Int>();
        protected PathFinder aStar;

        /// <summary>
        /// The next point entity will move to
        /// </summary>
        protected Vector2Int nextPoint;


        /// <summary> Get the distance to next point </summary>
        protected float DistanceToNextPoint => ((Vector2)entity.position - nextPoint).magnitude;
        /// <summary> Get the distance to final point </summary>
        public float CurrentDistance => ((Vector2)entity.position - ExpectedDestination).magnitude;
        /// <summary> Get the distance to final point </summary>
        public float NextPointCurrentDistance => (nextPoint - ExpectedDestination).magnitude;


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

        /// <summary>
        /// is the entity went further but still on the path
        /// </summary>
        /// <returns></returns>
        public bool IsFurtherOnPath()
        {
            if (cachePath.Count == 0)
            {
                return false;
            }
            return ((Vector2)entity.position - cachePath[0]).magnitude < (nextPoint - cachePath[0]).magnitude;
        }




        /// <summary>
        /// debug draw cached path
        /// </summary>
        protected virtual void DrawPath()
        {
            Debug.Log(cachePath.Count);
        }
    }
}
