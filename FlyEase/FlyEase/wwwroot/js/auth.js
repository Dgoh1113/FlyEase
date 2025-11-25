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

        if (document.getElementById('strengthFill')) {
            this.initPasswordStrength();
        }
    }

    initPasswordToggles() {
        document.querySelectorAll('.password-toggle').forEach(btn => {
            btn.addEventListener('click', (e) => {
                // Prevent form submit if inside form
                e.preventDefault();
                const button = e.currentTarget;
                const input = button.previousElementSibling;
                const icon = button.querySelector('i');

                if (input.type === 'password') {
                    input.type = 'text';
                    icon.classList.replace('fa-eye', 'fa-eye-slash');
                } else {
                    input.type = 'password';
                    icon.classList.replace('fa-eye-slash', 'fa-eye');
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

        // Updated: Phone now only checks digits because +60 is fixed
        if (input.name === 'Phone') {
            const phoneRegex = /^\d{9,11}$/;
            this.toggleError(input, phoneRegex.test(input.value));
        }
    }

    validatePasswordMatch() {
        if (!this.confirmInput || !this.passwordInput) return;

        const matchMsg = document.getElementById('passwordMatch');
        const isMatch = this.passwordInput.value === this.confirmInput.value;
        const bothFilled = this.passwordInput.value && this.confirmInput.value;

        if (bothFilled) {
            if (isMatch) {
                this.confirmInput.classList.remove('is-invalid');
                this.confirmInput.classList.add('is-valid');
                matchMsg.innerHTML = '<div class="text-success small mt-1"><i class="fas fa-check-circle"></i> Passwords match</div>';
            } else {
                this.confirmInput.classList.remove('is-valid');
                this.confirmInput.classList.add('is-invalid');
                matchMsg.innerHTML = '<div class="text-danger small mt-1"><i class="fas fa-times-circle"></i> Passwords do not match</div>';
            }
        } else {
            matchMsg.innerHTML = '';
            this.confirmInput.classList.remove('is-valid', 'is-invalid');
        }
    }

    toggleError(input, isValid) {
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
        // Added check for Special Character
        const reqs = {
            'reqLength': password.length >= 6,
            'reqUpper': /[A-Z]/.test(password),
            'reqLower': /[a-z]/.test(password),
            'reqNumber': /[0-9]/.test(password),
            'reqSpecial': /[!@#$%^&*/(),.?":{}|<>]/.test(password)
        };

        for (const [id, met] of Object.entries(reqs)) {
            const el = document.getElementById(id);
            if (el) {
                el.className = met ? 'requirement-met' : 'requirement-unmet';
                const icon = el.querySelector('i');
                icon.className = met ? 'fas fa-check-circle' : 'fas fa-circle';
            }
        }
    }

    updateStrengthBar(password) {
        const strengthFill = document.getElementById('strengthFill');

        let score = 0;
        if (password.length >= 6) score++;
        if (/[A-Z]/.test(password)) score++;
        if (/[a-z]/.test(password)) score++;
        if (/[0-9]/.test(password)) score++;
        if (/[!@#$%^&*/(),.?":{}|<>]/.test(password)) score++;

        // Adjusted for 5 requirements
        const config = {
            0: { width: '0%', class: '' },
            1: { width: '20%', class: 'strength-weak' },
            2: { width: '40%', class: 'strength-weak' },
            3: { width: '60%', class: 'strength-medium' },
            4: { width: '80%', class: 'strength-medium' },
            5: { width: '100%', class: 'strength-strong' }
        };

        const state = config[score] || config[0];
        strengthFill.className = `strength-fill ${state.class}`;
        strengthFill.style.width = state.width;
    }

    initFormSubmission() {
        if (!this.form) return;

        this.form.addEventListener('submit', (e) => {
            if (this.form.querySelectorAll('.is-invalid').length > 0) {
                e.preventDefault();
                return;
            }
            // Allow form to submit naturally
        });
    }
}

document.addEventListener('DOMContentLoaded', () => new AuthForms());