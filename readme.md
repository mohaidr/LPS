# LPS Command-Line Tool Documentation

## Overview
The LPS tool is crafted for Load, Performance, and Stress testing of web applications. It equips users to define, manage, and execute various HTTP-based tests to gauge software performance under simulated conditions.

`Command: lps [options]`
### Global Options:
    -tn, --testname <testname>: Specifies the test name, defaults to "Quick-Test-Plan".
    -nc, --numberOfClients, --numberofclients <numberOfClients>: Sets the number of clients to execute the plan, defaults to 1.
    -rp, --rampupPeriod, --rampupperiod <rampupPeriod>: Time in milliseconds to wait before a new client starts the test plan, defaults to 0.
    -dcc, --delayClientCreation, --delayclientcreation: Delays client creation until needed, defaults to False.
    -rip, --runInParallel, --runinparallel: Executes tests in parallel, defaults to True.
    -u, --url <url>: Target URL for the test (REQUIRED).
    -hm, --httpmethod, --method <method>: HTTP method to use, defaults to GET.
    -h, --header, --Header <header>: Specifies request headers.
    -rn, --runName, --runname <runName>: Designates the name for the 'HTTP Run', defaults to "Quick-Http-Run".
    -im, --iterationMode, --iterationmode <CB|CRB|D|DCB|R>: Defines the iteration mode for the HTTP run, defaults to R (Random).
    -rc, --requestCount, --requestcount <requestCount>: Specifies the number of requests to send.
    -d, --duration, --Duration <duration>: Duration for the test in seconds.
    -cdt, --coolDownTime, --cooldowntime <coolDownTime>: Cooldown period in seconds before sending the next batch.
    -bs, --batchsize <batchsize>: Number of requests per batch.
    -hv, --httpVersion, --httpversion <httpVersion>: HTTP version, defaults to 2.0.
    -dhtmler, --downloadHtmlEmbeddedResources, --downloadhtmlembeddedresources: Option to download HTML embedded resources, defaults to False.
    -sr, --saveResponse, --saveresponse: Option to save HTTP responses, defaults to False.
    -p, --payload, --Payload <payload>: Request payload, can be a path to a file or inline text.

  
`lps [command] [options]`
  
### Create Test
`Command: lps create [options]`

    Test Name (-tn, --testname <testname>): Required. Sets the name of the test.
        Number of Clients (-nc, --numberOfClients <numberOfClients>): Required. Defines how many clients will run the test simultaneously.
        Ramp-Up Period (-rp, --rampupPeriod <rampupPeriod>): Required. The delay in milliseconds before initiating a new client.
        Delay Client Creation (-dcc, --delayClientCreation): When set, client creation is postponed until necessary. Default is false.
        Parallel Execution (-rip, --runInParallel): Determines whether tests are run concurrently. Default is true.
        Help (-?, -h): Displays help and usage information.


### Add HTTP Run
`Command: lps add [options]`

    Test Name (-tn, --testname <testname>): Required. Specifies the test to which the HTTP run will be added.
    HTTP Run Name (-rn, --runName <runName>): Required. Names the individual HTTP run.
    Iteration Mode (-im, --iterationMode <CB|CRB|D|DCB|R>): Defines the behavior for repeating HTTP requests.
    Request Count (-rc, --requestCount <requestCount>): Indicates the total number of requests to send during the run.
    Run Duration (-d, --duration <duration>): Specifies the length of the test in seconds.
    Cooldown Time (-cdt, --coolDownTime <coolDownTime>): Sets the waiting time in seconds between batches.
    Batch Size (-bs, --batchsize <batchsize>): The number of requests sent in one batch.
    HTTP Method (-hm, --httpmethod <method>): Required. Chooses the HTTP method (e.g., GET, POST).
    HTTP Version (-hv, --httpVersion <httpVersion>): Specifies the HTTP protocol version. Default is 2.0.
    URL (-u, --url <url>): Required. The target URL for the HTTP requests.
    Download HTML Resources (-dhtmler, --downloadHtmlEmbeddedResources): Controls whether to download HTML embedded resources. Default is false.
    Save Response (-sr, --saveResponse): Decides if the HTTP responses should be saved. Default is false.
    Header (-h, --header <header>): Adds custom headers to the HTTP requests.
    Payload (-p, --payload <payload>): Provides the data sent with the request, either from a file or as inline text.

### Run Test  
`Command: lps run [options]`

    Test Name (-tn, --testname <testname>): Required. Identifies the test to be executed.
    Help (-?, -h): Shows help and options available for the command.
    
### Configure Logger
`Command: lps logger [options]`

    Log File Path (-lfp, --logFilePath <logFilePath>): Specifies the file path for logging output.
    Console Logging (-ecl, --enableConsoleLogging): Enables or disables logging to the console.
    Console Error Logging (-dcel, --disableConsoleErrorLogging): Toggles error logging to the console.
    File Logging (-dfl, --disableFileLogging): Enables or disables file logging.
    Logging Level (-ll, --loggingLevel <level>): Sets the verbosity of logs.
    Console Logging Level (-cll, --consoleLoggingLevel <level>): Determines the detail of logs shown on the console.

### Configure HTTP Client
`Command: lps httpclient [options]`

    Maximum Connections Per Server (-mcps, --maxConnectionsPerServer <maxConnectionsPerServer>): Limits the number of simultaneous connections per server.
    Connection Lifetime (-pclt, --poolConnectionLifeTime <poolConnectionLifeTime>): Defines how long a connection remains in the pool.
    Idle Timeout (-pcit, --poolConnectionIdelTimeout <poolConnectionIdelTimeout>): The maximum time a connection can stay idle in the pool.
    Client Timeout (-cto, --clientTimeout <clientTimeout>): Sets the timeout for HTTP client requests.

### Configure Watchdog
`Command: lps watchdog [options]`

    Memory Threshold (-mmm, --maxMemoryMB <maxMemoryMB>): Sets a memory usage limit that pauses the test upon being reached.
    CPU Threshold (-mcp, --maxCPUPercentage <maxCPUPercentage>): Specifies a CPU usage limit to pause the test.
    Memory Cooldown (-cdmm, --coolDownMemoryMB <coolDownMemoryMB>): Memory limit for resuming a paused test.
    CPU Cooldown (-coolDownCPUPercentage <coolDownCPUPercentage>): CPU usage threshold for test resumption.
    Concurrent Connection Threshold (-mcccphn, --maxConcurrentConnectionsCountPerHostName <maxConcurrentConnectionsCountPerHostName>): Limits concurrent connections per host to pause the test.
    Connection Cooldown (-cdcccphn, --coolDownConcurrentConnectionsCountPerHostName <coolDownConcurrentConnectionsCountPerHostName>): Concurrent connection limit for resuming tests.
    Retry Time (-cdrtis, --coolDownRetryTimeInSeconds <coolDownRetryTimeInSeconds>): Interval for checking if the test can be resumed.
    Suspension Mode (-sm, --suspensionMode <All|Any>): Decides whether to pause the test when all or any thresholds are exceeded.

### HelpHelp
**For additional details on command usage and options:**
`lps -?, -h`

This help command will provide comprehensive usage information for the LPS tool or any specific command.






