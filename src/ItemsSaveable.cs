namespace Cthangover.Mods.Items
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The items owner's existing slot retains ordinary ID/count stacks and recipes. Optional occurrence
    /// descriptors use a separate versioned blob in this same slot; absent item/domain providers never erase
    /// their identities or convert them to fungible counts. All load candidates are parsed before publication.
    /// </summary>
    public class ItemsSaveable : IModSaveable
    {
        private const string InventoryBlob = "Inventory";
        private const string RecipesBlob = "Recipes";
        private const string InstancesBlob = "Instances";

        /// <summary>Writes ordinary stock once and retains unresolved unique descriptors and receipts in their own blob.</summary>
        public void OnSave(ModSlot slot)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
            {
                return;
            }

            var saved = new List<CItem>();
            foreach (var container in runtime.Inventory.Items)
            {
                if (container is not InventoryInstanceContainer && container.Item != null)
                {
                    saved.Add(new CItem
                    {
                        ID = container.Item.ID,
                        Count = container.Count
                    });
                }
            }

            if (runtime.Inventory is Inventory inventory)
            {
                saved.AddRange(inventory.UnresolvedItems);
                var identities = inventory.CaptureInstances();
                identities.Validate();
                if (identities.Records.Count > 0 || identities.Receipts.Count > 0 || identities.Extra.Count > 0)
                {
                    slot.Data[InstancesBlob] = new DataBlob(1, identities);
                }
            }

            slot.Data[InventoryBlob] = new DataBlob(1, saved);
            slot.Data[RecipesBlob] = new DataBlob(1, new List<string>(runtime.RecipeData.Data));
        }

        /// <summary>
        /// Restores detached ordinary and occurrence candidates together. A malformed identity blob refuses the
        /// slot before stock changes, allowing the normal dispatcher to retain it dormant independently of scene3d.
        /// </summary>
        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            errorMessage = null;
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null || slot?.Data == null)
            {
                return true;
            }

            try
            {
                var hasStock = slot.Data.TryGetValue(InventoryBlob, out var stockBlob);
                var hasInstances = slot.Data.TryGetValue(InstancesBlob, out var identityBlob);
                InventoryInstanceState identities = null;
                if (hasInstances)
                {
                    if (identityBlob.Version != 1 || runtime.Inventory is not Inventory)
                    {
                        throw new ArgumentException("Unsupported inventory occurrence owner or version");
                    }

                    identities = identityBlob.ReadJson<InventoryInstanceState>()
                        ?? throw new ArgumentException("Missing inventory occurrence data");
                    identities.Validate();
                }
                else if (hasStock)
                {
                    identities = new InventoryInstanceState();
                }

                List<IItemContainer> containers = null;
                var unresolved = new List<CItem>();
                if (hasStock)
                {
                    containers = new List<IItemContainer>();
                    foreach (var saved in stockBlob.ReadJson<List<CItem>>() ?? new List<CItem>())
                    {
                        var item = ItemFactory.Instance.Get(saved.ID);
                        if (item != null)
                        {
                            containers.Add(new ItemContainer
                            {
                                Item = item,
                                Count = saved.Count
                            });
                        }
                        else
                        {
                            GameLogger.Log("INVENTORY", $"saved item '{saved.ID}' x{saved.Count} is not defined by any loaded mod; "
                                + "kept in the save", LogLevel.Warning);
                            unresolved.Add(saved);
                        }
                    }
                }

                HashSet<string> recipes = null;
                if (slot.Data.TryGetValue(RecipesBlob, out var recipeBlob))
                {
                    recipes = new HashSet<string>(recipeBlob.ReadJson<List<string>>() ?? new List<string>());
                }

                if (runtime.Inventory is Inventory inventory && identities != null)
                {
                    inventory.RestoreInstances(identities, containers);
                }
                else if (containers != null)
                {
                    runtime.Inventory.Items = containers;
                }

                if (hasStock && runtime.Inventory is Inventory owner)
                {
                    owner.UnresolvedItems.Clear();
                    owner.UnresolvedItems.AddRange(unresolved);
                }

                if (recipes != null)
                {
                    runtime.RecipeData.Data = recipes;
                }

                return true;
            }
            catch (Exception error)
            {
                errorMessage = error.Message;
                return false;
            }
        }
    }
}
