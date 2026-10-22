using System.Collections.Generic;
using Lighthouse.Modules.Rules;

namespace Lighthouse.Modules.Server
{
    // Server-only record of every module that exists in this match. The existence cap counts records,
    // so a module stays here from its first deposit until it is disassembled (JH05 calls Remove).
    // A module lying on a shore keeps its record but leaves the count (2026-10-22), and comes back in when picked up.
    // Ids are never reused: one match is one server run.
    public sealed class ModuleLedger
    {
        private readonly Dictionary<uint, ModuleRecord> _records = new Dictionary<uint, ModuleRecord>();
        private readonly Dictionary<int, int> _existingByRecipe = new Dictionary<int, int>();
        private uint _nextModuleId = 1;

        public int Count => _records.Count;

        // Modules of the recipe that count toward its cap, so shore modules are left out.
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

            if (IsCounted(state))
            {
                AddCount(recipeId, 1);
            }

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
            bool wasCounted = IsCounted(record.State);
            bool isCounted = IsCounted(state);

            if (wasCounted != isCounted)
            {
                AddCount(record.RecipeId, isCounted ? 1 : -1);
            }

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

            if (IsCounted(record.State))
            {
                AddCount(record.RecipeId, -1);
            }

            return true;
        }

        private static bool IsCounted(ModuleState state)
        {
            return state != ModuleState.OnGround;
        }

        private void AddCount(int recipeId, int delta)
        {
            int count = CountExisting(recipeId) + delta;

            if (count > 0)
            {
                _existingByRecipe[recipeId] = count;
            }
            else
            {
                _existingByRecipe.Remove(recipeId);
            }
        }
    }
}
