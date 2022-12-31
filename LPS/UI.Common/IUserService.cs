using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.UI.Common
{
    internal interface IUserService<TCommand, TEntity> where TCommand : ICommand<TEntity> where TEntity : IExecutable
    {
        bool SkipOptionalFields { get; set; }
        TCommand Command { get; set; }
        public void Challenge();
        public void ResetOptionalFields();

    }
}
