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

                // Find the parent container (the div with class "form-floating")
                const container = button.closest('.form-floating') || button.parentElement;

                // Find the actual input tag inside that container
                const input = container.querySelector('input');
                const icon = button.querySelector('i');

                if (!input) return; // Safety check

                // Toggle the type
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
        const form = document.querySelector('form');

        inputs.forEach(input => {
            // Validate on blur
            input.addEventListener('blur', () => this.validateField(input));

            // Clear error on input
            input.addEventListener('input', () => {
                if (input.classList.contains('is-invalid')) {
                    input.classList.remove('is-invalid');
                    const errorSpan = input.parentElement.querySelector('.text-danger');
                    if (errorSpan) errorSpan.textContent = '';
                }

                // Special handling for password matching
                if (input.id === 'confirmPassword' || (input.id === 'password' && this.confirmInput?.value)) {
                    this.validatePasswordMatch();
                }
            });
        });

        // Validate all fields on form submit
        if (form) {
            form.addEventListener('submit', (e) => {
                let hasErrors = false;

                inputs.forEach(input => {
                    if (input.hasAttribute('required') && !input.value.trim()) {
                        this.toggleError(input, false);
                        hasErrors = true;
                    }
                });

                if (hasErrors) {
                    e.preventDefault();
                    // Scroll to first error
                    const firstError = form.querySelector('.is-invalid');
                    if (firstError) {
                        firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }
            });
        }
    }

    validateField(input) {
        // Skip if empty and not required
        if (!input.value && !input.hasAttribute('required')) return;

        let isValid = true;
        let errorMessage = '';

        switch (input.type) {
            case 'email':
                const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                isValid = emailRegex.test(input.value);
                if (!isValid) errorMessage = 'Please enter a valid email address';
                break;

            case 'password':
                // REMOVED: Real-time password complexity validation
                // Only validate on form submission
                break;

            case 'text':
                if (input.name === 'Phone') {
                    const cleanVal = input.value.replace(/\D/g, '');
                    isValid = cleanVal.length >= 9 && cleanVal.length <= 11;
                    if (!isValid) errorMessage = 'Phone must be 9-11 digits';
                }
                break;
        }

        this.toggleError(input, isValid, errorMessage);
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

    toggleError(input, isValid, errorMessage = '') {
        const parent = input.parentElement;
        let errorSpan = parent.querySelector('.field-error');

        if (!errorSpan) {
            errorSpan = document.createElement('span');
            errorSpan.className = 'text-danger small field-error';
            parent.appendChild(errorSpan);
        }

        if (!isValid && input.value) {
            input.classList.add('is-invalid');
            input.classList.remove('is-valid');
            errorSpan.textContent = errorMessage;
            errorSpan.style.display = 'block';
        } else if (input.value) {
            input.classList.remove('is-invalid');
            input.classList.add('is-valid');
            errorSpan.textContent = '';
            errorSpan.style.display = 'none';
        } else {
            input.classList.remove('is-invalid', 'is-valid');
            errorSpan.textContent = '';
            errorSpan.style.display = 'none';
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

// Login Security Lockout System
// Locks user for 30 seconds after 3 failed attempts

class LoginLockout {
    constructor() {
        this.MAX_ATTEMPTS = 3;
        this.LOCKOUT_SECONDS = 30;
        this.STORAGE_KEY = 'login_lockout_data';
        
        this.failedAttempts = 0;
        this.isLocked = false;
        this.lockEndTime = null;
        this.timerInterval = null;
        
        this.init();
    }
    
    init() {
        this.loadFromStorage();
        this.setupForm();
        this.checkInitialLock();
    }
    
    loadFromStorage() {
        const data = localStorage.getItem(this.STORAGE_KEY);
        if (data) {
            try {
                const parsed = JSON.parse(data);
                this.failedAttempts = parsed.attempts || 0;
                this.lockEndTime = parsed.lockEndTime ? new Date(parsed.lockEndTime) : null;
                
                // Check if still locked
                if (this.lockEndTime && new Date() < this.lockEndTime) {
                    this.isLocked = true;
                    const remaining = Math.ceil((this.lockEndTime - new Date()) / 1000);
                    this.startLockout(remaining);
                } else if (this.lockEndTime && new Date() >= this.lockEndTime) {
                    // Lock expired
                    this.resetLockout();
                }
            } catch (e) {
                this.resetLockout();
            }
        }
    }
    
    saveToStorage() {
        const data = {
            attempts: this.failedAttempts,
            lockEndTime: this.lockEndTime ? this.lockEndTime.toISOString() : null
        };
        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
    }
    
    checkInitialLock() {
        if (this.isLocked) {
            this.lockForm();
            const remaining = Math.ceil((this.lockEndTime - new Date()) / 1000);
            this.startCountdown(remaining);
        }
    }
    
    setupForm() {
        this.form = document.getElementById('loginForm');
        this.emailInput = document.getElementById('Email');
        this.passwordInput = document.getElementById('Password');
        this.submitButton = document.querySelector('button[type="submit"]');
        this.lockoutContainer = document.getElementById('lockoutContainer');
        
        if (!this.form) return;
        
        // Intercept form submission
        this.form.addEventListener('submit', (e) => {
            if (this.isLocked) {
                e.preventDefault();
                this.showLockoutError();
                return false;
            }
        });
        
        // Monitor for server-side login failures
        this.monitorLoginFailures();
    }
    
    monitorLoginFailures() {
        // Check if there's an error message on page load (server-side)
        const errorElements = document.querySelectorAll('.text-danger');
        errorElements.forEach(el => {
            if (el.textContent.includes('Invalid email or password')) {
                this.recordFailedAttempt();
            }
        });
    }
    
    recordFailedAttempt() {
        this.failedAttempts++;
        
        if (this.failedAttempts >= this.MAX_ATTEMPTS) {
            this.startLockout(this.LOCKOUT_SECONDS);
            this.showLockoutMessage();
        } else {
            this.showAttemptWarning();
        }
        
        this.saveToStorage();
    }
    
    startLockout(seconds) {
        this.isLocked = true;
        this.lockEndTime = new Date(Date.now() + (seconds * 1000));
        this.lockForm();
        this.startCountdown(seconds);
        this.saveToStorage();
    }
    
    lockForm() {
        if (this.passwordInput) {
            this.passwordInput.disabled = true;
            this.passwordInput.placeholder = 'Locked - Please wait...';
            this.passwordInput.classList.add('form-control-locked');
        }
        
        if (this.submitButton) {
            this.submitButton.disabled = true;
            this.submitButton.innerHTML = '<i class="fas fa-lock me-2"></i>Locked';
            this.submitButton.classList.add('btn-locked');
        }
        
        // Show lockout container
        if (this.lockoutContainer) {
            this.lockoutContainer.classList.remove('d-none');
        }
    }
    
    unlockForm() {
        this.isLocked = false;
        this.failedAttempts = 0;
        this.lockEndTime = null;
        
        if (this.passwordInput) {
            this.passwordInput.disabled = false;
            this.passwordInput.placeholder = 'Password';
            this.passwordInput.classList.remove('form-control-locked');
        }
        
        if (this.submitButton) {
            this.submitButton.disabled = false;
            this.submitButton.innerHTML = '<i class="fas fa-sign-in-alt me-2"></i>Sign In';
            this.submitButton.classList.remove('btn-locked');
        }
        
        // Hide lockout container
        if (this.lockoutContainer) {
            this.lockoutContainer.classList.add('d-none');
        }
        
        this.saveToStorage();
    }
    
    resetLockout() {
        this.failedAttempts = 0;
        this.isLocked = false;
        this.lockEndTime = null;
        clearInterval(this.timerInterval);
        this.unlockForm();
        localStorage.removeItem(this.STORAGE_KEY);
    }
    
    startCountdown(seconds) {
        clearInterval(this.timerInterval);
        
        let remaining = seconds;
        this.updateCountdownDisplay(remaining);
        
        this.timerInterval = setInterval(() => {
            remaining--;
            
            if (remaining <= 0) {
                clearInterval(this.timerInterval);
                this.unlockForm();
                this.showUnlockMessage();
                return;
            }
            
            this.updateCountdownDisplay(remaining);
        }, 1000);
    }
    
    updateCountdownDisplay(seconds) {
        // Update the countdown timer display
        const timerElement = document.getElementById('countdownTimer');
        if (timerElement) {
            timerElement.textContent = seconds;
        }
        
        // Update progress bar
        const progressElement = document.getElementById('countdownProgress');
        if (progressElement) {
            const percentage = (seconds / this.LOCKOUT_SECONDS) * 100;
            progressElement.style.width = `${percentage}%`;
        }
        
        // Update time text
        const timeElement = document.getElementById('remainingTime');
        if (timeElement) {
            timeElement.textContent = `${seconds} second${seconds !== 1 ? 's' : ''}`;
        }
    }
    
    showAttemptWarning() {
        const attemptsLeft = this.MAX_ATTEMPTS - this.failedAttempts;
        const message = attemptsLeft === 1 
            ? 'Last attempt before lockout!' 
            : `${attemptsLeft} attempts remaining`;
        
        this.showAlert('warning', `
            <div class="d-flex align-items-center">
                <i class="fas fa-exclamation-triangle me-3 fs-4"></i>
                <div>
                    <strong class="d-block">Invalid login attempt</strong>
                    <span class="small">${message}</span>
                </div>
            </div>
        `);
    }
    
    showLockoutMessage() {
        this.showAlert('danger', `
            <div class="d-flex align-items-center">
                <i class="fas fa-lock me-3 fs-4"></i>
                <div>
                    <strong class="d-block">Account Locked</strong>
                    <span class="small">Too many failed attempts. Please wait 30 seconds.</span>
                </div>
            </div>
        `);
    }
    
    showLockoutError() {
        this.showAlert('danger', `
            <div class="d-flex align-items-center">
                <i class="fas fa-ban me-3 fs-4"></i>
                <div>
                    <strong class="d-block">Form Locked</strong>
                    <span class="small">Please wait for the countdown to finish.</span>
                </div>
            </div>
        `);
    }
    
    showUnlockMessage() {
        this.showAlert('success', `
            <div class="d-flex align-items-center">
                <i class="fas fa-unlock me-3 fs-4"></i>
                <div>
                    <strong class="d-block">Account Unlocked</strong>
                    <span class="small">You can now try logging in again.</span>
                </div>
            </div>
        `, 5000);
    }
    
    showAlert(type, content, timeout = 0) {
        // Remove existing alerts
        const existingAlerts = document.querySelectorAll('.security-alert');
        existingAlerts.forEach(alert => alert.remove());
        
        // Create new alert
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} security-alert alert-dismissible fade show`;
        alertDiv.innerHTML = content + `
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        
        // Insert at the top of the form
        const form = document.getElementById('loginForm');
        if (form) {
            form.insertBefore(alertDiv, form.firstChild);
        }
        
        // Auto-remove after timeout
        if (timeout > 0) {
            setTimeout(() => {
                alertDiv.remove();
            }, timeout);
        }
    }
}

// Initialize when page loads
document.addEventListener('DOMContentLoaded', () => {
    window.loginLockout = new LoginLockout();
    
    // If you want to simulate a failed attempt (for testing):
    // window.loginLockout.recordFailedAttempt();
});
