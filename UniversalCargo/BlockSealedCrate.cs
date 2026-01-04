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
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                BlockEntitySealedCrate? be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySealedCrate;
                ItemStack drop = new ItemStack(this);

                if (be != null)
                {
                    if (be.SavedInventoryData != null)
                    {
                        drop.Attributes["savedInventory"] = be.SavedInventoryData.Clone();
                    }
                    if (be.OriginalBlockCode != null)
                    {
                        drop.Attributes.SetString("originalBlockCode", be.OriginalBlockCode);
                    }
                    if (be.CrateType != null)
                    {
                        drop.Attributes.SetString("crateType", be.CrateType);
                    }
                }

                world.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

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
                be.CrateType = byItemStack.Attributes.GetString("crateType");
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
                world.Logger.Warning("[UniversalCargo] Unsealing failed: No block entity or saved data");
                return;
            }

            string crateType = be.CrateType ?? "wood-aged";
            TreeAttribute savedData = be.SavedInventoryData.Clone() as TreeAttribute;

            // Get the base crate block
            Block crateBlock = world.GetBlock(new AssetLocation("game:crate"));
            if (crateBlock == null)
            {
                world.Logger.Error("[UniversalCargo] Could not find crate block!");
                return;
            }

            // Place the crate block
            world.BlockAccessor.SetBlock(crateBlock.BlockId, pos);

            BlockEntityCrate? crateEntity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrate;
            if (crateEntity == null)
            {
                world.Logger.Error("[UniversalCargo] Failed to get crate block entity!");
                return;
            }

            try
            {
                // Restore the type field
                crateEntity.type = crateType;

                // Restore inventory
                if (savedData.HasAttribute("inventory") && crateEntity.Inventory is InventoryBase invBase)
                {
                    TreeAttribute? inventoryData = savedData["inventory"] as TreeAttribute;
                    if (inventoryData != null)
                    {
                        invBase.FromTreeAttributes(inventoryData);
                    }
                }

                // Mark dirty to trigger visual update
                crateEntity.MarkDirty(true);
            }
            catch (System.Exception ex)
            {
                world.Logger.Error($"[UniversalCargo] Exception during unsealing: {ex.Message}");
                world.Logger.Error($"[UniversalCargo] Stack trace: {ex.StackTrace}");
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