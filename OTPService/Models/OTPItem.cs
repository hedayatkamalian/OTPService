namespace HedKam.Services.Models;

public class OTPItem
{
    public string Code { get; set; }
    public Guid TrackId { get; set; }
    public string ClientName { get; set; }
    public DateTimeOffset ExpireIn { get; set; }
}
