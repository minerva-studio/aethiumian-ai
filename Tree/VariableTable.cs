using Minerva.Module;
using System.Collections;
using System.Collections.Generic;

namespace Amlos.AI
{
    public class VariableTable : IEnumerable<Variable>
    {
        private readonly Dictionary<UUID, Variable> uuidVariables;
        private readonly Dictionary<string, UUID> nameToUUID;

        public int Count => uuidVariables.Count;

        public VariableTable()
        {
            nameToUUID = new Dictionary<string, UUID>();
            uuidVariables = new Dictionary<UUID, Variable>();
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

        public void Set(string name, Variable value)
        {
            if (value == null) return;
            uuidVariables[nameToUUID[name]] = value;
        }

        public void Set(UUID uuid, Variable value)
        {
            if (value == null) return;
            uuidVariables[uuid] = value;
            nameToUUID[value.Name] = uuid;
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
