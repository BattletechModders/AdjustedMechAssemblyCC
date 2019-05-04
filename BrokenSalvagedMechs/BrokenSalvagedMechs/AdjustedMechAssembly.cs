using System;
using Harmony;
using System.Reflection;

namespace AdjustedMechAssembly {
    public class AdjustedMechAssembly {

        internal static string ModDirectory;
        public static Logger Logger;

        public static void Init(string directory, string settingsJSON) {
            Logger = new Logger(directory, "adjusted_mech_assembly");
            try {
                var harmony = HarmonyInstance.Create("de.morphyum.AdjustedMechAssembly");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                ModDirectory = directory;
                Logger.Log($"Mod settings are: {settingsJSON}");
            } catch (Exception e) {
                FileLog.Log(e.ToString());
                Logger.Log(e.Message);
            }
        }
    }
}
