/* =====================================================
   PROJECT MANAGEMENT - JavaScript
   Common functions for Project CRUD operations
   ===================================================== */

// ========== CONFIGURATION ==========
const PM_CONFIG = {
    debounceDelay: 300,
    maxFileSize: 10 * 1024 * 1024, // 10MB
    allowedImageTypes: ['image/jpeg', 'image/png', 'image/jpg'],
    sweetAlertConfig: {
        confirmButtonColor: '#6366f1',
        cancelButtonColor: '#64748b',
        showClass: {
            popup: 'animate__animated animate__fadeInDown'
        },
        hideClass: {
            popup: 'animate__animated animate__fadeOutUp'
        }
    }
};

// ========== UTILITY FUNCTIONS ==========

/**
 * Show loading state
 */
function showLoading(message = 'Đang xử lý...') {
    Swal.fire({
        title: message,
        allowOutsideClick: false,
        allowEscapeKey: false,
        showConfirmButton: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });
}

/**
 * Hide loading state
 */
function hideLoading() {
    Swal.close();
}

/**
 * Show success message
 */
function showSuccess(title, message, callback = null) {
    Swal.fire({
        icon: 'success',
        title: title,
        text: message,
        confirmButtonColor: '#10b981',
        timer: 2000,
        showConfirmButton: false
    }).then(() => {
        if (callback && typeof callback === 'function') {
            callback();
        }
    });
}

/**
 * Show error message
 */
function showError(title, message) {
    Swal.fire({
        icon: 'error',
        title: title,
        text: message,
        confirmButtonColor: '#ef4444'
    });
}

/**
 * Show warning message
 */
function showWarning(title, message) {
    Swal.fire({
        icon: 'warning',
        title: title,
        text: message,
        confirmButtonColor: '#f59e0b'
    });
}

/**
 * Confirm dialog
 */
function showConfirm(title, message, confirmText = 'Xác nhận', cancelText = 'Hủy') {
    return Swal.fire({
        title: title,
        html: message,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#6366f1',
        cancelButtonColor: '#64748b',
        confirmButtonText: `<i class="fas fa-check"></i> ${confirmText}`,
        cancelButtonText: `<i class="fas fa-times"></i> ${cancelText}`
    });
}

/**
 * Format date to dd/MM/yyyy
 */
function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    return `${day}/${month}/${year}`;
}

/**
 * Format date to yyyy-MM-dd (for input[type="date"])
 */
function formatDateForInput(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

/**
 * Format currency (VND)
 */
function formatCurrency(amount) {
    if (!amount || amount === 0) return '0 VNĐ';
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(amount);
}

/**
 * Debounce function
 */
function debounce(func, delay = PM_CONFIG.debounceDelay) {
    let timeoutId;
    return function (...args) {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => func.apply(this, args), delay);
    };
}

/**
 * Validate email format
 */
function isValidEmail(email) {
    const regex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return regex.test(email);
}

/**
 * Validate date range
 */
function isValidDateRange(startDate, endDate) {
    if (!startDate || !endDate) return true;
    return new Date(endDate) >= new Date(startDate);
}

/**
 * Calculate days between dates
 */
function daysBetween(startDate, endDate) {
    const start = new Date(startDate);
    const end = new Date(endDate);
    const diffTime = Math.abs(end - start);
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
}

// ========== PROJECT OPERATIONS ==========

/**
 * Fetch project list
 */
async function fetchProjects(filters = {}) {
    try {
        const params = new URLSearchParams(filters);
        const response = await fetch(`/Admin/GetProjects?${params}`);

        if (!response.ok) {
            throw new Error('Failed to fetch projects');
        }

        return await response.json();
    } catch (error) {
        console.error('Error fetching projects:', error);
        showError('Lỗi', 'Không thể tải danh sách dự án');
        return { success: false, projects: [] };
    }
}

/**
 * Fetch project details
 */
async function fetchProjectDetail(projectId) {
    try {
        const response = await fetch(`/Admin/GetProjectDetail/${projectId}`);

        if (!response.ok) {
            throw new Error('Failed to fetch project details');
        }

        return await response.json();
    } catch (error) {
        console.error('Error fetching project detail:', error);
        showError('Lỗi', 'Không thể tải thông tin dự án');
        return { success: false };
    }
}

