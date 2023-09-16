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
                string id = null;
                switch (name) {
                    case "Tree":
                        id = "tree";
                        break;
                    case "Rock":
                        id = "rock";
                        break;
                    case "Berry Bush":
                        id = "berrybush";
                        break;
                }
                if (id != null) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.CreateCard(id);
                        });
                    }
                }
            }

            item.DequeueItem();
        }
    }
}