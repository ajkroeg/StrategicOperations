﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abilifier;
using Abilifier.Patches;
using BattleTech;
using BattleTech.UI;
using CustomComponents;
using Harmony;
using HBS.Pooling;
using StrategicOperations.Framework;
using UnityEngine;
using UnityEngine.UI;
using MechStructureRules = BattleTech.MechStructureRules;
using Random = System.Random;

namespace StrategicOperations.Patches
{
    public class BattleArmorPatches
    {
        [HarmonyPatch(typeof(AbstractActor), "InitEffectStats",
            new Type[] {})]
        public static class AbstractActor_InitEffectStats
        {
            public static void Postfix(AbstractActor __instance)
            {
                __instance.StatCollection.AddStatistic<int>("InternalBattleArmorSquadCap", 0);
                __instance.StatCollection.AddStatistic<int>("InternalBattleArmorSquads", 0);
                __instance.StatCollection.AddStatistic<bool>("HasBattleArmorMounts", false);
                __instance.StatCollection.AddStatistic<bool>("IsBattleArmorHandsy", false);
                __instance.StatCollection.AddStatistic<bool>("BattleArmorMount", false);
                __instance.StatCollection.AddStatistic<bool>("BattleArmorDeSwarmerSwat", false);
                __instance.StatCollection.AddStatistic<bool>("BattleArmorDeSwarmerRoll", false);
            }
        }

        [HarmonyPatch(typeof(AbilityExtensions.SelectionStateMWTargetSingle), "CanTargetCombatant",
            new Type[] {typeof(ICombatant)})]
        public static class SelectionStateMWTargetSingle_CanTargetCombatant
        {
            public static bool Prefix(AbilityExtensions.SelectionStateMWTargetSingle __instance, ICombatant potentialTarget, ref bool __result)
            {
                if (potentialTarget is AbstractActor targetActor)
                {
                    if (__instance.SelectedActor == targetActor)
                    {
                        __result = false;
                        return false;
                    }

                    if (__instance.SelectedActor.IsMountedUnit() && targetActor.HasMountedUnits())
                    {
                        if (ModState.PositionLockMount[__instance.SelectedActor.GUID] == targetActor.GUID)
                        {
                            __result = true;
                            return false;
                        }
                        __result = false;
                        return false;
                    }

                    if (__instance.SelectedActor.IsMountedUnit() && !targetActor.HasMountedUnits())
                    {
                        __result = false;
                        return false;
                    }

                    if (!__instance.SelectedActor.IsMountedUnit() && targetActor.HasMountedUnits())
                    {
                        if (targetActor.getAvailableInternalBASpace() > 0)
                        {
                            __result = true;
                            return false;
                        }
                        // figure out carrying capacity here and set true
                        __result = false;
                        return false;
                    }

                    if (__instance.SelectedActor.IsSwarmingUnit() && targetActor.HasSwarmingUnits())
                    {
                        if (ModState.PositionLockSwarm[__instance.SelectedActor.GUID] == targetActor.GUID)
                        {
                            __result = true;
                            return false;
                        }
                        __result = false;
                        return false;
                    }

                    if (__instance.SelectedActor.IsSwarmingUnit() && !targetActor.HasSwarmingUnits())
                    {
                        __result = false;
                        return false;
                    }

                    if (!__instance.SelectedActor.IsSwarmingUnit() && targetActor.HasSwarmingUnits())
                    {
                        __result = true;
                        return false;
                    }

                    if (potentialTarget.team.IsFriendly(__instance.SelectedActor.team))
                    {
                        if (!__instance.SelectedActor.getIsBattleArmorHandsy() && !targetActor.getHasBattleArmorMounts() && targetActor.getAvailableInternalBASpace() <= 0)
                        {
                            __result = false;
                            return false;
                        }
                    }

                }
                __result = true;
                return true;
            }
        }

