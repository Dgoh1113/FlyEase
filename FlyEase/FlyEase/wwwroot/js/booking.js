console.log("booking.js loaded");
// --- 1. GALLERY CAROUSEL LOGIC ---
document.addEventListener("DOMContentLoaded", function () {
    var carousel = document.getElementById('carouselGallery');
    var counter = document.getElementById('carousel-counter');

    // Update counter when slide changes
    if (carousel && counter) {
        carousel.addEventListener('slide.bs.carousel', function (e) {
            // e.to is 0-indexed, so we add 1
            var currentIndex = e.to + 1;
            var total = counter.innerText.split('/')[1]; // Keep total from existing text
            counter.innerText = currentIndex + " /" + total;
        });
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

// --- 2. FORM VALIDATION ---
function validateAndSubmit() {
    var hasOptions = document.getElementById('hasOptionsFlag').value === "true";

    if (hasOptions) {
        var dropdown = document.getElementById('optionDropdown');
        var selectedVal = dropdown.value;

        if (!selectedVal) {
            dropdown.classList.add('is-invalid');
            dropdown.focus();

            Swal.fire({
                title: 'Selection Required',
                text: "Please select a package option from the dropdown menu.",
                icon: 'warning',
                confirmButtonColor: '#0d6efd',
                confirmButtonText: 'OK'
            });
            return;
        } else {
            dropdown.classList.remove('is-invalid');
        }
    }
    document.getElementById('bookingForm').submit();
}

// Auto-remove invalid class on change
document.addEventListener("DOMContentLoaded", function () {
    var dropdown = document.getElementById('optionDropdown');
    if (dropdown) {
        dropdown.addEventListener('change', function () {
            if (this.value) this.classList.remove('is-invalid');
        });
    }
});

// --- 3. PAX COUNTER ---
function updatePax(change) {
    var input = document.getElementById('paxInput');
    if (input) {
        var currentVal = parseInt(input.value);
        var newVal = currentVal + change;
        if (newVal >= 1 && newVal <= 10) input.value = newVal;
    }
}