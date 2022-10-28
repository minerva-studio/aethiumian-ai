using UnityEngine;

namespace Amlos.AI
{
    [DoNotRelease]
    public class SetRigidbody2D : Call
    {
        Rigidbody2D rb => gameObject.GetComponent<Rigidbody2D>();

        public override void Execute()
        {
            throw new System.NotImplementedException();
        }
    }
}