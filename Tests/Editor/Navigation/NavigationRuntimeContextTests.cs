using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies borrowed selection, invalid registration, and play-session reset.</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class NavigationRuntimeContextTests
    {
        private MapNavigationRuntime previous;

        [SetUp]
        public void SaveCurrent()
        {
            previous = NavigationRuntimeContext.Current;
            NavigationRuntimeContext.ClearCurrent(previous);
        }

        [TearDown]
        public void RestoreCurrent()
        {
            NavigationRuntimeContext.ClearCurrent(NavigationRuntimeContext.Current);
            if (previous != null && !previous.IsDisposed) NavigationRuntimeContext.SetCurrent(previous);
        }

        [Test]
        public void ReplacingUnpublishedRuntimeDoesNotDisposeOrLetOldOwnerClearReplacement()
        {
            using MapNavigationRuntime first = new(8, 128, 128);
            using MapNavigationRuntime second = new(8, 128, 128);
            NavigationRuntimeContext.SetCurrent(first);
            Assert.That(first.IsReady, Is.False);
            Assert.That(NavigationRuntimeContext.Current, Is.SameAs(first));
            NavigationRuntimeContext.SetCurrent(second);
            NavigationRuntimeContext.ClearCurrent(first);
            Assert.That(NavigationRuntimeContext.Current, Is.SameAs(second));
            Assert.That(first.IsDisposed, Is.False);
            NavigationRuntimeContext.ClearCurrent(second);
            Assert.That(NavigationRuntimeContext.Current, Is.Null);
            Assert.That(second.IsDisposed, Is.False);
        }

        [Test]
        public void InvalidRegistrationPreservesCurrentRuntime()
        {
            using MapNavigationRuntime current = new(8, 128, 128);
            using MapNavigationRuntime disposed = new(8, 128, 128);
            disposed.Dispose();
            NavigationRuntimeContext.SetCurrent(current);
            Assert.Throws<ArgumentNullException>(() => NavigationRuntimeContext.SetCurrent(null));
            Assert.Throws<ObjectDisposedException>(() => NavigationRuntimeContext.SetCurrent(disposed));
            Assert.That(NavigationRuntimeContext.Current, Is.SameAs(current));
        }

        [Test]
        public void SubsystemRegistrationClearsReferenceWithoutDisposingBorrowedRuntime()
        {
            using MapNavigationRuntime runtime = new(8, 128, 128);
            NavigationRuntimeContext.SetCurrent(runtime);
            MethodInfo initialize = typeof(NavigationRuntimeContext)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType
                    == RuntimeInitializeLoadType.SubsystemRegistration);
            initialize.Invoke(null, null);
            Assert.That(NavigationRuntimeContext.Current, Is.Null);
            Assert.That(runtime.IsDisposed, Is.False);
        }
    }
}
