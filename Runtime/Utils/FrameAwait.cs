#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI.Utils
{
    public static class FrameAwait
    {
        public static NextFrameAwaitable NextFrameAsync() => default;

        public readonly struct NextFrameAwaitable
        {
            public Awaiter GetAwaiter() => new Awaiter(0);
        }

        public readonly struct Awaiter : INotifyCompletion
        {
#if UNITY_2023_1_OR_NEWER
            private readonly Awaitable.Awaiter unityAwaiter;
#endif
            private readonly YieldAwaitable.YieldAwaiter taskAwaiter;
            private readonly bool useTaskYield;

            public Awaiter(byte _)
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
                    : Awaitable.NextFrameAsync().GetAwaiter();
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
