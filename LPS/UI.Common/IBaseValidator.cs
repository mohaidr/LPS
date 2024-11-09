using LPS.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface IBaseValidator<TCommand, TEntity> where TCommand : IValidCommand<TEntity> where TEntity : IDomainEntity
    {
        TCommand Dto { get;}
        bool Validate (string ptoprtty);
        void ValidateAndThrow(string property);
        public void PrintValidationErrors(string property);
        Dictionary<string, List<string>> ValidationErrors { get; }
    }

}
