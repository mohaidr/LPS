const endpointToCharts = {};
let darkMode = false;

async function fetchMetrics() {
    try {
        const response = await fetch('/api/metrics');
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        const metricsData = await response.json();
        updateDashboard(metricsData);
    } catch (error) {
        console.error('Fetch metrics failed:', error);
    }
}

function updateDashboard(metricsData) {
    const dashboard = document.getElementById('dashboard');
    const previousState = {};

    document.querySelectorAll('.collapse').forEach(element => {
        previousState[element.id] = element.classList.contains('show');
    });

    const statusOrder = {
        'Ongoing': 1,
        'NotStarted': 2,
        'NotRunning': 2,
        'Paused': 3,
        'Cancelled': 4,
        'Failed': 5,
        'Completed': 6
    };

    metricsData.sort((a, b) => statusOrder[a.executionStatus] - statusOrder[b.executionStatus]);

    metricsData.forEach((metric, index) => {
        const endpointKey = metric.endpoint.replace(/\W/g, '_');
        const containerId = `container-${endpointKey}`;
        const contentId = `${containerId}-content`;
        const responseTimeChartId = `responseTimeChart-${endpointKey}`;
        const connectionChartId = `connectionChart-${endpointKey}`;
        const requestsRateChartId = `requestsRateChart-${endpointKey}`;
        const responseSummaryChartId = `responseSummaryChart-${endpointKey}`;

        let container = document.getElementById(containerId);
        if (!container) {
            container = document.createElement('div');
            container.id = containerId;
            container.className = 'collapse-container';
            container.innerHTML = `
                <div class="card">
                    <div class="card-header collapsible" id="heading-${index}">
                        <h5 class="mb-0">
                            ${metric.endpoint}
                            <span class="status status-${metric.executionStatus}" id="status-${endpointKey}">Status: ${metric.executionStatus}</span>
                        </h5>
                        <span class="collapsible-icon" id="icon-${contentId}" onclick="toggleCollapse('${contentId}')">
                            <i class="bi bi-plus-lg"></i>
                        </span>
                    </div>
                    <div id="${contentId}" class="collapse ${previousState[contentId] ? 'show' : ''}" aria-labelledby="heading-${index}" data-parent="#dashboard">
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6 chart-container">
                                    <canvas id="${responseTimeChartId}"></canvas>
                                </div>
                                <div class="col-md-6 chart-container">
                                    <canvas id="${connectionChartId}"></canvas>
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6 chart-container">
                                    <canvas id="${requestsRateChartId}"></canvas>
                                </div>
                                <div class="col-md-6 chart-container">
                                    <canvas id="${responseSummaryChartId}"></canvas>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            dashboard.appendChild(container);

            endpointToCharts[responseTimeChartId] = new Chart(document.getElementById(responseTimeChartId).getContext('2d'), {
                type: 'line',
                data: {
                    labels: formatLabelsForChart(metric.responseTimeMetrics),
                    datasets: [{
                        label: 'Response Time Metrics',
                        data: formatDataForChart(metric.responseTimeMetrics),
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        borderColor: 'rgba(75, 192, 192, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });

            endpointToCharts[connectionChartId] = new Chart(document.getElementById(connectionChartId).getContext('2d'), {
                type: 'bar',
                data: {
                    labels: formatLabelsForChart(metric.connectionMetrics, 'count'),
                    datasets: [{
                        label: 'Connection Metrics',
                        data: formatDataForChart(metric.connectionMetrics, 'count'),
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        borderColor: 'rgba(75, 192, 192, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });

            endpointToCharts[requestsRateChartId] = new Chart(document.getElementById(requestsRateChartId).getContext('2d'), {
                type: 'bar',
                data: {
                    labels: formatLabelsForChart(metric.connectionMetrics, 'rate'),
                    datasets: [{
                        label: 'Requests Rate',
                        data: formatDataForChart(metric.connectionMetrics, 'rate'),
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        borderColor: 'rgba(75, 192, 192, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });

            endpointToCharts[responseSummaryChartId] = new Chart(document.getElementById(responseSummaryChartId).getContext('2d'), {
                type: 'bar',
                data: {
                    labels: formatLabelsForChart(metric.responseBreakDownMetrics.responseSummary, 'summary'),
                    datasets: [{
                        label: 'Response Summary',
                        data: formatDataForChart(metric.responseBreakDownMetrics.responseSummary, 'summary'),
                        backgroundColor: 'rgba(75, 192, 192, 0.2)',
                        borderColor: 'rgba(75, 192, 192, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });
        } else {
            updateChartData(endpointToCharts[responseTimeChartId], metric.responseTimeMetrics);
            updateChartData(endpointToCharts[connectionChartId], metric.connectionMetrics, 'count');
            updateChartData(endpointToCharts[requestsRateChartId], metric.connectionMetrics, 'rate');
            updateChartData(endpointToCharts[responseSummaryChartId], metric.responseBreakDownMetrics.responseSummary, 'summary');
            updateStatus(metric, endpointKey);

            dashboard.removeChild(container);
            dashboard.appendChild(container);
        }
    });
}

function updateChartData(chart, data, dataType = 'default') {
    chart.data.labels = formatLabelsForChart(data, dataType);
    chart.data.datasets[0].data = formatDataForChart(data, dataType);
    chart.update();
}

function formatLabelsForChart(data, dataType) {
    if (dataType === 'count') {
        return ['Requests Count', 'Active Requests', 'Successful Requests', 'Failed Requests'];
    } else if (dataType === 'rate') {
        return ['Requests Rate', 'Requests Rate Per Cool Down Period'];
    } else if (dataType === 'summary') {
        return data.map(item => `${item.httpStatusCode} ${item.httpStatusReason}`);
    }
    return ['Sum Response Time', 'Average Response Time', 'Min Response Time', 'Max Response Time', 'P90 Response Time', 'P50 Response Time', 'P10 Response Time'];
}

function formatDataForChart(data, dataType) {
    if (dataType === 'count') {
        return [data.requestsCount, data.activeRequestsCount, data.successfulRequestCount, data.failedRequestsCount];
    } else if (dataType === 'rate') {
        return [data.requestsRate.value, data.requestsRatePerCoolDownPeriod.value];
    } else if (dataType === 'summary') {
        return data.map(item => item.count);
    }
    return [data.sumResponseTime, data.averageResponseTime, data.minResponseTime, data.maxResponseTime, data.p90ResponseTime, data.p50ResponseTime, data.p10ResponseTime];
}

function toggleCollapse(containerId) {
    const container = document.getElementById(containerId);
    const icon = document.getElementById(`icon-${containerId}`).querySelector('i');

    if (container.classList.contains('show')) {
        container.classList.remove('show');
        icon.classList.remove('bi-dash-lg');
        icon.classList.add('bi-plus-lg');
    } else {
        container.classList.add('show');
        icon.classList.remove('bi-plus-lg');
        icon.classList.add('bi-dash-lg');
    }
}

function toggleDarkMode() {
    darkMode = !darkMode;
    document.body.classList.toggle('dark-mode', darkMode);
    document.getElementById('darkModeSwitch').checked = darkMode;
}

function updateStatus(metric, endpointKey) {
    const statusElement = document.getElementById(`status-${endpointKey}`);
    statusElement.textContent = `Status: ${metric.executionStatus}`;
    statusElement.className = `status status-${metric.executionStatus}`;
}

fetchMetrics();
setInterval(fetchMetrics, 5000);
