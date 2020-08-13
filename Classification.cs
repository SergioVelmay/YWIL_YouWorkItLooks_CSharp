namespace YWIL_YouWorkItLooks
{
    public class Classification
    {
        public string Label { get; set; }

        public string Probability { get; set; }

        public string PredictionToString()
        {
            return $"{Label} ({Probability}%)";
        }
    }
}
