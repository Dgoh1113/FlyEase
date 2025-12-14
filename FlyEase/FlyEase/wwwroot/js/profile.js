// Cleaned profile.js - REMOVE ALL OLD CODE AND REPLACE WITH THIS:

document.addEventListener('DOMContentLoaded', function () {
    console.log('Profile page loaded');

    // 1. Tab Switching Logic
    window.switchTab = function (tabName) {
        console.log('Switching to tab:', tabName);

        // Hide all tab panes
        document.querySelectorAll('.tab-pane').forEach(pane => {
            pane.classList.remove('active', 'show');
            pane.style.display = 'none';
        });

        // Remove active class from all nav links
        document.querySelectorAll('.tab-nav-item').forEach(link => {
            link.classList.remove('active');
        });

        // Show selected tab
        const selectedPane = document.getElementById(tabName + '-tab');
        if (selectedPane) {
            selectedPane.classList.add('active', 'show');
            selectedPane.style.display = 'block';
        }

        // Highlight nav link
        const selectedLink = document.getElementById('link-' + tabName);
        if (selectedLink) {
            selectedLink.classList.add('active');
        }
    };

    // 2. Check URL for tab parameter on page load
    const urlParams = new URLSearchParams(window.location.search);
    const tabParam = urlParams.get('tab');

    // Set initial tab - check for URL parameter first
    if (tabParam && ['bookings', 'reviews', 'payments', 'details', 'security'].includes(tabParam)) {
        // Wait a tiny bit to ensure DOM is ready
        setTimeout(() => switchTab(tabParam), 50);
    } else {
        // Default to bookings tab
        setTimeout(() => switchTab('bookings'), 50);
    }

    // 3. Add click listeners to all tab items
    document.querySelectorAll('.tab-nav-item').forEach(item => {
        item.addEventListener('click', function (e) {
            // Skip if it's a logout button
            if (this.querySelector('button[type="submit"]')) return;

            const tabId = this.id.replace('link-', '');
            if (tabId) {
                switchTab(tabId);

                // Update URL without reloading page
                const url = new URL(window.location);
                url.searchParams.set('tab', tabId);
                window.history.pushState({}, '', url);
            }
        });
    });

    // 4. Password Toggle for ALL password fields
    document.querySelectorAll('.password-toggle').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();

            // Find the nearest form-floating or parent container
            const container = this.closest('.form-floating') || this.closest('.position-relative') || this.parentElement;
            if (!container) return;

            const input = container.querySelector('input[type="password"], input[type="text"]');
            const icon = this.querySelector('i');

            if (!input || !icon) return;

            if (input.type === 'password') {
                input.type = 'text';
                icon.classList.remove('fa-eye');
                icon.classList.add('fa-eye-slash');
            } else {
                input.type = 'password';
                icon.classList.remove('fa-eye-slash');
                icon.classList.add('fa-eye');
            }
        });
    });

    // 5. Initialize Password Strength Meter for Profile Page
    const newPasswordInput = document.getElementById('NewPassword');
    const strengthFill = document.getElementById('strengthFill');
    const strengthText = document.getElementById('strengthText');

    if (newPasswordInput && strengthFill && strengthText) {
        console.log('Initializing password strength meter');

        newPasswordInput.addEventListener('input', function (e) {
            const password = e.target.value;
            updatePasswordStrength(password, strengthFill, strengthText);
        });

        // Initial check if there's already text
        if (newPasswordInput.value) {
            updatePasswordStrength(newPasswordInput.value, strengthFill, strengthText);
        }
    }

    // 6. Password Confirmation Matching
    const newPasswordField = document.getElementById('NewPassword');
    const confirmPasswordField = document.getElementById('ConfirmNewPassword');
    const passwordMatchDiv = document.getElementById('passwordMatch');

    if (newPasswordField && confirmPasswordField && passwordMatchDiv) {
        const checkPasswordMatch = () => {
            const password = newPasswordField.value;
            const confirm = confirmPasswordField.value;

            if (confirm === '') {
                passwordMatchDiv.innerHTML = '';
                confirmPasswordField.classList.remove('is-valid', 'is-invalid');
                return;
            }

            if (password === confirm) {
                passwordMatchDiv.innerHTML = '<small class="text-success"><i class="fas fa-check-circle me-1"></i> Passwords match</small>';
                confirmPasswordField.classList.remove('is-invalid');
                confirmPasswordField.classList.add('is-valid');
            } else {
                passwordMatchDiv.innerHTML = '<small class="text-danger"><i class="fas fa-times-circle me-1"></i> Passwords do not match</small>';
                confirmPasswordField.classList.remove('is-valid');
                confirmPasswordField.classList.add('is-invalid');
            }
        };

        newPasswordField.addEventListener('input', checkPasswordMatch);
        confirmPasswordField.addEventListener('input', checkPasswordMatch);
    }

    // 7. Form Validation
    document.querySelectorAll('form.needs-validation').forEach(form => {
        form.addEventListener('submit', function (event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        });
    });
});

