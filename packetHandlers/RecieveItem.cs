using StacklandsRandomizerNS;
using StacklandsRandomizerNS.IdeaMap;
﻿using Archipelago.MultiClient.Net.Helpers;
using UnityEngine;
using Archipelago.MultiClient.Net.Models;

namespace StacklandsRandomizerNS.ItemReceiver
{
    public class ItemReceived {
        public void OnItemReceived(ReceivedItemsHelper item) {
            string name = item.PeekItemName();

            Debug.Log(name);

            if (name.Contains("Booster Pack")) {
                StacklandsRandomizer.UnlockPack(name.Replace(" Booster Pack", ""));
            } else {
                Ideas.allIdeas.TryGetValue(name.Replace("Idea: ", ""), out List<string> ids);
                foreach (string idea in ids) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.CreateCard("blueprint_" + idea);
                        });
                    }
                }
            }

            item.DequeueItem();
        }
    }
}