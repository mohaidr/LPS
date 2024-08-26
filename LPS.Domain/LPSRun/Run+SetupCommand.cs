using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class Run
    {
        public class SetupCommand : ICommand<Run>
        {

            public SetupCommand()
            {
            }

            public void Execute(Run entity)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entity));
                }
                entity?.Setup(this);
            }
            public Guid? Id { get; set; }

            public bool IsValid { get; set; }
            public string Name { get; set; }

        }

        protected void Setup(SetupCommand command)
        {
            var validator = new Validator(this, command, _logger, _runtimeOperationIdProvider);
            if (command.IsValid)
            {
                this.Name= command.Name;
                this.IsValid= true;
            }
            else
            {
                this.IsValid = false;
                validator.PrintValidationErrors();
            }
        }
    }
}
