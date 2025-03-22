using Amlos.AI.References;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Test
{
    public class ActionTest : MonoBehaviour
    {
        public async Task TaskAction(NodeProgress progress)
        {
            await Awaitable.NextFrameAsync();
            Debug.Log("action 1");
            await Task.Delay(500);
        }

        public async Task<int> TaskReturnAction(NodeProgress progress)
        {
            await Awaitable.NextFrameAsync();
            Debug.Log("action 2");
            await Task.Delay(500);
            return 1;
        }

        public async Task TaskActionWithParam(NodeProgress progress, int p)
        {
            await Awaitable.NextFrameAsync();
            Debug.Log("action 3 with p val " + p);
            await Task.Delay(500);
        }

        public async Task<int> TaskReturnActionWithParam(NodeProgress progress, int p)
        {
            await Awaitable.NextFrameAsync();
            Debug.Log("action 4 with p val " + p);
            await Task.Delay(500);
            return p;
        }

        public IEnumerator CoroutineAction(NodeProgress progress)
        {
            yield return null;
            Debug.Log("action 5");
            yield return new WaitForSeconds(0.5f);
        }

        public IEnumerator CoroutineActionWithParam(NodeProgress progress, int p)
        {
            yield return null;
            Debug.Log("action 6 with p val " + p);
            yield return new WaitForSeconds(0.5f);
        }
    }
}