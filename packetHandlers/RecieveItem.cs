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

            if (name.Contains("Booster Pack")) {
                StacklandsRandomizer.UnlockPack(name.Replace(" Booster Pack", ""));
                if (WorldManager.instance.CurrentGameState != WorldManager.GameState.InMenu) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.SendNotification("Received " + name + " from " + item.PeekItem().Player);
                        });
                    }
                }
            } else {
                Ideas.allIdeas.TryGetValue(name.Replace("Idea: ", ""), out List<string> ids);
                foreach (string idea in ids) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.CreateCard("blueprint_" + idea);
                        });
                    }
                }
                if (WorldManager.instance.CurrentGameState != WorldManager.GameState.InMenu) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.SendNotification("Received Item", "Recieved " + name + " from " + item.PeekItem().Player);
                        });
                    }
                }
            }

            item.DequeueItem();
        }
    }
}