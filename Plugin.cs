using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace VentSpawnFix
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(GUID_LOBBY_COMPATIBILITY, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.ventspawnfix", PLUGIN_NAME = "Vent Spawn Fix", PLUGIN_VERSION = "1.2.3";
        internal static new ManualLogSource Logger;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_LOBBY_COMPATIBILITY))
            {
                Logger.LogInfo("CROSS-COMPATIBILITY - Lobby Compatibility detected");
                LobbyCompatibility.Init();
            }

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
            // this is needed, actually, to avoid group spawns being overridden
            if (vent.occupied)
            {
                Plugin.Logger.LogDebug($"A new enemy tried to occupy vent with \"{vent.enemyType.enemyName}\" already inside");

                List<EnemyVent> vents = __instance.allEnemyVents.Where(enemyVent => !enemyVent.occupied).ToList();

                if (vents.Count < 1)
                {
                    Plugin.Logger.LogDebug("Enemy spawn cancelled because all vents on the map are occupied");
                    __result = false;
                    return false;
                }

                vent = vents[__instance.AnomalyRandom.Next(0, vents.Count)];
                Plugin.Logger.LogDebug("Enemy successfully reassigned to another empty vent");
            }

            return true;
        }

        [HarmonyPatch(typeof(RoundManager), "AssignRandomEnemyToVent")]
        [HarmonyPostfix]
        static void RoundManagerPostAssignRandomEnemyToVent(RoundManager __instance, EnemyVent vent, bool __result, int ___currentHour)
        {
            // returning false means no enemy was able to spawn
            if (!__result)
                return;

            EnemyType enemy = vent?.enemyType;
            if (enemy == null)
            {
                Plugin.Logger.LogWarning("AssignRandomEnemyToVent completed without assigning an enemy to the vent. This shouldn't happen");
                return;
            }

            if (vent.enemyTypeIndex < 0 || vent.enemyTypeIndex > __instance.currentLevel.Enemies.Count || !__instance.currentLevel.Enemies.Any(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType == enemy))
            {
                Plugin.Logger.LogWarning("AssignRandomEnemyToVent assigned an enemy with an invalid index. This shouldn't happen");
                return;
            }

            if (enemy.spawnInGroupsOf > 1)
            {
                Plugin.Logger.LogDebug($"Enemy \"{enemy.enemyName}\" spawned in vent, requesting group of {enemy.spawnInGroupsOf}");

                int spawnsLeft = enemy.spawnInGroupsOf - 1;
                List<EnemyVent> vents = __instance.allEnemyVents.Where(enemyVent => !enemyVent.occupied).ToList();

                while (spawnsLeft > 0)
                {
                    if (vents.Count <= 0)
                    {
                        Plugin.Logger.LogDebug($"Can't spawn additional \"{enemy.enemyName}\" (all vents are occupied)");
                        return;
                    }

                    if (enemy.numberSpawned >= enemy.MaxCount)
                    {
                        Plugin.Logger.LogDebug($"Can't spawn additional \"{enemy.enemyName}\" ({enemy.MaxCount} have already spawned)");
                        return;
                    }

                    if (enemy.PowerLevel > __instance.currentMaxInsidePower - __instance.currentEnemyPower)
                    {
                        Plugin.Logger.LogDebug($"Can't spawn additional \"{enemy.enemyName}\" ({__instance.currentEnemyPower} + {enemy.PowerLevel} would exceed max power level of {__instance.currentMaxInsidePower})");
                        return;
                    }

                    int time = (int)vent.spawnTime; //(int)(__instance.timeScript.lengthOfHours * ___currentHour) + __instance.AnomalyRandom.Next(10, (int)(__instance.timeScript.lengthOfHours * __instance.hourTimeBetweenEnemySpawnBatches));
                    EnemyVent vent2 = vents[__instance.AnomalyRandom.Next(0, vents.Count)];

                    __instance.currentEnemyPower += enemy.PowerLevel;
                    vent2.enemyType = enemy;
                    vent2.enemyTypeIndex = vent.enemyTypeIndex;
                    vent2.occupied = true;
                    vent2.spawnTime = time;
                    if (__instance.timeScript.hour - ___currentHour <= 0)
                        vent2.SyncVentSpawnTimeClientRpc(time, vent.enemyTypeIndex);
                    enemy.numberSpawned++;

                    __instance.enemySpawnTimes.Add(time);
                    vents.Remove(vent2);

                    Plugin.Logger.LogDebug($"Spawning additional \"{enemy.enemyName}\" in vent");
                    spawnsLeft--;
                }

                if (spawnsLeft < enemy.spawnInGroupsOf - 1)
                    __instance.enemySpawnTimes.Sort();
            }
        }
    }
}