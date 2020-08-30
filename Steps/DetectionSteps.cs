using System.Collections.Generic;

namespace YWIL_YouWorkItLooks.Steps
{
    public static class DetectionSteps
    {
        public static readonly List<string> DetectionLabels = new List<string>()
        {
            "Step5.hole",
            "Step5.part",
            "Step5.error",
            "Step6.hole",
            "Step6.part"
        };

        public static readonly List<string> DetectionHelpMessages = new List<string>()
        {
            "Object Detection model for steps 5 and 6 ready",
            "Place the green parts in the four square holes",
            "Place the orange part in the central round hole",
            "Congratulations! Let's move on to the last step"
        };

        public static readonly List<string> DetectionErrorMessages = new List<string>()
        {
            "Remove the marked part, it is in the wrong hole"
        };
    }
}
