using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Amlos.AI.PathFinder
{
    public class PathFindingTest : MonoBehaviour
    {
        Camera cam;

        // Start is called before the first frame update
        void Start()
        {
            cam = Camera.main;
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                StopAllCoroutines();
                //Debug.Log(Time.realtimeSinceStartup);
                //Debug.Log("transform.position: " + transform.position);
                //Debug.Log("Input.mousePosition: " + Input.mousePosition);
                //Debug.Log("cam.ScreenToWorldPoint(Input.mousePosition): " + cam.ScreenToWorldPoint(Input.mousePosition));
                StartCoroutine(GoToDest());

            }
        }

        IEnumerator GoToDest()
        {
            //Debug.Log(Time.realtimeSinceStartup);
            Vector3 vector3 = cam.ScreenToWorldPoint(Input.mousePosition);


            //Map.Map.Instance.SetColor(Vector3Int.FloorToInt(new Vector3(vector3.x, vector3.y, 0)), Color.red, Map.TilemapLayer.geometry);
            AStarPathFinder pathFinder_A = new AStarPathFinder();
            List<Vector2Int> path = pathFinder_A.FindPath(Vector2Int.FloorToInt(transform.position), Vector2Int.FloorToInt(vector3));


            //Debug.Log(PathFinder_A.IsSolidBlock(Vector2Int.FloorToInt(new Vector2(vector3.x, vector3.y))));

            //Debug.Log(Time.realtimeSinceStartup);

            //transform.position = new Vector3(path[path.Count - 1].x, path[path.Count - 1].y, transform.position.z);
            for (int i = 0; i < path.Count; i++)
            {
                //Map.Map.Instance.SetColor(new Vector3Int(path[i].x, path[i].y, 0), Color.yellow, Map.TilemapLayer.background);
                //PathFinder_A.SetColor(Vector2Int.FloorToInt(path[i]));
                transform.position = new Vector3(path[i].x, path[i].y, transform.position.z);
                yield return new WaitForSeconds(0.05f);
            }
            //Debug.Log(Time.realtimeSinceStartup);
            //yield return new WaitForSeconds(0.1f);



            yield return null;
        }
    }
}
