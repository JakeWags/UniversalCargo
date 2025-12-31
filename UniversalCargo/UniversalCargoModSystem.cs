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

        // --- WHITELIST / BLACKLIST ---
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
        // -----------------------------

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

            // Subscribe to mouse button event
            api.Input.InWorldAction += OnClientInput;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;
            serverChannel = api.Network.GetChannel("universalcargonet");

            serverChannel.SetMessageHandler<SealRequestPacket>(OnSealRequest);
        }

        // --- CLIENT INPUT INTERCEPTOR ---
        private void OnClientInput(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            // Only handle UseHeldItemOverBlock action on keydown
            if (action != EnumEntityAction.RightMouseDown || !on) return;

            // Check for shift key
            if (!clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftLeft] &&
                !clientApi.Input.KeyboardKeyState[(int)GlKeys.ShiftRight]) return;

            // Item Check
            IClientPlayer player = clientApi.World.Player;
            ItemSlot handSlot = player.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty || handSlot.Itemstack.Collectible.Code.Path != "packingsupplies") return;

            // Target Check
            BlockSelection blockSel = player.CurrentBlockSelection;
            if (blockSel == null) return;

            Block block = clientApi.World.BlockAccessor.GetBlock(blockSel.Position);
            string blockCode = block.Code.ToString();

            // Validate & Send
            if (IsAllowed(blockCode))
            {
                clientChannel.SendPacket(new SealRequestPacket { Pos = blockSel.Position });

                // Prevent default handling
                handled = EnumHandling.PreventDefault;
            }
        }

        // --- SERVER LOGIC ---
        private void OnSealRequest(IServerPlayer player, SealRequestPacket packet)
        {
            // Security Checks
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
            TreeAttribute beData = new TreeAttribute();
            oldBe.ToTreeAttributes(beData);

            string originalBlockCode = world.BlockAccessor.GetBlock(pos).Code.ToString();

            Block sealedBlock = world.GetBlock(new AssetLocation("universalcargo:sealedcrate"));
            if (sealedBlock == null) return;

            world.BlockAccessor.SetBlock(sealedBlock.BlockId, pos);

            BlockEntitySealedCrate newBe = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySealedCrate;
            if (newBe != null)
            {
                newBe.SavedBlockEntityData = beData;
                newBe.OriginalBlockCode = originalBlockCode;
                newBe.MarkDirty(true);
            }

            world.PlaySoundAt(new AssetLocation("game:sounds/player/build/chest"), pos.X, pos.Y, pos.Z, null);

            packingSlot.TakeOut(1);
            packingSlot.MarkDirty();
        }
    }
}