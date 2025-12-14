// ========================================
// REPORT PAGE - ALL FUNCTIONALITY
// ========================================

// Initialize all charts and event listeners when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    // Initialize charts with data from data attributes
    initializeCharts();

    // Attach event listeners
    attachEventListeners();
});

// ========================================
// CHART INITIALIZATION
// ========================================
function initializeCharts() {
    initializeBookingStatusChart();
    initializePaymentMethodChart();
    initializeRevenueChart();
}

function initializeBookingStatusChart() {
    const chartElement = document.getElementById('bookingStatusChart');
    if (!chartElement) return;

    const labelsAttr = chartElement.getAttribute('data-labels');
    const valuesAttr = chartElement.getAttribute('data-values');

    if (!labelsAttr || !valuesAttr) return;

    try {
        const labels = JSON.parse(labelsAttr);
        const values = JSON.parse(valuesAttr);

        new Chart(chartElement, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: ['#1cc88a', '#f6c23e', '#e74a3b'],
                    borderColor: '#fff',
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            font: { size: 12, weight: 'bold' }
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Error initializing Booking Status chart:', error);
    }
}

function initializePaymentMethodChart() {
    const chartElement = document.getElementById('paymentMethodChart');
    if (!chartElement) return;

    const labelsAttr = chartElement.getAttribute('data-labels');
    const valuesAttr = chartElement.getAttribute('data-values');
    const colorsAttr = chartElement.getAttribute('data-colors');

    if (!labelsAttr || !valuesAttr || !colorsAttr) return;

    try {
        const labels = JSON.parse(labelsAttr);
        const values = JSON.parse(valuesAttr);
        const colors = JSON.parse(colorsAttr);

        new Chart(chartElement, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors,
                    borderColor: '#fff',
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            font: { size: 12, weight: 'bold' }
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Error initializing Payment Method chart:', error);
    }
}

function initializeRevenueChart() {
    const chartElement = document.getElementById('revenueChart');
    if (!chartElement) return;

    const datesAttr = chartElement.getAttribute('data-dates');
    const valuesAttr = chartElement.getAttribute('data-values');

    if (!datesAttr || !valuesAttr) return;

    try {
        const dates = JSON.parse(datesAttr);
        const values = JSON.parse(valuesAttr);

        new Chart(chartElement, {
            type: 'line',
            data: {
                labels: dates,
                datasets: [{
                    label: 'Daily Revenue (RM)',
                    data: values,
                    borderColor: '#4e73df',
                    backgroundColor: 'rgba(78, 115, 223, 0.1)',
                    borderWidth: 3,
                    pointRadius: 5,
                    pointBackgroundColor: '#4e73df',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    fill: true,
                    tension: 0.4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            font: { size: 12, weight: 'bold' }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return 'RM ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    } catch (error) {
        console.error('Error initializing Revenue chart:', error);
    }
}

// ========================================
// EVENT LISTENERS
// ========================================
function attachEventListeners() {
    // Export PDF button
    const exportPdfBtn = document.querySelector('[data-action="export-pdf"]');
    if (exportPdfBtn) {
        exportPdfBtn.addEventListener('click', printReport);
    }

    // Form submission
    const reportForm = document.querySelector('#reportFilterForm');
    if (reportForm) {
        reportForm.addEventListener('submit', function (e) {
            // Show loading spinner if needed
            console.log('Report filters submitted');
        });
    }
}
function printReport() {
    window.print();
}