        [HarmonyPatch(typeof(Ability), "Activate",
            new Type[] {typeof(AbstractActor), typeof(ICombatant)})]
        public static class Ability_Activate
        {
            public static void Postfix(Ability __instance, AbstractActor creator, ICombatant target)
            {
                if (creator == null) return;
                if (UnityGameInstance.BattleTechGame.Combat.ActiveContract.ContractTypeValue.IsSkirmish) return;

                if (__instance.IsAvailableBAAbility())
                {
                    if (target is AbstractActor targetActor)
                    {
                        if (creator.HasSwarmingUnits() && creator.GUID == targetActor.GUID)
                        {
                            var swarmingUnits = ModState.PositionLockSwarm.Where(x => x.Value == creator.GUID).ToList();

                                if (__instance.Def.Id == ModInit.modSettings.BattleArmorDeSwarmRoll)
                                {
                                    var baseChance = 0.5f;
                                    var pilotSkill = creator.GetPilot().Piloting;
                                    var finalChance = Mathf.Min(baseChance + (0.05f * pilotSkill), 0.95f);
                                    ModInit.modLog.LogMessage($"[Ability.Activate - BattleArmorDeSwarm] Deswarm chance: {finalChance} from baseChance {baseChance} + pilotSkill x 0.05 {0.05f * pilotSkill}, max 0.95.");
                                    var roll = ModInit.Random.NextDouble();
                                    foreach (var swarmingUnit in swarmingUnits)
                                    {
                                        var swarmingUnitActor = __instance.Combat.FindActorByGUID(swarmingUnit.Key);
                                        if (roll <= finalChance)
                                        {
                                            ModInit.modLog.LogMessage(
                                                $"[Ability.Activate - BattleArmorDeSwarm] Deswarm SUCCESS: {roll} <= {finalChance}.");
                                            var destroyBARoll = ModInit.Random.NextDouble();
                                            if (destroyBARoll <= .3f)
                                            {
                                                ModInit.modLog.LogMessage(
                                                    $"[Ability.Activate - DestroyBA on Roll] SUCCESS: {destroyBARoll} <=          0.30.");
                                                swarmingUnitActor.FlagForDeath("smooshed",
                                                    DeathMethod.VitalComponentDestroyed, DamageType.Melee, 8, -1,
                                                    creator.GUID, false);
                                                swarmingUnitActor.HandleDeath(creator.GUID);
                                            }
                                            else
                                            {
                                                ModInit.modLog.LogMessage(
                                                    $"[Ability.Activate - DestroyBA on Roll] FAILURE: {destroyBARoll} >          0.30.");
                                                swarmingUnitActor.DismountBA(creator);
                                            }
                                        }
                                        else
                                        {
                                            ModInit.modLog.LogMessage(
                                                $"[Ability.Activate - BattleArmorDeSwarm] Deswarm FAILURE: {roll} >          {finalChance}.");
                                        }
                                    }
                                }

                                else if (__instance.Def.Id == ModInit.modSettings.BattleArmorDeSwarmSwat)
                                {
                                    var baseChance = 0.3f;
                                    var pilotSkill = creator.GetPilot().Piloting;
                                    var missingActuatorCount = -8; 
                                    foreach (var armComponent in creator.allComponents.Where(x => x.IsFunctional && (x.Location == 2 || x.Location == 32)))
                                    {
                                        foreach (var CategoryID in ModInit.modSettings.ArmActuatorCategoryIDs)
                                        {
                                            if (armComponent.mechComponentRef.IsCategory(CategoryID))
                                            {
                                                missingActuatorCount += 1;
                                                break;
                                            }
                                        }
                                        
                                    }

                                    var finalChance = baseChance + (0.05f * pilotSkill) - (0.05f * missingActuatorCount);
                                    ModInit.modLog.LogMessage($"[Ability.Activate - BattleArmorDeSwarm] Deswarm chance: {finalChance} from baseChance {baseChance} + pilotSkill x 0.05 {0.05f * pilotSkill} - missingActuators x 0.05 {0.05f * missingActuatorCount}.");
                                    var roll = ModInit.Random.NextDouble();
                                    foreach (var swarmingUnit in swarmingUnits)
                                    {
                                        var swarmingUnitActor = __instance.Combat.FindActorByGUID(swarmingUnit.Key);
                                        if (roll <= finalChance)
                                        {
                                            ModInit.modLog.LogMessage(
                                                $"[Ability.Activate - BattleArmorDeSwarm] Deswarm SUCCESS: {roll} <=          {finalChance}.");
                                            swarmingUnitActor.DismountBA(creator);
                                        }
                                        else
                                        {
                                            ModInit.modLog.LogMessage(
                                                $"[Ability.Activate - BattleArmorDeSwarm] Deswarm FAILURE: {roll} > {finalChance}. Doing nothing and ending turn!");
                                        }
                                    }
                                }

                            if (creator is Mech mech)
                            {
                                mech.GenerateAndPublishHeatSequence(-1, true, false, mech.GUID);
                            }
                            if (__instance.Def.Id == ModInit.modSettings.BattleArmorDeSwarmRoll)
                            {
                                creator.FlagForKnockdown();
                                creator.HandleKnockdown(-1,creator.GUID,Vector2.one, null);
                            }
                            creator.DoneWithActor();
                            creator.OnActivationEnd(creator.GUID, -1);
                            return;
                        }

                        if (!creator.IsSwarmingUnit() && !creator.IsMountedUnit())
                        {
                            if (__instance.Def.Id == ModInit.modSettings.BattleArmorMountID && target.team.IsFriendly(creator.team))
                            {
                                foreach (var effectData in ModState.BAUnhittableEffect.effects)
                                {
                                    creator.Combat.EffectManager.CreateEffect(effectData, ModState.BAUnhittableEffect.ID,
                                        -1, creator, creator, default(WeaponHitInfo), 1);
                                }
                                targetActor.MountBattleArmorToChassis(creator);
                                //creator.GameRep.IsTargetable = false;
                                creator.TeleportActor(target.CurrentPosition);

                                //creator.GameRep.enabled = false;
                                //creator.GameRep.gameObject.SetActive(false);
                                //creator.GameRep.gameObject.Despawn();
                                //UnityEngine.Object.Destroy(creator.GameRep.gameObject);

                                //CombatMovementReticle.Instance.RefreshActor(creator); // or just end activation completely? definitely on use.
                                
                                ModState.PositionLockMount.Add(creator.GUID, target.GUID);
                                ModInit.modLog.LogMessage(
                                    $"[Ability.Activate - BattleArmorMountID] Added PositionLockMount with rider  {creator.DisplayName} {creator.GUID} and carrier {target.DisplayName} {target.GUID}.");
                                creator.DoneWithActor();//need to to onactivationend too
                                creator.OnActivationEnd(creator.GUID, -1);
                                
                            }
                            else if (__instance.Def.Id == ModInit.modSettings.BattleArmorMountID && target.team.IsEnemy(creator.team) && creator is Mech creatorMech)
                            {

                                var meleeChance = creator.Combat.ToHit.GetToHitChance(creator, creatorMech.MeleeWeapon, target, creator.CurrentPosition, target.CurrentPosition, 1, MeleeAttackType.Charge, false);
                                var roll = ModInit.Random.NextDouble();
                                ModInit.modLog.LogMessage(
                                    $"[Ability.Activate - BattleArmorSwarmID] Rolling simplified melee: roll {roll} vs hitChance {meleeChance}.");
                                if (roll <= meleeChance)
                                {
                                    foreach (var effectData in ModState.BAUnhittableEffect.effects)
                                    {
                                        creator.Combat.EffectManager.CreateEffect(effectData, ModState.BAUnhittableEffect.ID,
                                            -1, creator, creator, default(WeaponHitInfo), 1);
                                    }

                                    ModInit.modLog.LogMessage(
                                        $"[Ability.Activate - BattleArmorSwarmID] Cleaning up dummy attacksequence.");
                                    targetActor.MountBattleArmorToChassis(creator);
                                    //creator.GameRep.IsTargetable = false;
                                    creator.TeleportActor(target.CurrentPosition);

                                    //creator.GameRep.enabled = false;
                                    //creator.GameRep.gameObject.SetActive(false); //this might be the problem with attacking.
                                    //creator.GameRep.gameObject.Despawn();
                                    //UnityEngine.Object.Destroy(creator.GameRep.gameObject);
                                    //CombatMovementReticle.Instance.RefreshActor(creator);

                                    ModState.PositionLockSwarm.Add(creator.GUID, target.GUID);
                                    ModInit.modLog.LogMessage(
                                        $"[Ability.Activate - BattleArmorSwarmID] Added PositionLockSwarm with rider  {creator.DisplayName} {creator.GUID} and carrier {target.DisplayName} {target.GUID}.");
                                    creator.ResetPathing(false);
                                    creator.Pathing.UpdateCurrentPath(false);
                                    creator.DoneWithActor();
                                    creator.OnActivationEnd(creator.GUID, -1);
                                }
                                else
                                {

                                    ModInit.modLog.LogMessage(
                                        $"[Ability.Activate - BattleArmorSwarmID] Cleaning up dummy attacksequence.");
                                    ModInit.modLog.LogMessage(
                                        $"[Ability.Activate - BattleArmorSwarmID] No hits in HitInfo, plonking unit at target hex.");
                                    creator.TeleportActor(target.CurrentPosition);
                                    creator.ResetPathing(false);
                                    creator.Pathing.UpdateCurrentPath(false);
                                    creator.DoneWithActor();
                                    creator.OnActivationEnd(creator.GUID, -1);
                                }
                            }
                        }

                        else if (creator.IsSwarmingUnit() || creator.IsMountedUnit())
                        {
                            if (__instance.Def.Id == ModInit.modSettings.BattleArmorMountID)
                            {
                                creator.DismountBA(targetActor);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CombatHUDMechwarriorTray), "ResetMechwarriorButtons",
                new Type[] {typeof(AbstractActor)})]
        public static class CombatHUDMechwarriorTray_ResetMechwarriorButtons
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(CombatHUDMechwarriorTray __instance, AbstractActor actor)
            {
                if (UnityGameInstance.BattleTechGame.Combat.ActiveContract.ContractTypeValue.IsSkirmish) return;
                if (actor == null) return;
                if (actor.IsMountedUnit())
                {
                    ModInit.modLog.LogTrace(
                        $"[CombatHUDMechwarriorTray.ResetMechwarriorButtons] Actor {actor.DisplayName} {actor.GUID} found in PositionLockMount. Disabling buttons.");
                    __instance.FireButton.DisableButton();
                    __instance.MoveButton.DisableButton();
                    __instance.SprintButton.DisableButton();
                    __instance.JumpButton.DisableButton();
//                    __instance.DoneWithMechButton.DisableButton(); // we want this button
//                    __instance.EjectButton.DisableButton(); // we probably want this one too

                    var moraleButtons = Traverse.Create(__instance).Property("MoraleButtons")
                        .GetValue<CombatHUDActionButton[]>();

                    foreach (var moraleButton in moraleButtons)
                    {
                        moraleButton.DisableButton();
                    }

                    var abilityButtons = Traverse.Create(__instance).Property("AbilityButtons")
                        .GetValue<CombatHUDActionButton[]>();

                    foreach (var abilityButton in abilityButtons)
                    {
                        if (abilityButton?.Ability?.Def?.Id == ModInit.modSettings.BattleArmorMountID)
                            abilityButton?.DisableButton();
                    }
                    return;
                }
                else if (actor.IsSwarmingUnit())
                {
                    ModInit.modLog.LogTrace(
                        $"[CombatHUDMechwarriorTray.ResetMechwarriorButtons] Actor {actor.DisplayName} {actor.GUID} found in PositionLockSwarm. Disabling buttons.");
                    __instance.FireButton.DisableButton();
                    __instance.MoveButton.DisableButton();
                    __instance.SprintButton.DisableButton();
                    __instance.JumpButton.DisableButton();
                    //                    __instance.DoneWithMechButton.DisableButton(); // we want this button
                    //                    __instance.EjectButton.DisableButton(); // we probably want this one too

                    var moraleButtons = Traverse.Create(__instance).Property("MoraleButtons")
                        .GetValue<CombatHUDActionButton[]>();

                    foreach (var moraleButton in moraleButtons)
                    {
                        moraleButton.DisableButton();
                    }

                    var abilityButtons = Traverse.Create(__instance).Property("AbilityButtons")
                        .GetValue<CombatHUDActionButton[]>();

                    foreach (var abilityButton in abilityButtons)
                    {
                        if (abilityButton?.Ability?.Def?.Id == ModInit.modSettings.BattleArmorMountID)
                            abilityButton?.DisableButton();
                    }
                }
            }
        }


        [HarmonyPatch(typeof(AbstractActor), "HasLOFToTargetUnitAtTargetPosition",
            new Type[] { typeof(ICombatant), typeof(float), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Quaternion), typeof(bool) })]
        public static class AbstractActor_HasLOFToTargetUnitAtTargetPosition_Patch
        {
            static bool Prepare() => false; //disabled for now
            // make sure units doing swarming or riding cannot be targeted.
            public static void Postfix(AbstractActor __instance, ICombatant targetUnit, float maxRange, Vector3 attackPosition, Quaternion attackRotation, Vector3 targetPosition, Quaternion targetRotation, bool isIndirectFireCapable, ref bool __result)
            {
                if (targetUnit is AbstractActor targetActor)
                {
                    if (targetActor.IsSwarmingUnit() || targetActor.IsMountedUnit())
                    {
//                        ModInit.modLog.LogTrace($"[AbstractActor.HasLOFToTargetUnitAtTargetPosition] {targetActor.DisplayName} is swarming or mounted, preventing LOS.");
                        __result = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AbstractActor), "HasIndirectLOFToTargetUnit",
            new Type[] { typeof(Vector3), typeof(Quaternion), typeof(ICombatant), typeof(bool) })]
        public static class AbstractActor_HasIndirectLOFToTargetUnit_Patch
        {
            public static void Postfix(AbstractActor __instance, Vector3 attackPosition, Quaternion attackRotation, ICombatant targetUnit, bool enabledWeaponsOnly, ref bool __result)
            {
                if (__instance.IsSwarmingUnit() && targetUnit is AbstractActor targetActor)
                {
                    if (ModState.PositionLockSwarm[__instance.GUID] == targetActor.GUID)
                    {
//                        ModInit.modLog.LogTrace($"[AbstractActor.HasIndirectLOFToTargetUnit] {__instance.DisplayName} is swarming {targetActor.DisplayName}, forcing direct LOS for weapons");
                        __result = false;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Weapon), "WillFireAtTargetFromPosition",
            new Type[] {typeof(ICombatant), typeof(Vector3), typeof(Quaternion)})]
        public static class Weapon_WillFireAtTargetFromPosition
        {
            public static void Postfix(Weapon __instance, ICombatant target, Vector3 position, Quaternion rotation, ref bool __result)
            {
                if (__instance.parent.IsSwarmingUnit() && target is AbstractActor targetActor)
                {
                    if (ModState.PositionLockSwarm[__instance.parent.GUID] == targetActor.GUID)
                    {
 //                       ModInit.modLog.LogTrace($"[Weapon.WillFireAtTargetFromPosition] {__instance.parent.DisplayName} is swarming {targetActor.DisplayName}, forcing LOS for weapon {__instance.Name}");
                        __result = true;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(CombatHUDButtonBase), "OnClick",
            new Type[] { })]
        public static class CombatHUDButtonBase_OnClick
        {
            static bool Prepare() => true;
            public static void Prefix(CombatHUDButtonBase __instance)
            {
                if (__instance.GUID != "BTN_DoneWithMech") return;
                var hud = Traverse.Create(__instance).Property("HUD").GetValue<CombatHUD>();
                var actor = hud.SelectedActor;
                if (!actor.IsSwarmingUnit()) return;
                var target = actor.Combat.FindActorByGUID(ModState.PositionLockSwarm[actor.GUID]);
                ModInit.modLog.LogTrace($"[AbstractActor.DoneWithActor] Actor {actor.DisplayName} has active swarm attack on {target.DisplayName}");

                var weps = actor.Weapons.Where(x => x.IsEnabled && x.HasAmmo).ToList();

                //                var baselineAccuracyModifier = actor.StatCollection.GetValue<float>("AccuracyModifier");
                //                actor.StatCollection.Set<float>("AccuracyModifier", -99999.0f);
                //                ModInit.modLog.LogTrace($"[AbstractActor.DoneWithActor] Actor {actor.DisplayName} getting baselineAccuracyModifer set to {actor.AccuracyModifier}");

                var loc = ModState.BADamageTrackers[actor.GUID].BA_MountedLocations.Values.GetRandomElement();
                var attackStackSequence = new AttackStackSequence(actor, target, actor.CurrentPosition,
                    actor.CurrentRotation, weps, MeleeAttackType.NotSet, loc, -1);
                actor.Combat.MessageCenter.PublishMessage(new AddSequenceToStackMessage(attackStackSequence));

//                actor.StatCollection.Set<float>("AccuracyModifier", baselineAccuracyModifier);
//                ModInit.modLog.LogTrace($"[AbstractActor.DoneWithActor] Actor {actor.DisplayName} resetting baselineAccuracyModifer to {actor.AccuracyModifier}");
                return;
            }
        }


        [HarmonyPatch(typeof(SelectionStateFire), "ProcessClickedCombatant",
            new Type[] {typeof(ICombatant)})]
        public static class SelectionStateFire_ProcessClickedCombatant
        {
            static bool Prepare() => false; //disable for now, try with force-end turn.
            public static void Postfix(SelectionStateFire __instance, ref ICombatant combatant)
            {
                if (__instance.SelectedActor.IsSwarmingUnit())
                {
                    var newTarget =
                        __instance.SelectedActor.Combat.FindActorByGUID(
                            ModState.PositionLockSwarm[__instance.SelectedActor.GUID]);
                    combatant = newTarget;
                }
            }
        }

        [HarmonyPatch(typeof(Mech), "OnLocationDestroyed",
            new Type[] {typeof(ChassisLocations), typeof(Vector3), typeof(WeaponHitInfo), typeof(DamageType)})]
        public static class Mech_OnLocationDestroyed
        {
            public static void Prefix(Mech __instance, ChassisLocations location, Vector3 attackDirection,
                WeaponHitInfo hitInfo, DamageType damageType)
            {
                if (!__instance.HasMountedUnits() && !__instance.HasSwarmingUnits()) return;

                foreach (var squadInfo in ModState.BADamageTrackers.Where(x =>
                    x.Value.TargetGUID == __instance.GUID && !x.Value.IsSquadInternal &&
                    x.Value.BA_MountedLocations.ContainsValue((int)location)))
                {
                    ModInit.modLog.LogTrace(
                        $"[Mech.OnLocationDestroyed] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID}");
                    if (ModInit.Random.NextDouble() >= (double) 1 / 3) continue;
                    if (__instance.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int) location);
                        foreach (var mount in BattleArmorMounts)
                        {
                            var BALocArmor = (ArmorLocation) mount.Key;
                            var BALocStruct = MechStructureRules.GetChassisLocationFromArmorLocation(BALocArmor);
                            BattleArmorAsMech.NukeStructureLocation(hitInfo, 1, BALocStruct, attackDirection, damageType);
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(AbstractActor), "HandleDeath",
            new Type[] {typeof(string) })]
        public static class AbstractActor_HandleDeath
        {
            public static void Prefix(AbstractActor __instance, string attackerGUID)
            {
                if (__instance.HasSwarmingUnits())
                {
                    var swarmingUnits = ModState.PositionLockSwarm.Where(x => x.Value == __instance.GUID).ToList();
                    var wereSwarmingUnitsResponsible = swarmingUnits.Any(x => x.Key == attackerGUID);
                    foreach (var swarmingUnit in swarmingUnits)
                    {
                        var actor = __instance.Combat.FindActorByGUID(swarmingUnit.Key);

                        if (ModInit.Random.NextDouble() <= (double)1 / 3 && !wereSwarmingUnitsResponsible)
                        {
                            actor.FlagForDeath("MountDestroyed", DeathMethod.VitalComponentDestroyed, DamageType.Combat, 1, -1, attackerGUID, false);
                            actor.HandleDeath(attackerGUID);
                            continue;
                        }

                        actor.DismountBA(__instance, true);
                    }
                }

                if (__instance.HasMountedUnits())
                {
                    var mountedUnits = ModState.PositionLockMount.Where(x => x.Value == __instance.GUID);
                    foreach (var mountedUnit in mountedUnits)
                    {
                        var actor = __instance.Combat.FindActorByGUID(mountedUnit.Key);

                        if (ModInit.Random.NextDouble() <= (double)1 / 3)
                        {
                            actor.FlagForDeath("MountDestroyed", DeathMethod.VitalComponentDestroyed, DamageType.Combat, 1, -1, attackerGUID, false);
                            actor.HandleDeath(attackerGUID);
                            continue;
                        }
                        actor.DismountBA(__instance, true);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CombatHUDMechwarriorTray), "ResetAbilityButton",
            new Type[] { typeof(AbstractActor), typeof(CombatHUDActionButton), typeof(Ability), typeof(bool) })]
        public static class CombatHUDMechwarriorTray_ResetAbilityButton_Patch
        {
            public static void Postfix(CombatHUDMechwarriorTray __instance, AbstractActor actor, CombatHUDActionButton button, Ability ability, bool forceInactive)
            {
                if (UnityGameInstance.BattleTechGame.Combat.ActiveContract.ContractTypeValue.IsSkirmish) return;
                if (actor == null || ability == null) return;
//                if (button == __instance.FireButton)
//                {
 //                   ModInit.modLog.LogTrace(
 //                       $"Leaving Fire Button Enabled");
 //                   return;
//                }
                if (actor.IsMountedUnit() || actor.IsSwarmingUnit())
                {
                    button.DisableButton();
                }

                if (ability.Def.Id == ModInit.modSettings.BattleArmorDeSwarmRoll ||
                    ability.Def.Id == ModInit.modSettings.BattleArmorDeSwarmSwat)
                {
                    if (actor is Vehicle vehicle)
                    {
                        button.DisableButton();
                    }

                    if (!actor.HasSwarmingUnits())
                    {
                        button.DisableButton();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CombatSelectionHandler), "AddSprintState",
            new Type[] {typeof(AbstractActor)})]
        public static class CombatSelectionHandler_AddSprintState
        {
            public static bool Prefix(CombatSelectionHandler __instance, AbstractActor actor)
            {
                if (actor.IsMountedUnit() || actor.IsSwarmingUnit())
                {
                    ModInit.modLog.LogTrace($"[CombatSelectionHandler.AddSprintState] Actor {actor.DisplayName}: Disabling SprintState");
                    var SelectionStack = Traverse.Create(__instance).Property("SelectionStack").GetValue<List<SelectionState>>();
                    if (!SelectionStack.Any(x => x is SelectionStateDoneWithMech))
                    {
                        var HUD = Traverse.Create(__instance).Property("HUD").GetValue<CombatHUD>();
                        var doneState = new SelectionStateDoneWithMech(actor.Combat, HUD,
                            HUD.MechWarriorTray.DoneWithMechButton, actor);
                        var addState = Traverse.Create(__instance)
                            .Method("addNewState", new Type[] {typeof(SelectionState)});
                        addState.GetValue(doneState);
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CombatSelectionHandler), "AddMoveState",
            new Type[] { typeof(AbstractActor) })]
        public static class CombatSelectionHandler_AddMoveState
        {
            public static bool Prefix(CombatSelectionHandler __instance, AbstractActor actor)
            {
                if (actor.IsMountedUnit() || actor.IsSwarmingUnit())
                {
                    ModInit.modLog.LogTrace($"[CombatSelectionHandler.AddMoveState] Actor {actor.DisplayName}: Disabling AddMoveState");
                    var SelectionStack = Traverse.Create(__instance).Property("SelectionStack").GetValue<List<SelectionState>>();
                    if (!SelectionStack.Any(x => x is SelectionStateDoneWithMech))
                    {
                        var HUD = Traverse.Create(__instance).Property("HUD").GetValue<CombatHUD>();
                        var doneState = new SelectionStateDoneWithMech(actor.Combat, HUD,
                            HUD.MechWarriorTray.DoneWithMechButton, actor);
                        var addState = Traverse.Create(__instance)
                            .Method("addNewState", new Type[] { typeof(SelectionState) });
                        addState.GetValue(doneState);
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Mech), "DamageLocation",
            new Type[] {typeof(int), typeof(WeaponHitInfo), typeof(ArmorLocation), typeof(Weapon), typeof(float), typeof(float), typeof(int), typeof(AttackImpactQuality), typeof(DamageType)})]
        public static class Mech_DamageLocation_Patch
        {
            public static void Prefix(Mech __instance, int originalHitLoc, WeaponHitInfo hitInfo, ArmorLocation aLoc, Weapon weapon, ref float totalArmorDamage, ref float directStructureDamage, int hitIndex, AttackImpactQuality impactQuality, DamageType damageType)
            {
                if (!__instance.HasMountedUnits() && !__instance.HasSwarmingUnits()) return;

                foreach (var squadInfo in ModState.BADamageTrackers.Where(x => x.Value.TargetGUID == __instance.GUID && !x.Value.IsSquadInternal && x.Value.BA_MountedLocations.ContainsValue((int)aLoc)))
                {
                    ModInit.modLog.LogTrace($"[Mech.DamageLocation] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID}");
                    if (ModInit.Random.NextDouble() > (double)1 / 3) continue;
                    if (__instance.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        if (BattleArmorAsMech.GUID == hitInfo.attackerId) return;
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int) aLoc);
                        foreach (var mount in BattleArmorMounts)
                        {
                            var BALocArmor = (ArmorLocation) mount.Key;
                            //var BALocArmorString = BattleArmorAsMech.GetStringForArmorLocation(BALocArmor);
                            var BALocStruct = MechStructureRules.GetChassisLocationFromArmorLocation(BALocArmor);
                            //var BALocStructString = BattleArmorAsMech.GetStringForStructureLocation(BALocStruct);

                            var BattleArmorLocArmor = BattleArmorAsMech.ArmorForLocation((int) BALocArmor);
                            var BattleArmorLocStruct = BattleArmorAsMech.StructureForLocation((int) BALocStruct);

                            if (directStructureDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Mech.DamageLocation] directStructureDamage: {directStructureDamage}");
                                var directStructureDiff = directStructureDamage - BattleArmorLocStruct;
                                if (directStructureDiff >= 0)
                                {
                                    directStructureDamage -= BattleArmorLocStruct;
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Mech directStructureDamage decremented to {directStructureDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon, 0,
                                        BattleArmorLocStruct, hitIndex, damageType);
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocStruct} direct structure damage");
                                    continue;
                                }
                                
                                else if (directStructureDiff < 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Mech directStructureDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon, 0,
                                        Mathf.Abs(directStructureDamage), hitIndex, damageType);
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] Battle Armor at location {BALocArmor} takes {directStructureDamage} direct structure damage");
                                    directStructureDamage = 0;
                                }
                            }

                            if (totalArmorDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Mech.DamageLocation] totalArmorDamage: {totalArmorDamage}");
                                var totalArmorDamageDiff =
                                    totalArmorDamage - (BattleArmorLocArmor + BattleArmorLocStruct);
                                if (totalArmorDamageDiff > 0)
                                {
                                    totalArmorDamage -= totalArmorDamageDiff;
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Mech totalArmorDamage decremented to {totalArmorDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamageDiff), 0, hitIndex, damageType);
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocArmor} damage");
                                }

                                else if (totalArmorDamageDiff <= 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Mech totalArmorDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamage), 0, hitIndex, damageType);
                                    ModInit.modLog.LogMessage(
                                        $"[Mech.DamageLocation] Battle Armor at location {BALocArmor} takes {totalArmorDamage} damage");
                                    totalArmorDamage = 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Vehicle), "DamageLocation",
            new Type[] { typeof(WeaponHitInfo), typeof(int), typeof(VehicleChassisLocations), typeof(Weapon), typeof(float), typeof(float), typeof(AttackImpactQuality) })]
        public static class Vehicle_DamageLocation_Patch
        {
            public static void Prefix(Vehicle __instance, WeaponHitInfo hitInfo, int originalHitLoc, VehicleChassisLocations vLoc, Weapon weapon, ref float totalArmorDamage, ref float directStructureDamage, AttackImpactQuality impactQuality)
            {
                if (!__instance.HasMountedUnits() && !__instance.HasSwarmingUnits()) return;

                foreach (var squadInfo in ModState.BADamageTrackers.Where(x => x.Value.TargetGUID == __instance.GUID && !x.Value.IsSquadInternal && x.Value.BA_MountedLocations.ContainsValue((int)vLoc)))
                {
                    ModInit.modLog.LogTrace($"[Vehicle.DamageLocation] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID}");
                    if (ModInit.Random.NextDouble() > (double)1 / 3) continue;
                    if (__instance.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        if (BattleArmorAsMech.GUID == hitInfo.attackerId) return;
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int)vLoc);
                        foreach (var mount in BattleArmorMounts)
                        {
                            var BALocArmor = (ArmorLocation)mount.Key;
                            //var BALocArmorString = BattleArmorAsMech.GetStringForArmorLocation(BALocArmor);
                            var BALocStruct = MechStructureRules.GetChassisLocationFromArmorLocation(BALocArmor);
                            //var BALocStructString = BattleArmorAsMech.GetStringForStructureLocation(BALocStruct);

                            var BattleArmorLocArmor = BattleArmorAsMech.ArmorForLocation((int) BALocArmor);
                            var BattleArmorLocStruct = BattleArmorAsMech.StructureForLocation((int) BALocStruct);

                            if (directStructureDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Vehicle.DamageLocation] directStructureDamage: {directStructureDamage}");
                                var directStructureDiff = directStructureDamage - BattleArmorLocStruct;
                                if (directStructureDiff >= 0)
                                {
                                    directStructureDamage -= BattleArmorLocStruct;
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Vehicle directStructureDamage decremented to {directStructureDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon, 0,
                                        BattleArmorLocStruct, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocStruct} direct structure damage");
                                    continue;
                                }

                                else if (directStructureDiff < 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Vehicle directStructureDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon, 0,
                                        Mathf.Abs(directStructureDamage), 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] Battle Armor at location {BALocArmor} takes {directStructureDamage} direct structure damage");
                                    directStructureDamage = 0;
                                }
                            }

