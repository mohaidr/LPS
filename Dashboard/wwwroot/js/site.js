
const endpointToCharts = {};
let darkMode = false;

async function fetchMetrics() {
    const response = await fetch('/api/metrics');
    const metricsData = await response.json();
    updateDashboard(metricsData);
}

function updateDashboard(metricsData) {
    const dashboard = document.getElementById('dashboard');
    const previousState = {};

    // Save the collapsed state
    document.querySelectorAll('.collapse').forEach(element => {
        previousState[element.id] = element.classList.contains('show');
    });

    metricsData.sort((a, b) => (a.executionStatus === 'Ongoing' ? -1 : 1));
    dashboard.innerHTML = ''; // Clear existing content

    metricsData.forEach((metric, index) => {
        const endpointKey = metric.endpoint.replace(/\W/g, '_');
        const containerId = `container-${endpointKey}`;
        const responseTimeChartId = `responseTimeChart-${endpointKey}`;
        const connectionChartId = `connectionChart-${endpointKey}`;
        const requestsRateChartId = `requestsRateChart-${endpointKey}`;
        const responseSummaryChartId = `responseSummaryChart-${endpointKey}`;

        const container = document.createElement('div');
        container.className = 'collapse-container';
        container.innerHTML = `
                    <div class="card">
                        <div class="card-header collapsible" id="heading-${index}">
                            <h5 class="mb-0">
                                <button class="btn btn-link" onclick="toggleCollapse('${containerId}')">
                                    ${metric.endpoint}
                                </button>
                            </h5>
                        </div>
                        <div id="${containerId}" class="collapse ${previousState[containerId] ? 'show' : ''}" aria-labelledby="heading-${index}" data-parent="#dashboard">
                            <div class="card-body">
                                <p class="status" id="status-${endpointKey}">Status: ${metric.executionStatus}</p>
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

        updateStatus(metric, endpointKey);
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
    if (container.classList.contains('show')) {
        container.classList.remove('show');
    } else {
        container.classList.add('show');
    }
}

function toggleDarkMode() {
    darkMode = !darkMode;
    document.body.classList.toggle('dark-mode', darkMode);
}

function updateStatus(metric, endpointKey) {
    const statusElement = document.getElementById(`status-${endpointKey}`);
    statusElement.textContent = `Status: ${metric.executionStatus}`;
    statusElement.className = `status status-${metric.executionStatus}`;
}

// Initial fetch and set interval for periodic updates
fetchMetrics();
setInterval(fetchMetrics, 5000);