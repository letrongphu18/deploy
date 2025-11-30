// ===== VERIFY OTP PAGE - JAVASCRIPT =====

// Timer for OTP expiry (5 minutes = 300 seconds)
let otpExpirySeconds = 300;
let resendCooldownSeconds = 120;
let timerInterval;
let resendInterval;

// Start OTP expiry timer
function startExpiryTimer() {
    const timerElement = document.getElementById('timer');
    timerInterval = setInterval(() => {
        otpExpirySeconds--;

        const minutes = Math.floor(otpExpirySeconds / 60);
        const seconds = otpExpirySeconds % 60;
        timerElement.textContent =
            `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

        if (otpExpirySeconds <= 60) {
            timerElement.classList.add('votp-warning');
        }

        if (otpExpirySeconds <= 0) {
            clearInterval(timerInterval);
            Swal.fire({
                icon: 'warning',
                title: 'Hết thời gian!',
                text: 'Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới',
                confirmButtonColor: '#7A3F30'
            }).then(() => {
                window.location.href = '/Account/ForgotPassword';
            });
        }
    }, 1000);
}

// Start resend cooldown timer
function startResendCooldown() {
    resendCooldownSeconds = 120;
    const btnResend = document.getElementById('btnResend');
    const resendText = document.getElementById('resendText');
    btnResend.disabled = true;

    resendInterval = setInterval(() => {
        resendCooldownSeconds--;
        resendText.textContent = `Gửi lại mã (${resendCooldownSeconds}s)`;

        if (resendCooldownSeconds <= 0) {
            clearInterval(resendInterval);
            btnResend.disabled = false;
            resendText.textContent = 'Gửi lại mã';
        }
    }, 1000);
}

// Auto-focus next input
const otpInputs = document.querySelectorAll('.votp-input');
otpInputs.forEach((input, index) => {
    input.addEventListener('input', function (e) {
        if (this.value.length === 1 && index < otpInputs.length - 1) {
            otpInputs[index + 1].focus();
        }
    });

    input.addEventListener('keydown', function (e) {
        if (e.key === 'Backspace' && this.value === '' && index > 0) {
            otpInputs[index - 1].focus();
        }
    });

    // Only allow numbers
    input.addEventListener('keypress', function (e) {
        if (!/[0-9]/.test(e.key)) {
            e.preventDefault();
        }
    });

    // Paste support
    input.addEventListener('paste', function (e) {
        e.preventDefault();
        const pastedData = e.clipboardData.getData('text').replace(/\D/g, '');
        if (pastedData.length === 6) {
            otpInputs.forEach((inp, idx) => {
                inp.value = pastedData[idx] || '';
            });
            otpInputs[5].focus();
        }
    });
});

// Focus first input on load
otpInputs[0].focus();

// Start timers
startExpiryTimer();
startResendCooldown();

// Handle form submission
document.getElementById('verifyOtpForm').addEventListener('submit', async function (e) {
    e.preventDefault();

    const email = document.getElementById('email').value;
    const otp = Array.from(otpInputs).map(input => input.value).join('');

    if (otp.length !== 6) {
        Swal.fire({
            icon: 'warning',
            title: 'Chú ý!',
            text: 'Vui lòng nhập đầy đủ 6 số',
            confirmButtonColor: '#7A3F30'
        });
        return;
    }

    const btn = document.getElementById('btnVerify');
    const originalText = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner votp-spinner"></i>Đang xác thực...';

    try {
        const response = await fetch('/Account/VerifyOtpJson', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                email: email,
                otpCode: otp
            })
        });

        const result = await response.json();

        if (result.success) {
            clearInterval(timerInterval);
            clearInterval(resendInterval);

            await Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: result.message,
                confirmButtonColor: '#7A3F30',
                timer: 1500
            });

            window.location.href = `/Account/ResetPassword?email=${encodeURIComponent(email)}&otp=${otp}`;
        } else {
            Swal.fire({
                icon: 'error',
                title: 'Lỗi!',
                text: result.message,
                confirmButtonColor: '#7A3F30'
            });

            // Clear OTP inputs
            otpInputs.forEach(input => input.value = '');
            otpInputs[0].focus();

            btn.disabled = false;
            btn.innerHTML = originalText;
        }
    } catch (error) {
        Swal.fire({
            icon: 'error',
            title: 'Lỗi!',
            text: 'Có lỗi xảy ra. Vui lòng thử lại',
            confirmButtonColor: '#7A3F30'
        });

        btn.disabled = false;
        btn.innerHTML = originalText;
    }
});

// Handle resend OTP
document.getElementById('btnResend').addEventListener('click', async function () {
    const email = document.getElementById('email').value;
    const btn = this;
    const originalText = btn.innerHTML;

    btn.disabled = true;
    btn.innerHTML = '<i class="fas fa-spinner votp-spinner"></i><span>Đang gửi...</span>';

    try {
        const response = await fetch('/Account/ResendOtpJson', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email: email })
        });

        const result = await response.json();

        if (result.success) {
            // Reset timers
            clearInterval(timerInterval);
            otpExpirySeconds = 300;
            document.getElementById('timer').classList.remove('votp-warning');
            startExpiryTimer();
            startResendCooldown();

            // Clear OTP inputs
            otpInputs.forEach(input => input.value = '');
            otpInputs[0].focus();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: result.message,
                confirmButtonColor: '#7A3F30',
                timer: 2000
            });
        } else {
            Swal.fire({
                icon: 'error',
                title: 'Lỗi!',
                text: result.message,
                confirmButtonColor: '#7A3F30'
            });

            btn.disabled = false;
            btn.innerHTML = originalText;
        }
    } catch (error) {
        Swal.fire({
            icon: 'error',
            title: 'Lỗi!',
            text: 'Có lỗi xảy ra. Vui lòng thử lại',
            confirmButtonColor: '#7A3F30'
        });

        btn.disabled = false;
        btn.innerHTML = originalText;
    }
});