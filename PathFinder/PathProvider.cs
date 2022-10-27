using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Amlos.AI.PathFinder
{
    public abstract class PathProvider
    {
        public static bool drawPath = false;


        protected Transform entity;
        protected List<Vector2Int> cachePath = new List<Vector2Int>();

        protected AStarPathFinder aStar;

        public abstract bool HasNext();
        public abstract Vector2 Next();

        protected void DrawPath()
        {
            Color black = new Color(Random.value, Random.value, Random.value);
            foreach (var item in cachePath)
            {
                Level.Map.Instance.SetColor((Vector3Int)item, black, Level.TilemapLayer.background);
            }
            Debug.Log(cachePath.Count);
        }
    }
}
