using UnityEngine;

namespace Amlos.AI.Test
{
    public class ScriptVariable : MonoBehaviour
    {
        public static bool globalValueBool;

        public Vector3 value1;

        public string value2;

        [SerializeField] int a;

        public int value3
        {
            get => a;
            set => a = value;
        }

        public int value4
        {
            get => a;
        }

        public int value5
        {
            set => a = value;
        }
    }
}