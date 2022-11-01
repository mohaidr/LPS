using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using System.Threading.Tasks;

namespace AsyncTest.UI.Core
{
    internal interface ITestService<T1, T2> where T1 : ICommand<T2> where T2: IExecutable
    {
        public Task Run(T1 command, string[] args);
    }
}