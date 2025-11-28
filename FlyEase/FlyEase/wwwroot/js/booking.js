// Function to update Pax count in the input field
function updatePax(change) {
    var input = document.getElementById('paxInput');
    var currentVal = parseInt(input.value);

    // Limits: Min 1, Max 10 (as defined in BookingViewModel)
    var newVal = currentVal + change;

    if (newVal >= 1 && newVal <= 10) {
        input.value = newVal;
    }
}

// Function to open the Modal at the specific image index
function openGalleryModal(index) {
    var myModal = new bootstrap.Modal(document.getElementById('galleryModal'));

    // Get the carousel instance
    var carouselEl = document.getElementById('carouselGallery');
    var carousel = bootstrap.Carousel.getOrCreateInstance(carouselEl);

    // Go to the clicked image index
    carousel.to(index);

    myModal.show();
}