﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;

namespace StrategicOperations.Framework
{
    public static class BattleArmorUtils
    {
        public static void ReInitIndicator(this CombatHUD hud, AbstractActor actor)
        {
            var indicators = Traverse.Create(hud.InWorldMgr).Field("AttackDirectionIndicators")
                .GetValue<List<AttackDirectionIndicator>>();
            foreach (var indicator in indicators)
            {
                if (indicator.Owner.GUID == actor.GUID)
                {
                    indicator.Init(actor, hud);
                }
            }
        }

        public static bool IsAvailableBAAbility(this Ability ability)
        {
            var flag = true;
            if (ability.parentComponent != null)
            {
                flag = ability.parentComponent.IsFunctional;
                if (!flag)
                {
                    foreach (var componentAbility in ability.parentComponent.parent.ComponentAbilities)
                    {
                        if (componentAbility.parentComponent.IsFunctional && componentAbility.Def.Id == ability.Def.Id)
                        {
                            flag = true;
                            break;
                        }
                    }
                }
            }
            return ability.CurrentCooldown < 1 && (ability.Def.NumberOfUses < 1 || ability.NumUsesLeft > 0) && flag;
        }

        public static void MountBattleArmorToChassis(this AbstractActor carrier, AbstractActor battleArmor)
        {
            if (battleArmor is Mech battleArmorAsMech)
            {
                var baseScale = battleArmor.GameRep.transform.localScale;
                ModState.SavedBAScale.Add(battleArmor.GUID, baseScale);
                battleArmor.GameRep.transform.localScale = new Vector3(.01f, .01f, .01f);

                if (!ModState.BADamageTrackers.ContainsKey(battleArmorAsMech.GUID))
                {
                    ModState.BADamageTrackers.Add(battleArmorAsMech.GUID, new Classes.BA_DamageTracker(carrier.GUID, false, new Dictionary<int, int>()));
                }

                var tracker = ModState.BADamageTrackers[battleArmorAsMech.GUID];
                if (tracker.TargetGUID != carrier.GUID)
                {
                    tracker.TargetGUID = carrier.GUID;
                    tracker.BA_MountedLocations = new Dictionary<int, int>();
                }

                if (carrier.team.IsFriendly(battleArmorAsMech.team))
                {
                    var internalCap = carrier.getInternalBACap();
                    var currentInternalSquads = carrier.getInternalBASquads();
                    if (currentInternalSquads < internalCap)
                    {
                        ModInit.modLog.LogMessage($"[MountBattleArmorToChassis] - target unit {carrier.DisplayName} has internal BA capacity of {internalCap}. Currently used: {currentInternalSquads}, mounting squad internally.");
                        carrier.modifyInternalBASquads(1);
                        tracker.IsSquadInternal = true;
                        return;
                    }
                }

                foreach (ChassisLocations BattleArmorChassisLoc in Enum.GetValues(typeof(ChassisLocations)))
                {
                    var locationDef = battleArmorAsMech.MechDef.Chassis.GetLocationDef(BattleArmorChassisLoc);
                    if (locationDef.MaxArmor > 0f || locationDef.InternalStructure > 1f)
                    {
                        if (carrier is Mech mech)
                        {
                            if (carrier.team.IsEnemy(battleArmorAsMech.team))
                            {
                                var hasLoopedOnce = false;
                                loopagain:
                                foreach (ArmorLocation tgtMechLoc in ModState.MechArmorSwarmOrder)
                                {
                                    if (tracker.BA_MountedLocations.ContainsValue((int)tgtMechLoc) && !hasLoopedOnce)
                                    {
                                        continue;
                                    }
                                    var chassisLocSwarm =
                                        MechStructureRules.GetChassisLocationFromArmorLocation(tgtMechLoc);
                                    if (mech.GetLocationDamageLevel(chassisLocSwarm) > LocationDamageLevel.Penalized) continue;
                                    if (!tracker.BA_MountedLocations.ContainsKey((int)BattleArmorChassisLoc))
                                    {
                                        ModInit.modLog.LogMessage($"[MountBattleArmorToChassis] - mounting BA {BattleArmorChassisLoc} to enemy mech location {tgtMechLoc}.");
                                        tracker.BA_MountedLocations.Add((int)BattleArmorChassisLoc, (int)tgtMechLoc);
                                        break;
                                    }
                                    hasLoopedOnce = true;
                                    goto loopagain;
                                }
                            }
                            else if (carrier.team.IsFriendly(battleArmorAsMech.team))
                            {
                                var hasLoopedOnce = false;
                                loopagain:
                                foreach (ArmorLocation tgtMechLoc in ModState.MechArmorMountOrder)
                                {
                                    if (tracker.BA_MountedLocations.ContainsValue((int)tgtMechLoc) && !hasLoopedOnce)
                                    {
                                        continue;
                                    }
                                    var chassisLocSwarm =
                                        MechStructureRules.GetChassisLocationFromArmorLocation(tgtMechLoc);
                                    if (mech.GetLocationDamageLevel(chassisLocSwarm) > LocationDamageLevel.Penalized) continue;
                                    if (!tracker.BA_MountedLocations.ContainsKey((int)BattleArmorChassisLoc))
                                    {
                                        ModInit.modLog.LogMessage($"[MountBattleArmorToChassis] - mounting BA {BattleArmorChassisLoc} to friendly mech location {tgtMechLoc}.");
                                        tracker.BA_MountedLocations.Add((int)BattleArmorChassisLoc, (int)tgtMechLoc);
                                        break;
                                    }
                                    hasLoopedOnce = true;
                                    goto loopagain;
                                }
                            }
                        }
                        else if (carrier is Vehicle vehicle)
                        {
                            var hasLoopedOnce = false;
                            loopagain:
                            foreach (VehicleChassisLocations tgtVicLoc in ModState.VehicleMountOrder)
                            {
                                if (tracker.BA_MountedLocations.ContainsValue((int)tgtVicLoc) && !hasLoopedOnce)
                                {
                                    continue;
                                }

                                if (vehicle.GetLocationDamageLevel(tgtVicLoc) > LocationDamageLevel.Penalized) continue;
                                if (!tracker.BA_MountedLocations.ContainsKey((int)BattleArmorChassisLoc))
                                {
                                    ModInit.modLog.LogMessage($"[MountBattleArmorToChassis] - mounting BA {BattleArmorChassisLoc} to vehicle location {tgtVicLoc}.");
                                    tracker.BA_MountedLocations.Add((int)BattleArmorChassisLoc, (int)tgtVicLoc);
                                    break;
                                }
                                hasLoopedOnce = true;
                                goto loopagain;
                            }
                        }
                        else if (carrier is Turret turret)
                        {
                            ModInit.modLog.LogMessage($"[MountBattleArmorToChassis] - mounting BA {BattleArmorChassisLoc} to turret location {BuildingLocation.Structure}.");
                            tracker.BA_MountedLocations.Add((int)BattleArmorChassisLoc, (int)BuildingLocation.Structure);
                        }
                        else
                        {
                            //borken
                        }
                    }
                }
            }
        }

