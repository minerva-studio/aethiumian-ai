using Aethiumian.AI.Variables;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aethiumian.AI
{
    /// <summary>
    /// Table of variables in a behaviour tree, allowing access by both name and UUID
    /// </summary>
    public class VariableTable : IEnumerable<RuntimeVariable>
    {
        private readonly IDictionary<UUID, RuntimeVariable> uuidVariables;
        private readonly IDictionary<string, UUID> nameToUUID;

        public int Count => uuidVariables.Count;

        public VariableTable() : this(false)
        {
        }

        public VariableTable(bool isLocal = false)
        {
            if (!isLocal)
            {
                uuidVariables = new ConcurrentDictionary<UUID, RuntimeVariable>();
                nameToUUID = new ConcurrentDictionary<string, UUID>();
            }
            else
            {
                uuidVariables = new Dictionary<UUID, RuntimeVariable>();
                nameToUUID = new Dictionary<string, UUID>();
            }
            uuidVariables[UUID.Empty] = null;
        }

        public RuntimeVariable this[string index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        public RuntimeVariable this[UUID index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        public RuntimeVariable Get(string name)
        {
            return uuidVariables[nameToUUID[name]];
        }

        public RuntimeVariable Get(UUID uuid)
        {
            return uuidVariables[uuid];
        }

        public void Set(UUID uuid, RuntimeVariable value)
        {
            if (value?.IsValid != true) return;
            uuidVariables[uuid] = value;
            nameToUUID[value.Name] = uuid;
        }

        public void Set(string name, RuntimeVariable value)
        {
            if (value?.IsValid != true) return;
            uuidVariables[nameToUUID[name]] = value;
        }

        public bool TryGetValue(UUID uuid, out RuntimeVariable variable)
        {
            return uuidVariables.TryGetValue(uuid, out variable);
        }

        public bool TryGetValue(string name, out RuntimeVariable variable)
        {
            if (!nameToUUID.TryGetValue(name, out var uuid))
            {
                variable = null;
                return false;
            }

            return uuidVariables.TryGetValue(uuid, out variable);
        }

        public VariableType? GetVariableType(string name)
        {
            if (TryGetValue(name, out var val))
            {
                return val.Type;
            }
            return null;

        }

        public VariableType? GetVariableType(UUID name)
        {
            if (TryGetValue(name, out var val))
            {
                return val.Type;
            }
            return null;
        }


        public IEnumerator<RuntimeVariable> GetEnumerator()
        {
            return uuidVariables.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
