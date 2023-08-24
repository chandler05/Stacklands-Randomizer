using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;

namespace StacklandsRandomizerNS
{
    public class StacklandsRandomizer : Mod
    {
        public void Awake() {
            Logger.Log("Awake!");
            Harmony = new Harmony("com.github.stacklandsrandomizer");
            InterceptQuestComplete.Logger = Logger;
            Harmony.PatchAll();
        }
        public override void Ready()
        {
            Logger.Log("Ready!");
        }
    }

    [HarmonyPatch(typeof(WorldManager), "QuestCompleted")]
    public class InterceptQuestComplete {
        public static ModLogger Logger;
        public static void Prefix() {
            Logger.Log("QuestComplete!");
            WorldManager.instance.CreateCard(WorldManager.instance.CurrentBoard.NormalizedPosToWorldPos(new Vector2(0.6f, 0.5f)), "tree", false, false, true);
        }
    }

    [HarmonyPatch(typeof(QuestManager), "JustUnlockedPack")]
    public class StopGameFromUnlockingPacks {
        static void Postfix(ref BoosterpackData __result) {
            __result = null;
        }
    }

    [HarmonyPatch(typeof(QuestManager), "BoosterIsUnlocked")]
    public class LockPacks {
        static bool Prefix() {
            return false;
        }
    }
}