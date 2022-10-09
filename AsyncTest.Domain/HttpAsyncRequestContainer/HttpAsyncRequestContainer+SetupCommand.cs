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
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncRequestContainer
    {
        public class SetupCommand: ICommand<HttpAsyncRequestContainer>
        {

            public SetupCommand()
            {
                HttpRequest = new HttpAsyncRequest.SetupCommand();
            }

            public void Execute(HttpAsyncRequestContainer entity)
            {
                entity.Setup(this);
            }

            async public Task ExecuteAsync(HttpAsyncRequestContainer entity)
            {
                throw new NotImplementedException();
            }

            public HttpAsyncRequest.SetupCommand HttpRequest { get; set; }

            public int NumberofAsyncRepeats { get; set; }

            public bool IsValid { get; set; }

            public string Name { get; set; }
        }

        private void Setup(SetupCommand dto)
        {
            new Validator(this, dto);

            if (dto.IsValid)
            {

                this.NumberofAsyncRepeats = dto.NumberofAsyncRepeats;
                this.Name = dto.Name;
                this.HttpRequest = new HttpAsyncRequest(dto.HttpRequest, this._logger);
                this.IsValid = true;
            }
        }
    }
}
