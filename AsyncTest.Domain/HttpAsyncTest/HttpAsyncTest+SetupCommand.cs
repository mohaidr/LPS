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
                HttpRequestContainers = new List<HttpAsyncRequestContainer.SetupCommand>();
            }

            public void Execute(HttpAsyncTest entity)
            {
                entity.Setup(this);
            }

            async public Task ExecuteAsync(HttpAsyncTest entity)
            {
                throw new NotImplementedException();
            }

            public List<HttpAsyncRequestContainer.SetupCommand> HttpRequestContainers { get; set; }

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
                foreach (var command in dto.HttpRequestContainers)
                {
                    HttpRequestContainers.Add(new HttpAsyncRequestContainer(command, this._logger));
                }
            }
        }  
    }
}
