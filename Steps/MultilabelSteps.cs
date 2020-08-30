using System.Collections.Generic;

namespace YWIL_YouWorkItLooks.Steps
{
    public static class MultilabelSteps
    {
        public static readonly List<string> MultilabelLabels = new List<string>()
        {
            "Step0",
            "Step1",
            "Step2",
            "Step3",
            "true",
            "false",
            "Step4",
            "front",
            "back"
        };

        public static readonly List<string> MultilabelHelpMessages = new List<string>()
        {
            "Place the first part in the rectangular hole",
            "Place the red part as shown in the help image",
            "Place the pink part with the round facing up",
            "Place the gray rotor on top of the pink part"
        };

        public static readonly List<string> MultilabelErrorMessages = new List<string>()
        {
            "Flip the pink part, it is placed backwards"
        };
    }
}
