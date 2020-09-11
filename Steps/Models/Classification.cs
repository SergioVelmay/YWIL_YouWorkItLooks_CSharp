namespace YWIL_YouWorkItLooks.Steps.Models
{
    public class Classification
    {
        public string Label { get; set; }

        public string Probability { get; set; }

        public string PredictionToString()
        {
            string[] words = Label.ToUpper().Split('.');

            if (Label.StartsWith("Step"))
            {
                words[0] = words[0].Insert(4, " #");
            }

            return $"{string.Join(" - ", words)}   ( {Probability}% )";
        }
    }
}
