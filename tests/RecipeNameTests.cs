namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Testing;

    /// <summary>A recipe without its own <c>Name</c> is named after its first output item (plan C4).</summary>
    public class RecipeNameTests
    {
        [ModTest("recipes/name_defaults_to_first_output_item")]
        static void NameDefaultsToFirstOutputItem(ModTestContext ctx)
        {
            var names = new Dictionary<string, string> { ["probe/salve"] = "probe/item/salve/name", ["probe/rag"] = "probe/item/rag/name" };
            string Lookup(string id) => names.TryGetValue(id, out var key) ? key : null;
            var output = new List<IngredientInfo>
            {
                new IngredientInfo { ID = "probe/salve", Count = 1 },
                new IngredientInfo { ID = "probe/rag", Count = 1 },
            };

            ModAssert.Equal("probe/item/salve/name", RecipeFactory.ResolveName(null, output, Lookup), "no Name: the first output item's name");
            ModAssert.Equal("probe/item/salve/name", RecipeFactory.ResolveName("", output, Lookup), "an empty Name counts as none");
            ModAssert.Equal("probe/own/name", RecipeFactory.ResolveName("probe/own/name", output, Lookup), "an own Name wins");
            ModAssert.True(RecipeFactory.ResolveName(null, new List<IngredientInfo>(), Lookup) == null, "no output: no name");
            ModAssert.True(RecipeFactory.ResolveName(null, new List<IngredientInfo> { new IngredientInfo { ID = "probe/unknown", Count = 1 } }, Lookup) == null,
                "an output item without a name key gives no name");
        }
    }
}
