using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow
{
    public partial class Session
    {
        public class SetupCommand : ICommand<Session>, IValidCommand<Session>
        {
            public SetupCommand()
            {
                ValidationErrors = new Dictionary<string, List<string>>();

            }
            public Guid? Id { get; set; }
            public bool IsValid { get; set; }
            public IDictionary<string, List<string>> ValidationErrors { get; set; }

            public void Execute(Session entity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }
                entity?.Setup(this);
            }
        }

        protected virtual void Setup(SetupCommand command)
        {
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);

            if (command.IsValid)
            {
                this.IsValid = true;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }
    }
}
