public class AllRentalInfo
{
    public string CustomerName { get; set; }
    public int CustomerId { get; set; }
    public string OrderNumber { get; set; }
    public DateTime RentalStart { get; set; }
    public DateTime? RentalEnd { get; set; }
    public bool IsEnded { get; set; }
    public decimal OffeneZahlungen { get; set; }
}