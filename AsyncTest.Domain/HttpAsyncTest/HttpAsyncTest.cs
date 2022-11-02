using System;
using System.Collections.Generic;
using Serilog;
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncTest : IAggregateRoot, IValidEntity, IExecutable
    {
        private IFileLogger _logger;
        private HttpAsyncTest()
        {

        }
        public HttpAsyncTest(SetupCommand dto, IFileLogger logger)
        {
            HttpRequestContainers = new List<HttpAsyncRequestContainer>();
            _logger = logger;
            this.Setup(dto);
        }

        public IList<HttpAsyncRequestContainer> HttpRequestContainers { get; private set; }

        public string Name { set; private get; }

        public bool IsRedo { get; private set; }

        public bool IsValid { get; private set; }
    }
}
