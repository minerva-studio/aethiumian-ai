using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aethiumian.AI.References
{
    /// <summary>
    /// Extension helpers for completing a <see cref="NodeProgress"/> from common async and Unity lifetime sources.
    /// </summary>
    public static class NodeProgressExtensions
    {
        /// <summary>
        /// Complete the node when the Unity object is destroyed.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="obj">The Unity object watched for destruction.</param>
        /// <param name="result">The result returned when the object is destroyed.</param>
        [CompletesAction]
        public static void CompleteWhenDestroyed(this NodeProgress progress, UnityEngine.Object obj, bool result = true)
        {
            progress.CompleteWhenDestroyed(obj, result);
        }

        /// <summary>
        /// Complete the node when the cancellation token is raised.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="token">The token watched for cancellation.</param>
        /// <param name="result">The result returned when the token is canceled.</param>
        [CompletesAction]
        public static void CompleteWhenCanceled(this NodeProgress progress, CancellationToken token, bool result = true)
        {
            progress.CompleteWhenCanceled(token, result);
        }

        /// <summary>
        /// Complete the node when the task completes.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="task">The task watched for completion.</param>
        /// <param name="result">The result returned when the task completes successfully.</param>
        /// <param name="canceledResult">The result returned when the task is canceled.</param>
        [CompletesAction]
        public static void CompleteWhenCompleted(this NodeProgress progress, Task task, bool result = true, bool canceledResult = false)
        {
            progress.CompleteWhenCompleted(task, result, canceledResult);
        }

        /// <summary>
        /// Complete the node when the boolean task completes.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="task">The task watched for completion.</param>
        /// <param name="canceledResult">The result returned when the task is canceled.</param>
        [CompletesAction]
        public static void CompleteWhenCompleted(this NodeProgress progress, Task<bool> task, bool canceledResult = false)
        {
            progress.CompleteWhenCompleted(task, canceledResult);
        }

        /// <summary>
        /// Complete the node when the condition becomes false.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="condition">The condition watched while it remains true.</param>
        /// <param name="result">The result returned when the condition becomes false.</param>
        [CompletesAction]
        public static void CompleteWhenFalse(this NodeProgress progress, Func<bool> condition, bool result = true)
        {
            progress.CompleteWhenFalse(condition, result);
        }

        /// <summary>
        /// Complete the node when the condition becomes false.
        /// </summary>
        /// <param name="progress">The node progress to complete.</param>
        /// <param name="condition">The condition watched while it remains true.</param>
        /// <param name="resultProvider">The result provider evaluated when the condition becomes false.</param>
        [CompletesAction]
        public static void CompleteWhenFalse(this NodeProgress progress, Func<bool> condition, Func<bool> resultProvider)
        {
            progress.CompleteWhenFalse(condition, resultProvider);
        }
    }
}
