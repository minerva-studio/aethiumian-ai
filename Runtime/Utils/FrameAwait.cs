#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI.Utils
{
    public static class FrameAwait
    {
        /// <summary>
        /// Awaits the next frame and observes the supplied cancellation token.
        /// </summary>
        /// <param name="cancellationToken">Token that cancels the frame wait.</param>
        public static NextFrameAwaitable NextFrameAsync(CancellationToken cancellationToken = default) => new(cancellationToken);

        public readonly struct NextFrameAwaitable
        {
            private readonly CancellationToken cancellationToken;

            public NextFrameAwaitable(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            public Awaiter GetAwaiter() => new Awaiter(cancellationToken);
        }

        public readonly struct Awaiter : INotifyCompletion
        {
#if UNITY_2023_1_OR_NEWER
            private readonly Awaitable.Awaiter unityAwaiter;
#endif
            private readonly YieldAwaitable.YieldAwaiter taskAwaiter;
            private readonly bool useTaskYield;

            public Awaiter(CancellationToken cancellationToken)
            {
#if UNITY_2023_1_OR_NEWER
#if UNITY_EDITOR
                useTaskYield = !Application.isPlaying;
#else
                useTaskYield = false;
#endif
#else
                useTaskYield = true;
#endif

#if UNITY_2023_1_OR_NEWER
                unityAwaiter = useTaskYield
                    ? default
                    : Awaitable.NextFrameAsync(cancellationToken).GetAwaiter();
#endif

                taskAwaiter = useTaskYield
                    ? Task.Yield().GetAwaiter()
                    : default;
            }

            public bool IsCompleted
            {
                get
                {
#if UNITY_2023_1_OR_NEWER
                    return useTaskYield
                        ? taskAwaiter.IsCompleted
                        : unityAwaiter.IsCompleted;
#else
                    return taskAwaiter.IsCompleted;
#endif
                }
            }

            public void OnCompleted(Action continuation)
            {
#if UNITY_2023_1_OR_NEWER
                if (useTaskYield)
                {
                    taskAwaiter.OnCompleted(continuation);
                }
                else
                {
                    unityAwaiter.OnCompleted(continuation);
                }
#else
                taskAwaiter.OnCompleted(continuation);
#endif
            }

            public void GetResult()
            {
#if UNITY_2023_1_OR_NEWER
                if (useTaskYield)
                {
                    taskAwaiter.GetResult();
                }
                else
                {
                    unityAwaiter.GetResult();
                }
#else
                taskAwaiter.GetResult();
#endif
            }
        }
    }
}
