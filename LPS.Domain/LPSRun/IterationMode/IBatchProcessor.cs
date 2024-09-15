using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal interface IBatchProcessor<TExucuteCommand, TDomainEntity> where TExucuteCommand : IAsyncCommand<TDomainEntity> where TDomainEntity : IDomainEntity
    {
        public Task<int> SendBatchAsync(TExucuteCommand command, int batchSize, Func<bool> batchCondition);

    }
}
