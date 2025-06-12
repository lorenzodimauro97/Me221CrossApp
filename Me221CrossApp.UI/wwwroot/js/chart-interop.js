const charts = {};

export function createChart(elementId, options) {
    if (charts[elementId]) {
        charts[elementId].destroy();
    }

    const chartOptions = {
        chart: {
            id: elementId,
            type: 'line',
            height: '100%',
            background: 'transparent',
            zoom: {
                enabled: true
            },
            toolbar: {
                show: true,
                tools: {
                    download: false,
                    selection: true,
                    zoom: true,
                    zoomin: true,
                    zoomout: true,
                    pan: true,
                    reset: true
                }
            },
        },
        theme: {
            mode: 'dark'
        },
        stroke: {
            curve: 'straight',
            width: 2
        },
        markers: {
            size: 0
        },
        xaxis: {
            type: 'numeric',
            title: {
                text: options.xAxisLabel
            }
        },
        yaxis: {
            title: {
                text: options.yAxisLabel
            }
        },
        series: [{
            name: options.yAxisLabel,
            data: options.seriesData
        }]
    };

    const chart = new ApexCharts(document.getElementById(elementId), chartOptions);
    charts[elementId] = chart;
    chart.render();
}

export function updateChart(elementId, seriesData) {
    if (charts[elementId]) {
        charts[elementId].updateSeries([{
            data: seriesData
        }]);
    }
}

export function destroyChart(elementId) {
    if (charts[elementId]) {
        charts[elementId].destroy();
        delete charts[elementId];
    }
}