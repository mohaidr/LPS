using AsyncTest.Domain;
using AsyncTest.Domain.Common;

namespace AsyncTest.UI.Core
{
    internal interface ITestService<T1, T2> where T1 : ICommand<T2> where T2: IExecutable
    {
        internal void Run(T1 command) => throw new System.NotImplementedException();
    }
}