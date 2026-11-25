namespace InventoryManagement.Domain.Validation;

internal static class TextValidation
{
    internal static string NormalizeRequired(
        string value,
        int maxLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalizedValue = value.Trim();
        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }
    internal static string? NormalizeOptional(
        string? value,
        int maxLength,
        string parameterName)
    {
        string? normalizedValue = value?.Trim();
        if (normalizedValue?.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return string.IsNullOrEmpty(normalizedValue)
            ? null
            : normalizedValue;
    }
}