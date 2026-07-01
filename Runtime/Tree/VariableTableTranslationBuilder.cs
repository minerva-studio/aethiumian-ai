#nullable enable

using Aethiumian.AI.Accessors;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI
{
    [Serializable]
    public class VariableTableTranslationBuilder : IDuplicable
    {
        public VariableTranslationTable.Entry[] entries = new VariableTranslationTable.Entry[0];
        public VariableTableTranslationBuilder() { }

        public VariableTranslationTable Build(VariableTable sourceTable)
        {
            return new VariableTranslationTable(sourceTable, entries);
        }

        public object Duplicate()
        {
            var newEntries = new VariableTranslationTable.Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                // value type, no need to duplicate
                newEntries[i] = entries[i];
            }
            return new VariableTableTranslationBuilder() { entries = newEntries };
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
