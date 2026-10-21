using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.World.Harvest.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lighthouse.Workbench.Editor
{
    // Sets up carrying modules by hand (2026-10-20): the four boat module recipes (boat module decision 9,
    // play-test amounts) on the open scene's workbench catalog, and take/put back on that workbench.
    // Running it again only adds what is missing. The scene is not saved: look at it, then save it yourself.
    public static class ModuleCarrySetup
    {
        private const string ResourceFolder = "Assets/02.Prefabs/World/Data/MapGeneration/ResourceDefs/";
        private const string RecipeParent = "Assets/02.Prefabs/Lighthouse/Data";
        private const string RecipeFolder = RecipeParent + "/Boat";
        private const string BoatPrefabPath = "Assets/02.Prefabs/Player/Boat.prefab";

        // Lighthouse recipes use 1-4; boat recipes start at 101 so the two never collide.
        private static readonly BoatRecipeSpec[] BoatRecipes =
        {
            new BoatRecipeSpec(101, "Searchlight", "탐조등 모듈", BoatModuleType.Searchlight, ("Glass", 3), ("Iron", 2)),
            // The boat has no dash kind yet, so the recipe exists and installs are refused until Player adds one.
            new BoatRecipeSpec(102, "Dash", "대쉬 모듈", BoatModuleType.None, ("Iron", 3), ("Fiber", 3)),
            new BoatRecipeSpec(103, "ObstacleAlarm", "장애물 경보 모듈", BoatModuleType.DepthAlarm, ("Iron", 2), ("Glass", 1), ("Crystal", 1)),
            new BoatRecipeSpec(104, "Stabilizer", "선체 안정기 모듈", BoatModuleType.Stabilizer, ("Wood", 4), ("Iron", 3), ("Fiber", 2))
        };

        [MenuItem("Lighthouse/Modules/Set Up Module Carry In Open Scene")]
        public static void SetUp()
        {
            WorkbenchNet bench = Object.FindFirstObjectByType<WorkbenchNet>(FindObjectsInactive.Include);

            if (!bench || !bench.Catalog)
            {
                Debug.LogError($"[{nameof(ModuleCarrySetup)}] No {nameof(WorkbenchNet)} with a catalog in the open scene.");
                return;
            }

            ModuleRecipeCatalog catalog = bench.Catalog;
            int added = 0;

            for (int i = 0; i < BoatRecipes.Length; i++)
            {
                ModuleRecipe recipe = LoadOrCreate(BoatRecipes[i]);

                if (recipe && AddToCatalog(catalog, recipe))
                {
                    added++;
                }
            }

            AssetDatabase.SaveAssets();
            InteractionTarget target = bench.GetComponent<InteractionTarget>();
            AddSupportedAction(target, InteractionAction.TakeModule);
            AddSupportedAction(target, InteractionAction.ReturnModule);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            string error = catalog.Validate();
            Debug.Log($"[{nameof(ModuleCarrySetup)}] {catalog.name}: {added} boat recipes added. {bench.name} takes and returns modules. "
                + $"{(error == null ? "Catalog valid." : $"Catalog problem: {error}")} Save the scene to keep it.");
        }

        // Boat.prefab belongs to Player (최명기). Run only once that change is agreed: it lets the boat take InstallModule
        // requests, which is how a carried boat module reaches BoatModuleEquipment.
        [MenuItem("Lighthouse/Modules/Allow Module Install On Boat Prefab")]
        public static void AllowInstallOnBoat()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BoatPrefabPath);

            try
            {
                InteractionTarget target = root.GetComponentInChildren<InteractionTarget>(true);

                if (!target)
                {
                    Debug.LogError($"[{nameof(ModuleCarrySetup)}] {BoatPrefabPath} has no {nameof(InteractionTarget)}.");
                    return;
                }

                AddSupportedAction(target, InteractionAction.InstallModule);
                PrefabUtility.SaveAsPrefabAsset(root, BoatPrefabPath);
                Debug.Log($"[{nameof(ModuleCarrySetup)}] {BoatPrefabPath} now takes {InteractionAction.InstallModule}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ModuleRecipe LoadOrCreate(BoatRecipeSpec spec)
        {
            string path = $"{RecipeFolder}/ModuleRecipe_Boat{spec.AssetName}.asset";
            ModuleRecipe recipe = AssetDatabase.LoadAssetAtPath<ModuleRecipe>(path);

            // An existing asset keeps whatever was tuned on it.
            if (recipe)
            {
                return recipe;
            }

            if (!AssetDatabase.IsValidFolder(RecipeFolder))
            {
                AssetDatabase.CreateFolder(RecipeParent, "Boat");
            }

            recipe = ScriptableObject.CreateInstance<ModuleRecipe>();
            AssetDatabase.CreateAsset(recipe, path);
            SerializedObject serialized = new SerializedObject(recipe);
            serialized.FindProperty("_id").intValue = spec.Id;
            serialized.FindProperty("_displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("_maxExisting").intValue = 0;
            serialized.FindProperty("_isTestValue").boolValue = true;
            serialized.FindProperty("_isBoatModule").boolValue = true;
            serialized.FindProperty("_boatModuleType").intValue = (int)spec.BoatModuleType;
            SerializedProperty ingredients = serialized.FindProperty("_ingredients");
            ingredients.arraySize = spec.Ingredients.Length;

            for (int i = 0; i < spec.Ingredients.Length; i++)
            {
                (string resourceName, int amount) = spec.Ingredients[i];
                ResourceDefinition resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResourceFolder}Resource_{resourceName}.asset");

                if (!resource)
                {
                    Debug.LogError($"[{nameof(ModuleCarrySetup)}] Missing Resource_{resourceName}; {path} is left incomplete.");
                }

                SerializedProperty ingredient = ingredients.GetArrayElementAtIndex(i);
                ingredient.FindPropertyRelative("_resource").objectReferenceValue = resource;
                ingredient.FindPropertyRelative("_amount").intValue = amount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static bool AddToCatalog(ModuleRecipeCatalog catalog, ModuleRecipe recipe)
        {
            if (catalog.IndexOf(recipe.Id) >= 0)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty recipes = serialized.FindProperty("_recipes");
            recipes.arraySize++;
            recipes.GetArrayElementAtIndex(recipes.arraySize - 1).objectReferenceValue = recipe;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return true;
        }

        private static void AddSupportedAction(InteractionTarget target, InteractionAction action)
        {
            if (!target || target.Supports(action))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty actions = serialized.FindProperty("_supportedActions");
            actions.arraySize++;
            actions.GetArrayElementAtIndex(actions.arraySize - 1).intValue = (int)action;
            serialized.ApplyModifiedProperties();
        }

        private readonly struct BoatRecipeSpec
        {
            public readonly int Id;
            public readonly string AssetName;
            public readonly string DisplayName;
            public readonly BoatModuleType BoatModuleType;
            public readonly (string Resource, int Amount)[] Ingredients;

            public BoatRecipeSpec(int id, string assetName, string displayName, BoatModuleType boatModuleType,
                params (string Resource, int Amount)[] ingredients)
            {
                Id = id;
                AssetName = assetName;
                DisplayName = displayName;
                BoatModuleType = boatModuleType;
                Ingredients = ingredients;
            }
        }
    }
}
