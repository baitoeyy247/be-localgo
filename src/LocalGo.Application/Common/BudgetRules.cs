namespace LocalGo.Application.Common;

/// <summary>Normalizes marketplace budget input from FE codes and legacy Thai labels.</summary>
public static class BudgetRules
{
    public const string BudgetTypeOpen = "open";
    public const string BudgetTypeFixed = "fixed";

    public static string NormalizeBudgetText(string? budgetText, string? budgetType)
    {
        if (string.Equals(budgetType, BudgetTypeOpen, StringComparison.OrdinalIgnoreCase))
        {
            return FieldRules.OpenBudgetCanonical;
        }

        var text = budgetText?.Trim() ?? string.Empty;
        if (FieldRules.IsOpenBudget(text))
        {
            return FieldRules.OpenBudgetCanonical;
        }

        return text;
    }
}
