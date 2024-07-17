using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Tooltip("Determine transform is outside of screen")]
    public sealed class IsInScreen : Determine
    {
        public override bool GetValue()
        {
            var position = transform.position;
            var camPoint = Camera.main.WorldToScreenPoint(position);
            // out of screen
            return camPoint.x >= 0 && camPoint.y >= 0 && camPoint.x <= Screen.width && camPoint.y <= Screen.height;
        }
    }
}
