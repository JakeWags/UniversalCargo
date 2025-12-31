using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace UniversalCargo
{
    [ProtoContract]
    public class SealRequestPacket
    {
        [ProtoMember(1)]
        public BlockPos Pos;
    }

    public class UniversalCargoModSystem : ModSystem
    {
        private ICoreClientAPI clientApi;
        private ICoreServerAPI serverApi;
        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        private readonly List<string> Whitelist = new List<string>
        {
            "*:crate-*",
            "*:*crate*",
            "*:basket-*",
            "*:storagevessel-*",
            "*:vessel-*",
            "*:labeledchest-*",
            "*:chest-*-labeled-*"
        };

        private readonly List<string> Blacklist = new List<string>
        {
            "*:*chest*",
            "*:*trunk*",
            "*:*treasure*",
        };

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemPackingSupplies", typeof(ItemPackingSupplies));
            api.RegisterBlockClass("BlockSealedCrate", typeof(BlockSealedCrate));
            api.RegisterBlockEntityClass("BlockEntitySealedCrate", typeof(BlockEntitySealedCrate));

            api.Network.RegisterChannel("universalcargonet")
                .RegisterMessageType(typeof(SealRequestPacket));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;
            clientChannel = api.Network.GetChannel("universalcargonet");
            api.Input.InWorldAction += OnClientInput;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;
            serverChannel = api.Network.GetChannel("universalcargonet");
            serverChannel.SetMessageHandler<SealRequestPacket>(OnSealRequest);
        }

        private void OnClientInput(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action != EnumEntityAction.RightMouseDown || !on) return;
            if (!clientApi.Input.MouseGrabbed) return;

            if (!clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftLeft] &&
                !clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftRight]) return;

            IClientPlayer player = clientApi.World.Player;
            ItemSlot handSlot = player.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty || handSlot.Itemstack.Collectible.Code.Path != "packingsupplies") return;

            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return;

            Block block = clientApi.World.BlockAccessor.GetBlock(blockSel.Position);
            if (IsAllowed(block.Code.ToString()))
            {
                clientChannel.SendPacket(new SealRequestPacket { Pos = blockSel.Position });
                handled = EnumHandling.PreventDefault;
            }
        }

        private void OnSealRequest(IServerPlayer player, SealRequestPacket packet)
        {
            if (!player.Entity.Controls.Sneak) return;

            ItemSlot handSlot = player.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty || handSlot.Itemstack.Collectible.Code.Path != "packingsupplies") return;

            BlockPos pos = packet.Pos;
            Block block = serverApi.World.BlockAccessor.GetBlock(pos);

            if (!IsAllowed(block.Code.ToString())) return;

            BlockEntity be = serverApi.World.BlockAccessor.GetBlockEntity(pos);
            if (be is IBlockEntityContainer)
            {
                SealContainer(serverApi.World, pos, be, player, handSlot);
            }
        }

        private bool IsAllowed(string blockCode)
        {
            foreach (string s in Blacklist)
                if (WildcardUtil.Match(s, blockCode)) return false;

            foreach (string s in Whitelist)
                if (WildcardUtil.Match(s, blockCode)) return true;

            return false;
        }

        private void SealContainer(IWorldAccessor world, BlockPos pos, BlockEntity oldBe, IServerPlayer player, ItemSlot packingSlot)
        {
            TreeAttribute fullBeData = new TreeAttribute();
            oldBe.ToTreeAttributes(fullBeData);

            string crateType = fullBeData.GetString("type", "aged");

            Block originalBlock = world.BlockAccessor.GetBlock(pos);
            string originalBlockCode = originalBlock.Code.ToString();

            world.Logger.Event($"[UniversalCargo] Sealing: Code='{originalBlockCode}', Type='{crateType}'");
            (world.Api as ICoreServerAPI)?.SendMessage(player, 0, $"Sealed: {crateType} crate", EnumChatType.Notification);

            Block sealedBlock = world.GetBlock(new AssetLocation("universalcargo:sealedcrate"));
            if (sealedBlock == null)
            {
                world.Logger.Error("[UniversalCargo] Could not find sealedcrate block!");
                return;
            }

            world.BlockAccessor.SetBlock(sealedBlock.BlockId, pos);

            BlockEntitySealedCrate newBe = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySealedCrate;
            if (newBe != null)
            {
                newBe.SavedInventoryData = fullBeData;
                newBe.OriginalBlockCode = originalBlockCode;
                newBe.CrateType = crateType;
                newBe.MarkDirty(true);

                world.Logger.Event($"[UniversalCargo] Saved: Type={newBe.CrateType}");
            }
            else
            {
                world.Logger.Error("[UniversalCargo] Failed to get sealed crate block entity!");
            }

            world.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), pos.X, pos.Y, pos.Z, null);

            packingSlot.TakeOut(1);
            packingSlot.MarkDirty();
        }
    }
}