namespace HedKam.Services.Options;

public class OTPErrors
{
    public string TrackIdDoesNotExist { get; set; } = nameof(TrackIdDoesNotExist);
    public string CodeIsInvalid { get; set; } = nameof(CodeIsInvalid);
    public string CodeIsExpired { get; set; } = nameof(CodeIsExpired);
    public string ClientNameDoesNotMatch { get; set; } = nameof(ClientNameDoesNotMatch);

}
