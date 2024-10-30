using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface IChallengeUserService<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IDomainEntity
    {
        bool SkipOptionalFields { get; }
        TCommand Command { get;}
        public void Challenge();
        public void ResetOptionalFields();

    }
}