        public static int getInternalBACap(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<int>("InternalBattleArmorSquadCap");
        }
        public static int getInternalBASquads(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<int>("InternalBattleArmorSquads");
        }

        public static void modifyInternalBASquads(this AbstractActor actor, int value)
        {
            actor.StatCollection.ModifyStat("BAMountDismount", -1, "InternalBattleArmorSquads", StatCollection.StatOperation.Int_Add, value);
        }

        public static int getAvailableInternalBASpace(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<int>("InternalBattleArmorSquadCap") - actor.StatCollection.GetValue<int>("InternalBattleArmorSquads");
        }

        public static bool getHasBattleArmorMounts(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<bool>("HasBattleArmorMounts");
        }
        public static bool getIsBattleArmorHandsy(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<bool>("IsBattleArmorHandsy");
        }

        public static bool HasMountedUnits(this AbstractActor actor)
        {
            return ModState.PositionLockMount.ContainsValue(actor.GUID);
        }
        public static bool IsMountedUnit(this AbstractActor actor)
        {
            return ModState.PositionLockMount.ContainsKey(actor.GUID);
        }

        public static bool HasSwarmingUnits(this AbstractActor actor)
        {
            return ModState.PositionLockSwarm.ContainsValue(actor.GUID);
        }
        public static bool IsSwarmingUnit(this AbstractActor actor)
        {
            return ModState.PositionLockSwarm.ContainsKey(actor.GUID);
        }

        public static Vector3 FetchAdjacentHex(AbstractActor actor)
        {
            var points = actor.Combat.HexGrid.GetAdjacentPointsOnGrid(actor.CurrentPosition);
            var point = points.GetRandomElement();
            point.y = actor.Combat.MapMetaData.GetLerpedHeightAt(point, false);
            return point;
        }

        public static void DismountBA (this AbstractActor actor, AbstractActor carrier, bool calledFromHandleDeath = false)
        {
            if (ModState.BADamageTrackers.ContainsKey(actor.GUID))
            {
                ModState.BADamageTrackers.Remove(actor.GUID);
            }

            var em = actor.Combat.EffectManager;
            var effects = em.GetAllEffectsTargetingWithUniqueID(actor, ModState.BAUnhittableEffect.ID);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                ModInit.modLog.LogMessage(
                    $"[DismountBA] Cancelling effect on {actor.DisplayName}: {effects[i].EffectData.Description.Name}.");
                em.CancelEffect(effects[i]);
            }
            var hud = Traverse.Create(CameraControl.Instance).Property("HUD").GetValue<CombatHUD>();
            //actor.GameRep.IsTargetable = true;
            if (ModState.SavedBAScale.ContainsKey(actor.GUID))
            {
                actor.GameRep.transform.localScale = ModState.SavedBAScale[actor.GUID];
                ModState.SavedBAScale.Remove(actor.GUID);
            }

            ModState.PositionLockMount.Remove(actor.GUID);
            ModState.PositionLockSwarm.Remove(actor.GUID);
            ModState.CachedUnitCoordinates.Remove(carrier.GUID);

            if (!calledFromHandleDeath)
            {
                hud.MechWarriorTray.JumpButton.ResetButtonIfNotActive(actor);
                hud.MechWarriorTray.SprintButton.ResetButtonIfNotActive(actor);
                hud.MechWarriorTray.MoveButton.ResetButtonIfNotActive(actor);
                hud.SelectionHandler.AddJumpState(actor);
                hud.SelectionHandler.AddSprintState(actor);
                hud.SelectionHandler.AddMoveState(actor);
                actor.ResetPathing(false);
                actor.Pathing.UpdateCurrentPath(false);
            }

            ModInit.modLog.LogMessage(
                $"[DismountBA] Removing PositionLock with rider  {actor.DisplayName} {actor.GUID} and carrier {carrier.DisplayName} {carrier.GUID}.");

        }
    }
}