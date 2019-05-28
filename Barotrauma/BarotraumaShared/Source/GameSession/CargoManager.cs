﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class PurchasedItem
    {
        public readonly ItemPrefab ItemPrefab;
        public int Quantity;

        public PurchasedItem(ItemPrefab itemPrefab, int quantity)
        {
            this.ItemPrefab = itemPrefab;
            this.Quantity = quantity;
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

        public void PurchaseItem(ItemPrefab item, int quantity = 1)
        {
            PurchasedItem purchasedItem = PurchasedItems.Find(pi => pi.ItemPrefab == item);

            if (purchasedItem != null && quantity == 1)
            {
                campaign.Money -= item.GetPrice(campaign.Map.CurrentLocation).BuyPrice;
                purchasedItem.Quantity += 1;
            }
            else
            {
                campaign.Money -= item.GetPrice(campaign.Map.CurrentLocation).BuyPrice * quantity;
                purchasedItem = new PurchasedItem(item, quantity);
                purchasedItems.Add(purchasedItem);
            }

            OnItemsChanged?.Invoke();
        }

        public void SellItem(PurchasedItem purchasedItem, int quantity = 1)
        {
            quantity = Math.Min(purchasedItem.Quantity, quantity);
            campaign.Money += purchasedItem.ItemPrefab.GetPrice(campaign.Map.CurrentLocation).BuyPrice * quantity;
            purchasedItem.Quantity -= quantity;
            if (purchasedItem != null && purchasedItem.Quantity <= 0)
            {
                PurchasedItems.Remove(purchasedItem);
            }

            OnItemsChanged?.Invoke();
        }

        public int GetTotalItemCost()
        {
            return purchasedItems.Sum(i => i.ItemPrefab.GetPrice(campaign.Map.CurrentLocation).BuyPrice * i.Quantity);
        }

        public void CreateItems()
        {
            CreateItems(purchasedItems);
            OnItemsChanged?.Invoke();
        }

        public static void CreateItems(List<PurchasedItem> itemsToSpawn)
        {
            if (itemsToSpawn.Count == 0) { return; }

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

#if CLIENT
            new GUIMessageBox("", TextManager.GetWithVariable("CargoSpawnNotification", "[roomname]", cargoRoom.DisplayName, true));
#endif

            Dictionary<ItemContainer, int> availableContainers = new Dictionary<ItemContainer, int>();
            ItemPrefab containerPrefab = null;
            foreach (PurchasedItem pi in itemsToSpawn)
            {
                Vector2 position = new Vector2(
                    Rand.Range(cargoRoom.Rect.X + 20, cargoRoom.Rect.Right - 20),
                    cargoRoom.Rect.Y - cargoRoom.Rect.Height + pi.ItemPrefab.Size.Y / 2);

                ItemContainer itemContainer = null;
                if (!string.IsNullOrEmpty(pi.ItemPrefab.CargoContainerIdentifier))
                {
                    itemContainer = availableContainers.Keys.ToList().Find(ac => 
                        ac.Item.Prefab.Identifier == pi.ItemPrefab.CargoContainerIdentifier || 
                        ac.Item.Prefab.Tags.Contains(pi.ItemPrefab.CargoContainerIdentifier.ToLowerInvariant()));

                    if (itemContainer == null)
                    {
                        containerPrefab = MapEntityPrefab.List.Find(ep => 
                            ep.Identifier == pi.ItemPrefab.CargoContainerIdentifier || 
                            (ep.Tags != null && ep.Tags.Contains(pi.ItemPrefab.CargoContainerIdentifier.ToLowerInvariant()))) as ItemPrefab;

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
#if SERVER
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.CreateNetworkEvent(itemContainer.Item, false);
                        }
#endif
                    }                    
                }
                for (int i = 0; i < pi.Quantity; i++)
                {
                    if (itemContainer == null)
                    {
                        //no container, place at the waypoint
#if SERVER
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.AddToSpawnQueue(pi.ItemPrefab, position, wp.Submarine);
                        }
                        else
                        {
#endif
                            new Item(pi.ItemPrefab, position, wp.Submarine);
#if SERVER
                        }
#endif
                        continue;
                    }
                    //if the intial container has been removed due to it running out of space, add a new container
                    //of the same type and begin filling it
                    if (!availableContainers.ContainsKey(itemContainer))
                    {
                        Item containerItemOverFlow = new Item(containerPrefab, position, wp.Submarine);
                        itemContainer = containerItemOverFlow.GetComponent<ItemContainer>();
                        availableContainers.Add(itemContainer, itemContainer.Capacity);
#if SERVER
                        if (GameMain.Server != null)
                        {
                            Entity.Spawner.CreateNetworkEvent(itemContainer.Item, false);
                        }
#endif
                    }

                    //place in the container
#if SERVER
                    if (GameMain.Server != null)
                    {
                        Entity.Spawner.AddToSpawnQueue(pi.ItemPrefab, itemContainer.Inventory);
                    }
                    else
                    {
#endif
                        var item = new Item(pi.ItemPrefab, position, wp.Submarine);
                        itemContainer.Inventory.TryPutItem(item, null);
#if SERVER
                    }
#endif

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
