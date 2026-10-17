namespace Lighthouse.Modules.Rules
{
    public readonly struct ModuleRecord
    {
        public readonly uint ModuleId;
        public readonly int RecipeId;
        public readonly ModuleState State;

        public ModuleRecord(uint moduleId, int recipeId, ModuleState state)
        {
            ModuleId = moduleId;
            RecipeId = recipeId;
            State = state;
        }

        public ModuleRecord WithState(ModuleState state)
        {
            return new ModuleRecord(ModuleId, RecipeId, state);
        }
    }
}
