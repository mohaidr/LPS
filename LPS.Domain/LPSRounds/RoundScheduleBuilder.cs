using System.Collections.Generic;

namespace LPS.Domain
{
    public static class RoundScheduleBuilder
    {
        public static IEnumerable<ClientSchedule> Build(Round round)
        {
            var stages = round.Stages != null && round.Stages.Count > 0
                ? round.Stages
                : new[] { new Stage(round.NumberOfClients, round.ArrivalDelay ?? 0) };
            var sequential = round.RunClientsSequentially == true;
            long stageOffsetMs = 0;

            for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
            {
                var stage = stages[stageIndex];
                stageOffsetMs += stage.StartupDelay;

                for (var clientIndex = 0; clientIndex < stage.NumberOfClients; clientIndex++)
                {
                    yield return new ClientSchedule(
                        stageIndex,
                        clientIndex,
                        sequential ? 0 : stageOffsetMs + (long)clientIndex * stage.ArrivalDelay,
                        sequential && clientIndex == 0 ? stage.StartupDelay : 0);
                }

                if (!sequential && stage.NumberOfClients > 1)
                    stageOffsetMs += (long)(stage.NumberOfClients - 1) * stage.ArrivalDelay;
            }
        }
    }
}