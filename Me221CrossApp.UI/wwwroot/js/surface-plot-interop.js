const plots = {};

export function createPlot(elementId, plotData) {
    if (plots[elementId]) {
        Plotly.purge(elementId);
        delete plots[elementId];
    }

    const data = [{
        z: plotData.z,
        x: plotData.x,
        y: plotData.y,
        type: 'surface',
        colorscale: 'Viridis',
        contours: {
            z: {
                show: true,
                usecolormap: true,
                highlightcolor: "#42f462",
                project: { z: true }
            }
        }
    }];

    const layout = {
        title: plotData.title,
        autosize: true,
        margin: { l: 40, r: 40, b: 40, t: 60 },
        scene: {
            xaxis: { title: 'X-Axis', color: '#fff', gridcolor: 'rgba(255,255,255,0.2)' },
            yaxis: { title: 'Y-Axis', color: '#fff', gridcolor: 'rgba(255,255,255,0.2)' },
            zaxis: { title: 'Value', color: '#fff', gridcolor: 'rgba(255,255,255,0.2)' }
        },
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        font: {
            color: '#fff'
        }
    };

    const config = {
        responsive: true
    };

    Plotly.newPlot(elementId, data, layout, config);
    plots[elementId] = true;
}

export function updatePlot(elementId, plotData) {
    if (plots[elementId]) {
        const data = {
            z: [plotData.z]
        };
        Plotly.restyle(elementId, data);
    }
}

export function destroyPlot(elementId) {
    if (plots[elementId]) {
        Plotly.purge(elementId);
        delete plots[elementId];
    }
}