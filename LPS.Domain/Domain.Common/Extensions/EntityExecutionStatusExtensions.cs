using LPS.Domain.Domain.Common.Enums;

namespace LPS.Domain.Domain.Common.Extensions
{
    public static class EntityExecutionStatusExtensions
    {
        public static bool IsTerminal(this EntityExecutionStatus status) =>
            status is EntityExecutionStatus.Success
                or EntityExecutionStatus.Failed
                or EntityExecutionStatus.Terminated
                or EntityExecutionStatus.Cancelled
                or EntityExecutionStatus.Skipped;
    }
}
