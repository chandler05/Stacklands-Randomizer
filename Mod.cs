using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using StacklandsRandomizerNS.ItemReceiver;
using System.Collections;

namespace StacklandsRandomizerNS
{
    public class StacklandsRandomizer : Mod
    {
        private readonly ItemReceived _itemReceived = new();

        public static readonly object _lock = new();
        public static readonly Queue<Action> _mainThreadActions = new();

        public static List<string> unlockedPacks = new List<string>();
        public static ArchipelagoSession session;
        public void Awake() {
            Harmony = new Harmony("com.github.stacklandsrandomizer");
            Harmony.PatchAll();

            InterceptQuestComplete.Logger = Logger;
            RevealAllQuests.Logger = Logger;

            session = ArchipelagoSessionFactory.CreateSession(new Uri("ws://localhost:58214"));

            session.Items.ItemReceived += _itemReceived.OnItemReceived;

            Connect(session);
        }

        private static void Connect(ArchipelagoSession session)
        {
            LoginResult result = null;

            try
            {
                Debug.Log("Trying to connect");

                result = session.TryConnectAndLogin("Stacklands", "Chandler", ItemsHandlingFlags.AllItems);

                Debug.Log(result);
            }
            catch (Exception e)
            {
                result = new LoginFailure(e.GetBaseException().Message);
            }

            if (!result.Successful)
            {
                LoginFailure failure = (LoginFailure)result;
                string errorMessage = $"";
                foreach (string error in failure.Errors)
                {
                    errorMessage += $"\n    {error}";
                }
                foreach (ConnectionRefusedError error in failure.ErrorCodes)
                {
                    errorMessage += $"\n    {error}";
                }

                Debug.Log(errorMessage);

                return;
            }

            var loginSuccess = (LoginSuccessful)result;

            Debug.Log(loginSuccess);

            Debug.Log(session.Locations.AllLocations);

            foreach (var location in session.Locations.AllLocations)
            {
                Debug.Log(location);
            }
        }

        public static void SendLocation(string locationID) {
            session.Locations.CompleteLocationChecks(session.Locations.GetLocationIdFromName("Stacklands", locationID));
        }

        public static void UnlockPack(string packName) {
            unlockedPacks.Add(packName);
        }

        public static void CreateCard(string cardName) {
            Debug.Log("Creating card " + cardName + " at 0.6, 0.5");
            WorldManager.instance.CreateCard(new Vector2(0.0f, 0.0f), cardName, false, false, true);
            Debug.Log("Card created " + cardName + " at 0.6, 0.5");
        }

        public void Update() {
            if (InputController.instance.GetKeyDown(Key.Backslash)) {
                Debug.Log("Unlocking packs");
                UnlockPack("Humble Beginnings");
                CreateCard("berrybush");
            }

            lock(_lock) {
                while (_mainThreadActions.Count > 0) {
                    _mainThreadActions.Dequeue().Invoke();
                }
            }
        }
    
        [HarmonyPatch(typeof(WorldManager), "QuestCompleted")]
        public class InterceptQuestComplete {
            public static ModLogger Logger;
            public static void Prefix(Quest quest) {
                SendLocation(quest.Description);
                Logger.Log("QuestComplete!");
                GetAllBoosterPacks();
            }

            public static void GetAllBoosterPacks() {
                foreach (BuyBoosterBox boosterpack in FindObjectsOfType<BuyBoosterBox>())
                {
                    Logger.Log(boosterpack.transform.position.ToString());
                    Logger.Log(boosterpack.Booster.Name);
                }
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
            
            static void Postfix(BoosterpackData p, bool allowDebug, ref bool __result) {
                __result = unlockedPacks.Contains(p.Name);
            }
        }

        [HarmonyPatch(typeof(GameScreen), "UpdateQuestLog")]
        public class RevealAllQuests {
            public static ModLogger Logger;
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);

                for (int i = 0; i < code.Count; i++) {
                    if (code[i].ToString().Contains("brfalse") && code[i].ToString().Contains("Label5")) {
                        code[i].opcode = OpCodes.Brtrue;
                    }
                }

                return code.AsEnumerable();
            }
        }
    }
}