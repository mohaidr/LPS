using LPS.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface ILPSBaseValidator<TCommand, TEntity> where TCommand : IValidCommand<TEntity> where TEntity : IDomainEntity
    {
        TCommand Command { get;}
        bool Validate (string ptoprtty);
        void ValidateAndThrow(string property);
        Dictionary<string, List<string>> ValidationErrors { get; }
    }

}
