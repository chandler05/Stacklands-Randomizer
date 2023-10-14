using System.Threading.Tasks;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using StacklandsRandomizerNS.ItemReceiver;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json.Linq;
using Archipelago.MultiClient.Net.Helpers;

namespace StacklandsRandomizerNS
{
    public class StacklandsRandomizer : Mod
    {
        public static StacklandsRandomizer instance;
        public static readonly ItemReceived _itemReceived = new();

        public static readonly object _lock = new();
        public static readonly Queue<Action> _mainThreadActions = new();

        public static List<string> unlockedPacks = new List<string>();
        public static ArchipelagoSession session;
        public static bool connected = false;
        static ConfigEntry<string> port;
        static ConfigEntry<string> slotName;
        static ConfigEntry<string> password;
        static ConfigEntry<bool> makeConnection;
        static ConfigEntry<bool> sendGoal;

        public Settings settings;

        public void Awake() {      
            instance = this;
            port = Config.GetEntry<string>("Server", "archipelago.gg:");
            slotName = Config.GetEntry<string>("Slot Name", "Player");
            password = Config.GetEntry<string>("Password", "");
            makeConnection = Config.GetEntry<bool>("Make Connection", false);
            sendGoal = Config.GetEntry<bool>("Send Goal", false);

            sendGoal.UI.OnUI = (ConfigEntryBase entry) =>
            {
                var btn = Instantiate(PrefabManager.instance.ButtonPrefab, ModOptionsScreen.instance.ButtonsParent);
                btn.transform.localScale = Vector3.one;
                btn.transform.localPosition = Vector3.zero;
                btn.transform.localRotation = Quaternion.identity;

                btn.TextMeshPro.text = "Connect";
                btn.TooltipText = "Connect to the Archipelago server (Stacklands will restart)";
                btn.Clicked += () =>
                { 
                    makeConnection.Value = true;

                    Config.Save();
                };

                var btn2 = Instantiate(PrefabManager.instance.ButtonPrefab, ModOptionsScreen.instance.ButtonsParent);
                btn2.transform.localScale = Vector3.one;
                btn2.transform.localPosition = Vector3.zero;
                btn2.transform.localRotation = Quaternion.identity;

                btn2.TextMeshPro.text = "Send Goal";
                btn2.TooltipText = "Check and send victory condition (TEMPORARY SOLUTION, WILL CHANGE IN THE FUTURE)";
                btn2.Clicked += () =>
                { 
                    makeConnection.Value = true;
                    if (WorldManager.instance.CurrentSave.CompletedAchievementIds.Exists((string locationID) => locationID == "kill_demon_lord")) {
                        sendGoal.Value = true;
                    }
                    

                    Config.Save();
                };
            };  
            
            makeConnection.UI.Hidden = true;
            sendGoal.UI.Hidden = true;

            Harmony = new Harmony("com.github.stacklandsrandomizer");
            Harmony.PatchAll();

            ChangeValues();

            InterceptQuestComplete.Logger = Logger;
            RevealAllQuests.Logger = Logger;

            CheckForConnection(this);
        }

        private static void CheckForConnection(StacklandsRandomizer instance) {
            Debug.Log(makeConnection.Value);
            if (makeConnection.Value && !connected) {
                session = ArchipelagoSessionFactory.CreateSession(new Uri("ws://" + port.Value));

                session.Items.ItemReceived += _itemReceived.OnItemReceived;

                connected = true;

                Debug.Log(slotName.Value);

                makeConnection.Value = false;
                instance.Config.Save();

                Connect(session, slotName.Value, password.Value.Length > 0 ? password.Value : null);
            }
        }

        private static void Connect(ArchipelagoSession session, string slotName, string? password = null)
        {
            LoginResult result = null;

            try
            {
                Debug.Log("Trying to connect");

                result = session.TryConnectAndLogin("Stacklands", slotName, ItemsHandlingFlags.AllItems, password: password);

                Debug.Log(result);
            }
            catch (Exception e)
            {
                Debug.Log(e);
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

                connected = false;

                return;
            }

            LoginSuccessful login = (LoginSuccessful)result;

            instance.settings = ((JObject)login.SlotData["settings"]).ToObject<Settings>() ?? new Settings();

            if (sendGoal.Value) {
                SendGoalAsync(instance.settings.goal);
                sendGoal.Value = false;
                instance.Config.Save();
            }
        }

        private static void Disconnect() {
            if (session != null) {
                session.Socket.DisconnectAsync();
                session = null;
            }
        }

        public static string GetPlayerName(int slot) {
            return session.Players.GetPlayerName(slot);
        }

