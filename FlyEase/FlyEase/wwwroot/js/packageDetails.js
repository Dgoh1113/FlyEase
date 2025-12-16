console.log("packageDetails.js loaded");

document.addEventListener("DOMContentLoaded", function () {
    // --- GALLERY RADIO LOGIC ---
    setupGalleryRadios();

    // --- CHECKBOX LOGIC ---
    var checkboxes = document.querySelectorAll('.option-trigger');
    checkboxes.forEach(function (checkbox) {
        checkbox.addEventListener('change', function () {
            toggleCheckboxStyle(this);
        });
    });

    // --- MAP LOGIC (Reads from HTML Data Attributes) ---
    var mapContainer = document.getElementById('bookingMap');
    if (mapContainer) {
        var lat = parseFloat(mapContainer.getAttribute('data-lat'));
        var lng = parseFloat(mapContainer.getAttribute('data-lng'));
        var title = mapContainer.getAttribute('data-title');
        var desc = mapContainer.getAttribute('data-desc');

        var map = L.map('bookingMap').setView([lat, lng], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        L.marker([lat, lng]).addTo(map)
            .bindPopup("<b>" + title + "</b><br>" + desc)
            .openPopup();

        setTimeout(function () { map.invalidateSize(); }, 500);
    }
});

// Setup Gallery Radio Button Logic
function setupGalleryRadios() {
    const radios = document.querySelectorAll('.gallery-radio');
    const images = document.querySelectorAll('.gallery-main-image');

    radios.forEach((radio, index) => {
        radio.addEventListener('change', function () {
            if (this.checked) {
                // Remove active class from all images
                images.forEach(img => img.classList.remove('active'));
                
                // Add active class to the corresponding image
                if (images[index]) {
                    images[index].classList.add('active');
                }
            }
        });
    });

    // Initialize first image as active
    if (images.length > 0) {
        images[0].classList.add('active');
    }
}

// Helper for Checkboxes (Visual Toggle)
function toggleCheckboxStyle(checkbox) {
    var label = document.querySelector('label[for="' + checkbox.id + '"]');
    if (!label) return;

    var uncheckedIcon = label.querySelector('.check-icon-unchecked');
    var checkedIcon = label.querySelector('.check-icon-checked');

    if (checkbox.checked) {
        label.classList.add('selected-card');
        label.classList.remove('text-dark', 'border', 'btn-outline-light');

        if (uncheckedIcon) uncheckedIcon.classList.add('d-none');
        if (checkedIcon) checkedIcon.classList.remove('d-none');
    } else {
        label.classList.remove('selected-card');
        label.classList.add('text-dark', 'border', 'btn-outline-light');

        if (uncheckedIcon) uncheckedIcon.classList.remove('d-none');
        if (checkedIcon) checkedIcon.classList.add('d-none');
    }
}

// --- VALIDATION LOGIC ---
function validateBookNow(event) {
    console.log("validateBookNow called");
    var input = document.getElementById('paxInput');
    if (!input) return true;

    var currentVal = parseInt(input.value) || 0;
    var maxVal = parseInt(input.getAttribute('max')) || 0;

    if (currentVal > maxVal) {
        event.preventDefault();
        Swal.fire({
            icon: 'error',
            title: 'Not Enough Slots',
            text: `Sorry, only ${maxVal} slots are available for this package.`,
            confirmButtonColor: '#0d6efd'
        });
        input.value = maxVal;
        return false;
    }

    if (currentVal < 1) {
        event.preventDefault();
        Swal.fire({
            icon: 'warning',
            title: 'Invalid Quantity',
            text: 'Please select at least 1 guest.',
            confirmButtonColor: '#0d6efd'
        });
        return false;
    }

    return true;
}
