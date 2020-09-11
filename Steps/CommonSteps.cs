using System;
using System.Collections.Generic;

namespace YWIL_YouWorkItLooks.Steps
{
    public static class CommonSteps
    {
        public static readonly List<string> ModelFolders = new List<string>()
        {
            "ClassificationMultilabel",
            "ObjectDetection",
            "ClassificationMulticlass"
        };

        public static readonly List<string> StepCheckingColors = new List<string>()
        {
            "#ec1f24", // Red --> No
            "#0ea84b", // Green --> Yes
            "#868686" // Gray --> Pause - Arrow
        };

        public static readonly List<Uri> StepCheckingUris = new List<Uri>()
        {
            new Uri("pack://application:,,,/Resources/StepNo.png"),
            new Uri("pack://application:,,,/Resources/StepYes.png"),
            new Uri("pack://application:,,,/Resources/StepPause.png"),
            new Uri("pack://application:,,,/Resources/StepArrow.png"),
            new Uri("pack://application:,,,/Resources/AssemblyCompleted.png")
        };

        public static Uri GetImageStepUri(int step)
        {
            return new Uri($"pack://application:,,,/Resources/Step{step}.jpg");
        }
    }
}
