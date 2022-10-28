using UnityEngine;

namespace Amlos.AI
{
    public delegate void OnObstacle(Collider2D col);

    public class ObstacleDetector : MonoBehaviour
    { 
        public event OnObstacle onEnterObstacle; 
        public event OnObstacle onStayObstacle;
        public event OnObstacle onExitObstacle;

        private void OnTriggerEnter2D(Collider2D col)
        {
            onEnterObstacle?.Invoke(col);
        }
        private void OnTriggerStay2D(Collider2D col)
        {
            onStayObstacle?.Invoke(col);
        }
        private void OnTriggerExit2D(Collider2D col)
        {
            onExitObstacle?.Invoke(col);
        }
    }
}