using AsyncTest.Domain.Common;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncCalls.UI.Common
{
    internal interface IValidator<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IExecutable
    {
        bool Validate (string ptoprtty, TCommand command);
    }
}
