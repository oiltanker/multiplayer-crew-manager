using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Barotrauma;

namespace MultiplayerCrewManager {
    partial class McmSession {
        private bool AllowMissionEnd;
        public GameSession Session { get; private set; }

        public McmSession(GameSession session) {
            Session = session;
            AllowMissionEnd = true;
        }

        public void OnCampaignModeEnd(CampaignMode self)  {
            if (!McmMod.IsCampaign) return;

            foreach (var character in Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && c.IsDead)) {
                character.DespawnNow(false);
                Entity.Spawner.AddEntityToRemoveQueue(character);
            }
            Entity.Spawner.Update();
        }

        /*public void OnBeforeEndRound(GameSession self) {
            if (!McmMod.IsCampaign) return;
            if (Session != self) Session = self;

            foreach (var mission in Session.Missions) mission.End();

            var characters = Character.CharacterList.Where(c => c.TeamID == CharacterTeamType.Team1 && !c.IsDead).ToList();
            characters.ForEach(c => c.CheckTalents(AbilityEffectType.OnRoundEnd));

            if (Session.Missions.Count()) {
                var completedCount = Session.Missions.Where(m => m.Completed).Count();

                if (completedCount > 0) characters.ForEach(c => c.CheckTalents(AbilityEffectType.OnAnyMissionCompleted));
                if (completedCount == Session.Missions.Count()) characters.ForEach(c => c.CheckTalents(AbilityEffectType.OnAllMissionsCompleted));
            }

            AllowMissionEnd = false;
        }
        public void OnAfterEndRound(GameSession self) {
            if (!McmMod.IsCampaign) return;
            if (Session != self) Session = self;
            AllowMissionEnd = true;
        }*/

        private static FieldInfo isFinished = typeof(MoneyAction).GetField("isFinished", BindingFlags.Instance | BindingFlags.NonPublic);
        public bool? OnMoneyActionUpdate(MoneyAction self, float deltaTime) { // MoneyAction
            if (!McmMod.IsCampaign) return null;

            if ((isFinished.GetValue(self) as bool?).Value) return true; // override

            if (GameMain.GameSession?.GameMode is CampaignMode campaign)
            {
                var targets = self.ParentEvent.GetTargets(self.TargetTag);
                foreach (Entity entity in targets) {
                    if (entity is Character character) {
                        character.Wallet.Give(self.Amount);
                    }
                }
                GameAnalyticsManager.AddMoneyGainedEvent(self.Amount, GameAnalyticsManager.MoneySource.Event, self.ParentEvent.Prefab.Identifier.Value);
            }

            isFinished.SetValue(self, new object[] { true });
            return true; // override
        }

        private static FieldInfo levelField = typeof(Mission).GetField("level", BindingFlags.Instance | BindingFlags.NonPublic);
        public bool? OnMissionGiveReward(Mission self) { // Mission
            if (!McmMod.IsCampaign) return null;

            if (!(GameMain.GameSession.GameMode is CampaignMode campaign)) return true;
            var level = levelField.GetValue(self) as Level;

            int reward = self.GetReward(Submarine.MainSub);

            float baseExperienceGain = reward * 0.09f;

            float difficultyMultiplier = 1 + level.Difficulty / 100f;
            baseExperienceGain *= difficultyMultiplier;

            var crewCharacters = GameSession.GetSessionCrewCharacters(CharacterType.Both).ToList();

            // use multipliers here so that we can easily add them together without introducing multiplicative XP stacking
            var experienceGainMultiplier = new AbilityExperienceGainMultiplier(1f);
            crewCharacters.ForEach(c => c.CheckTalents(AbilityEffectType.OnAllyGainMissionExperience, experienceGainMultiplier));
            crewCharacters.ForEach(c => experienceGainMultiplier.Value += c.GetStatValue(StatTypes.MissionExperienceGainMultiplier));

            int experienceGain = (int)(baseExperienceGain * experienceGainMultiplier.Value);
            foreach (Character character in crewCharacters)
            {
                character.Info?.GiveExperience(experienceGain, isMissionExperience: true);
            }

            // apply money gains afterwards to prevent them from affecting XP gains
            var missionMoneyGainMultiplier = new AbilityMissionMoneyGainMultiplier(self, 1f);
            crewCharacters.ForEach(c => c.CheckTalents(AbilityEffectType.OnGainMissionMoney, missionMoneyGainMultiplier));
            crewCharacters.ForEach(c => missionMoneyGainMultiplier.Value += c.GetStatValue(StatTypes.MissionMoneyGainMultiplier));

            int totalReward = (int)(reward * missionMoneyGainMultiplier.Value);
            GameAnalyticsManager.AddMoneyGainedEvent(totalReward, GameAnalyticsManager.MoneySource.MissionReward, self.Prefab.Identifier.Value);

            totalReward = Mission.DistributeRewardsToCrew(GameSession.GetSessionCrewCharacters(CharacterType.Both), totalReward);
            if (totalReward > 0)
            {
                campaign.Bank.Give(totalReward);
            }

            foreach (Character character in crewCharacters)
            {
                character.Info.MissionsCompletedSinceDeath++;
            }

            foreach (KeyValuePair<Identifier, float> reputationReward in self.ReputationRewards)
            {
                if (reputationReward.Key == "location")
                {
                    self.Locations[0].Reputation.AddReputation(reputationReward.Value);
                    self.Locations[1].Reputation.AddReputation(reputationReward.Value);
                }
                else
                {
                    Faction faction = campaign.Factions.Find(faction1 => faction1.Prefab.Identifier == reputationReward.Key);
                    if (faction != null) { faction.Reputation.AddReputation(reputationReward.Value); }
                }
            }

            if (self.Prefab.DataRewards != null)
            {
                foreach (var (identifier, value, operation) in self.Prefab.DataRewards)
                {
                    SetDataAction.PerformOperation(campaign.CampaignMetadata, identifier, value, operation);
                }
            }
            return true; // override
        }

