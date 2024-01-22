using LPS.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface IBuilderService<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IDomainEntity
    {
        TEntity Build(TCommand command);
    }
}
