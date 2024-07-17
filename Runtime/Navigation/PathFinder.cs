using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Navigation
{
    public delegate bool IsSolidBlock(Vector2Int worldPosition);
    public delegate bool CanStandAt(Vector3 worldPosition, Vector2 size, bool foot);

    /// <summary>
    /// Path finder base class
    /// </summary>
    public abstract class PathFinder
    {
        protected IsSolidBlock isSolidBlock;
        protected CanStandAt canStandAt;
        protected Vector2 size;

        public Vector2 ObjectSize => size;


        protected PathFinder()
        {
        }

        protected PathFinder(IsSolidBlock isSolidBlock, CanStandAt canStandAt)
        {
            this.isSolidBlock = isSolidBlock;
            this.canStandAt = canStandAt;
            this.size = Vector2.one;
        }

        protected PathFinder(Vector2 size, IsSolidBlock isSolidBlock, CanStandAt canStandAt)
        {
            this.isSolidBlock = isSolidBlock;
            this.canStandAt = canStandAt;
            this.size = size;
        }



        /// <summary>
        /// find the path between to point
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public List<Vector2Int> FindPath(Vector3 startPoint, Vector3 endPoint) => FindPath(Vector2Int.FloorToInt(startPoint), Vector2Int.FloorToInt(endPoint));

        /// <summary>
        /// find the path between to point
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public abstract List<Vector2Int> FindPath(Vector2Int startPoint, Vector2Int endPoint);
    }
}
