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

Initializes a new test configuration. Essential parameters such as the test's name, the number of simulated clients, and the ramp-up period are defined here. This command sets the groundwork for a load test, specifying how the test environment will mimic user traffic.
  
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

Adds an HTTP run to a predefined test plan. This command configures the specifics of the HTTP testing scenario to be made during the test, including the type of requests, target URLs, and the sequence of the testing scenarios. It is crucial for tailoring the test to simulate specific user interactions with the web application.


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

Executes a prepared test plan according to its specifications. It requires the name of the test to identify and launch the appropriate testing procedure. This command triggers the actual load, performance, or stress testing by sending the configured HTTP traffic to the target application.



    Test Name (-tn, --testname <testname>): Required. Identifies the test to be executed.
    Help (-?, -h): Shows help and options available for the command.

### Example
    lps create --testname "login-test-plan-01" --numberOfClients 5 --rampupPeriod 1000
    lps add --testname "login-test-plan-01" --runName "LoginTest" --httpmethod GET --url "https://example.com/login" --iterationMode CRB --requestCount 500 --batchsize 20  --coolDownTime 5
    lps run --testname "login-test-plan-01"

The Iteration Mode parameter in the LPS tool offers a flexible way to control how HTTP requests are repeated during a test run. Each mode provides a unique method for managing the flow of requests based on different testing needs. Here’s an explanation of each iteration mode and how they enhance the tool's testing capabilities:

The Iteration Mode parameter in the LPS tool offers a flexible way to control how HTTP requests are repeated during a test run. Each mode provides a unique method for managing the flow of requests based on different testing needs. Here’s an explanation of each iteration mode and how they enhance the tool's testing capabilities:

### Iteration Modes:
#### CB (Cooldown-Batchsize)CB (Cooldown-Batchsize)
**Description**: In this mode, HTTP requests are issued in batches specified by the --batchsize option. After each batch is sent, the test pauses for a period defined by the --coolDownTime. This mode is useful for testing how well the application handles bursts of traffic followed by periods of inactivity.
**Use Case:** Ideal for scenarios where you want to simulate users coming in waves, such as during a flash sale or a timed quiz on an educational website.
#### CRB (Cooldown-Request-Batchsize)CRB (Cooldown-Request-Batchsize)
**Description**: This mode extends the CB mode by allowing control over the total number of requests (--requestCount) in addition to batch size and cooldown periods. It ensures that the entire test run sends a precise number of requests, distributed across several batches with rest periods in between.
**Use Case**: Suitable for more detailed stress testing where the total load and its distribution over time are critical, such as testing e-commerce sites on Black Friday sales.
#### D (Duration)
**Description**: In the Duration mode, requests are continuously sent for the specified time period (--duration). Similar to the Request Count mode, each request in this mode is issued sequentially; a new request is sent only after the previous one completes. This mode evaluates the endurance of the application by maintaining a consistent load over an extended period.
**Use Case**: This mode is ideal for scenarios where the performance stability of an application over time is critical. It’s especially relevant for services that expect consistent usage, such as continuous data entry systems or online examination platforms where users might be interacting with the system steadily over lengthy sessions.
#### DCB (Duration-Cooldown-Batchsize)DCB (Duration-Cooldown-Batchsize)
**Description**: Combines the elements of duration and CB mode. Requests are sent in batches for the total duration of the test, with cooldown periods between each batch. This hybrid approach provides a comprehensive stress test scenario.
**Use Case**: Perfect for applications requiring reliability under periodic spikes in user activity, such as online ticketing services during event launches.
#### R (Request Count)
**Description**: In the Request Count mode, the tool is set to send a predetermined total number of requests (--requestCount). Each request is sent individually, one after another, with the next request only being dispatched after the previous one has completed. This mode is designed to assess how the application handles a steady stream of incoming requests, one at a time.

**Use Case**: This is particularly useful for testing the application's capacity to process consecutive requests efficiently. It simulates a scenario where users perform actions one after another, such as submitting forms or completing sequential tasks on a workflow application. It's an excellent way to measure how well the server recovers and readies itself for the next request under a controlled load.

### Flexibility and Testing Capabilities:Flexibility and Testing Capabilities:
The iteration modes provide significant flexibility in how tests are structured, allowing testers to closely mimic a variety of real-world scenarios. By varying the sequence and intensity of load conditions, these modes help identify potential performance bottlenecks and ensure that the application is robust enough to handle different types of user interactions and traffic patterns. This tailored approach is crucial for developing highly reliable and scalable web applications.

# LPS Configuration
    
### Configure Logger
`Command: lps logger [options]`

Sets up logging parameters for the test operations. This command allows customization of log output locations, console logging preferences, and log verbosity. Effective logging is vital for later analysis and troubleshooting of test results.



    Log File Path (-lfp, --logFilePath <logFilePath>): Specifies the file path for logging output.
    Console Logging (-ecl, --enableConsoleLogging): Enables or disables logging to the console.
    Console Error Logging (-dcel, --disableConsoleErrorLogging): Toggles error logging to the console.
    File Logging (-dfl, --disableFileLogging): Enables or disables file logging.
    Logging Level (-ll, --loggingLevel <level>): Sets the verbosity of logs.
    Console Logging Level (-cll, --consoleLoggingLevel <level>): Determines the detail of logs shown on the console.

### Configure HTTP Client

Configures the HTTP client that will be used for sending requests in a test. Adjustments can be made to connection pooling, client timeout settings, and connection limits per server. These settings optimize the HTTP client's performance to suit the test needs and reduce potential bottlenecks.


`Command: lps httpclient [options]`

    Maximum Connections Per Server (-mcps, --maxConnectionsPerServer <maxConnectionsPerServer>): Limits the number of simultaneous connections per server.
    Connection Lifetime (-pclt, --poolConnectionLifeTime <poolConnectionLifeTime>): Defines how long a connection remains in the pool.
    Idle Timeout (-pcit, --poolConnectionIdelTimeout <poolConnectionIdelTimeout>): The maximum time a connection can stay idle in the pool.
    Client Timeout (-cto, --clientTimeout <clientTimeout>): Sets the timeout for HTTP client requests.

### Configure Watchdog

Manages the watchdog mechanism that monitors and controls resource usage during tests. It establishes thresholds for memory and CPU usage that, when exceeded, will pause the test to safeguard the system. It also defines conditions for when a paused test can resume, ensuring that the testing does not overload the system or network.


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






