using LPS.Domain.Common;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace LPS.UI.Common
{
    internal interface ILPSCommandParser<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IDomainEntity
    {
        TCommand Command { get; set; }
        void Parse(CancellationToken cancellationToken);
    }
}
