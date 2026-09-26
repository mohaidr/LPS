using System.Collections.Generic;

namespace LPS.Domain
{
    internal readonly record struct PreparedClient(
        ClientSchedule Schedule,
        IReadOnlyList<(HttpIteration.ExecuteCommand Command, HttpIteration Iteration)> Commands);
}