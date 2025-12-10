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

// --- 3. PAX COUNTER ---
function updatePax(change) {
    var input = document.getElementById('paxInput');
    if (input) {
        var currentVal = parseInt(input.value);
        var newVal = currentVal + change;
        if (newVal >= 1 && newVal <= 10) input.value = newVal;
    }
}