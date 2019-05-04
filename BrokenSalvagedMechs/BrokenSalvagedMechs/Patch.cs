using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using CustomComponents;
using BattleTech;
using BattleTech.UI;

namespace AdjustedMechAssembly
{

    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch {

        private static bool CanAssembleVariant(MechDef variant) {
            var settings = Helper.Settings;
            if (settings.VariantExceptions.Contains(variant.Description.Id))
                return false;

            if (settings.TagExceptions != null && settings.TagExceptions.Count > 0) {
                foreach (var tag in settings.TagExceptions) {
                    if (variant.MechTags.Contains(tag)) {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Prefix(SimGameState __instance, string id) {
            try {
                AdjustedMechAssembly.Logger.LogIfDebug($"SGS:AMP - entered for id:{id}");

                __instance.AddItemStat(id, "MECHPART", false);

                Settings settings = Helper.Settings;
                Dictionary<MechDef, int> possibleMechs = new Dictionary<MechDef, int>();
                MechDef currentVariant = __instance.DataManager.MechDefs.Get(id);
                int itemCount = 0;
                if (settings.AssembleVariants && CanAssembleVariant(currentVariant)) {
                    AdjustedMechAssembly.Logger.LogIfDebug($" Assembling variant: {currentVariant.ChassisID}");
                    itemCount = BuildPotentialAssemblies(__instance, id, possibleMechs, currentVariant, itemCount);
                } else {
                    itemCount = __instance.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                }

                int defaultMechPartMax = __instance.GetMechPartsRequired(currentVariant);
                AdjustedMechAssembly.Logger.LogIfDebug($" Found {itemCount} potential assemblies, with {defaultMechPartMax} defaultMechPartMax");

                if (itemCount >= defaultMechPartMax) {
                    MechDef mechDef = null;
                    List<KeyValuePair<MechDef, int>> mechlist = possibleMechs.ToList();
                    mechlist = possibleMechs.OrderByDescending(o => o.Value).ToList();

                    if (settings.AssembleVariants && CanAssembleVariant(currentVariant)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Attempting to assemble variants");

                        if (settings.AssembleMostParts) {
                            mechDef = AssembleMostParts(currentVariant, mechlist);
                        } else {
                            mechDef = AssembleMech(defaultMechPartMax, mechDef, mechlist);
                        }

                        AdjustedMechAssembly.Logger.LogIfDebug($" Iterating mech parts.");
                        int j = 0;
                        int i = 0;
                        int currentPart = 1;
                        while (i < defaultMechPartMax) {
                            if (currentPart > mechlist[j].Value) {
                                j++;
                                currentPart = 1;
                            }
                            AdjustedMechAssembly.Logger.LogIfDebug($" Found mechlist:({mechlist[j].Key.Description.Id})");
                            ReflectionHelper.InvokePrivateMethode(__instance, "RemoveItemStat", new object[] { mechlist[j].Key.Description.Id, "MECHPART", false });
                            currentPart++;
                            i++;
                        }
                    } else {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Assembling specific mech");
                        for (int i = 0; i < defaultMechPartMax; i++) {
                            ReflectionHelper.InvokePrivateMethode(__instance, "RemoveItemStat", new object[] { id, "MECHPART", false });
                        }
                        mechDef = new MechDef(__instance.DataManager.MechDefs.Get(id), __instance.GenerateSimGameUID());
                    }

                    if ((!__instance.Constants.Salvage.EquipMechOnSalvage && !settings.IgnoreUnequiped) || settings.ForceUnequiped) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Removing salvaged mech equipment.");
                        mechDef.SetInventory(DefaultHelper.ClearInventory(mechDef, __instance));
                    }

                    Random rng = new Random();

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting head structure");
                    if (!settings.HeadRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting head structure to 0");
                        mechDef.Head.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.Head.CurrentInternalStructure = Math.Max(1f, mechDef.Head.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting head structure to: {mechDef.Head.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting heleftArmad structure");
                    if (!settings.LeftArmRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftArm structure to 0");
                        mechDef.LeftArm.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftArm.CurrentInternalStructure = Math.Max(1f, mechDef.LeftArm.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftArm structure to: {mechDef.LeftArm.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting rightArm structure");
                    if (!settings.RightArmRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightArm structure to 0");
                        mechDef.RightArm.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightArm.CurrentInternalStructure = Math.Max(1f, mechDef.RightArm.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightArm structure to: {mechDef.RightArm.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting leftLeg structure");
                    if (!settings.LeftLegRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftLeg structure to 0");
                        mechDef.LeftLeg.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftLeg.CurrentInternalStructure = Math.Max(1f, mechDef.LeftLeg.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftLeg structure to: {mechDef.LeftLeg.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting rightLeg structure");
                    if (!settings.RightLegRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightLeg structure to 0");
                        mechDef.RightLeg.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightLeg.CurrentInternalStructure = Math.Max(1f, mechDef.RightLeg.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightLeg structure to: {mechDef.RightLeg.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting centerTorso structure");
                    if (!settings.CentralTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting centerTorso structure to 0");
                        mechDef.CenterTorso.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.CenterTorso.CurrentInternalStructure = Math.Max(1f, mechDef.CenterTorso.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting centerTorso structure to: {mechDef.CenterTorso.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting rightTorso structure");
                    if (!settings.RightTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightTorso structure to 0");
                        mechDef.RightTorso.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightTorso.CurrentInternalStructure = Math.Max(1f, mechDef.RightTorso.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting rightTorso structure to: {mechDef.RightTorso.CurrentInternalStructure}");
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Setting leftTorso structure");
                    if (!settings.LeftTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftTorso structure to 0");
                        mechDef.LeftTorso.CurrentInternalStructure = 0f;
                    } else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftTorso.CurrentInternalStructure = Math.Max(1f, mechDef.LeftTorso.CurrentInternalStructure * (float)rng.NextDouble());
                        AdjustedMechAssembly.Logger.LogIfDebug($" Setting leftTorso structure to: {mechDef.LeftTorso.CurrentInternalStructure}");
                    }


                    // each component is checked and destroyed if the settings are set that way
                    // if RepairMechComponents is true it will variably make components either functional, nonfunctional, or destroyed based on settings
                    foreach (MechComponentRef mechComponent in mechDef.Inventory) {
                        AdjustedMechAssembly.Logger.LogIfDebug($"Checking mechComponent:{mechComponent.ComponentDefID}");

                        if (!settings.RepairMechComponents || mechDef.IsLocationDestroyed(mechComponent.MountedLocation)) {
                            AdjustedMechAssembly.Logger.LogIfDebug($"Destroying location: {mechComponent.MountedLocation}");
                            mechComponent.DamageLevel = ComponentDamageLevel.Destroyed;
                            continue;
                        }

                        if (settings.RepairMechComponents) {
                            double repairRoll = rng.NextDouble();
                            AdjustedMechAssembly.Logger.LogIfDebug($"Repair roll was: {repairRoll}%");
                            if (repairRoll <= settings.RepairComponentsFunctionalThreshold) {
                                AdjustedMechAssembly.Logger.LogIfDebug($"Component set to destroyed.");
                                mechComponent.DamageLevel = ComponentDamageLevel.Destroyed;
                            } else if (repairRoll <= settings.RepairComponentsNonFunctionalThreshold) {
                                AdjustedMechAssembly.Logger.LogIfDebug($"Component set to functional.");
                                mechComponent.DamageLevel = ComponentDamageLevel.Functional;
                            } else {
                                AdjustedMechAssembly.Logger.LogIfDebug($"Component set to non-functional.");
                                mechComponent.DamageLevel = ComponentDamageLevel.NonFunctional;
                            }
                            
                        }
                    }

                    AdjustedMechAssembly.Logger.LogIfDebug($"Adding an instance of mechDef:{mechDef}");
                    __instance.AddMech(0, mechDef, true, false, true, null);

                    AdjustedMechAssembly.Logger.LogIfDebug($"Displaying mech creation dialog");
                    SimGameInterruptManager interrupt = (SimGameInterruptManager)ReflectionHelper.GetPrivateField(__instance, "interruptQueue");
                    interrupt.DisplayIfAvailable();
                    __instance.MessageCenter.PublishMessage(new SimGameMechAddedMessage(mechDef, defaultMechPartMax, true));
                }

                return false;
            } catch (Exception e) {
                AdjustedMechAssembly.Logger.LogError(e);
                AdjustedMechAssembly.Logger.Log($" Allowing normal prefix to fire due to error.");
                return true;
            }
        }

        private static MechDef AssembleMech(int defaultMechPartMax, MechDef mechDef, List<KeyValuePair<MechDef, int>> mechlist) {
            AdjustedMechAssembly.Logger.LogIfDebug($"Assembling mechDef: {mechDef}");
            Random rand = new Random();
            int numberOfDifferentVariants = mechlist.Count;
            double roll = rand.NextDouble();
            double currentTotal = 0;
            foreach (KeyValuePair<MechDef, int> mech in mechlist) {
                AdjustedMechAssembly.Logger.LogIfDebug($"  mech: {mech.Key.Description.Id}");
                currentTotal += (double)mech.Value / (double)defaultMechPartMax;
                if (roll <= currentTotal) {
                    mechDef = mech.Key;
                    break;
                }
            }

            return mechDef;
        }

        private static MechDef AssembleMostParts(MechDef currentVariant, List<KeyValuePair<MechDef, int>> mechlist) {
            MechDef mechDef;
            // This is the list of mechs which have the most parts
            // (there could be more than one if the parts are equal)
            // Don't include the variant which we've just found a part for.
            List<MechDef> topMechList = new List<MechDef>();
            if (mechlist[0].Key.ChassisID != currentVariant.ChassisID) {
                AdjustedMechAssembly.Logger.LogIfDebug($"  TopMechList: {mechlist[0].Key}");
                topMechList.Add(mechlist[0].Key);
            }

            for (int mechlistI = 1; mechlistI < mechlist.Count && mechlist[mechlistI - 1].Value == mechlist[mechlistI].Value; ++mechlistI) {
                MechDef mechToAdd = mechlist[mechlistI].Key;
                AdjustedMechAssembly.Logger.LogIfDebug($" Adding mechDef: {mechToAdd.Description.Id}");
                if (mechToAdd.ChassisID != currentVariant.ChassisID) {
                    topMechList.Add(mechlist[mechlistI].Key);
                }
            }

            // Now if the most parts list is empty, choose the current variant.
            // If it has one element, choose it
            // (we prefer the variant which we have previously had the parts for, all else being equal)
            // if there's more than one variant, choose one from this list randomly.
            //
            // This approach gives the commander some control over what variant will be assembled.
            // For example, if the commander has 3 of one variant and 2 of another and the parts required is 6, 
            // they can be sure that the first variant will be constructed once they get another part
            // no matter what it is.
            // So commanders can sell parts if they choose to manipulate this.
            switch (topMechList.Count) {
                case 0:
                    mechDef = currentVariant;
                    AdjustedMechAssembly.Logger.LogIfDebug($"Assembling current varient: {mechDef.Description.Id}");
                    break;
                case 1:
                    mechDef = topMechList[0];
                    AdjustedMechAssembly.Logger.LogIfDebug($"Assembling topMechList: {mechDef.Description.Id}");
                    break;
                default:
                    Random rand = new Random();
                    int roll = (int)rand.NextDouble() * topMechList.Count;
                    mechDef = topMechList[Math.Min(roll, topMechList.Count - 1)];
                    AdjustedMechAssembly.Logger.LogIfDebug($"Assembling random toMechList: {mechDef.Description.Id}");
                    break;
            }

            return mechDef;
        }

        private static int BuildPotentialAssemblies(SimGameState __instance, string id, Dictionary<MechDef, int> possibleMechs, MechDef currentVariant, int itemCount) {
            AdjustedMechAssembly.Logger.LogIfDebug($" Building potential assemblies.");
            foreach (KeyValuePair<string, MechDef> pair in __instance.DataManager.MechDefs) {
                AdjustedMechAssembly.Logger.LogIfDebug($"  Checking assembly with pair: {pair.Key}");
                if (pair.Value.Chassis.PrefabIdentifier.Equals(currentVariant.Chassis.PrefabIdentifier) &&
                    CanAssembleVariant(pair.Value) &&
                    pair.Value.Chassis.Tonnage.Equals(__instance.DataManager.MechDefs.Get(id).Chassis.Tonnage)) {
                    AdjustedMechAssembly.Logger.LogIfDebug($"  Found suitable assembly: {pair.Value.Description.Id}");
                    int numberOfParts = __instance.GetItemCount(pair.Value.Description.Id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    AdjustedMechAssembly.Logger.LogIfDebug($"  numberOfParts: {numberOfParts}");
                    if (numberOfParts > 0) {
                        itemCount += numberOfParts;
                        AdjustedMechAssembly.Logger.LogIfDebug($"  Adding possibleMech for pair.");
                        possibleMechs.Add(new MechDef(pair.Value, __instance.GenerateSimGameUID()), numberOfParts);
                    }
                }
            }

            return itemCount;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "GetAllInventoryMechParts")]
    public static class SimGameState_GetAllInventoryMechParts_Patch
    {
        public static void Postfix(SimGameState __instance, List<ChassisDef> __result)
        {
            try
            {
                foreach (ChassisDef chassis in __result)
                {
                    int partsRequired = __instance.GetMechPartsRequired(chassis);
                    chassis.MechPartMax = partsRequired;
                }
            }
            catch (Exception e)
            {
                AdjustedMechAssembly.Logger.LogError(e);
            }
        }
    }
}