        private static FieldInfo charactersField = typeof(PirateMission).GetField("characters", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo characterItemsField = typeof(PirateMission).GetField("characterItems", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo characterConfigField = typeof(PirateMission).GetField("characterConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo missionDifficultyField = typeof(PirateMission).GetField("missionDifficulty", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo addedMissionDifficultyPerPlayerField = typeof(PirateMission).GetField("addedMissionDifficultyPerPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo levelDataField = typeof(PirateMission).GetField("levelData", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo GetDifficultyModifiedAmountMethod = typeof(PirateMission).GetMethod("GetDifficultyModifiedAmount", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo characterTypeConfigField = typeof(PirateMission).GetField("characterTypeConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo GetRandomDifficultyModifiedElementMethod = typeof(PirateMission).GetMethod("GetRandomDifficultyModifiedElement", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo CreateHumanMethod = typeof(Mission).GetMethod("CreateHuman", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo GetHumanPrefabFromElementMethod = typeof(Mission).GetMethod("GetHumanPrefabFromElement", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo enemySubField = typeof(PirateMission).GetField("enemySub", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo patrolPositionsField = typeof(PirateMission).GetField("patrolPositions", BindingFlags.Instance | BindingFlags.NonPublic);
        public bool? OnPirateMissionInitPirates(PirateMission self) { // PirateMission
            if (!McmMod.IsCampaign) return null;
            var characters = charactersField.GetValue(self) as List<Character>;
            var characterItems = characterItemsField.GetValue(self) as Dictionary<Character, List<Item>>;
            var characterConfig = characterConfigField.GetValue(self) as XElement;
            var missionDifficulty = (missionDifficultyField.GetValue(self) as float?).Value;
            var addedMissionDifficultyPerPlayer = (addedMissionDifficultyPerPlayerField.GetValue(self) as float?).Value;
            var levelData = levelDataField.GetValue(self) as LevelData;
            Func<int, int, float, Random, int> GetDifficultyModifiedAmount = (int minAmount, int maxAmount, float levelDifficulty, Random rand) =>
                (GetDifficultyModifiedAmountMethod.Invoke(self, new object[] { minAmount, maxAmount, levelDifficulty, rand }) as int?).Value;
            var characterTypeConfig = characterTypeConfigField.GetValue(self) as XElement;
            Func<XElement, float, float, XElement> GetRandomDifficultyModifiedElement = (XElement parentElement, float levelDifficulty, float randomnessModifier) =>
                GetRandomDifficultyModifiedElementMethod.Invoke(self, new object[] { parentElement, levelDifficulty, randomnessModifier}) as XElement;
            Func<HumanPrefab, List<Character>, Dictionary<Character, List<Item>>, Submarine, CharacterTeamType, ISpatialEntity, Character> CreateHuman = (HumanPrefab humanPrefab, List<Character> characters, Dictionary<Character, List<Item>> characterItems, Submarine submarine, CharacterTeamType teamType, ISpatialEntity positionToStayIn) =>
                CreateHumanMethod.Invoke(self, new object[] { humanPrefab, characters, characterItems, submarine, teamType, positionToStayIn, Rand.RandSync.ServerAndClient, true }) as Character;
            Func<XElement, HumanPrefab> GetHumanPrefabFromElement = (XElement element) => GetHumanPrefabFromElementMethod.Invoke(self, new object[] { element }) as HumanPrefab;
            var enemySub = enemySubField.GetValue(self) as Submarine;
            var patrolPositions = patrolPositionsField.GetValue(self) as List<Vector2>;

            characters.Clear();
            characterItems.Clear();

            if (characterConfig == null)
            {
                DebugConsole.ThrowError("Failed to initialize characters for escort mission (characterConfig == null)");
                return true; // override
            }

            int playerCount = GameSession.GetSessionCrewCharacters(CharacterType.Both).Count();

            float enemyCreationDifficulty = missionDifficulty + playerCount * addedMissionDifficultyPerPlayer;

            Random rand = new MTRandom(ToolBox.StringToInt(levelData.Seed));

            bool commanderAssigned = false;
            foreach (XElement element in characterConfig.Elements())
            {
                // it is possible to get more than the "max" amount of characters if the modified difficulty is high enough; this is intentional
                // if necessary, another "hard max" value could be used to clamp the value for performance/gameplay concerns
                int amountCreated = GetDifficultyModifiedAmount(element.GetAttributeInt("minamount", 0), element.GetAttributeInt("maxamount", 0), enemyCreationDifficulty, rand);
                for (int i = 0; i < amountCreated; i++)
                {
                    XElement characterType = characterTypeConfig.Elements().Where(e => e.GetAttributeString("typeidentifier", string.Empty) == element.GetAttributeString("typeidentifier", string.Empty)).FirstOrDefault();

                    if (characterType == null)
                    {
                        DebugConsole.ThrowError($"No character types defined in CharacterTypes for a declared type identifier in mission \"{self.Prefab.Identifier}\".");
                        return true; // override
                    }

                    XElement variantElement = GetRandomDifficultyModifiedElement(characterType, enemyCreationDifficulty, 25); // const value PirateMission.RandomnessModifier

                    Character spawnedCharacter = CreateHuman(GetHumanPrefabFromElement(variantElement), characters, characterItems, enemySub, CharacterTeamType.None, null);
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

                    foreach (Item item in spawnedCharacter.Inventory.AllItems)
                    {
                        if (item?.Prefab.Identifier == "idcard")
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