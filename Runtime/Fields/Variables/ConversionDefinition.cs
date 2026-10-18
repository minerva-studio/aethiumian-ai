using System;

namespace Aethiumian.AI.Variables
{
    /// <summary>Owns the matching, executable binding, and authoring explanation of one conversion or conversion family.</summary>
    /// <remarks>Definitions are immutable registry-owned instances. Use ConversionRegistry.Find to select a definition before requesting its explanation.</remarks>
    public abstract class ConversionDefinition
    {
        /// <summary>Checks whether this definition applies to the supplied closed CLR types.</summary>
        /// <param name="source">The non-null source type.</param>
        /// <param name="target">The non-null target type.</param>
        /// <returns>Whether this definition can bind the requested pair; individual values may still fail conversion.</returns>
        public abstract bool Matches(Type source, Type target);

        /// <summary>Returns the short authoring explanation for a matching pair, prioritizing its result or the condition that can fail.</summary>
        /// <param name="source">The source type for which this definition was selected.</param>
        /// <param name="target">The target type for which this definition was selected.</param>
        /// <returns>One short consequence-focused sentence; runtime value conversion does not request it.</returns>
        public abstract string Describe(Type source, Type target);

        /// <summary>Binds the executable delegate after the registry has established that this definition matches the generic pair.</summary>
        internal abstract ConversionRule<TSource, TTarget> Bind<TSource, TTarget>();
    }

    /// <summary>Keeps an exact generic pair, its strongly typed operation, and its explanation in one immutable definition.</summary>
    internal sealed class ConversionDefinition<TSource, TTarget> : ConversionDefinition
    {
        private readonly TryConversion<TSource, TTarget> converter;
        private readonly string description;

        /// <summary>Declares an exact conversion whose matching types are supplied by its generic arguments.</summary>
        internal ConversionDefinition(TryConversion<TSource, TTarget> converter, string description)
        {
            this.converter = converter;
            this.description = description;
        }

        /// <inheritdoc/>
        public override bool Matches(Type source, Type target) => source == typeof(TSource) && target == typeof(TTarget);

        /// <inheritdoc/>
        public override string Describe(Type source, Type target) => description;

        /// <inheritdoc/>
        internal override ConversionRule<TReadSource, TReadTarget> Bind<TReadSource, TReadTarget>()
        {
            // The definition owns both matching types and the typed delegate; the registry binds only its exact match.
            return new ConversionRule<TReadSource, TReadTarget>((TryConversion<TReadSource, TReadTarget>)(Delegate)converter);
        }
    }
}
