using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Client
{
    public class LPSHttpClientManager : ILPSHttpClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>>
    {
        ICustomLogger _logger;
        public LPSHttpClientManager(ICustomLogger logger)
        {
            _logger = logger;
        }

        public ILPSClientService<LPSHttpRequest> CreateInstance(ILPSClientConfiguration<LPSHttpRequest> config)
        {
            return new LPSHttpClientService(config, _logger);
        }
    }
}
