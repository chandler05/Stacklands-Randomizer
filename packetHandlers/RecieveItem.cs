using StacklandsRandomizerNS;
using StacklandsRandomizerNS.IdeaMap;
using Archipelago.MultiClient.Net.Helpers;
using UnityEngine;

namespace StacklandsRandomizerNS.ItemReceiver
{
    public class ItemReceived {
        public void OnItemReceived(ReceivedItemsHelper item) {
            string name = item.PeekItemName();
            string player = StacklandsRandomizer.GetPlayerName(item.PeekItem().Player);
            Debug.Log(item.PeekItem().Player + " received " + name + " from " + player);

            if (name.Contains("Booster Pack")) {
                StacklandsRandomizer.UnlockPack(name.Replace(" Booster Pack", ""));
                if (WorldManager.instance.CurrentGameState != WorldManager.GameState.InMenu) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.SendNotification("Received Item", "Received " + name + " from " + player);
                        });
                    }
                }
            } else {
                Ideas.allIdeas.TryGetValue(name.Replace("Idea: ", ""), out List<string> ids);
                foreach (string idea in ids) {
                    lock(StacklandsRandomizer._lock) {
                        if (!WorldManager.instance.CurrentSave.FoundCardIds.Contains("blueprint_" + idea)) {
                            StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                                StacklandsRandomizer.CreateCard("blueprint_" + idea);
                            });
                        }
                    }
                }
                if (WorldManager.instance.CurrentGameState != WorldManager.GameState.InMenu) {
                    lock(StacklandsRandomizer._lock) {
                        StacklandsRandomizer._mainThreadActions.Enqueue(() => {
                            StacklandsRandomizer.SendNotification("Received Item", "Recieved " + name + " from " + player);
                        });
                    }
                }
            }

            item.DequeueItem();
        }
    }
}