// Password Strength Calculation Function
function updatePasswordStrength(password, strengthFill, strengthText) {
    let score = 0;
    const requirements = [
        password.length >= 6,
        /[A-Z]/.test(password),
        /[a-z]/.test(password),
        /[0-9]/.test(password),
        /[!@#$%^&*(),.?":{}|<>/]/.test(password)
    ];

    score = requirements.filter(Boolean).length;
    const percentage = (score / 5) * 100;

    // Update visual
    strengthFill.style.width = percentage + '%';
    strengthFill.className = 'strength-fill';

    // Update text and color
    if (score <= 1) {
        strengthFill.classList.add('strength-weak');
        strengthText.textContent = 'Weak';
        strengthText.className = 'fw-bold text-danger';
    } else if (score <= 3) {
        strengthFill.classList.add('strength-medium');
        strengthText.textContent = 'Medium';
        strengthText.className = 'fw-bold text-warning';
    } else {
        strengthFill.classList.add('strength-strong');
        strengthText.textContent = 'Strong';
        strengthText.className = 'fw-bold text-success';
    }
}

// Refresh Bookings Function
window.refreshBookings = function () {
    const btn = document.getElementById('refreshBtn');
    if (!btn) return;

    const originalHtml = btn.innerHTML;
    btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Refreshing...';
    btn.disabled = true;

    fetch('/Auth/RefreshBookings')
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
            return response.text();
        })
        .then(html => {
            const bookingTableBody = document.getElementById('bookingTableBody');
            if (bookingTableBody) {
                bookingTableBody.innerHTML = html;
            }

            btn.innerHTML = originalHtml;
            btn.disabled = false;

            // Show success message
            const existingAlert = document.querySelector('#bookings-tab .alert');
            if (existingAlert) existingAlert.remove();

            const alert = document.createElement('div');
            alert.className = 'alert alert-success alert-dismissible fade show mt-3';
            alert.innerHTML = `
                <i class="fas fa-check-circle me-2"></i> Bookings refreshed successfully!
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;

            const bookingsTab = document.querySelector('#bookings-tab');
            if (bookingsTab) {
                bookingsTab.prepend(alert);

                // Auto-remove alert after 5 seconds
                setTimeout(() => {
                    const bsAlert = new bootstrap.Alert(alert);
                    bsAlert.close();
                }, 5000);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            btn.innerHTML = originalHtml;
            btn.disabled = false;

            // Show error message
            const alert = document.createElement('div');
            alert.className = 'alert alert-danger alert-dismissible fade show mt-3';
            alert.innerHTML = `
                <i class="fas fa-exclamation-circle me-2"></i> Failed to refresh bookings. Please try again.
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;

            const bookingsTab = document.querySelector('#bookings-tab');
            if (bookingsTab) {
                bookingsTab.prepend(alert);
            }
        });
};

// Reset Password Form Function
window.resetPasswordForm = function () {
    const form = document.querySelector('#security-tab form');
    if (form) {
        form.reset();

        // Reset strength meter
        const strengthFill = document.getElementById('strengthFill');
        const strengthText = document.getElementById('strengthText');
        const passwordMatchDiv = document.getElementById('passwordMatch');

        if (strengthFill) {
            strengthFill.style.width = '0%';
            strengthFill.className = 'strength-fill';
        }

        if (strengthText) {
            strengthText.textContent = 'None';
            strengthText.className = 'fw-bold text-muted';
        }

        if (passwordMatchDiv) {
            passwordMatchDiv.innerHTML = '';
        }

        // Clear validation classes
        document.querySelectorAll('.is-valid, .is-invalid').forEach(el => {
            el.classList.remove('is-valid', 'is-invalid');
        });

        // Reset password toggles
        document.querySelectorAll('.password-toggle i').forEach(icon => {
            icon.className = 'fas fa-eye';
        });

        document.querySelectorAll('#security-tab input[type="text"]').forEach(input => {
            if (input.id.includes('Password')) {
                input.type = 'password';
            }
        });
    }
};

// Reset Password Form Function
window.resetPasswordForm = function () {
    const form = document.querySelector('#security-tab form');
    if (form) {
        form.reset();

        // Reset strength meter
        const strengthFill = document.getElementById('strengthFill');
        const strengthText = document.getElementById('strengthText');
        const passwordMatchDiv = document.getElementById('passwordMatch');

        if (strengthFill) {
            strengthFill.style.width = '0%';
            strengthFill.className = 'strength-fill';
        }

        if (strengthText) {
            strengthText.textContent = 'None';
            strengthText.className = 'strength-text fw-bold text-muted';
        }

        if (passwordMatchDiv) {
            passwordMatchDiv.innerHTML = `
                <div class="d-flex align-items-center">
                    <i class="fas fa-circle text-muted me-2" style="font-size: 0.5rem;"></i>
                    <small class="text-muted">Passwords will be checked for match</small>
                </div>`;
        }

        // Clear validation classes
        document.querySelectorAll('.is-valid, .is-invalid').forEach(el => {
            el.classList.remove('is-valid', 'is-invalid');
        });

        // Reset password toggles
        document.querySelectorAll('#security-tab .password-toggle i').forEach(icon => {
            icon.className = 'fas fa-eye';
        });

        document.querySelectorAll('#security-tab input[type="text"]').forEach(input => {
            if (input.id.includes('Password')) {
                input.type = 'password';
            }
        });

        // Clear error messages
        document.querySelectorAll('#security-tab .error-container .text-danger').forEach(el => {
            el.textContent = '';
        });
    }
};