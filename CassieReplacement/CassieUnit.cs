namespace CassieReplacement
{
    using System.Globalization;

    public static class CassieUnit
    {
        /// <summary>Splits a unit name such as "EPSILON-11" into its letter part and number.</summary>
        public static bool TryParse(string unitName, out string letter, out int number)
        {
            letter = string.Empty;
            number = 0;

            if (string.IsNullOrWhiteSpace(unitName))
            {
                return false;
            }

            int dash = unitName.IndexOf('-');
            if (dash <= 0 || dash == unitName.Length - 1)
            {
                return false;
            }

            letter = unitName.Substring(0, dash);
            return int.TryParse(unitName.Substring(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
    }
}
