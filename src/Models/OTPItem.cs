namespace HedKam.Services.Models;

public class OTPItem
{
    public required string Code { get; set; }
    public required string ClientName { get; set; }
    public DateTimeOffset ExpireIn { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public int Attempts { get; set; }
}
