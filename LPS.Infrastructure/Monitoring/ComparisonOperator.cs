namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Defines comparison operators for metric evaluation.
    /// Used in failure rules, termination rules, and success rules.
    /// </summary>
    public enum ComparisonOperator
    {
        /// <summary>
        /// Equals (=)
        /// </summary>
        Equals,

        /// <summary>
        /// Not equals (!=)
        /// </summary>
        NotEquals,

        /// <summary>
        /// Greater than (>)
        /// </summary>
        GreaterThan,

        /// <summary>
        /// Less than (<)
        /// </summary>
        LessThan,

        /// <summary>
        /// Greater than or equal (>=)
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// Less than or equal (<=)
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// Between two values (between x and y)
        /// </summary>
        Between
    }
}
