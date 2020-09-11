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
            "back",
            "Step7",
            "right",
            "wrong"
        };

        public static readonly List<string> MultilabelHelpMessages = new List<string>()
        {
            "Multilabel classification from 0 to 4 ready",
            "Waiting for the white base to be placed...",
            "Place the first part in the rectangular hole",
            "Place the red part as shown in the help image",
            "Place the pink part with the hole facing up",
            "Place the gray rotor on top of the pink part",
            "Well done! Let's continue with the next steps"
        };

        public static readonly List<string> MultilabelErrorMessages = new List<string>()
        {
            "Flip the pink part, it is placed backwards",
            "Remove the previous assembly from the base"
        };
    }
}
