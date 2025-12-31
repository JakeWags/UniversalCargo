using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace UniversalCargo
{
    public class BlockEntitySealedCrate : BlockEntity
    {
        public TreeAttribute? SavedInventoryData;
        public string? OriginalBlockCode;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (SavedInventoryData != null) tree["savedInventory"] = SavedInventoryData;
            if (OriginalBlockCode != null) tree.SetString("originalBlockCode", OriginalBlockCode);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            SavedInventoryData = tree["savedInventory"] as TreeAttribute;
            OriginalBlockCode = tree.GetString("originalBlockCode");
        }
    }
}