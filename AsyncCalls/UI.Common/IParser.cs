using AsyncTest.Domain.Common;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncCalls.UI.Common
{
    internal interface IParser<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IExecutable
    {
        void Parse(TCommand command);
    }
}
