namespace GameDeveloperKit.Story.Execution
{
    public static class TimeRules
    {
        public static bool IsFiniteNonNegative(double seconds)
        {
            return double.IsNaN(seconds) is false &&
                   double.IsInfinity(seconds) is false &&
                   seconds >= 0d;
        }

        public static bool IsFinitePositive(double seconds)
        {
            return double.IsNaN(seconds) is false &&
                   double.IsInfinity(seconds) is false &&
                   seconds > 0d;
        }
    }
}
