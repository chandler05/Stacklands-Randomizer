using StacklandsRandomizerNS;
using StacklandsRandomizerNS.IdeaMap;
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