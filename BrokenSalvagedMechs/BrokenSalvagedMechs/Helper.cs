using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;

namespace AdjustedMechAssembly {
    public static class Helper
    {
        private static Settings _settings;
        public static Settings Settings
        {
            get
            {
                try
                {
                    if (_settings == null)
                    {
                        using (StreamReader r = new StreamReader($"{AdjustedMechAssembly.ModDirectory}/settings.json"))
                        {
                            string json = r.ReadToEnd();
                            _settings = JsonConvert.DeserializeObject<Settings>(json);
                        }
                    }
                    return _settings;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    return null;
                }
            }
        }

        private static List<int> _partsByWeightTable;

        public static List<int> PartsByWeightTable
        {
            get
            {
                if (_partsByWeightTable == null)
                {
                    _partsByWeightTable = new List<int>();
                    int tonnage = 5;
                    int partsRequired = 1;
                    foreach (int cutoff in Settings.WeightThresholds)
                    {
                        while (tonnage <= cutoff)
                        {
                            _partsByWeightTable.Add(partsRequired);
                            tonnage += 5;
                        }

                        ++partsRequired;
                    }
                }

                return _partsByWeightTable;
            }
        }

        public static int GetMechPartsRequiredByWeight(ChassisDef chassisDef)
        {
            int index = ((int) chassisDef.Tonnage) / 5 - 1;
            return index >= PartsByWeightTable.Count
                ? PartsByWeightTable.Count
                : index < 0
                ? PartsByWeightTable[0]
                : PartsByWeightTable[index];
        }

        public static int GetMechPartsRequired(this SimGameState simGameState, ChassisDef chassisDef)
        {
            return Settings.UseWeightThresholds 
                ? GetMechPartsRequiredByWeight(chassisDef)
                : simGameState.Constants.Story.DefaultMechPartMax;
        }

        public static int GetMechPartsRequired(this SimGameState simGameState, MechDef mechDef)
        {
            return simGameState.GetMechPartsRequired(mechDef.Chassis);
        }
    }
}