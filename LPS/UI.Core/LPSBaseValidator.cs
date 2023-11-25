using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Domain.Common;
using LPS.UI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    public abstract class LPSBaseValidator<TCommand, TEntity> : AbstractValidator<TCommand>, ILPSBaseValidator<TCommand, TEntity>
        where TCommand : ICommand<TEntity>
        where TEntity : IDomainEntity
    {
        public abstract TCommand Command { get; }

        ValidationResult _validationResults;

        public Dictionary<string, List<string>> ValidationErrors => _validationResults.Errors
        .GroupBy(error => error.PropertyName)
        .ToDictionary(
            group => group.Key,
            group => group.Select(error => error.ErrorMessage).ToList()
        );

        public bool Validate(string property)
        {
            _validationResults = Validate(Command);
            return !_validationResults.Errors.Any(error => error.PropertyName == property);
        }

        public void ValidateAndThrow(string property)
        {
            _validationResults = Validate(Command);
            if (!_validationResults.Errors.Any(error => error.PropertyName == property))
            {
                StringBuilder errorMessage = new StringBuilder("Validation failed. Details:\n");
                foreach (var error in ValidationErrors)
                {
                    errorMessage.AppendLine($"{error.Key}: {error.Value}");
                }
                throw new LPSValidationException(errorMessage.ToString());
            }
        }
    }
}
