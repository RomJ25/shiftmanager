using System.Globalization;

namespace ShiftManager.Services
{
    public interface ILocalizationService
    {
        string FormatDate(DateTime date);
        string FormatTime(DateTime time);
        string FormatDateTime(DateTime dateTime);
        string FormatNumber(int number);
        string FormatDecimal(decimal number);
        string FormatCurrency(decimal amount);
        bool IsHebrew { get; }
        CultureInfo CurrentCulture { get; }
    }
}