using System;

namespace Vendr.Contrib.PaymentProviders.QuickPay.Extensions
{
    ///<summary>
    /// String extension methods
    ///</summary>
    public static class StringExtensions
    {
        public static string TrimStart(this string value, string trimString)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (string.IsNullOrEmpty(trimString)) return value;

            string result = value;
            while (result.StartsWith(trimString, StringComparison.InvariantCultureIgnoreCase))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string value, string trimString)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (string.IsNullOrEmpty(trimString)) return value;

            string result = value;
            while (result.EndsWith(trimString, StringComparison.InvariantCultureIgnoreCase))
            {
                result = result.Substring(0, result.Length - trimString.Length);
            }

            return result;
        }
    }
}
