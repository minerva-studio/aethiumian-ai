using Amlos.AI.Variables;
using Minerva.Module;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Amlos.AI
{
    /// <summary>
    /// Table of variables in a behaviour tree, allowing access by both name and UUID
    /// </summary>
    public class VariableTable : IEnumerable<Variable>
    {
        private readonly IDictionary<UUID, Variable> uuidVariables;
        private readonly IDictionary<string, UUID> nameToUUID;

        public int Count => uuidVariables.Count;

        public VariableTable() : this(false)
        {
        }

        public VariableTable(bool isLocal = false)
        {
            if (!isLocal)
            {
                uuidVariables = new ConcurrentDictionary<UUID, Variable>();
                nameToUUID = new ConcurrentDictionary<string, UUID>();
            }
            else
            {
                uuidVariables = new Dictionary<UUID, Variable>();
                nameToUUID = new Dictionary<string, UUID>();
            }
            uuidVariables[UUID.Empty] = null;
        }

        public Variable this[string index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        public Variable this[UUID index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        public Variable Get(string name)
        {
            return uuidVariables[nameToUUID[name]];
        }

        public Variable Get(UUID uuid)
        {
            return uuidVariables[uuid];
        }

        public void Set(UUID uuid, Variable value)
        {
            if (value?.IsValid != true) return;
            uuidVariables[uuid] = value;
            nameToUUID[value.Name] = uuid;
        }

        public void Set(string name, Variable value)
        {
            if (value?.IsValid != true) return;
            uuidVariables[nameToUUID[name]] = value;
        }

        public bool TryGetValue(UUID uuid, out Variable variable)
        {
            return uuidVariables.TryGetValue(uuid, out variable);
        }

        public bool TryGetValue(string name, out Variable variable)
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


        public IEnumerator<Variable> GetEnumerator()
        {
            return uuidVariables.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
