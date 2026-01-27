namespace LPS.UI.Common
{
    /// <summary>
    /// Simple helper to share command execution context across the application.
    /// Set by CommandLineManager at startup, consumed by services that need to know command type.
    /// </summary>
    public static class CommandContext
    {
        /// <summary>
        /// The command line arguments passed to the application.
        /// </summary>
        public static string[]? Args { get; private set; }

        /// <summary>
        /// True if the current command is a test execution command (not a config command).
        /// </summary>
        public static bool IsTestExecutionCommand { get; private set; }

        /// <summary>
        /// Initialize the command context. Called once at startup by CommandLineManager.
        /// </summary>
        public static void Initialize(string[]? args, bool isTestExecutionCommand)
        {
            Args = args;
            IsTestExecutionCommand = isTestExecutionCommand;
        }
    }
}
