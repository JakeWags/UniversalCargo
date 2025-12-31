using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace UniversalCargo
{
    public class ItemPackingSupplies : Item
    {
        // Logic handled in UniversalCargoModSystem.cs via Input Interception

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // We let the Event Bus handle the logic. 
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "universalcargo:heldhelp-seal",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                }
            };
        }
    }
}