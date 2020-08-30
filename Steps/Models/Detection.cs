namespace YWIL_YouWorkItLooks.Steps.Models
{
    public class Detection : Classification
    {
        public Boundary Box { get; set; }
    }
}
