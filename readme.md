# LPS Command-Line Tool Documentation

### Overview
The LPS tool is designed for Load, Performance, and Stress testing of web applications. It allows users to design, manage, and execute a variety of HTTP-based tests to assess application performance under simulated load conditions.


### Example
`lps --url https://www.example.com -rc 1000`


# Commands

`Command: lps [options]`

A comprehensice command used to initiate various testing scenarion on the fly, depending on the options provided.


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

This command is used to set up a new test plan. You specify the test name, number of clients that will simulate the load, and the ramp-up period among other settings. This is the foundational step where you define the basic parameters of your load test.

  
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

This command adds an HTTP run to an existing test. It involves specifying details like the HTTP method, the target URL, request count, and other HTTP-specific settings. Each HTTP run represents a testing scenario that will be performed during the execution of the plan, simulate real-world usage of the web application.


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

This command is used to execute a test plan that has been previously created and configured. It requires the test name as a parameter to identify which test to run. This is the command that actually starts the load, performance, or stress test, generating and sending the traffic as per the defined test plan parameters.


    Test Name (-tn, --testname <testname>): Required. Identifies the test to be executed.
    Help (-?, -h): Shows help and options available for the command.
    
### Configure Logger
`Command: lps logger [options]`

This command configures logging options for the LPS tool. It allows you to specify where log files should be saved, whether logs should be output to the console, the level of detail in the logs, and other logging preferences. Proper logging is crucial for analyzing the results and behavior of tests.


    Log File Path (-lfp, --logFilePath <logFilePath>): Specifies the file path for logging output.
    Console Logging (-ecl, --enableConsoleLogging): Enables or disables logging to the console.
    Console Error Logging (-dcel, --disableConsoleErrorLogging): Toggles error logging to the console.
    File Logging (-dfl, --disableFileLogging): Enables or disables file logging.
    Logging Level (-ll, --loggingLevel <level>): Sets the verbosity of logs.
    Console Logging Level (-cll, --consoleLoggingLevel <level>): Determines the detail of logs shown on the console.

### Configure HTTP Client

This command sets parameters for the HTTP client that will be used to send requests. It includes settings such as the maximum number of simultaneous connections per server, how long a connection should stay alive in the pool, and the timeout settings for client requests. These settings help optimize the performance of the HTTP client according to the specific requirements of the test and target environment.


`Command: lps httpclient [options]`

    Maximum Connections Per Server (-mcps, --maxConnectionsPerServer <maxConnectionsPerServer>): Limits the number of simultaneous connections per server.
    Connection Lifetime (-pclt, --poolConnectionLifeTime <poolConnectionLifeTime>): Defines how long a connection remains in the pool.
    Idle Timeout (-pcit, --poolConnectionIdelTimeout <poolConnectionIdelTimeout>): The maximum time a connection can stay idle in the pool.
    Client Timeout (-cto, --clientTimeout <clientTimeout>): Sets the timeout for HTTP client requests.

### Configure Watchdog

This command configures the watchdog settings that monitor resource usage (like memory and CPU) of the client machine during a test. It sets thresholds for pausing the test if resource usage becomes too high, which helps prevent the testing process from negatively impacting the system or the network environment. The command also defines the conditions under which the test will resume.


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






