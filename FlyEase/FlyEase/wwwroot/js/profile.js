document.addEventListener('DOMContentLoaded', function () {

    // 1. Tab Switching Logic
    window.switchTab = function (tabName) {
        // Hide all tab panes
        document.querySelectorAll('.tab-pane').forEach(pane => {
            pane.classList.remove('active');
            pane.style.display = 'none';
        });

        // Remove active class from nav links
        document.querySelectorAll('.nav-link-custom').forEach(link => {
            link.classList.remove('active');
        });

        // Show selected tab
        const selectedPane = document.getElementById(tabName + '-tab');
        if (selectedPane) {
            selectedPane.classList.add('active');
            selectedPane.style.display = 'block';
        }

        // Highlight nav link (Find by ID or fallback to click target if passed)
        const selectedLink = document.getElementById('link-' + tabName);
        if (selectedLink) {
            selectedLink.classList.add('active');
        }
    };

    // 2. Toggle Eye Icon (Password Visibility)
    document.querySelectorAll('.password-toggle').forEach(btn => {
        btn.addEventListener('click', function (e) {
            e.preventDefault(); // Prevent form submit

            // Find the input within the same container
            const container = this.closest('.input-with-icon');
            const input = container.querySelector('input');
            const icon = this.querySelector('i');

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

    // 3. Password Strength Meter (Real-time)
    const newPassInput = document.getElementById('newPassword');
    const strengthFill = document.getElementById('strengthFill');

    if (newPassInput && strengthFill) {
        newPassInput.addEventListener('input', function (e) {
            const val = e.target.value;
            let score = 0;

            // Logic
            if (val.length >= 6) score++;
            if (/[A-Z]/.test(val)) score++;
            if (/[a-z]/.test(val)) score++;
            if (/[0-9]/.test(val)) score++;
            if (/[!@#$%^&*(),.?":{}|<>/]/.test(val)) score++;

            // Visuals
            const pct = (score / 5) * 100;
            strengthFill.style.width = pct + '%';

            // Colors
            strengthFill.className = 'progress-bar'; // Reset
            if (score <= 2) strengthFill.classList.add('bg-danger');
            else if (score <= 4) strengthFill.classList.add('bg-warning');
            else strengthFill.classList.add('bg-success');
        });
    }
});