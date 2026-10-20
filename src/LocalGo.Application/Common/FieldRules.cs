using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalGo.Application.Common;

public static class FieldRules
{
    public const string OpenBudget = "เปิดรับราคา";
    public const int BudgetMin = 1;
    public const int BudgetMax = 1_000_000;

    public static void RequireThaiMobile(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        var digits = phone.Trim();
        if (digits.Length != 10 || digits[0] != '0' || !digits.All(char.IsDigit))
        {
            throw new AppException("เบอร์โทรต้องเป็นเบอร์มือถือ 10 หลัก ขึ้นต้นด้วย 0", 400);
        }
    }

    public static void RequireBudget(string? budgetText)
    {
        if (string.IsNullOrWhiteSpace(budgetText))
        {
            throw new AppException("กรุณาระบุงบประมาณหรือเลือกเปิดรับราคา", 400);
        }

        var text = budgetText.Trim();
        if (text == OpenBudget)
        {
            return;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || amount < BudgetMin
            || amount > BudgetMax)
        {
            throw new AppException("งบประมาณต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000 หรือเปิดรับราคา", 400);
        }
    }

    public static void RequirePrice(string? price)
    {
        if (string.IsNullOrWhiteSpace(price) || !IsPriceText(price))
        {
            throw new AppException("ราคาต้องเป็นตัวเลขหรือช่วงราคา เช่น 40 หรือ 40-80", 400);
        }
    }

    public static bool IsPriceText(string price)
    {
        var trimmed = price.Trim().Replace("บาท", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (trimmed.Length == 0
            || trimmed.StartsWith('-')
            || trimmed.StartsWith('–')
            || trimmed.StartsWith('—')
            || trimmed.StartsWith("ถึง", StringComparison.Ordinal))
        {
            return false;
        }

        // Keep empty segments so a leading or doubled separator ("-1", "40--80") is not a valid range.
        var parts = trimmed.Split(["-", "–", "—", "ถึง"], StringSplitOptions.None);
        if (parts.Length is < 1 or > 2 || parts.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        if (!TryPositiveAmount(parts[0], out var low))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        return TryPositiveAmount(parts[1], out var high) && low <= high;
    }

    private static bool TryPositiveAmount(string text, out decimal amount)
    {
        amount = 0;
        if (!PositiveAmountPattern.IsMatch(text))
        {
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        return amount > 0 && amount <= BudgetMax;
    }

    private static readonly Regex PositiveAmountPattern = new(@"^\d+(?:\.\d+)?$", RegexOptions.CultureInvariant);
}
