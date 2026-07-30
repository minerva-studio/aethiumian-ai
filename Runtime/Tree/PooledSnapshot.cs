#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Aethiumian.AI
{
    /// <summary>
    /// A synchronous lease over a pooled collection snapshot.
    /// </summary>
    /// <typeparam name="T">The reference type captured in the snapshot.</typeparam>
    /// <remarks>
    /// Use this type only through a <c>using</c> statement or declaration, and do not copy the lease.
    /// It deliberately does not implement <see cref="IDisposable"/> because C# 9 does not allow
    /// interfaces on <c>ref struct</c> types.
    /// </remarks>
    internal ref struct PooledSnapshot<T> where T : class
    {
        private T[] buffer;

        /// <summary>
        /// Gets the number of captured entries.
        /// </summary>
        internal int Count { get; }

        /// <summary>
        /// Gets a captured entry by index.
        /// </summary>
        internal T this[int index] => buffer[index];

        private PooledSnapshot(T[] buffer, int count)
        {
            this.buffer = buffer;
            Count = count;
        }

        /// <summary>
        /// Captures the source collection into a pooled array.
        /// </summary>
        /// <param name="source">The collection to copy.</param>
        /// <returns>A lease that owns the captured array until disposal.</returns>
        internal static PooledSnapshot<T> Capture(ICollection<T> source)
        {
            int count = source.Count;
            if (count == 0)
            {
                return new PooledSnapshot<T>(Array.Empty<T>(), 0);
            }

            T[] buffer = ArrayPool<T>.Shared.Rent(count);
            try
            {
                source.CopyTo(buffer, 0);
                return new PooledSnapshot<T>(buffer, count);
            }
            catch
            {
                ArrayPool<T>.Shared.Return(buffer, clearArray: true);
                throw;
            }
        }

        /// <summary>
        /// Captures a stack into a pooled array while preserving its <see cref="Stack{T}.CopyTo"/> order.
        /// </summary>
        /// <param name="source">The stack to copy.</param>
        /// <returns>A lease that owns the captured array until disposal.</returns>
        internal static PooledSnapshot<T> Capture(Stack<T> source)
        {
            int count = source.Count;
            if (count == 0)
            {
                return new PooledSnapshot<T>(Array.Empty<T>(), 0);
            }

            T[] buffer = ArrayPool<T>.Shared.Rent(count);
            try
            {
                source.CopyTo(buffer, 0);
                return new PooledSnapshot<T>(buffer, count);
            }
            catch
            {
                ArrayPool<T>.Shared.Return(buffer, clearArray: true);
                throw;
            }
        }

        /// <summary>
        /// Returns the captured array to its pool.
        /// </summary>
        internal void Dispose()
        {
            T[] rented = buffer;
            buffer = null;
            if (rented?.Length > 0)
            {
                ArrayPool<T>.Shared.Return(rented, clearArray: true);
            }
        }
    }

}