        static void SendAllCompletedLocations() {
            // Read completed quests from the save file
            WorldManager.instance.CurrentSave.CompletedAchievementIds.ForEach(async (string locationID) => {
                await SendLocation(QuestManager.GetAllQuests().Find((Quest quest) => quest.Id == locationID).Description);
            });
        }

        public static async Task SendLocation(string locationID, bool sendNotification = false) {
            Debug.Log("Sending location " + locationID);
            if (session.Locations.GetLocationIdFromName("Stacklands", locationID) != -1) {
                await session.Locations.CompleteLocationChecksAsync(session.Locations.GetLocationIdFromName("Stacklands", locationID));
            }
            if (locationID == "Kill the Demon Lord") {
                SendGoalAsync(Settings.GoalType.KillDemonLord);
            }
            // Send a notification with the item that was sent
            if (sendNotification) {
                if (session.Locations.GetLocationIdFromName("Stacklands", locationID) != -1) {
                    LocationInfoPacket info = await session.Locations.ScoutLocationsAsync(session.Locations.GetLocationIdFromName("Stacklands", locationID));
                    // If the item was sent to another player, send a notification to the player that sent the item
                    if (session.Locations.GetLocationIdFromName("Stacklands", locationID) != -1 && info.Locations[0].Player != session.ConnectionInfo.Slot) {
                        SendNotification(SokLoc.Translate("label_quest_completed") + " ", locationID + "\nSent " + session.Items.GetItemName(info.Locations[0].Item) + " to " + GetPlayerName(info.Locations[0].Player));
                    } else {
                        // Mimic the notification that would appear for completing a quest if the item was sent to the player that found the check
                        SendNotification(SokLoc.Translate("label_quest_completed") + " ", locationID);
                    }
                } else {
                    // Mimic the notification that would appear for completing a quest if the item was sent to the player that found the check
                    SendNotification(SokLoc.Translate("label_quest_completed") + " ", locationID);
                }
            }
        }

        public static void SendGoalAsync(Settings.GoalType goal)
        {
            if (goal != instance.settings.goal)
                return;

            StatusUpdatePacket statusUpdatePacket = new StatusUpdatePacket
            {
                Status = ArchipelagoClientState.ClientGoal
            };

            Debug.Log("SENDING");

            //await session.Socket.SendPacketAsync(statusUpdatePacket);

            //await session.Locations.CompleteLocationChecksAsync(session.Locations.GetLocationIdFromName("Stacklands", "Kill the Demon"));

            session.Socket.SendPacketAsync(statusUpdatePacket);
        }

        public static void UnlockPack(string packName) {
            unlockedPacks.Add(packName);
        }

        public static void ChangeValues() {
            AllQuests.PauseGame = new Quest("pause_game")
            {
                OnSpecialAction = ((string action) => action == "pause_game"),
                QuestGroup = QuestGroup.Starter
            };
        }

        public static void CreateCard(string cardName) {
            WorldManager.instance.CreateCard(new Vector2(0.0f, 0.0f), cardName, false, false, true);
        }

        public static void SendNotification(string message, string text) {
            GameScreen.instance.AddNotification(message, text);
        }

        public static string PickBasicRandomCard() {
            List<string> cards = new List<string>() {"berrybush","rock","tree"};
            return cards[UnityEngine.Random.Range(0, cards.Count)];
        }

        public void Update() {
            if (InputController.instance.GetKeyDown(Key.Backslash)) {
                //WorldManager.instance.QuestCompleted(AllQuests.KillDemonLord);
                //WorldManager.instance.CurrentSave.CompletedAchievementIds.Add("kill_demon_lord");
                //WorldManager.instance.SaveAndGoBackToMenu();
            }

            if (makeConnection.Value) {
                Disconnect();
                System.Diagnostics.Process.Start(Application.dataPath.Replace("_Data", ".exe"), "--no-intro");
                Application.Quit();
            }

            lock(_lock) {
                if (WorldManager.instance.CurrentGameState != WorldManager.GameState.InMenu) {
                    while (_mainThreadActions.Count > 0) {
                        _mainThreadActions.Dequeue().Invoke();
                    }
                }
            }
        }

        private void LateUpdate() {
            if (connected && WorldManager.instance.CurrentGameState == WorldManager.GameState.InMenu && FindObjectOfType<MainMenu>() != null) {
                FindObjectOfType<MainMenu>().UpdateText.text = "Connected to Archipelago";
                FindObjectOfType<MainMenu>().UpdateInfoButton.TooltipText = "Connected to Archipelago\nPort: " + port.Value + "\nSlot Name: " + slotName.Value;
            }
        }

