using System.Collections.Generic;

namespace YWIL_YouWorkItLooks.Steps
{
    public static class MulticlassSteps
    {
        public static readonly List<string> MulticlassLabels = new List<string>()
        {
            "Step7.start",
            "Step7.true",
            "Step7.false.A",
            "Step7.false.B"
        };

        public static readonly List<string> MulticlassHelpMessages = new List<string>()
        {
            "Place the gray gear on top of the mechanism"
        };

        public static readonly List<string> MulticlassErrorMessages = new List<string>()
        {
            "Flip the gray gear, it is placed backwards",
            "Rotate the gear a bit until it fits properly"
        };
    }
}
