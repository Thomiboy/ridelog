namespace RideLog.Application.Contact;

/// <summary>
/// Hard length caps for a contact submission, enforced at the endpoint and mirrored by the stored
/// column lengths. This is the first place an anonymous stranger writes a row (#168), so the caps are
/// a guard, not just validation.
/// </summary>
public static class ContactLimits
{
    public const int NameMax = 100;
    public const int EmailMax = 254;
    public const int MessageMax = 5000;
}
