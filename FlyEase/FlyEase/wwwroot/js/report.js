document.addEventListener("DOMContentLoaded", function () {

    // Check if we are on the Printed Report Page
    if (document.querySelector('.printed-report-body')) {

        // 1. Initialize Chart
        initSalesChart();

        // 2. Trigger Print Dialog after a short delay to allow chart to render
        setTimeout(function () {
            window.print();
        }, 800);
    }

});

function initSalesChart() {
    var ctx = document.getElementById('salesChart');
    console.log(ctx);
    if (!ctx) return;

    // Retrieve data from hidden inputs
    var rawLabels = document.getElementById('chartLabels').value;
    var rawValues = document.getElementById('chartValues').value;
    console.log(rawLabels, rawValues);
    if (!rawLabels || !rawValues) return;

    var labels = rawLabels.split(',');
    var values = rawValues.split(',').map(Number);

    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Revenue (RM)',
                data: values,
                backgroundColor: 'rgba(94, 114, 228, 0.7)', // FlyEase Primary Blue
                borderColor: '#5e72e4',
                borderWidth: 1,
                borderRadius: 4,
                barThickness: 30
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false, // Disable animation for printing
            plugins: {
                legend: { display: false },
                title: { display: false }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: { color: '#f0f0f0' },
                    ticks: { font: { size: 10 } }
                },
                x: {
                    grid: { display: false },
                    ticks: { font: { size: 10 } }
                }
            }
        }
    });
}