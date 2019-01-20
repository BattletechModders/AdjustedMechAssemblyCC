using BattleTech;
using BattleTech.UI;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using CustomComponents;

namespace AdjustedMechAssembly
{

    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        private static bool CanAssembleVariant(MechDef variant)
        {
            var settings = Helper.Settings;
            if (settings.VariantExceptions.Contains(variant.Description.Id))
                return false;

            if (settings.TagExceptions != null && settings.TagExceptions.Count > 0)
            {
                foreach (var tag in settings.TagExceptions)
                {
                    if (variant.MechTags.Contains(tag))
                        return false;
                }
            }

            return true;
        }

        private static bool Prefix(SimGameState __instance, string id)
        {
            try
            {
                __instance.AddItemStat(id, "MECHPART", false);

                Settings settings = Helper.Settings;
                Dictionary<MechDef, int> possibleMechs = new Dictionary<MechDef, int>();
                MechDef currentVariant = __instance.DataManager.MechDefs.Get(id);
                int itemCount = 0;

                if (settings.AssembleVariants && CanAssembleVariant(currentVariant))
                {
                    foreach (KeyValuePair<string, MechDef> pair in __instance.DataManager.MechDefs)
                    {
                        if (pair.Value.Chassis.PrefabIdentifier.Equals(currentVariant.Chassis.PrefabIdentifier) &&
                            CanAssembleVariant(pair.Value) &&
                            pair.Value.Chassis.Tonnage.Equals(__instance.DataManager.MechDefs.Get(id).Chassis.Tonnage))
                        {
                            int numberOfParts = __instance.GetItemCount(pair.Value.Description.Id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                            if (numberOfParts > 0) {
                                itemCount += numberOfParts;
                                possibleMechs.Add(new MechDef(pair.Value, __instance.GenerateSimGameUID()), numberOfParts);
                            }
                        }
                    }
                }

                else {
                    itemCount = __instance.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                }

                int defaultMechPartMax = __instance.GetMechPartsRequired(currentVariant);
                if (itemCount >= defaultMechPartMax) {
                    MechDef mechDef = null;
                    List<KeyValuePair<MechDef, int>> mechlist = possibleMechs.ToList();
                    mechlist = possibleMechs.OrderByDescending(o => o.Value).ToList();
                    if (settings.AssembleVariants && CanAssembleVariant(currentVariant)) {
                        if (settings.AssembleMostParts) {
                            // This is the list of mechs which have the most parts
                            // (there could be more than one if the parts are equal)
                            // Don't include the variant which we've just found a part for.
                            List<MechDef> topMechList = new List<MechDef>();
                            if (mechlist[0].Key.ChassisID != currentVariant.ChassisID)
                            {
                                topMechList.Add(mechlist[0].Key);
                            }

                            for (int mechlistI = 1;
                                mechlistI < mechlist.Count &&
                                mechlist[mechlistI - 1].Value == mechlist[mechlistI].Value;
                                ++mechlistI)
                            {
                                MechDef mechToAdd = mechlist[mechlistI].Key;
                                if (mechToAdd.ChassisID != currentVariant.ChassisID)
                                {
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
                            switch (topMechList.Count)
                            {
                                case 0:
                                    mechDef = currentVariant;
                                    break;
                                case 1:
                                    mechDef = topMechList[0];
                                    break;
                                default:
                                    Random rand = new Random();
                                    int roll = (int) rand.NextDouble() * topMechList.Count;
                                    mechDef = topMechList[Math.Min(roll, topMechList.Count - 1)];
                                    break;
                            }

                        }
                        else {
                            Random rand = new Random();
                            int numberOfDifferentVariants = mechlist.Count;
                            double roll = rand.NextDouble();
                            double currentTotal = 0;
                            foreach (KeyValuePair<MechDef, int> mech in mechlist) {
                                currentTotal += (double) mech.Value / (double) defaultMechPartMax;
                                if (roll <= currentTotal) {
                                    mechDef = mech.Key;
                                    break;
                                }
                            }
                        }
                        int j = 0;
                        int i = 0;
                        int currentPart = 1;
                        while (i < defaultMechPartMax) {
                            if (currentPart > mechlist[j].Value) {
                                j++;
                                currentPart = 1;
                            }
                            ReflectionHelper.InvokePrivateMethode(__instance, "RemoveItemStat", new object[] { mechlist[j].Key.Description.Id, "MECHPART", false });
                            currentPart++;
                            i++;
                        }
                    }
                    else {
                        for (int i = 0; i < defaultMechPartMax; i++) {
                            ReflectionHelper.InvokePrivateMethode(__instance, "RemoveItemStat", new object[] { id, "MECHPART", false });
                        }
                        mechDef = new MechDef(__instance.DataManager.MechDefs.Get(id), __instance.GenerateSimGameUID());
                    }

                    if ((!__instance.Constants.Salvage.EquipMechOnSalvage && !settings.IgnoreUnequiped) || settings.ForceUnequiped)
                    {
                        mechDef.SetInventory(DefaultHelper.ClearInventory(mechDef, __instance));
                    }

                    Random rng = new Random();
                    if (!settings.HeadRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.Head.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.Head.CurrentInternalStructure = Math.Max(1f, mechDef.Head.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.LeftArmRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.LeftArm.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftArm.CurrentInternalStructure = Math.Max(1f, mechDef.LeftArm.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.RightArmRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.RightArm.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightArm.CurrentInternalStructure = Math.Max(1f, mechDef.RightArm.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.LeftLegRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.LeftLeg.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftLeg.CurrentInternalStructure = Math.Max(1f, mechDef.LeftLeg.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.RightLegRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.RightLeg.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightLeg.CurrentInternalStructure = Math.Max(1f, mechDef.RightLeg.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.CentralTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.CenterTorso.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.CenterTorso.CurrentInternalStructure = Math.Max(1f, mechDef.CenterTorso.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.RightTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.RightTorso.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.RightTorso.CurrentInternalStructure = Math.Max(1f, mechDef.RightTorso.CurrentInternalStructure * (float)rng.NextDouble());
                    }
                    if (!settings.LeftTorsoRepaired && (!settings.RepairMechLimbs || rng.NextDouble() > settings.RepairMechLimbsChance)) {
                        mechDef.LeftTorso.CurrentInternalStructure = 0f;
                    }
                    else if (settings.RandomStructureOnRepairedLimbs) {
                        mechDef.LeftTorso.CurrentInternalStructure = Math.Max(1f, mechDef.LeftTorso.CurrentInternalStructure * (float)rng.NextDouble());
                    }


                    // each component is checked and destroyed if the settings are set that way
                    // if RepairMechComponents is true it will variably make components either functional, nonfunctional, or destroyed based on settings
                    foreach (MechComponentRef mechComponent in mechDef.Inventory) {
                        if (!settings.RepairMechComponents || mechDef.IsLocationDestroyed(mechComponent.MountedLocation)) {
                            mechComponent.DamageLevel = ComponentDamageLevel.Destroyed;
                            continue;
                        }
                        if (settings.RepairMechComponents) {
                            double repairRoll = rng.NextDouble();
                            if (repairRoll <= settings.RepairComponentsFunctionalThreshold)
                                mechComponent.DamageLevel = ComponentDamageLevel.Functional;
                            else if (repairRoll <= settings.RepairComponentsNonFunctionalThreshold)
                                mechComponent.DamageLevel = ComponentDamageLevel.NonFunctional;
                            else mechComponent.DamageLevel = ComponentDamageLevel.Destroyed;
                        }
                    }

                    __instance.AddMech(0, mechDef, true, false, true, null);
                    SimGameInterruptManager interrupt = (SimGameInterruptManager)ReflectionHelper.GetPrivateField(__instance, "interruptQueue");
                    interrupt.DisplayIfAvailable();
                    __instance.MessageCenter.PublishMessage(new SimGameMechAddedMessage(mechDef, defaultMechPartMax, true));
                }

                return false;
            }
            catch (Exception e) {
                Logger.LogError(e);
                return true;
            }
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
                Logger.LogError(e);
            }
        }
    }
}
