namespace cvsimporter
{
    public class RentalInfo
    {
        public int RentalID { get; set; }
        public string Instrument { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Display => $"{Instrument} ({StartDate:dd.MM.yyyy}" +
            $"{(EndDate.HasValue ? " - " + EndDate.Value.ToString("dd.MM.yyyy") : "")})";
    }
}