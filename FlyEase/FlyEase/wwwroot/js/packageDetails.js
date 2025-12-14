console.log("booking.js loaded");

document.addEventListener("DOMContentLoaded", function () {
    // --- EXISTING CAROUSEL LOGIC ---
    var carousel = document.getElementById('carouselGallery');
    var counter = document.getElementById('carousel-counter');

    if (carousel && counter) {
        carousel.addEventListener('slide.bs.carousel', function (e) {
            var currentIndex = e.to + 1;
            var total = counter.innerText.split('/')[1];
            counter.innerText = currentIndex + " /" + total;
        });
    }

    // --- EXISTING CHECKBOX LOGIC ---
    var checkboxes = document.querySelectorAll('.option-trigger');
    checkboxes.forEach(function (checkbox) {
        checkbox.addEventListener('change', function () {
            toggleCheckboxStyle(this);
        });
    });

    // --- NEW: MAP LOGIC (Reads from HTML Data Attributes) ---
    var mapContainer = document.getElementById('bookingMap');
    if (mapContainer) {
        // 1. Read Data
        var lat = parseFloat(mapContainer.getAttribute('data-lat'));
        console.log("Latitude:", lat);
        var lng = parseFloat(mapContainer.getAttribute('data-lng'));
        console.log("Longitude:", lng);
        var title = mapContainer.getAttribute('data-title');
        var desc = mapContainer.getAttribute('data-desc');

        // 2. Init Map
        var map = L.map('bookingMap').setView([lat, lng], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        L.marker([lat, lng]).addTo(map)
            .bindPopup("<b>" + title + "</b><br>" + desc)
            .openPopup();

        // 3. Fix Grey Box Issue
        setTimeout(function () { map.invalidateSize(); }, 500);
    }
});


// Helper for Checkboxes (Visual Toggle)
function toggleCheckboxStyle(checkbox) {
    var label = document.querySelector('label[for="' + checkbox.id + '"]');
    if (!label) return;

    var uncheckedIcon = label.querySelector('.check-icon-unchecked');
    var checkedIcon = label.querySelector('.check-icon-checked');

    if (checkbox.checked) {
        // --- APPLIED SELECTED STATE ---
        label.classList.add('selected-card'); // Adds the dark blue style

        // Remove default styles to prevent conflicts
        label.classList.remove('text-dark', 'border', 'btn-outline-light');

        // Swap Icons
        if (uncheckedIcon) uncheckedIcon.classList.add('d-none');
        if (checkedIcon) checkedIcon.classList.remove('d-none');
    } else {
        // --- REMOVED SELECTED STATE ---
        label.classList.remove('selected-card');

        // Restore default styles
        label.classList.add('text-dark', 'border', 'btn-outline-light');

        // Swap Icons Back
        if (uncheckedIcon) uncheckedIcon.classList.remove('d-none');
        if (checkedIcon) checkedIcon.classList.add('d-none');
    }
}

// --- NEW VALIDATION LOGIC ---
function validateBookNow(event) {
    console.log("bookNowCheck called");
    var input = document.getElementById('paxInput');
    if (!input) return true;

    // Parse values (default to 0 or 1 if empty)
    var currentVal = parseInt(input.value) || 0;
    var maxVal = parseInt(input.getAttribute('max')) || 0;

    // 1. Check if exceeds max
    if (currentVal > maxVal) {
        event.preventDefault(); // STOP FORM SUBMISSION

        Swal.fire({
            icon: 'error',
            title: 'Not Enough Slots',
            text: `Sorry, only ${maxVal} slots are available for this package.`,
            confirmButtonColor: '#0d6efd'
        });

        input.value = maxVal; // Auto-correct to max
        return false;
    }

    // 2. Check if less than 1
    if (currentVal < 1) {
        event.preventDefault(); // STOP FORM SUBMISSION

        Swal.fire({
            icon: 'warning',
            title: 'Invalid Quantity',
            text: 'Please select at least 1 guest.',
            confirmButtonColor: '#0d6efd'
        });
        return false;
    }

    // If valid, return true (allow submit)
    return true;
}

function openGalleryModal(index) {
    var myModal = new bootstrap.Modal(document.getElementById('galleryModal'));
    var carouselEl = document.getElementById('carouselGallery');
    var carousel = bootstrap.Carousel.getOrCreateInstance(carouselEl);

    // Jump to specific slide
    carousel.to(index);

    // Update Counter Immediately
    var counter = document.getElementById('carousel-counter');
    if (counter) {
        var total = counter.innerText.split('/')[1];
        counter.innerText = (index + 1) + " /" + total;
    }

    myModal.show();
}
