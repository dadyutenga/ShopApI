namespace ShopApI.Helpers;

public static class MaskingHelper
{
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return email;

        var parts = email.Split('@');
        var local = parts[0];
        if (local.Length <= 2)
            return $"***@{parts[1]}";

        return $"{local.Substring(0, 2)}***@{parts[1]}";
    }
}
