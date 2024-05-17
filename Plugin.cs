using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace VentSpawnFix
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.ventspawnfix", PLUGIN_NAME = "Vent Spawn Fix", PLUGIN_VERSION = "1.0.0";
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
                Plugin.Logger.LogInfo($"A new enemy tried to occupy vent with {vent.enemyType.name} already inside");

                List<EnemyVent> vents = new();
                foreach (EnemyVent enemyVent in __instance.allEnemyVents)
                    if (!enemyVent.occupied)
                        vents.Add(enemyVent);
                
                if (vents.Count < 1)
                {
                    Plugin.Logger.LogInfo("All vents on the map are occupied");
                    __result = false;
                    return false;
                }

                vent = vents[Random.Range(0, vents.Count)];
                Plugin.Logger.LogInfo("Enemy successfully reassigned to another empty vent");
            }

            return true;
        }
    }
}