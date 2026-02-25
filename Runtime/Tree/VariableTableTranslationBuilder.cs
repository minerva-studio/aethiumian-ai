#nullable enable

using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI
{
    [Serializable]
    public class VariableTableTranslationBuilder
    {
        public VariableTranslationTable.Entry[] entries = new VariableTranslationTable.Entry[0];
        public VariableTableTranslationBuilder() { }

        public VariableTranslationTable Build(VariableTable sourceTable)
        {
            return new VariableTranslationTable(sourceTable, entries);
        }
    }

    public sealed class VariableTranslationTable
    {
        public static readonly VariableTranslationTable Empty = new VariableTranslationTable(new VariableTable(), Array.Empty<Entry>());

        [Serializable]
        public struct Entry
        {
            public UUID from;
            public UUID to;
        }

        private readonly IReadOnlyDictionary<UUID, UUID> translationDict;
        private readonly VariableTable table;

        public VariableTranslationTable(VariableTable variables, Entry[] entries)
        {
            this.table = variables;
            var dictionary = new Dictionary<UUID, UUID>();
            foreach (var entry in entries)
            {
                dictionary[entry.from] = entry.to;
            }
            this.translationDict = dictionary;
        }

        public Variable? GetVariable(UUID uuid)
        {
            if (translationDict.TryGetValue(uuid, out var to))
            {
                return table.Get(to);
            }
            return null;
        }
    }
}
