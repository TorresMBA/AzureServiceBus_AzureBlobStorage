namespace SalesCsv.Domain;

public class SaleRow 
{
    public long OrderId { get; set; }

    public string? CustomerName { get; set; }

    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTime CreatedUtc { get; set; }
}
