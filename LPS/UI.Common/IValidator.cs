using LPS.Domain.Common;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface IValidator<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IExecutable
    {
        bool Validate (string ptoprtty, TCommand command);
    }
}
