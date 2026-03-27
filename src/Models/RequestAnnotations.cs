namespace Kx.Resty.Models;

public class RequestAnnotations
{
    public bool NoRedirect       { get; set; }
    public bool NoLog            { get; set; }
    public bool NoCookieJar      { get; set; }
    public bool NoAutoEncoding   { get; set; }
    public int? TimeoutSeconds          { get; set; }
    public int? ConnectionTimeoutSeconds { get; set; }
}
