﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class PurchasedItem
    {
        public ItemPrefab itemPrefab;
        public int quantity;

        public PurchasedItem(ItemPrefab itemPrefab, int quantity)
        {
            this.itemPrefab = itemPrefab;
            this.quantity = quantity;
        }
    }

    class CargoManager
    {
        private readonly List<PurchasedItem> purchasedItems;

        private readonly CampaignMode campaign;

        public Action OnItemsChanged;

        public List<PurchasedItem> PurchasedItems
        {
            get { return purchasedItems; }
        }
        
        public CargoManager(CampaignMode campaign)
        {
            purchasedItems = new List<PurchasedItem>();
            this.campaign = campaign;
        }

        public void SetPurchasedItems(List<PurchasedItem> items)
        {
            purchasedItems.Clear();
            purchasedItems.AddRange(items);

            OnItemsChanged?.Invoke();
        }

        public void PurchaseItem(ItemPrefab item, int Quantity = 1)
        {
            PurchasedItem purchasedItem = PurchasedItems.Find(pi => pi.itemPrefab == item);

            if (purchasedItem != null && Quantity == 1)
            {
                campaign.Money -= item.Price;
                purchasedItem.quantity += 1;
            }
            else
            {
                campaign.Money -= (item.Price * Quantity);
                purchasedItem = new PurchasedItem(item, Quantity);
                purchasedItems.Add(purchasedItem);
            }

            OnItemsChanged?.Invoke();
        }

        public void SellItem(ItemPrefab item, int quantity = 1)
        {
            PurchasedItem purchasedItem = PurchasedItems.Find(pi => pi.itemPrefab == item);
            if (purchasedItem != null && purchasedItem.quantity - quantity > 0)
            {
                purchasedItem.quantity -= quantity;
            }
            else
            {
                PurchasedItems.Remove(purchasedItem);
            }

            OnItemsChanged?.Invoke();
        }

        public int GetTotalItemCost()
        {
            return purchasedItems.Sum(i => (i.itemPrefab.Price * i.quantity));
        }

        public void CreateItems()
        {
            CreateItems(purchasedItems);
            OnItemsChanged?.Invoke();
        }

        public static void CreateItems(List<PurchasedItem> itemsToSpawn)
        {
            WayPoint wp = WayPoint.GetRandom(SpawnType.Cargo, null, Submarine.MainSub);

            if (wp == null)
            {
                DebugConsole.ThrowError("The submarine must have a waypoint marked as Cargo for bought items to be placed correctly!");
                return;
            }

            Hull cargoRoom = Hull.FindHull(wp.WorldPosition);

            if (cargoRoom == null)
            {
                DebugConsole.ThrowError("A waypoint marked as Cargo must be placed inside a room!");
                return;
            }

            Dictionary<ItemContainer, int> availableContainers = new Dictionary<ItemContainer, int>();
            ItemPrefab containerPrefab = null;
            foreach (PurchasedItem pi in itemsToSpawn)
            {
                Vector2 position = new Vector2(
                    Rand.Range(cargoRoom.Rect.X + 20, cargoRoom.Rect.Right - 20),
                    cargoRoom.Rect.Y - cargoRoom.Rect.Height + pi.itemPrefab.Size.Y / 2);

                ItemContainer itemContainer = null;
                if (!string.IsNullOrEmpty(pi.itemPrefab.CargoContainerName))
                {
                    itemContainer = availableContainers.Keys.ToList().Find(ac =>
                        ac.Item.Prefab.NameMatches(pi.itemPrefab.CargoContainerName) ||
                        ac.Item.Prefab.Tags.Contains(pi.itemPrefab.CargoContainerName.ToLowerInvariant()));

                    if (itemContainer == null)
                    {
                        containerPrefab = MapEntityPrefab.List.Find(ep =>
                            ep.NameMatches(pi.itemPrefab.CargoContainerName) ||
                            (ep.Tags != null && ep.Tags.Contains(pi.itemPrefab.CargoContainerName.ToLowerInvariant()))) as ItemPrefab;

                        if (containerPrefab == null)
                        {
                            DebugConsole.ThrowError("Cargo spawning failed - could not find the item prefab for container \"" + containerPrefab.Name + "\"!");
                            continue;
                        }

                        Item containerItem = new Item(containerPrefab, position, wp.Submarine);
                        itemContainer = containerItem.GetComponent<ItemContainer>();
                        if (itemContainer == null)
                        {
                            DebugConsole.ThrowError("Cargo spawning failed - container \"" + containerItem.Name + "\" does not have an ItemContainer component!");
                            continue;
                        }
                        availableContainers.Add(itemContainer, itemContainer.Capacity);
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.CreateNetworkEvent(itemContainer.Item, false);
                        }
                    }                    
                }

                for (int i = 0; i < pi.quantity; i++)
                {
                    if (itemContainer == null)
                    {
                        //no container, place at the waypoint
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.AddToSpawnQueue(pi.itemPrefab, position, wp.Submarine);
                        }
                        else
                        {
                            new Item(pi.itemPrefab, position, wp.Submarine);
                        }
                        continue;
                    }
                    //if the intial container has been removed due to it running out of space, add a new container
                    //of the same type and begin filling it
                    if (!availableContainers.ContainsKey(itemContainer))
                    {
                        Item containerItemOverFlow = new Item(containerPrefab, position, wp.Submarine);
                        itemContainer = containerItemOverFlow.GetComponent<ItemContainer>();
                        availableContainers.Add(itemContainer, itemContainer.Capacity);
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.CreateNetworkEvent(itemContainer.Item, false);
                        }
                    }
                    //place in the container
                    if (GameMain.Server != null)
                    {
                        Entity.Spawner.AddToSpawnQueue(pi.itemPrefab, itemContainer.Inventory);
                    }
                    else
                    {
                        var item = new Item(pi.itemPrefab, position, wp.Submarine);
                        itemContainer.Inventory.TryPutItem(item, null);
                    }

                    //reduce the number of available slots in the container
                    //if there is a container
                    if (availableContainers.ContainsKey(itemContainer))
                    {
                        availableContainers[itemContainer]--;
                    }
                    if (availableContainers.ContainsKey(itemContainer) && availableContainers[itemContainer] <= 0)
                    {
                        availableContainers.Remove(itemContainer);
                    }
                }
            }

            itemsToSpawn.Clear();
        }
    }
}
