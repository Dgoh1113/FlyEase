// --- 1. VALIDATION LOGIC ---
function validateAndSubmit() {
    // Check our hidden flag
    var hasOptions = document.getElementById('hasOptionsFlag').value === "true";

    // IF options exist, we MUST check the dropdown
    if (hasOptions) {
        var dropdown = document.getElementById('optionDropdown');
        var selectedVal = dropdown.value; // Will be "" if default disabled option is selected

        if (!selectedVal) {
            // 1. Highlight Dropdown Red
            dropdown.classList.add('is-invalid');
            dropdown.focus();

            // 2. Fire SweetAlert
            Swal.fire({
                title: 'Selection Required',
                text: "Please select a package option from the dropdown menu to proceed.",
                icon: 'warning',
                confirmButtonColor: '#0d6efd',
                confirmButtonText: 'OK'
            });

            return; // STOP: Do not submit form
        } else {
            // If valid, remove red highlight
            dropdown.classList.remove('is-invalid');
        }
    }

    // If we reach here, everything is valid
    document.getElementById('bookingForm').submit();
}

// --- 2. REMOVE ERROR ON CHANGE ---
// Automatically remove red border when user selects something
document.addEventListener("DOMContentLoaded", function () {
    var dropdown = document.getElementById('optionDropdown');
    if (dropdown) {
        dropdown.addEventListener('change', function () {
            if (this.value) {
                this.classList.remove('is-invalid');
            }
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

// --- 4. GALLERY LOGIC ---
function openGalleryModal(index) {
    var myModal = new bootstrap.Modal(document.getElementById('galleryModal'));
    var carouselEl = document.getElementById('carouselGallery');
    var carousel = bootstrap.Carousel.getOrCreateInstance(carouselEl);
    carousel.to(index);
    myModal.show();
}