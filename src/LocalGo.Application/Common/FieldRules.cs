using System.Globalization;
using System.Text.RegularExpressions;

namespace LocalGo.Application.Common;

public static class FieldRules
{
    public const string OpenBudgetLegacy = "เปิดรับราคา";
    public const string OpenBudgetCanonical = "เปิดรับข้อเสนอ";
    public const string OpenBudget = OpenBudgetCanonical;
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

    public static bool IsOpenBudget(string? budgetText)
    {
        if (string.IsNullOrWhiteSpace(budgetText))
        {
            return false;
        }

        var text = budgetText.Trim();
        return text is OpenBudgetCanonical or OpenBudgetLegacy;
    }

    public static void RequireBudget(string? budgetText)
    {
        if (string.IsNullOrWhiteSpace(budgetText))
        {
            throw new AppException("กรุณาระบุงบประมาณหรือเลือกเปิดรับข้อเสนอ", 400);
        }

        var text = budgetText.Trim();
        if (IsOpenBudget(text))
        {
            return;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            || amount < BudgetMin
            || amount > BudgetMax)
        {
            throw new AppException(
                "งบประมาณต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000 หรือเปิดรับข้อเสนอ",
                400);
        }
    }

    /// <summary>
    /// Requires a desired start that is not in the past. An optional end must be the same instant or later.
    /// Returns the values normalized to UTC for timestamptz storage.
    /// </summary>
    public static (DateTime Start, DateTime? End) RequirePreferredTime(DateTime? start, DateTime? end, DateTime utcNow)
    {
        if (start is null)
        {
            throw new AppException("กรุณาเลือกวันที่และเวลาที่ต้องการ", 400);
        }

        var startUtc = AsUtc(start.Value);
        var earliest = AsUtc(utcNow).AddMinutes(-1);
        if (startUtc < earliest)
        {
            throw new AppException("วันที่และเวลาที่ต้องการต้องไม่เป็นเวลาในอดีต", 400);
        }

        if (end is null)
        {
            return (startUtc, null);
        }

        var endUtc = AsUtc(end.Value);
        if (endUtc < startUtc)
        {
            throw new AppException("เวลาสิ้นสุดต้องไม่ก่อนเวลาที่ต้องการ", 400);
        }

        return (startUtc, endUtc);
    }

    public static void RequireRequestText(string? title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AppException("กรุณากรอกหัวข้องาน", 400);
        }

        if (title.Trim().Length > 100)
        {
            throw new AppException("หัวข้องานต้องไม่เกิน 100 ตัวอักษร", 400);
        }

        if (description is not null && description.Trim().Length > 1000)
        {
            throw new AppException("รายละเอียดต้องไม่เกิน 1000 ตัวอักษร", 400);
        }
    }

    public static void RequireServiceTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new AppException("กรุณากรอกชื่อบริการ", 400);
        }

        if (title.Trim().Length > 80)
        {
            throw new AppException("ชื่อบริการต้องไม่เกิน 80 ตัวอักษร", 400);
        }
    }

    public static void RequirePrice(string? price)
    {
        if (string.IsNullOrWhiteSpace(price))
        {
            throw new AppException("กรุณากรอกราคา", 400);
        }

        var trimmed = NormalizePrice(price);
        if (trimmed.Contains('.') || trimmed.Contains(','))
        {
            throw new AppException("ราคาต้องเป็นจำนวนเต็ม", 400);
        }

        var parts = trimmed.Split(["-", "–", "—", "ถึง"], StringSplitOptions.None);
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var low)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var high)
            && low > high)
        {
            throw new AppException("ราคาต่ำสุดต้องไม่มากกว่าราคาสูงสุด", 400);
        }

        if (!IsPriceText(price))
        {
            throw new AppException("ราคาต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000 หรือช่วงราคาที่ต่ำไม่เกินสูง เช่น 40 หรือ 40-80", 400);
        }
    }

    /// <summary>Bid prices are a single integer baht amount, not a range.</summary>
    public static int RequireBidPrice(string? priceText, decimal? amount)
    {
        var trimmed = NormalizePrice(priceText ?? "");
        if (trimmed.Contains('.') || trimmed.Contains(','))
        {
            throw new AppException("ราคาต้องเป็นจำนวนเต็ม", 400);
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed < BudgetMin
            || parsed > BudgetMax)
        {
            throw new AppException("ราคาต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000", 400);
        }

        if (amount is decimal value && value != parsed)
        {
            throw new AppException("ราคาต้องเป็นจำนวนเต็มตั้งแต่ 1 ถึง 1000000", 400);
        }

        return parsed;
    }

    public static void RequireBidDescription(string? description)
    {
        if (description is not null && description.Trim().Length > 2000)
        {
            throw new AppException("รายละเอียดข้อเสนอต้องไม่เกิน 2000 ตัวอักษร", 400);
        }
    }

    public static void RequireReviewComment(string? comment)
    {
        if (comment is not null && comment.Trim().Length > 2000)
        {
            throw new AppException("ความคิดเห็นต้องไม่เกิน 2000 ตัวอักษร", 400);
        }
    }

    private static string NormalizePrice(string price) =>
        price.Trim().Replace("บาท", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);

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

        if (!decimal.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        return amount > 0 && amount <= BudgetMax && amount == decimal.Truncate(amount);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static readonly Regex PositiveAmountPattern = new(@"^\d+$", RegexOptions.CultureInvariant);
}
