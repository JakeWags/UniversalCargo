using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace UniversalCargo
{
    public class BlockEntitySealedCrate : BlockEntity
    {
        public TreeAttribute? SavedInventoryData;
        public string? OriginalBlockCode; // Store the block code for fallback
        public string? CrateType; // Store the crate type variant (pine, oak, birch, aged, etc.)

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (SavedInventoryData != null) tree["savedInventory"] = SavedInventoryData;
            if (OriginalBlockCode != null) tree.SetString("originalBlockCode", OriginalBlockCode);
            if (CrateType != null) tree.SetString("crateType", CrateType);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            SavedInventoryData = tree["savedInventory"] as TreeAttribute;
            OriginalBlockCode = tree.GetString("originalBlockCode");
            CrateType = tree.GetString("crateType");
        }
    }
}