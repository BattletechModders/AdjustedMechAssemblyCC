using System.Collections.Generic;

namespace AdjustedMechAssembly {
    public class Settings {
        public bool HeadRepaired = false;
        public bool LeftArmRepaired = false;
        public bool RightArmRepaired = false;
        public bool CentralTorsoRepaired = false;
        public bool LeftTorsoRepaired = false;
        public bool RightTorsoRepaired = false;
        public bool LeftLegRepaired = false;
        public bool RightLegRepaired = false;

        public bool RepairMechLimbs = false;
        public float RepairMechLimbsChance = 0.75f;
        public bool RandomStructureOnRepairedLimbs = false;

        public bool RepairMechComponents = false;
        public float RepairComponentsFunctionalThreshold = 0.25f;
        public float RepairComponentsNonFunctionalThreshold = 0.5f;

        public bool AssembleVariants = true;
        public bool AssembleMostParts = false;
        public List<string> VariantExceptions;

        public bool UseWeightThresholds = false;
        public List<int> WeightThresholds = new List<int> { 0, 0, 40, 45, 50, 60, 65, 70, 80, 85, 95, 95 };
    }
}