                            if (totalArmorDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Vehicle.DamageLocation] totalArmorDamage: {totalArmorDamage}");
                                var totalArmorDamageDiff =
                                    totalArmorDamage - (BattleArmorLocArmor + BattleArmorLocStruct);
                                if (totalArmorDamageDiff > 0)
                                {
                                    totalArmorDamage -= totalArmorDamageDiff;
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Vehicle totalArmorDamage decremented to {totalArmorDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamageDiff), 0, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocArmor} damage");
                                }

                                else if (totalArmorDamageDiff <= 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Vehicle totalArmorDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamage), 0, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Vehicle.DamageLocation] Battle Armor at location {BALocArmor} takes {totalArmorDamage} damage");
                                    totalArmorDamage = 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Turret), "DamageLocation",
            new Type[] { typeof(WeaponHitInfo), typeof(BuildingLocation), typeof(Weapon), typeof(float), typeof(float) })]
        public static class Turret_DamageLocation_Patch
        {
            public static void Prefix(Turret __instance, WeaponHitInfo hitInfo, BuildingLocation bLoc, Weapon weapon, ref float totalArmorDamage, ref float directStructureDamage)
            {
                if (bLoc == BuildingLocation.None || bLoc == BuildingLocation.Invalid)
                {
                    return;
                }

                if (!__instance.HasMountedUnits() && !__instance.HasSwarmingUnits()) return;

                foreach (var squadInfo in ModState.BADamageTrackers.Where(x => x.Value.TargetGUID == __instance.GUID && !x.Value.IsSquadInternal && x.Value.BA_MountedLocations.ContainsValue((int)bLoc)))
                {
                    ModInit.modLog.LogTrace($"[Turret.DamageLocation] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID}");
                    if (ModInit.Random.NextDouble() > (double)1 / 3) continue;
                    if (__instance.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int)bLoc);
                        foreach (var mount in BattleArmorMounts)
                        {
                            var BALocArmor = (ArmorLocation)mount.Key;
                            //var BALocArmorString = BattleArmorAsMech.GetStringForArmorLocation(BALocArmor);
                            var BALocStruct = MechStructureRules.GetChassisLocationFromArmorLocation(BALocArmor);
                            //var BALocStructString = BattleArmorAsMech.GetStringForStructureLocation(BALocStruct);

                            var BattleArmorLocArmor = BattleArmorAsMech.ArmorForLocation((int)BALocArmor);
                            var BattleArmorLocStruct = BattleArmorAsMech.StructureForLocation((int)BALocStruct);

                            if (directStructureDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Turret.DamageLocation] directStructureDamage: {directStructureDamage}");
                                var directStructureDiff = directStructureDamage - BattleArmorLocStruct;
                                if (directStructureDiff >= 0)
                                {
                                    directStructureDamage -= BattleArmorLocStruct;
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Turret directStructureDamage decremented to {directStructureDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon, 0,
                                        BattleArmorLocStruct, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocStruct} direct structure damage");
                                    continue;
                                }

                                else if (directStructureDiff < 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] directStructureDamage Diff: {directStructureDiff}. Turret directStructureDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int) BALocArmor, weapon, 0,
                                        Mathf.Abs(directStructureDamage), 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] Battle Armor at location {BALocArmor} takes {directStructureDamage} direct structure damage");
                                    directStructureDamage = 0;
                                }
                            }

                            if (totalArmorDamage > 0)
                            {
                                ModInit.modLog.LogMessage(
                                    $"[Turret.DamageLocation] totalArmorDamage: {totalArmorDamage}");
                                var totalArmorDamageDiff =
                                    totalArmorDamage - (BattleArmorLocArmor + BattleArmorLocStruct);
                                if (totalArmorDamageDiff > 0)
                                {
                                    totalArmorDamage -= totalArmorDamageDiff;
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Turret totalArmorDamage decremented to {totalArmorDamage}");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamageDiff), 0, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] Battle Armor at location {BALocArmor} takes {BattleArmorLocArmor} damage");
                                }

                                else if (totalArmorDamageDiff <= 0)
                                {
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] totalArmorDamageDiff Diff: {totalArmorDamageDiff}. Turret totalArmorDamage decremented to 0");
                                    BattleArmorAsMech.TakeWeaponDamage(hitInfo, (int)BALocArmor, weapon,
                                        Mathf.Abs(totalArmorDamage), 0, 1, DamageType.Combat);
                                    ModInit.modLog.LogMessage(
                                        $"[Turret.DamageLocation] Battle Armor at location {BALocArmor} takes {totalArmorDamage} damage");
                                    totalArmorDamage = 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CombatHUDMechTrayArmorHover), "setToolTipInfo",
            new Type[] {typeof(Mech), typeof(ArmorLocation)})]
        public static class CombatHUDMechTrayArmorHover_setToolTipInfo
        {
            public static void Postfix(CombatHUDMechTrayArmorHover __instance, Mech mech, ArmorLocation location)
            {
                if (!mech.HasSwarmingUnits() && !mech.HasMountedUnits()) return;
                var tooltip = Traverse.Create(__instance).Property("ToolTip").GetValue<CombatHUDTooltipHoverElement>();
                foreach (var squadInfo in ModState.BADamageTrackers.Where(x =>
                    x.Value.TargetGUID == mech.GUID && !x.Value.IsSquadInternal &&
                    x.Value.BA_MountedLocations.ContainsValue((int)location)))
                {
                    ModInit.modLog.LogTrace(
                        $"[CombatHUDMechTrayArmorHover.setToolTipInfo] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID} for tooltip infos");
                    
                    if (mech.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int) location);
                        foreach (var mount in BattleArmorMounts)
                        {

                            var BALocArmor = (ArmorLocation)mount.Key;
                            //var BALocArmorString = BattleArmorAsMech.GetStringForArmorLocation(BALocArmor);
                            var BALocStruct = MechStructureRules.GetChassisLocationFromArmorLocation(BALocArmor);
                            //var BALocStructString = BattleArmorAsMech.GetStringForStructureLocation(BALocStruct);

                            var BattleArmorLocArmor = BattleArmorAsMech.ArmorForLocation((int)BALocArmor);
                            var BattleArmorLocStruct = BattleArmorAsMech.StructureForLocation((int)BALocStruct);
                            var newText =
                                new Localize.Text(
                                    $"Battle Armor: Arm. {Mathf.RoundToInt(BattleArmorLocArmor)} / Str. {Mathf.RoundToInt(BattleArmorLocStruct)}",
                                    Array.Empty<object>());
                            if (mech.team.IsFriendly(BattleArmorAsMech.team))
                            {
                                tooltip.BuffStrings.Add(newText);
                            }
                            else
                            {
                                tooltip.DebuffStrings.Add(newText);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CombatHUDVehicleArmorHover), "setToolTipInfo",
            new Type[] { typeof(Vehicle), typeof(VehicleChassisLocations) })]
        public static class CombatHUDVehicleArmorHover_setToolTipInfo
        {
            public static void Postfix(CombatHUDVehicleArmorHover __instance, Vehicle vehicle, VehicleChassisLocations location)
            {
                if (!vehicle.HasSwarmingUnits() && !vehicle.HasMountedUnits()) return;
                var tooltip = Traverse.Create(__instance).Property("ToolTip").GetValue<CombatHUDTooltipHoverElement>();
                foreach (var squadInfo in ModState.BADamageTrackers.Where(x =>
                    x.Value.TargetGUID == vehicle.GUID && !x.Value.IsSquadInternal &&
                    x.Value.BA_MountedLocations.ContainsValue((int)location)))
                {
                    ModInit.modLog.LogTrace(
                        $"[CombatHUDMechTrayArmorHover.setToolTipInfo] Evaluating {squadInfo.Key} for {squadInfo.Value.TargetGUID} for tooltip infos");

                    if (vehicle.Combat.FindActorByGUID(squadInfo.Key) is Mech BattleArmorAsMech)
                    {
                        var BattleArmorMounts = squadInfo.Value.BA_MountedLocations.Where(x => x.Value == (int)location);
                        foreach (var mount in BattleArmorMounts)
                        {

                            var BALocArmor = (VehicleChassisLocations)mount.Key;
                            //var BALocArmorString = BattleArmorAsMech.GetStringForArmorLocation(BALocArmor);
                            //var BALocStructString = BattleArmorAsMech.GetStringForStructureLocation(BALocStruct);

                            var BattleArmorLocArmor = BattleArmorAsMech.ArmorForLocation((int)BALocArmor);
                            var BattleArmorLocStruct = BattleArmorAsMech.StructureForLocation((int)BALocArmor);
                            var newText =
                                new Localize.Text(
                                    $"Battle Armor: Arm. {Mathf.RoundToInt(BattleArmorLocArmor)} / Str. {Mathf.RoundToInt(BattleArmorLocStruct)}",
                                    Array.Empty<object>());
                            if (vehicle.team.IsFriendly(BattleArmorAsMech.team))
                            {
                                tooltip.BuffStrings.Add(newText);
                            }
                            else
                            {
                                tooltip.DebuffStrings.Add(newText);
                            }
                        }
                    }
                }
            }
        }
    }
}