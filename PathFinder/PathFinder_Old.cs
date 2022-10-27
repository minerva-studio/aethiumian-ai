using Amlos;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.PathFinder
{
    [Obsolete]
    public static class PathFinder_Old
    {
        public static float neighbor = 1;
        public static float angle = 1.414f;

        public class TileInfo
        {
            public Vector3Int coordinate;

            public int countToOrigin;
            public float realDistanceToOrigin;

            public TileInfo()
            {
            }

            public TileInfo(Vector3Int coordinate, int countToOrigin, float realDistanceToOrigin)
            {
                this.coordinate = coordinate;
                this.countToOrigin = countToOrigin;
                this.realDistanceToOrigin = realDistanceToOrigin;
            }
        }

        public static List<Vector3Int> GetPath(Vector3Int startPoint, Vector3Int endPoint, int maxDistance)
        {
            Debug.Log("count");
            //Debug.Log(startPoint);
            //Debug.Log(endPoint);
            List<Vector3Int> ClosedTiles = new List<Vector3Int>();
            List<Vector3Int> OpenTiles = new List<Vector3Int>() { startPoint };
            Dictionary<Vector3Int, TileInfo> grid = new Dictionary<Vector3Int, TileInfo>();
            TileInfo tileInfo = new TileInfo(startPoint, 0, 0);
            grid.Add(startPoint, tileInfo);
            int count = 0;
            while (!grid.ContainsKey(endPoint) && count < maxDistance)
            {
                ClosedTiles = ClosedTiles.Union(OpenTiles).ToList();
                int currentListCount = OpenTiles.Count;
                for (int i = 0; i < currentListCount; i++)
                {
                    Vector3Int item = OpenTiles[i];
                    foreach (var near in item.GetNearbyCoordinate())
                    {
                        if (grid.ContainsKey(near)) continue;
                        //if (!near.CanStandOn()) { ClosedTiles.Add(near); continue; }
                        grid.Add(near, new TileInfo(near, grid[item].countToOrigin + 1, grid[item].realDistanceToOrigin + neighbor));
                        OpenTiles.Add(near);
                    }
                    foreach (var near in item.GetAngleCoordinate())
                    {
                        if (grid.ContainsKey(near)) continue;
                        //if (!near.CanStandOn()) { ClosedTiles.Add(near); continue; }
                        grid.Add(near, new TileInfo(near, grid[item].countToOrigin + 1, grid[item].realDistanceToOrigin + angle));
                        OpenTiles.Add(near);
                    }
                }
                OpenTiles = OpenTiles.Except(ClosedTiles).ToList();
                count++;
                // Debug.Log(count);
                if (grid.ContainsKey(endPoint))
                {
                    //Debug.Log(grid[endPoint]);
                    //Debug.Log(count); 
                    break;
                }
            }
            //Debug.Log("end maping: " + count);

            List<Vector3Int> path = new List<Vector3Int>() { endPoint };
            TileInfo predicted = null;
            for (int i = 0; i < count; i++)
            {
                foreach (var test in path.LastOrDefault().GetNearbyCoordinate().Union(path.LastOrDefault().GetAngleCoordinate()))
                {
                    if (!grid.ContainsKey(test)) continue;
                    if (predicted is null) predicted = grid[test];
                    if (grid[test].realDistanceToOrigin < predicted.realDistanceToOrigin) predicted = grid[test];
                }
                path.Add(predicted.coordinate);
            }

            path.Reverse();
            path.Remove(startPoint);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3Int item = path[i];
                Debug.DrawLine(item, path[i + 1], Color.black, 5);
                //Debug.Log(item);
            }
            //Vector2Int direction = Vector2Int.one;
            //var ignore = new List<Vector3Int>();
            //for (int i = 0; i < path.Count - 1; i++)
            //{
            //    var dir = path[i + 1] - path[i];
            //    if (dir.x == direction.x && dir.y == direction.y) { ignore.Add(path[i + 1]); i--; }

            //}
            return path;
        }


        public static Vector3Int[] GetNearbyCoordinate(this Vector3Int coordinate)
        {
            return new Vector3Int[] {
                new Vector3Int(coordinate.x+1,coordinate.y,0),
                new Vector3Int(coordinate.x,coordinate.y-1,0),
                new Vector3Int(coordinate.x,coordinate.y+1,0),
                new Vector3Int(coordinate.x-1,coordinate.y,0),
            };
        }

        public static Vector3Int[] GetAngleCoordinate(this Vector3Int coordinate)
        {
            return new Vector3Int[] {
                new Vector3Int(coordinate.x+1,coordinate.y-1,0),
                new Vector3Int(coordinate.x+1,coordinate.y+1,0),
                new Vector3Int(coordinate.x-1,coordinate.y-1,0),
                new Vector3Int(coordinate.x-1,coordinate.y+1,0),
            };
        }

        public static bool CanStandOn(this Vector3Int vector3Int)
        {
            return !Level.Map.Instance.GetTile(vector3Int);
        }
    }
}
