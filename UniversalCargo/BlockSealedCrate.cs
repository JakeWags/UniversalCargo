using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace UniversalCargo
{
    public class BlockSealedCrate : Block
    {
        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // Return empty array - we handle dropping in OnBlockBroken
            return new ItemStack[0];
        }

        // Handle breaking with NBT data preservation
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            // Only drop items on server side and not in creative mode
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                BlockEntitySealedCrate? be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySealedCrate;
                ItemStack drop = new ItemStack(this);

                if (be != null)
                {
                    // Clone the data to ensure it persists in the item
                    if (be.SavedInventoryData != null)
                    {
                        drop.Attributes["savedInventory"] = be.SavedInventoryData.Clone();
                    }

                    if (be.OriginalBlockCode != null)
                    {
                        drop.Attributes.SetString("originalBlockCode", be.OriginalBlockCode);
                    }
                }

                world.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            // Call base to handle block removal, sounds, etc. - but AFTER we've spawned our item
            world.BlockAccessor.SetBlock(0, pos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
        }

        // Handle placing the block and restoring data from item to block entity
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            BlockEntitySealedCrate? be = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntitySealedCrate;
            if (be != null && byItemStack?.Attributes != null)
            {
                if (byItemStack.Attributes.HasAttribute("savedInventory"))
                {
                    be.SavedInventoryData = (byItemStack.Attributes["savedInventory"] as TreeAttribute)?.Clone() as TreeAttribute;
                }

                be.OriginalBlockCode = byItemStack.Attributes.GetString("originalBlockCode");
                be.MarkDirty(true);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack?.Collectible is ItemAxe)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    UnsealCrate(world, blockSel.Position);
                }
                return true;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        private void UnsealCrate(IWorldAccessor world, BlockPos pos)
        {
            BlockEntitySealedCrate? be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySealedCrate;

            if (be == null || be.SavedInventoryData == null)
            {
                return;
            }

            string originalCode = be.OriginalBlockCode ?? "game:crate-oak-north";

            // Get the saved inventory data
            TreeAttribute savedInventory = be.SavedInventoryData;

            Block? originalBlock = world.GetBlock(new AssetLocation(originalCode));
            if (originalBlock == null) return;

            // Swap the block - this creates a fresh, new block entity with correct position
            world.BlockAccessor.SetBlock(originalBlock.BlockId, pos);

            // IMMEDIATELY restore ONLY the inventory to the fresh block entity
            BlockEntity? restoredBe = world.BlockAccessor.GetBlockEntity(pos);
            if (restoredBe != null && restoredBe is IBlockEntityContainer container)
            {
                if (container.Inventory is InventoryBase invBase)
                {
                    invBase.FromTreeAttributes(savedInventory);
                    restoredBe.MarkDirty(true);
                }
            }

            world.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), pos.X, pos.Y, pos.Z, null);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            Item? axeItem = world.GetItem(new AssetLocation("game:axe-copper"));
            ItemStack? axeStack = axeItem != null ? new ItemStack(axeItem) : null;
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "universalcargo:blockhelp-unseal-axe",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = axeStack != null ? new ItemStack[] { axeStack } : null
                }
            };
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack?.Attributes != null && inSlot.Itemstack.Attributes.HasAttribute("savedInventory"))
            {
                string? originalCode = inSlot.Itemstack.Attributes.GetString("originalBlockCode");
                if (!string.IsNullOrEmpty(originalCode))
                {
                    dsc.AppendLine("Sealed: " + originalCode);
                }
                dsc.AppendLine("Contains stored items");
            }
        }
    }
}