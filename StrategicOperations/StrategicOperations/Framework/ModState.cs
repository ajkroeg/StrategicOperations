﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Abilifier;
using BattleTech;
using UnityEngine;
using static StrategicOperations.Framework.Classes;

namespace StrategicOperations.Framework
{
    public static class ModState
    {
        public static List<BA_Spawner> CurrentContractBASpawners = new List<BA_Spawner>();

        public static float SwarmSuccessChance = 0f;
        public static float DeSwarmSuccessChance = 0f;
        
        public static Dictionary<string, Dictionary<string, List<string>>> CachedFactionAssociations = new Dictionary<string, Dictionary<string, List<string>>>();
        public static Dictionary<string, Dictionary<string, List<AI_BeaconProxyInfo>>> CachedFactionCommandBeacons = new Dictionary<string, Dictionary<string, List<AI_BeaconProxyInfo>>>(); // key1 is abilityID, key2 is faction name

        public static Dictionary<string, int> CurrentBattleArmorSquads = new Dictionary<string, int>();
        public static Dictionary<string, Dictionary<string,int>> CurrentCommandUnits = new Dictionary<string, Dictionary<string, int>>();

        public static List<AI_FactionCommandAbilitySetting> CurrentFactionSettingsList = new List<AI_FactionCommandAbilitySetting>();

        public static bool IsStrafeAOE = false;
        public static Dictionary<string, PendingStrafeWave> PendingStrafeWaves =
            new Dictionary<string, PendingStrafeWave>();
        public static List<ArmorLocation> MechArmorMountOrder = new List<ArmorLocation>();
        public static List<ArmorLocation> MechArmorSwarmOrder = new List<ArmorLocation>();

        public static List<VehicleChassisLocations> VehicleMountOrder = new List<VehicleChassisLocations>();

        public static Dictionary<string, BA_DamageTracker> 
            BADamageTrackers = new Dictionary<string, BA_DamageTracker>(); // key is GUID of BA squad

        public static Dictionary<string, Vector3> SavedBAScale = new Dictionary<string, Vector3>();

        public static Dictionary<string, Vector3> CachedUnitCoordinates = new Dictionary<string, Vector3>();
        public static Dictionary<string, string> PositionLockMount = new Dictionary<string, string>(); // key is mounted unit, value is carrier
        public static Dictionary<string, string> PositionLockSwarm = new Dictionary<string, string>(); // key is mounted unit, value is carrier

        public static List<Ability> CommandAbilities = new List<Ability>();

        public static List<KeyValuePair<string, Action>>
            DeferredInvokeSpawns = new List<KeyValuePair<string, Action>>();

        public static List<KeyValuePair<string, Action>>
            DeferredInvokeBattleArmor = new List<KeyValuePair<string, Action>>();

        public static Dictionary<string, AbstractActor> DeferredDespawnersFromStrafe =
            new Dictionary<string, AbstractActor>();

        public static string DeferredActorResource = "";
        public static string PopupActorResource = "";
        public static int StrafeWaves;
        public static string PilotOverride= null;
        public static bool DeferredSpawnerFromDelegate;
        public static bool DeferredBattleArmorSpawnerFromDelegate;
        public static bool OutOfRange;

        public static Dictionary<string, AI_DealWithBAInvocation> AiDealWithBattleArmorCmds = new Dictionary<string, AI_DealWithBAInvocation>();

        public static Dictionary<string, AI_CmdInvocation> AiCmds = new Dictionary<string, AI_CmdInvocation>();

        public static Dictionary<string, BA_MountOrSwarmInvocation> AiBattleArmorAbilityCmds = new Dictionary<string, BA_MountOrSwarmInvocation>();

        public static List<CmdUseInfo> CommandUses = new List<CmdUseInfo>();

        public static List<CmdUseStat> DeploymentAssetsStats = new List<CmdUseStat>();

        public static BA_TargetEffect BAUnhittableEffect = new BA_TargetEffect();

