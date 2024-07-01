using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VentSpawnFix
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.ventspawnfix", PLUGIN_NAME = "Vent Spawn Fix", PLUGIN_VERSION = "1.1.0";
        internal static new ManualLogSource Logger;

        void Awake()
        {
            Logger = base.Logger;

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class VentSpawnFixPatches
    {
        [HarmonyPatch(typeof(RoundManager), "AssignRandomEnemyToVent")]
        [HarmonyPrefix]
        static bool RoundManagerPreAssignRandomEnemyToVent(RoundManager __instance, ref EnemyVent vent, ref bool __result)
        {
            if (vent.occupied)
            {
                Plugin.Logger.LogInfo($"A new enemy tried to occupy vent with \"{vent.enemyType.enemyName}\" already inside");

                List<EnemyVent> vents = __instance.allEnemyVents.Where(enemyVent => !enemyVent.occupied).ToList();
                
                if (vents.Count < 1)
                {
                    Plugin.Logger.LogInfo("Enemy spawn cancelled because all vents on the map are occupied");
                    __result = false;
                    return false;
                }

                vent = vents[__instance.AnomalyRandom.Next(0, vents.Count)];
                Plugin.Logger.LogInfo("Enemy successfully reassigned to another empty vent");
            }

            return true;
        }

        [HarmonyPatch(typeof(RoundManager), "AssignRandomEnemyToVent")]
        [HarmonyPostfix]
        static void RoundManagerPostAssignRandomEnemyToVent(RoundManager __instance, EnemyVent vent, int ___currentHour)
        {
            EnemyType enemy = vent.enemyType;
            if (enemy.spawnInGroupsOf > 1)
            {
                Plugin.Logger.LogInfo($"Enemy \"{enemy.enemyName}\" spawned in vent, requesting group of {enemy.spawnInGroupsOf}");

                int enemyIndex = -1;
                for (int i = 0; i < __instance.currentLevel.Enemies.Count; i++)
                {
                    if (__instance.currentLevel.Enemies[i].enemyType == enemy)
                    {
                        enemyIndex = i;
                        break;
                    }
                }

                if (enemyIndex < 0)
                {
                    Plugin.Logger.LogWarning($"Tried to spawn additional \"{enemy.enemyName}\", but they do not exist in the current level's spawn pool");
                    return;
                }

                int spawnsLeft = enemy.spawnInGroupsOf - 1;
                List<EnemyVent> vents = __instance.allEnemyVents.Where(enemyVent => !enemyVent.occupied).ToList();

                while (spawnsLeft > 0)
                {
                    if (vents.Count <= 0)
                    {
                        Plugin.Logger.LogInfo($"Can't spawn additional \"{enemy.enemyName}\" (all vents are occupied)");
                        return;
                    }

                    if (enemy.numberSpawned >= enemy.MaxCount)
                    {
                        Plugin.Logger.LogInfo($"Can't spawn additional \"{enemy.enemyName}\" ({enemy.MaxCount} have already spawned)");
                        return;
                    }

                    if (enemy.PowerLevel > __instance.currentMaxInsidePower - __instance.currentEnemyPower)
                    {
                        Plugin.Logger.LogInfo($"Can't spawn additional \"{enemy.enemyName}\" ({__instance.currentEnemyPower} + {enemy.PowerLevel} would exceed max power level of {__instance.currentMaxInsidePower})");
                        return;
                    }

                    int time = (int)(__instance.timeScript.lengthOfHours * ___currentHour) + __instance.AnomalyRandom.Next(10, (int)(__instance.timeScript.lengthOfHours * __instance.hourTimeBetweenEnemySpawnBatches));
                    EnemyVent vent2 = vents[__instance.AnomalyRandom.Next(0, vents.Count)];

                    __instance.currentEnemyPower += enemy.PowerLevel;
                    vent2.enemyType = enemy;
                    vent2.enemyTypeIndex = enemyIndex;
                    vent2.occupied = true;
                    vent2.spawnTime = time;
                    if (__instance.timeScript.hour - ___currentHour <= 0)
                        vent2.SyncVentSpawnTimeClientRpc(time, enemyIndex);
                    enemy.numberSpawned++;

                    __instance.enemySpawnTimes.Add(time);
                    vents.Remove(vent2);

                    Plugin.Logger.LogInfo($"Spawning additional \"{enemy.enemyName}\" in vent");
                    spawnsLeft--;
                }

                if (spawnsLeft < enemy.spawnInGroupsOf - 1)
                    __instance.enemySpawnTimes.Sort();
            }
        }
    }
}