/**
 * Create new project
 */
async function createProject(projectData) {
    try {
        showLoading('Đang tạo dự án...');

        const response = await fetch('/Admin/CreateProjectPost', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(projectData)
        });

        const result = await response.json();
        hideLoading();

        if (result.success) {
            showSuccess('Thành công!', result.message, () => {
                window.location.href = '/Admin/ProjectList';
            });
        } else {
            showError('Lỗi', result.message);
        }

        return result;
    } catch (error) {
        hideLoading();
        console.error('Error creating project:', error);
        showError('Lỗi', 'Có lỗi xảy ra khi tạo dự án');
        return { success: false };
    }
}

/**
 * Update project
 */
async function updateProject(projectData) {
    try {
        showLoading('Đang cập nhật...');

        const response = await fetch('/Admin/UpdateProject', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(projectData)
        });

        const result = await response.json();
        hideLoading();

        if (result.success) {
            showSuccess('Thành công!', result.message, () => {
                window.location.href = `/Admin/ProjectDetail/${projectData.projectId}`;
            });
        } else {
            showError('Lỗi', result.message);
        }

        return result;
    } catch (error) {
        hideLoading();
        console.error('Error updating project:', error);
        showError('Lỗi', 'Có lỗi xảy ra khi cập nhật dự án');
        return { success: false };
    }
}

/**
 * Delete project with confirmation
 */
async function deleteProjectWithConfirm(projectId, projectName) {
    const result = await showConfirm(
        'Xác nhận xóa dự án',
        `<p>Bạn có chắc muốn xóa dự án <strong>${projectName}</strong>?</p>` +
        `<p class="text-muted" style="font-size: 0.875rem; margin-top: 0.5rem;">` +
        `⚠️ Hành động này không thể hoàn tác!</p>`,
        'Xóa dự án',
        'Hủy'
    );

    if (result.isConfirmed) {
        try {
            showLoading('Đang xóa dự án...');

            const response = await fetch('/Admin/DeleteProject', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ projectId })
            });

            const data = await response.json();
            hideLoading();

            if (data.success) {
                showSuccess('Đã xóa!', data.message, () => {
                    location.reload();
                });
            } else {
                showError('Lỗi', data.message);
            }
        } catch (error) {
            hideLoading();
            console.error('Error deleting project:', error);
            showError('Lỗi', 'Có lỗi xảy ra khi xóa dự án');
        }
    }
}

// ========== MEMBER MANAGEMENT ==========

/**
 * Add member to project
 */
async function addMemberToProject(projectId, userId, role = 'Member') {
    try {
        const response = await fetch('/Admin/ManageProjectMember', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                projectId,
                userId,
                action: 'Add',
                role
            })
        });

        return await response.json();
    } catch (error) {
        console.error('Error adding member:', error);
        return { success: false, message: 'Có lỗi xảy ra' };
    }
}

/**
 * Remove member from project
 */
async function removeMemberFromProject(projectId, userId) {
    try {
        const response = await fetch('/Admin/ManageProjectMember', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                projectId,
                userId,
                action: 'Remove'
            })
        });

        return await response.json();
    } catch (error) {
        console.error('Error removing member:', error);
        return { success: false, message: 'Có lỗi xảy ra' };
    }
}

// ========== SEARCH & FILTER ==========

/**
 * Setup search functionality
 */
function setupSearch(inputSelector, itemsSelector, searchFields = ['name', 'dept']) {
    const searchInput = document.querySelector(inputSelector);
    if (!searchInput) return;

    const debouncedSearch = debounce((searchTerm) => {
        const items = document.querySelectorAll(itemsSelector);
        searchTerm = searchTerm.toLowerCase();

        items.forEach(item => {
            let matches = false;

            searchFields.forEach(field => {
                const value = item.dataset[field];
                if (value && value.toLowerCase().includes(searchTerm)) {
                    matches = true;
                }
            });

            item.style.display = matches ? '' : 'none';
        });
    });

    searchInput.addEventListener('input', (e) => {
        debouncedSearch(e.target.value);
    });
}

