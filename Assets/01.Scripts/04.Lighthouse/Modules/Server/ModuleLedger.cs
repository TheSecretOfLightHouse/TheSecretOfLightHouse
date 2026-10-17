using System.Collections.Generic;
using Lighthouse.Modules.Rules;

namespace Lighthouse.Modules.Server
{
    // Server-only record of every module that exists in this match. The existence cap counts records,
    // so a module stays here from its first deposit until it is disassembled (JH05 calls Remove).
    // Ids are never reused: one match is one server run.
    public sealed class ModuleLedger
    {
        private readonly Dictionary<uint, ModuleRecord> _records = new Dictionary<uint, ModuleRecord>();
        private readonly Dictionary<int, int> _existingByRecipe = new Dictionary<int, int>();
        private uint _nextModuleId = 1;

        public int Count => _records.Count;

        public int CountExisting(int recipeId)
        {
            return _existingByRecipe.TryGetValue(recipeId, out int count) ? count : 0;
        }

        public bool CanCreate(int recipeId, int maxExisting)
        {
            return maxExisting <= 0 || CountExisting(recipeId) < maxExisting;
        }

        public uint Create(int recipeId, ModuleState state)
        {
            uint moduleId = _nextModuleId++;
            _records.Add(moduleId, new ModuleRecord(moduleId, recipeId, state));
            _existingByRecipe[recipeId] = CountExisting(recipeId) + 1;
            return moduleId;
        }

        public bool TryGet(uint moduleId, out ModuleRecord record)
        {
            return _records.TryGetValue(moduleId, out record);
        }

        public bool TrySetState(uint moduleId, ModuleState state)
        {
            if (state == ModuleState.None || !_records.TryGetValue(moduleId, out ModuleRecord record))
            {
                return false;
            }

            _records[moduleId] = record.WithState(state);
            return true;
        }

        // Disassembly is the only way a module stops existing, which reopens its recipe.
        public bool Remove(uint moduleId)
        {
            if (!_records.TryGetValue(moduleId, out ModuleRecord record))
            {
                return false;
            }

            _records.Remove(moduleId);
            int remaining = CountExisting(record.RecipeId) - 1;

            if (remaining > 0)
            {
                _existingByRecipe[record.RecipeId] = remaining;
            }
            else
            {
                _existingByRecipe.Remove(record.RecipeId);
            }

            return true;
        }
    }
}
