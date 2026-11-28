using Client.MirScenes;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;
using Client.MirGraphics;
using S = ServerPackets;

namespace Client.MirObjects
{
    public partial class UserObject : PlayerObject
    {
        public void GetMaxGain(UserItem item)
        {
            int freeSpace = FreeSpace(Inventory);

            if (freeSpace > 0)
            {
                return;
            }

            ushort canGain = 0;

            foreach (UserItem inventoryItem in Inventory)
            {
                if (inventoryItem.Info != item.Info)
                {
                    continue;
                }

                int availableStack = (int)Math.Min(inventoryItem.Info.StackSize, int.MaxValue) - inventoryItem.Count;

                if (availableStack == 0)
                {
                    continue;
                }

                canGain += (ushort)availableStack;

                if (canGain >= item.Count)
                {
                    return;
                }
            }

            if (canGain == 0)
            {
                item.Count = 0;
                return;
            }

            item.Count = canGain;
        }

        private int FreeSpace(UserItem[] array)
        {
            int freeSlots = 0;

            foreach (UserItem slot in array)
            {
                if (slot == null)
                {
                    freeSlots++;
                }
            }

            return freeSlots;
        }


        public void BindAllItems()
        {
            for (int i = 0; i < Inventory.Length; i++)
            {
                if (Inventory[i] == null) continue;
                GameScene.Bind(Inventory[i]);
            }

            for (int i = 0; i < Equipment.Length; i++)
            {
                if (Equipment[i] == null) continue;
                GameScene.Bind(Equipment[i]);
            }

            for (int i = 0; i < QuestInventory.Length; i++)
            {
                if (QuestInventory[i] == null) continue;
                GameScene.Bind(QuestInventory[i]);
            }
        }



    }
}