        // Send location when a quest is completed
        [HarmonyPatch(typeof(WorldManager), "QuestCompleted")]
        public class InterceptQuestComplete {
            public static ModLogger Logger;
            public static async void Prefix(Quest quest) {
                await SendLocation(quest.Description, true);
                Logger.Log("QuestComplete!");
            }
        }

        // Prevent the game from unlocking packs
        [HarmonyPatch(typeof(QuestManager), "JustUnlockedPack")]
        public class StopGameFromUnlockingPacks {
            static void Postfix(ref BoosterpackData __result) {
                __result = null;
            }
        }

        // Unlock packs when they are received
        [HarmonyPatch(typeof(QuestManager), "BoosterIsUnlocked")]
        public class LockPacks {
            
            static void Postfix(BoosterpackData p, bool allowDebug, ref bool __result) {
                __result = unlockedPacks.Contains(p.Name);
            }
        }

        // Reveal all quests
        [HarmonyPatch(typeof(QuestManager), "QuestIsVisible")]
        public class ShowAllQuests {
            
            static void Postfix(ref bool __result) {
                __result = true;
            }
        }

        // Reveal all quest categories
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

        // Replace blueprints with basic cards
        [HarmonyPatch(typeof(WorldManager), "GetRandomCard")]
        public class RemoveBlueprints {
            static void Prefix(ref List<CardChance> chances, ref bool removeCard) {
                foreach (CardChance item in chances) {
                    if (item.Id.Contains("blueprint")) {
                        item.Id = PickBasicRandomCard();
                    }
                }
            }
        }

        // Removes blueprints from card bags and replaces them with basic cards
        [HarmonyPatch(typeof(CardBag), "GetCardsInBag", new Type[] { typeof(GameDataLoader) })]
        public class RemoveBlueprintsFromBags {
            static void Postfix(ref GameDataLoader loader, ref List<string> __result) {
                List<string> toReplace = new();
                foreach (string card in __result) {
                    if (card.Contains("blueprint_")) {
                        toReplace.Add(card);
                    }
                }
                foreach (string card in toReplace) {
                    __result.Insert(__result.IndexOf(card), PickBasicRandomCard());
                    __result.Remove(card);
                }
            }
        }

        // Removes blueprints from set bags and replaces them with basic cards
        [HarmonyPatch(typeof(CardBag), "GetRawCardChanges")]
        public class RemoveBlueprintsFromSetBags {
            static void Postfix(ref List<CardChance> __result) {
                List<CardChance> toReplace = new();
                foreach (CardChance card in __result) {
                    if (card.Id.Contains("blueprint_")) {
                        toReplace.Add(card);
                    }
                }
                foreach (CardChance card in toReplace) {
                    int index = __result.IndexOf(card);
                    __result.Remove(card);
                    card.Id = PickBasicRandomCard();
                    __result.Insert(index, card);
                }
            }
        }

        // Removes the Combat Intro Boosterpack
        [HarmonyPatch(typeof(Boosterpack), "Clicked")]
        public class RemoveCombatIntroPack{
            static void Prefix() {
                if (!WorldManager.instance.CurrentSave.FoundBoosterIds.Contains("combat_intro")) {
                    WorldManager.instance.CurrentSave.FoundBoosterIds.Add("combat_intro");
                }
            }
        }

        // Disable the blueprint timer for undiscovered blueprints 
        [HarmonyPatch(typeof(GameCard), "StartBlueprintTimer")]
        public class RemoveTimerForUndiscoveredBlueprints {
            static bool Prefix(ref string blueprintId) {
                if (!WorldManager.instance.CurrentSave.FoundCardIds.Contains(blueprintId)) {
                    return false;
                }
                return true;
            }
        }

        // Disconnect when quitting
        [HarmonyPatch(typeof(MainMenu), "Awake")]
        public class DisconnectOnQuit {
            static void Prefix(ref MainMenu __instance) {
                __instance.QuitButton.Clicked += delegate() {
                    Disconnect();
                };
            }
        }

        // Run code on loading into a game
        [HarmonyPatch(typeof(WorldManager), "LoadSaveRound")]
        public class OnGameStart {
            static void Postfix(ref WorldManager __instance) {
                SendAllCompletedLocations();
            }
        }

        // Prevent Quest Completed notifications from appearing, since we send our own
        [HarmonyPatch(typeof(GameScreen), "AddNotification")]
        public class RemoveQuestNotifications {
            static bool Prefix(ref string title, ref string text) {
                if (title.Equals(SokLoc.Translate("label_quest_completed"))) {
                    return false;
                }
                return true;
            }
        }
    }
}