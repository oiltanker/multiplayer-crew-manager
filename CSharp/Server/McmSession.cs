using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Barotrauma;

namespace MultiplayerCrewManager
{
    partial class McmSession
    {
        private bool AllowMissionEnd;

        public McmSession(GameSession session)
        {
            AllowMissionEnd = true;
        }

        public void OnCampaignModeEnd(CampaignMode self)
        {
            if (!McmMod.IsCampaign) return;

            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && c.IsDead).ToArray())
            {
                character.DespawnNow(false);
                Entity.Spawner.AddEntityToRemoveQueue(character);
            }
            Entity.Spawner.Update();
        }

        private static FieldInfo isFinished = typeof(MoneyAction).GetField("isFinished", BindingFlags.Instance | BindingFlags.NonPublic);

        public bool? OnMoneyActionUpdate(MoneyAction self, float deltaTime)
        { // MoneyAction
            if (!McmMod.IsCampaign) return null;

            if ((isFinished.GetValue(self) as bool?).Value) return true; // override

            if (GameMain.GameSession?.GameMode is CampaignMode campaign)
            {
                var targets = self.ParentEvent.GetTargets(self.TargetTag);
                foreach (Entity entity in targets)
                {
                    if (entity is Character character)
                    {
                        character.Wallet.Give(self.Amount);
                    }
                }
                GameAnalyticsManager.AddMoneyGainedEvent(self.Amount, GameAnalyticsManager.MoneySource.Event, self.ParentEvent.Prefab.Identifier.Value);
            }

            isFinished.SetValue(self, new object[] { true });
            return true; // override
        }

        private static FieldInfo charactersField = typeof(PirateMission).GetField("characters", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo characterItemsField = typeof(PirateMission).GetField("characterItems", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo characterConfigField = typeof(PirateMission).GetField("characterConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo missionDifficultyField = typeof(PirateMission).GetField("missionDifficulty", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo addedMissionDifficultyPerPlayerField = typeof(PirateMission).GetField("addedMissionDifficultyPerPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo levelDataField = typeof(PirateMission).GetField("levelData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo GetDifficultyModifiedAmountMethod = typeof(PirateMission).GetMethod("GetDifficultyModifiedAmount", BindingFlags.Static | BindingFlags.NonPublic);
        private static FieldInfo characterTypeConfigField = typeof(PirateMission).GetField("characterTypeConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo GetRandomDifficultyModifiedElementMethod = typeof(PirateMission).GetMethod("GetRandomDifficultyModifiedElement", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo CreateHumanMethod = typeof(Mission).GetMethod("CreateHuman", BindingFlags.Static | BindingFlags.NonPublic);
        private static MethodInfo GetHumanPrefabFromElementMethod = typeof(Mission).GetMethod("GetHumanPrefabFromElement", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo enemySubField = typeof(PirateMission).GetField("enemySub", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo patrolPositionsField = typeof(PirateMission).GetField("patrolPositions", BindingFlags.Instance | BindingFlags.NonPublic);
        public bool? OnPirateMissionInitPirates(PirateMission self)
        { // PirateMission
            if (!McmMod.IsCampaign) return null;
            var characters = charactersField.GetValue(self) as List<Character>;
            var characterItems = characterItemsField.GetValue(self) as Dictionary<Character, List<Item>>;
            var characterConfig = characterConfigField.GetValue(self) as ContentXElement;
            var missionDifficulty = (missionDifficultyField.GetValue(self) as float?).Value;
            var addedMissionDifficultyPerPlayer = (addedMissionDifficultyPerPlayerField.GetValue(self) as float?).Value;
            var levelData = levelDataField.GetValue(self) as LevelData;
            Func<int, int, float, Random, int> GetDifficultyModifiedAmount = (int minAmount, int maxAmount, float levelDifficulty, Random rand) =>
                (GetDifficultyModifiedAmountMethod.Invoke(self, new object[] { minAmount, maxAmount, levelDifficulty, rand }) as int?).Value;
            var characterTypeConfig = characterTypeConfigField.GetValue(self) as ContentXElement;
            Func<XElement, float, float, XElement> GetRandomDifficultyModifiedElement = (XElement parentElement, float levelDifficulty, float randomnessModifier) =>
                GetRandomDifficultyModifiedElementMethod.Invoke(self, new object[] { parentElement, levelDifficulty, randomnessModifier }) as XElement;
            Func<HumanPrefab, List<Character>, Dictionary<Character, List<Item>>, Submarine, CharacterTeamType, ISpatialEntity, Rand.RandSync, Character> CreateHuman = (HumanPrefab humanPrefab, List<Character> characters, Dictionary<Character, List<Item>> characterItems, Submarine submarine, CharacterTeamType teamType, ISpatialEntity positionToStayIn, Rand.RandSync randSync) =>
                        CreateHumanMethod.Invoke(self, new object[] { humanPrefab, characters, characterItems, submarine, teamType, positionToStayIn, randSync, }) as Character;

            Func<XElement, HumanPrefab> GetHumanPrefabFromElement = (XElement element) => GetHumanPrefabFromElementMethod.Invoke(self, new object[] { element }) as HumanPrefab;
            var enemySub = enemySubField.GetValue(self) as Submarine;
            var patrolPositions = patrolPositionsField.GetValue(self) as List<Vector2>;

            McmUtils.Trace("PIRATE MISSION: Clearing characters");
            characters.Clear();
            characterItems.Clear();

            if (characterConfig == null)
            {
                DebugConsole.ThrowError("Failed to initialize characters for escort mission (characterConfig == null)");
                return true; // override
            }

            int playerCount = GameSession.GetSessionCrewCharacters(CharacterType.Both).Count();
            McmUtils.Trace($"PIRATE MISSION: Estimated player count [{playerCount}]");
            float enemyCreationDifficulty = missionDifficulty + playerCount * addedMissionDifficultyPerPlayer;
            McmUtils.Trace($"PIRATE MISSION: Enemy Difficulty scalar [{enemyCreationDifficulty}]");
            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));

            bool commanderAssigned = false;
            foreach (ContentXElement element in characterConfig.Elements())
            {
                // it is possible to get more than the "max" amount of characters if the modified difficulty is high enough; this is intentional
                // if necessary, another "hard max" value could be used to clamp the value for performance/gameplay concerns
                int amountCreated = GetDifficultyModifiedAmount(element.GetAttributeInt("minamount", 0), element.GetAttributeInt("maxamount", 0), enemyCreationDifficulty, rand);
                McmUtils.Trace($"PIRATE MISSION: amount created [{amountCreated}]");
                var characterId = element.GetAttributeString("typeidentifier", string.Empty);
                McmUtils.Trace($"PIRATE MISSION: Got character ID [{characterId}]");
                for (int i = 0; i < amountCreated; i++)
                {
                    XElement characterType = characterTypeConfig.Elements().Where(e => e.GetAttributeString("typeidentifier", string.Empty) == element.GetAttributeString("typeidentifier", string.Empty)).FirstOrDefault();
                    McmUtils.Trace($"PIRATE MISSION: Creating character of type : [{characterType}]");
                    if (characterType == null)
                    {
                        DebugConsole.ThrowError($"No character types defined in CharacterTypes for a declared type identifier in mission \"{self.Prefab.Identifier}\".");
                        return true;
                    }

                    XElement variantElement = GetRandomDifficultyModifiedElement(characterType, enemyCreationDifficulty, 25); //PirateMission.cs 87 "private const float RandomnessModifier = 25;"

                    var humanPrefab = GetHumanPrefabFromElement(variantElement);
                    if (humanPrefab == null) { continue; }

                    Character spawnedCharacter = CreateHuman(GetHumanPrefabFromElement(variantElement), characters, characterItems, enemySub, CharacterTeamType.None, null, Rand.RandSync.ServerAndClient);
                    if (!commanderAssigned)
                    {
                        bool isCommander = variantElement.GetAttributeBool("iscommander", false);
                        if (isCommander && spawnedCharacter.AIController is HumanAIController humanAIController)
                        {
                            humanAIController.InitShipCommandManager();
                            foreach (var patrolPos in patrolPositions)
                            {
                                humanAIController.ShipCommandManager.patrolPositions.Add(patrolPos);
                            }
                            commanderAssigned = true;
                        }
                    }

                    McmUtils.Trace("PIRATE MISSION: Granting ID Tags to Pirates");
                    foreach (Item item in spawnedCharacter.Inventory.AllItems)
                    {
                        if (item?.GetComponent<Barotrauma.Items.Components.IdCard>() != null)
                        {
                            item.AddTag("id_pirate");
                        }
                    }
                }
            }
            return true; // override
        }

    }
}