using AsyncTest.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncTest
    {
        public class SetupCommand: ICommand<HttpAsyncTest>
        {

            public SetupCommand()
            {
                HttpRequestWrappers = new List<HttpAsyncRequestWrapper.SetupCommand>();
            }

            public void Execute(HttpAsyncTest entity)
            {
                entity.Setup(this);
            }

            public List<HttpAsyncRequestWrapper.SetupCommand> HttpRequestWrappers { get; set; }

            public string Name { get; set; }

            public bool IsValid { get; set; }

            public bool IsCommandLine { get; set; }

        }

        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {
                this.Name = dto.Name;
                this.IsValid = true;
                foreach (var command in dto.HttpRequestWrappers)
                {
                    HttpRequestWrappers.Add(new HttpAsyncRequestWrapper(command, this._logger));
                }
            }
        }  
    }
}
