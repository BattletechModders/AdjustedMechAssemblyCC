using System;
using Harmony;
using System.Reflection;

namespace AdjustedMechAssembly {
    public class AdjustedMechAssembly {
        internal static string ModDirectory;
        public static void Init(string directory, string settingsJSON) {
            try
            {
                var harmony = HarmonyInstance.Create("de.morphyum.AdjustedMechAssembly");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                ModDirectory = directory;
            }
            catch (Exception e)
            {
                FileLog.Log(e.ToString());
                Logger.LogError(e);
            }
        }
    }
}
