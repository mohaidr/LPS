using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core.Host
{
    public interface IDashboardService
    {
        public void Start();
        public Task EnsureDashboardUpdateBeforeExitAsync();
    }
}
