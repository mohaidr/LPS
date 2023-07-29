using LPS.Extensions;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.CommandLine;
using System.CommandLine.Binding;
using LPS.UI.Core.UI.Build.Services;
using System.Net.Http;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Xml.Linq;
using System.CommandLine.Parsing;
using System.ComponentModel.DataAnnotations;
using static LPS.Domain.LPSHttpTestCase;

namespace LPS.UI.Core.LPSCommandLine
{

    public class CommandLineOptions
    {
        static CommandLineOptions()
        {
            TestNameOption.AddAlias("-tn");
            NumberOfClientsOption.AddAlias("-nc");
            RampupPeriodOption.AddAlias("-rp");
            MaxConnectionsPerServerption.AddAlias("-mcps");
            PoolConnectionLifeTimeOption.AddAlias("-pclt");
            PoolConnectionIdelTimeoutOption.AddAlias("-pcit");
            DelayClientCreation.AddAlias("-dcc");
            RunInParaller.AddAlias("-rip");
            ClientTimeoutOption.AddAlias("-cto");
            CaseNameOption.AddAlias("-cn");
            RequestCountOption.AddAlias("-rc");
            Duratiion.AddAlias("-d");
            BatchSize.AddAlias("-bs");
            CoolDownTime.AddAlias("-cdt");
            HttpMethodOption.AddAlias("-hm");
            HttpversionOption.AddAlias("-hv");
            UrlOption.AddAlias("-u");
            HeaderOption.AddAlias("-h");
            PayloadOption.AddAlias("-p");
            IterationModeOption.AddAlias("-im");

        }

        public static Option<string> TestNameOption = new Option<string>("--testname", "Test name") { IsRequired = true, Arity = ArgumentArity.ExactlyOne };
        public static Option<int> NumberOfClientsOption = new Option<int>("--numberOfClients", "Number of clients to execute the plan") { IsRequired = true, };
        public static Option<int> RampupPeriodOption = new Option<int>("--rampupPeriod", "Time in millisencds to wait before creating starting a new client") { IsRequired = false, };
        public static Option<int> MaxConnectionsPerServerption = new Option<int>("--maxConnectionsPerServer", "max number of connections per client per server") { IsRequired = true, };
        public static Option<int> PoolConnectionLifeTimeOption = new Option<int>("--poolConnectionLifeTime", () => 25, "Pooled connection life time defines the maximal connection lifetime in the pool, tracking its age from when the connection was established, regardless of how much time it spent idle or active.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionlifetime?view=net-8.0") { IsRequired = false, };
        public static Option<int> PoolConnectionIdelTimeoutOption = new Option<int>("--poolConnectionIdelTimeout", () => 5, "Pooled connection idle timeout defines the maximum idle time for a connection in the pool.\nSee this link for more details https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.pooledconnectionidletimeout?view=net-8.0") { IsRequired = false, };
        public static Option<int> ClientTimeoutOption = new Option<int>("--clientTimeout", () => 240, "Timeout") { IsRequired = false };
        public static Option<bool> DelayClientCreation = new Option<bool>("--delayClientCreation", () => false, "Delay Client Creation Until is Needed") { IsRequired = false };
        public static Option<bool> RunInParaller = new Option<bool>("--runInParaller", () => true, "Run Test Cases In Parallel") { IsRequired = false };
        public static Option<string> CaseNameOption = new Option<string>("--caseName", "The name of the test case") { IsRequired = true };
        public static Option<IterationMode> IterationModeOption = new Option<IterationMode>("--iterationMode", "The Test Iteration Mode") { IsRequired = false, };
        public static Option<int?> RequestCountOption = new Option<int?>("--requestCount", "The number of requests") { IsRequired = false, };
        public static Option<int?> Duratiion = new Option<int?>("--duration", "Test time in seconds") { IsRequired = false, };
        public static Option<int?> CoolDownTime = new Option<int?>("--coolDownTime", "The time to cooldown during the test") { IsRequired = false, };
        public static Option<int?> BatchSize = new Option<int?>("--batchsize", "The number of requests to be sent in a batch") { IsRequired = false, };
        public static Option<string> HttpMethodOption = new Option<string>("--method", "HTTP method") { IsRequired = true };
        public static Option<string> HttpversionOption = new Option<string>("--version", () => "1.1", "HTTP version") { IsRequired = false };
        public static Option<string> UrlOption = new Option<string>("--url", "URL") { IsRequired = true };
        public static Option<IList<string>> HeaderOption = new Option<IList<string>>("--header", "Header") { IsRequired = false, AllowMultipleArgumentsPerToken = true, };
        public static Option<string> PayloadOption = new Option<string>("--payload", "Request Payload, can be a path to a file or inline text") { IsRequired = false, };
    }
}