        public static void Initialize()
        {
            MechArmorMountOrder.Add(ArmorLocation.CenterTorso);
            MechArmorMountOrder.Add(ArmorLocation.CenterTorsoRear);
            MechArmorMountOrder.Add(ArmorLocation.RightTorso);
            MechArmorMountOrder.Add(ArmorLocation.RightTorsoRear);
            MechArmorMountOrder.Add(ArmorLocation.LeftTorso);
            MechArmorMountOrder.Add(ArmorLocation.LeftTorsoRear);

            MechArmorSwarmOrder.Add(ArmorLocation.CenterTorso);
            MechArmorSwarmOrder.Add(ArmorLocation.CenterTorsoRear);
            MechArmorSwarmOrder.Add(ArmorLocation.RightTorso);
            MechArmorSwarmOrder.Add(ArmorLocation.RightTorsoRear);
            MechArmorSwarmOrder.Add(ArmorLocation.LeftTorso);
            MechArmorSwarmOrder.Add(ArmorLocation.LeftTorsoRear);
            MechArmorSwarmOrder.Add(ArmorLocation.LeftArm); // LA, RA, LL, RL, HD are for swarm only
            MechArmorSwarmOrder.Add(ArmorLocation.RightArm);
            MechArmorSwarmOrder.Add(ArmorLocation.LeftLeg);
            MechArmorSwarmOrder.Add(ArmorLocation.RightLeg);
            MechArmorSwarmOrder.Add(ArmorLocation.Head);

            VehicleMountOrder.Add(VehicleChassisLocations.Front);
            VehicleMountOrder.Add(VehicleChassisLocations.Rear);
            VehicleMountOrder.Add(VehicleChassisLocations.Left);
            VehicleMountOrder.Add(VehicleChassisLocations.Right);
            VehicleMountOrder.Add(VehicleChassisLocations.Turret);


            BAUnhittableEffect = ModInit.modSettings.BATargetEffect;
            foreach (var jObject in ModInit.modSettings.BATargetEffect.effectDataJO)
            {
                var effectData = new EffectData();
                effectData.FromJSON(jObject.ToString());
                BAUnhittableEffect.effects.Add(effectData);
            }
        }

        public static void ResetAll()
        {
            CurrentContractBASpawners = new List<BA_Spawner>();
            SwarmSuccessChance = 0f;
            DeSwarmSuccessChance = 0f;
            CurrentBattleArmorSquads = new Dictionary<string, int>();
            CurrentFactionSettingsList = new List<AI_FactionCommandAbilitySetting>();
            PendingStrafeWaves = new Dictionary<string, PendingStrafeWave>();
            BADamageTrackers = new Dictionary<string, BA_DamageTracker>(); 
            CommandAbilities = new List<Ability>();
            DeferredInvokeSpawns = new List<KeyValuePair<string, Action>>();
            DeferredInvokeBattleArmor = new List<KeyValuePair<string, Action>>();
            DeferredDespawnersFromStrafe = new Dictionary<string, AbstractActor>();
            CommandUses = new List<CmdUseInfo>();
            DeploymentAssetsStats = new List<CmdUseStat>();
            SavedBAScale = new Dictionary<string, Vector3>();
            CachedUnitCoordinates = new Dictionary<string, Vector3>();
            PositionLockMount = new Dictionary<string, string>();
            PositionLockSwarm = new Dictionary<string, string>();
            DeferredActorResource = "";
            PopupActorResource = "";
            StrafeWaves = 0; // this is TBD-> want to make beacons define # of waves.
            PilotOverride = null;
            DeferredSpawnerFromDelegate = false;
            DeferredBattleArmorSpawnerFromDelegate = false;
            OutOfRange = false;
            AiCmds = new Dictionary<string, AI_CmdInvocation>();
            AiBattleArmorAbilityCmds = new Dictionary<string, BA_MountOrSwarmInvocation>();
            IsStrafeAOE = false;
        }

        public static void ResetDelegateInfos()
        {
            DeferredSpawnerFromDelegate = false;
            DeferredActorResource = "";
            PopupActorResource = "";
            PilotOverride = null;
        }

        public static void ResetDeferredSpawners()
        {
            DeferredInvokeSpawns = new List<KeyValuePair<string, Action>>();
        }
        public static void ResetDeferredBASpawners()
        {
            DeferredInvokeBattleArmor = new List<KeyValuePair<string, Action>>();
        }
    }
}
