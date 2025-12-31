using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace UniversalCargo
{
    public class BlockEntitySealedCrate : BlockEntity
    {
        public TreeAttribute SavedBlockEntityData; // Changed from SavedInventory
        public string OriginalBlockCode;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (SavedBlockEntityData != null) tree["savedData"] = SavedBlockEntityData;
            if (OriginalBlockCode != null) tree.SetString("originalBlockCode", OriginalBlockCode);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            SavedBlockEntityData = tree["savedData"] as TreeAttribute;
            OriginalBlockCode = tree.GetString("originalBlockCode");
        }
    }
}