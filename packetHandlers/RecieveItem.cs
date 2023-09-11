using StacklandsRandomizerNS;
﻿using Archipelago.MultiClient.Net.Helpers;
using UnityEngine;

namespace StacklandsRandomizerNS.ItemReceiver
{
    public class ItemReceived {
        public void OnItemReceived(ReceivedItemsHelper item) {
            Debug.Log(item);

            string name = item.PeekItemName();

            if (name.Contains("Booster Pack")) {
                StacklandsRandomizer.UnlockPack(name.Replace(" Booster Pack", ""));
            } else {
                switch (name) {
                    case "Tree":
                        StacklandsRandomizer.CreateCard("tree");
                        break;
                    case "Rock":
                        StacklandsRandomizer.CreateCard("rock");
                        break;
                    case "Berry Bush":
                        StacklandsRandomizer.CreateCard("berrybush");
                        break;
                }
            }

            item.DequeueItem();
        }
    }
}