/**
 * Setup member selection count
 */
function setupMemberCounter(checkboxSelector, counterSelector) {
    const checkboxes = document.querySelectorAll(checkboxSelector);
    const counter = document.querySelector(counterSelector);

    if (!counter) return;

    function updateCount() {
        const count = document.querySelectorAll(`${checkboxSelector}:checked`).length;
        counter.textContent = count;
    }

    checkboxes.forEach(checkbox => {
        checkbox.addEventListener('change', updateCount);
    });

    updateCount(); // Initial count
}

// ========== FORM VALIDATION ==========

/**
 * Validate project form
 */
function validateProjectForm(formData) {
    const errors = [];

    // Required fields
    if (!formData.projectName || formData.projectName.trim() === '') {
        errors.push('Tên dự án không được để trống');
    }

    // Date validation
    if (formData.startDate && formData.endDate) {
        if (!isValidDateRange(formData.startDate, formData.endDate)) {
            errors.push('Ngày kết thúc phải sau ngày bắt đầu');
        }
    }

    // Budget validation
    if (formData.budget && formData.budget < 0) {
        errors.push('Ngân sách không hợp lệ');
    }

    return {
        isValid: errors.length === 0,
        errors: errors
    };
}

/**
 * Show validation errors
 */
function showValidationErrors(errors) {
    const errorList = errors.map(error => `<li>${error}</li>`).join('');

    Swal.fire({
        icon: 'warning',
        title: 'Dữ liệu không hợp lệ',
        html: `<ul style="text-align: left; margin-left: 1.5rem;">${errorList}</ul>`,
        confirmButtonColor: '#f59e0b'
    });
}

// ========== DARK MODE ==========

/**
 * Toggle dark mode
 */
function toggleDarkMode() {
    const body = document.body;
    const isDarkMode = body.classList.contains('staff-dark-mode');

    if (isDarkMode) {
        body.classList.remove('staff-dark-mode');
        localStorage.setItem('dark-mode', 'disabled');
    } else {
        body.classList.add('staff-dark-mode');
        localStorage.setItem('dark-mode', 'enabled');
    }
}

/**
 * Apply dark mode on page load
 */
function applyDarkMode() {
    if (localStorage.getItem('dark-mode') === 'enabled') {
        document.body.classList.add('staff-dark-mode');
    }
}

// ========== INITIALIZATION ==========

/**
 * Initialize project management features
 */
function initProjectManagement() {
    // Apply dark mode
    applyDarkMode();

    // Setup search if exists
    setupSearch('#memberSearch', '.member-checkbox-item', ['name', 'dept']);

    // Setup member counter
    setupMemberCounter('.member-checkbox', '#selectedCount');

    // Auto-select leader as member
    const leaderSelect = document.getElementById('leaderId');
    if (leaderSelect) {
        leaderSelect.addEventListener('change', function () {
            const leaderId = this.value;
            if (leaderId) {
                const checkbox = document.querySelector(`.member-checkbox[value="${leaderId}"]`);
                if (checkbox && !checkbox.checked) {
                    checkbox.checked = true;
                    // Trigger change event to update counter
                    checkbox.dispatchEvent(new Event('change'));
                }
            }
        });
    }

    console.log('✅ Project Management initialized');
}

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initProjectManagement);
} else {
    initProjectManagement();
}

// ========== EXPORT FOR GLOBAL USE ==========
window.PM = {
    // Utilities
    showLoading,
    hideLoading,
    showSuccess,
    showError,
    showWarning,
    showConfirm,
    formatDate,
    formatDateForInput,
    formatCurrency,
    debounce,

    // Project operations
    fetchProjects,
    fetchProjectDetail,
    createProject,
    updateProject,
    deleteProjectWithConfirm,

    // Member management
    addMemberToProject,
    removeMemberFromProject,

    // Validation
    validateProjectForm,
    showValidationErrors,

    // Dark mode
    toggleDarkMode,
    applyDarkMode,

    // Config
    config: PM_CONFIG
};

console.log('📦 Project Management JS loaded');