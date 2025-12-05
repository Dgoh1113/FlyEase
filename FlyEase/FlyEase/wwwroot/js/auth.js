class AuthForms {
    constructor() {
        this.form = document.querySelector('form');
        this.passwordInput = document.getElementById('password');
        this.confirmInput = document.getElementById('confirmPassword');
        this.submitBtn = document.querySelector('button[type="submit"]');

        this.init();
    }

    init() {
        this.initPasswordToggles();
        this.initRealTimeValidation();
        this.initFormSubmission();

        // Only run strength meter if the element exists (Register page)
        if (document.getElementById('strengthFill')) {
            this.initPasswordStrength();
        }
    }

    initPasswordToggles() {
        document.querySelectorAll('.password-toggle').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault(); // Stop form submission
                const button = e.currentTarget;

                // --- THE FIX ---
                // 1. Find the parent container (the div with class "form-floating")
                const container = button.closest('.form-floating') || button.parentElement;

                // 2. Find the actual input tag inside that container
                const input = container.querySelector('input');
                const icon = button.querySelector('i');

                if (!input) return; // Safety check

                // 3. Toggle the type
                if (input.type === 'password') {
                    input.type = 'text'; // Show password
                    icon.classList.remove('fa-eye');
                    icon.classList.add('fa-eye-slash');
                } else {
                    input.type = 'password'; // Hide password
                    icon.classList.remove('fa-eye-slash');
                    icon.classList.add('fa-eye');
                }
            });
        });
    }

    initRealTimeValidation() {
        const inputs = document.querySelectorAll('input');

        inputs.forEach(input => {
            input.addEventListener('blur', () => this.validateField(input));
            input.addEventListener('input', () => {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                }
                // Check password match whenever user types in either box
                if (input.id === 'confirmPassword' || (input.id === 'password' && this.confirmInput?.value)) {
                    this.validatePasswordMatch();
                }
            });
        });
    }

    validateField(input) {
        if (!input.value && input.hasAttribute('required')) return;

        if (input.type === 'email') {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            this.toggleError(input, emailRegex.test(input.value));
        }

        if (input.name === 'Phone') {
            // Remove non-digits
            const cleanVal = input.value.replace(/\D/g, '');
            // Valid length between 9 and 11 digits
            const isValid = cleanVal.length >= 9 && cleanVal.length <= 11;
            this.toggleError(input, isValid);
        }
    }

    validatePasswordMatch() {
        if (!this.confirmInput || !this.passwordInput) return;

        // Find or create the message div
        let matchMsg = document.getElementById('passwordMatch');
        if (!matchMsg) {
            matchMsg = document.createElement('div');
            matchMsg.id = 'passwordMatch';
            // Insert it after the password group
            this.confirmInput.closest('.form-floating').parentNode.appendChild(matchMsg);
        }

        const isMatch = this.passwordInput.value === this.confirmInput.value;
        const bothFilled = this.passwordInput.value && this.confirmInput.value;

        if (bothFilled) {
            if (isMatch) {
                this.confirmInput.classList.remove('is-invalid');
                this.confirmInput.classList.add('is-valid');
                matchMsg.innerHTML = '<small class="text-success mt-1"><i class="fas fa-check-circle"></i> Passwords match</small>';
            } else {
                this.confirmInput.classList.remove('is-valid');
                this.confirmInput.classList.add('is-invalid');
                matchMsg.innerHTML = '<small class="text-danger mt-1"><i class="fas fa-times-circle"></i> Passwords do not match</small>';
            }
        } else {
            matchMsg.innerHTML = '';
            this.confirmInput.classList.remove('is-valid', 'is-invalid');
        }
    }

    toggleError(input, isValid) {
        if (input.value === '') {
            input.classList.remove('is-valid', 'is-invalid');
            return;
        }

        if (!isValid) {
            input.classList.add('is-invalid');
        } else {
            input.classList.remove('is-invalid');
            input.classList.add('is-valid');
        }
    }

    initPasswordStrength() {
        this.passwordInput.addEventListener('input', (e) => {
            const val = e.target.value;
            this.updateRequirements(val);
            this.updateStrengthBar(val);
        });
    }

    updateRequirements(password) {
        // IDs must match the HTML elements in Register.cshtml
        const reqs = {
            'req-length': password.length >= 8,
            'req-upper': /[A-Z]/.test(password),
            'req-number': /[0-9]/.test(password),
            'req-special': /[!@#$%^&*/(),.?":{}|<>]/.test(password)
        };

        for (const [id, met] of Object.entries(reqs)) {
            const el = document.getElementById(id);
            if (el) {
                const icon = el.querySelector('i');
                if (met) {
                    el.classList.add('requirement-met');
                    el.classList.remove('requirement-unmet');
                    if (icon) icon.className = 'fas fa-check-circle me-2';
                } else {
                    el.classList.remove('requirement-met');
                    el.classList.add('requirement-unmet');
                    if (icon) icon.className = 'fas fa-circle me-2';
                }
            }
        }
    }

    updateStrengthBar(password) {
        const strengthFill = document.getElementById('strengthFill');
        const strengthText = document.getElementById('strengthText');

        if (!strengthFill) return;

        let score = 0;
        if (password.length >= 8) score++;
        if (/[A-Z]/.test(password)) score++;
        if (/[0-9]/.test(password)) score++;
        if (/[!@#$%^&*/(),.?":{}|<>]/.test(password)) score++;

        // Map score (0-4) to percentage and class
        const config = {
            0: { width: '0%', class: '', text: 'Weak' },
            1: { width: '25%', class: 'strength-weak', text: 'Weak' },
            2: { width: '50%', class: 'strength-medium', text: 'Medium' },
            3: { width: '75%', class: 'strength-medium', text: 'Medium' },
            4: { width: '100%', class: 'strength-strong', text: 'Strong' }
        };

        const state = config[score] || config[0];

        strengthFill.className = `strength-fill ${state.class}`;
        strengthFill.style.width = state.width;

        if (strengthText) strengthText.innerText = state.text;
    }

    initFormSubmission() {
        if (!this.form) return;

        this.form.addEventListener('submit', (e) => {
            if (this.form.querySelectorAll('.is-invalid').length > 0) {
                e.preventDefault();
                // Optional: Scroll to first error
                this.form.querySelector('.is-invalid').focus();
            }
        });
    }
}

// Initialize the class when the DOM is ready
document.addEventListener('DOMContentLoaded', () => new AuthForms());