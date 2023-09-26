using Minerva.Module;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.Navigation
{
    /// <summary>
    /// standard a* path finder 
    /// <br/>
    /// Author : Garry, Wendell
    /// </summary>
    public class AStarPathFinder : PathFinder
    {
        /// <summary>
        /// class to store tile info
        /// </summary>
        public class TileInfo
        {
            public Vector2Int coordinate;
            public TileInfo prevTile;

            public int distFromStart;
            public int distToEnd;
            public int Sum => distFromStart + distToEnd;

            public TileInfo(Vector2Int coordinate, int distFromStart, int distToEnd)
            {
                this.coordinate = coordinate;
                this.distFromStart = distFromStart;
                this.distToEnd = distToEnd;
            }
        }

        private const int NEIGHBOR_COST = 10;
        private const int DIAGONAL_COST = 14;
        private const int MAX_OPEN_TILE = 10000;
        private const int MAX_CLOSE_TILE = 10000;
        private Vector2 size;

        public AStarPathFinder(Vector2 size, IsSolidBlock isSolidBlock, CanStandAt canStandAt) : base(isSolidBlock, canStandAt)
        {
            this.size = size;
        }

        private int EstimateCost(Vector2Int start, Vector2Int end)
        {
            // an estimation of distance from one tile to another
            // cost estimated by moving diagonally to the end then straight for the remaining tiles

            int xDis = Mathf.Abs(start.x - end.x);
            int yDis = Mathf.Abs(start.y - end.y);
            int remaining = Mathf.Abs(xDis - yDis);
            return NEIGHBOR_COST * remaining + DIAGONAL_COST * Mathf.Min(xDis, yDis);
        }

        private List<Vector2Int> CalculatePath(TileInfo currentTile)
        {
            var path = new List<Vector2Int>();
            while (currentTile != null)
            {
                path.Add(currentTile.coordinate);
                currentTile = currentTile.prevTile;
            }
            path.Reverse();


            //for (int i = 0; i < path.Count - 1; i++)
            //{
            //    //same axis
            //    if (path[i].x == path[i + 1].x || path[i].y == path[i + 1].y) continue;

            //    //diagonal
            //    var xBlock = path[i] + new Vector2Int(path[i + 1].x - path[i].x, 0);
            //    var yBlock = path[i] + new Vector2Int(0, path[i + 1].y - path[i].y);

            //    if (IsSolidBlock(xBlock))
            //    {
            //        path.Insert(i + 1, yBlock);
            //        i++;
            //    }
            //    else if (IsSolidBlock(yBlock))
            //    {
            //        path.Insert(i + 1, xBlock);
            //        i++;
            //    }
            //}

            return path;
        }

        private List<Vector2Int> GetNeighbors(Vector2Int coordinate)
        {
            var neighbors = new List<Vector2Int>()
            {
                new Vector2Int(coordinate.x + 1, coordinate.y),
                new Vector2Int(coordinate.x, coordinate.y - 1),
                new Vector2Int(coordinate.x, coordinate.y + 1),
                new Vector2Int(coordinate.x - 1, coordinate.y)
            };

            if (!IsCorner(coordinate, 1, 1)) neighbors.Add(new Vector2Int(coordinate.x + 1, coordinate.y + 1));
            if (!IsCorner(coordinate, 1, -1)) neighbors.Add(new Vector2Int(coordinate.x + 1, coordinate.y - 1));
            if (!IsCorner(coordinate, -1, 1)) neighbors.Add(new Vector2Int(coordinate.x - 1, coordinate.y + 1));
            if (!IsCorner(coordinate, -1, -1)) neighbors.Add(new Vector2Int(coordinate.x - 1, coordinate.y - 1));


            return neighbors;
        }


        private bool CanStandAt(Vector2Int dest, bool needFoothold = false) => CanStandAt(new Vector3(dest.x, dest.y, 0), needFoothold);
        private bool CanStandAt(Vector3 dest, bool needFoothold = false)
        {
            return canStandAt?.Invoke(dest, size, needFoothold) == true;
        }

        private bool IsSolidBlock(Vector2Int vector2Int)
        {
            return isSolidBlock?.Invoke(vector2Int) != false;
        }


        /// <summary>
        /// find the path between to point
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        /// <returns></returns>
        public override List<Vector2Int> FindPath(Vector2Int startPoint, Vector2Int endPoint)
        {
            //the final point is the same point
            if (startPoint == endPoint)
            {
                return new List<Vector2Int>() { endPoint };
            }

            if (IsSolidBlock(endPoint))
            {
                Debug.Log("Destination is a solid block!");
                return null;
            }

            var closedTiles = new HashSet<Vector2Int>();
            PriorityQueue<TileInfo, int> openTiles = new PriorityQueue<TileInfo, int>();

            openTiles.Enqueue(new TileInfo(startPoint, 0, EstimateCost(startPoint, endPoint)), EstimateCost(startPoint, endPoint));

            while (openTiles.Count > 0)
            {
                //frontier is too large (situation doesn't seems right)
                if (openTiles.Count > MAX_OPEN_TILE) return null;
                //already look about 6 * 6 chunks, too large (situation doesn't seems right)
                if (closedTiles.Count > MAX_CLOSE_TILE) return null;


                // find the tile with the lowest sum
                var currTile = openTiles.Dequeue();
                closedTiles.Add(currTile.coordinate);

                if (currTile.coordinate == endPoint)
                {
                    return CalculatePath(currTile);
                }

                closedTiles.Add(currTile.coordinate);

                // get the neighbors of the lowest sum tile
                var neighbors = GetNeighbors(currTile.coordinate);
                foreach (var neighbor in neighbors)
                {
                    // if closedTiles already has neighbor in it, continue
                    if (closedTiles.Contains(neighbor)) continue;
                    //neighbor is solid block
                    if (IsSolidBlock(neighbor) || !CanStandAt(neighbor))
                    {
                        closedTiles.Add(neighbor);
                        continue;
                    }


                    var newDistFromStart = currTile.distFromStart + (currTile.coordinate.x == neighbor.x || currTile.coordinate.y == neighbor.y ? NEIGHBOR_COST : DIAGONAL_COST);
                    var newDistToEnd = EstimateCost(neighbor, endPoint);
                    var newTile = new TileInfo(neighbor, newDistFromStart, newDistToEnd);
                    newTile.prevTile = currTile;

                    //found the path
                    if (newTile.coordinate == endPoint)
                    {
                        return CalculatePath(newTile);
                    }


                    var sameTile = openTiles.UnorderedItems.FirstOrDefault(x => x.Element.coordinate == newTile.coordinate).Element;
                    //tile exist already
                    if (sameTile != null)
                    {
                        if (sameTile.distFromStart > newTile.distFromStart)
                        {
                            sameTile.distFromStart = newTile.distFromStart;
                            sameTile.prevTile = newTile.prevTile;
                            //Debug.Log("Tile " + neighbor + " distance changed by " + currTile.coordinate);
                        }
                    }
                    //new tile
                    else
                    {
                        //Debug.Log("Tile " + neighbor + " add by " + currTile.coordinate + $"({newTile.sum})");
                        openTiles.Enqueue(newTile, newTile.Sum);
                    }
                }

            }

            //no path found
            return null;
        }


        //private bool IsCorner(Vector2Int coordinate, int offsetX, int offsetY)
        //{
        //    return IsSolidBlock(new Vector2Int(coordinate.x + offsetX, coordinate.y))
        //        && IsSolidBlock(new Vector2Int(coordinate.x, coordinate.y + offsetY));
        //}
        private bool IsCorner(Vector2Int coordinate, int offsetX, int offsetY)
        {
            // return acccording to the signs of offsets
            return IsSolidBlock(new Vector2Int(coordinate.x + offsetX, coordinate.y + offsetY - (offsetY >= 0 ? 1 : -1)))
                && IsSolidBlock(new Vector2Int(coordinate.x + offsetX - (offsetX >= 0 ? 1 : -1), coordinate.y + offsetY));
        }

        private bool TryFall(Vector2Int current, out Vector2Int vector2Int)
        {
            for (int i = 0; i < 200; i++)
            {
                if (IsSolidBlock(current))
                {
                    vector2Int = current;
                    vector2Int.y++;
                    return true;
                }
                current.y--;
            }

            vector2Int = Vector2Int.zero;
            //it seems like this is a buttomless pit
            return false;
        }

        private int FallHeight(Vector2Int vector2Int)
        {
            for (int i = 0; i < 200; i++)
            {
                if (IsSolidBlock(vector2Int))
                {
                    return i;
                }
                vector2Int.y--;
            }

            //it seems like this is a buttomless pit
            return int.MaxValue;
        }
    }
}
