// ============================================
// PERSONAL TASK MANAGEMENT - JAVASCRIPT
// Drag & Drop Kanban Board
// ============================================

let draggedElement = null;
let currentEditId = 0;

// ============================================
// DRAG & DROP FUNCTIONALITY
// ============================================
document.addEventListener('DOMContentLoaded', function () {
    initializeDragAndDrop();
    initializeCalendar();
});

function initializeDragAndDrop() {
    const taskCards = document.querySelectorAll('.pt-task-card');
    const columns = document.querySelectorAll('.pt-column-body');

    // Make task cards draggable
    taskCards.forEach(card => {
        card.addEventListener('dragstart', handleDragStart);
        card.addEventListener('dragend', handleDragEnd);
    });

    // Make columns droppable
    columns.forEach(column => {
        column.addEventListener('dragover', handleDragOver);
        column.addEventListener('drop', handleDrop);
        column.addEventListener('dragleave', handleDragLeave);
    });
}

function handleDragStart(e) {
    draggedElement = this;
    this.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/html', this.innerHTML);
}

function handleDragEnd(e) {
    this.classList.remove('dragging');

    // Remove drag-over class from all columns
    document.querySelectorAll('.pt-column-body').forEach(col => {
        col.classList.remove('drag-over');
    });
}

function handleDragOver(e) {
    if (e.preventDefault) {
        e.preventDefault();
    }
    e.dataTransfer.dropEffect = 'move';
    this.classList.add('drag-over');
    return false;
}

function handleDragLeave(e) {
    this.classList.remove('drag-over');
}

function handleDrop(e) {
    if (e.stopPropagation) {
        e.stopPropagation();
    }

    this.classList.remove('drag-over');

    if (draggedElement !== this) {
        const newStatus = this.closest('.pt-kanban-column').dataset.status;
        const taskId = draggedElement.dataset.id;

        // Move card to new column
        this.appendChild(draggedElement);

        // Update status in database
        updateTaskStatusAjax(taskId, newStatus);
    }

    return false;
}

// ============================================
// UPDATE TASK STATUS
// ============================================
function updateTaskStatusAjax(userTaskId, newStatus) {
    fetch('/PersonalTask/UpdateTaskStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({
            userTaskId: parseInt(userTaskId),
            newStatus: newStatus
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('success', data.message || 'Cập nhật trạng thái thành công!');

                // Update column counts
                updateColumnCounts();
            } else {
                showToast('error', data.message || 'Có lỗi xảy ra!');
                // Reload page to restore correct state
                setTimeout(() => location.reload(), 1500);
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi cập nhật!');
            setTimeout(() => location.reload(), 1500);
        });
}

function updateTaskStatus(selectElement) {
    const userTaskId = selectElement.dataset.id;
    const newStatus = selectElement.value;
    updateTaskStatusAjax(userTaskId, newStatus);
}

function updateColumnCounts() {
    const columns = document.querySelectorAll('.pt-kanban-column');
    columns.forEach(column => {
        const status = column.dataset.status;
        const count = column.querySelectorAll('.pt-task-card').length;
        const countElement = column.querySelector('.pt-column-count');
        if (countElement) {
            countElement.textContent = count;
        }
    });
}

// ============================================
// MODAL FUNCTIONS
// ============================================
function showCreateModal() {
    currentEditId = 0;
    document.getElementById('modalTitle').innerHTML = '<i class="fas fa-plus-circle"></i> Tạo Công Việc Mới';
    document.getElementById('taskForm').reset();
    document.getElementById('userTaskId').value = '0';
    document.getElementById('taskModal').classList.add('open');
}

function showEditModal(userTaskId) {
    currentEditId = userTaskId;
    document.getElementById('modalTitle').innerHTML = '<i class="fas fa-edit"></i> Chỉnh Sửa Công Việc';

    // Fetch task details
    fetch(`/PersonalTask/GetTaskDetail?userTaskId=${userTaskId}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const task = data.task;
                document.getElementById('userTaskId').value = task.userTaskId;
                document.getElementById('taskName').value = task.taskName;
                document.getElementById('description').value = task.description || '';
                document.getElementById('platform').value = task.platform || '';
                document.getElementById('priority').value = task.priority || 'Medium';
                document.getElementById('deadline').value = task.deadline || '';

                document.getElementById('taskModal').classList.add('open');
            } else {
                showToast('error', data.message || 'Không tìm thấy công việc!');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra!');
        });
}

function closeModal() {
    document.getElementById('taskModal').classList.remove('open');
    document.getElementById('taskForm').reset();
    currentEditId = 0;
}

// Close modal when clicking outside
document.addEventListener('click', function (e) {
    const modal = document.getElementById('taskModal');
    if (e.target === modal) {
        closeModal();
    }
});

// ============================================
// FORM SUBMISSION
// ============================================
document.getElementById('taskForm')?.addEventListener('submit', function (e) {
    e.preventDefault();

    const userTaskId = parseInt(document.getElementById('userTaskId').value);
    const isEdit = userTaskId > 0;

    const formData = {
        taskName: document.getElementById('taskName').value.trim(),
        description: document.getElementById('description').value.trim() || null,
        platform: document.getElementById('platform').value.trim() || null,
        priority: document.getElementById('priority').value,
        deadline: document.getElementById('deadline').value || null
    };

    if (isEdit) {
        formData.userTaskId = userTaskId;
    }

    const url = isEdit ? '/PersonalTask/UpdateTask' : '/PersonalTask/CreateTask';
    const submitBtn = document.getElementById('submitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang xử lý...';

    fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(formData)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('success', data.message || (isEdit ? 'Cập nhật thành công!' : 'Tạo công việc thành công!'));
                closeModal();

                // Reload page after short delay
                setTimeout(() => {
                    location.reload();
                }, 1000);
            } else {
                showToast('error', data.message || 'Có lỗi xảy ra!');
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-save"></i> Lưu Công Việc';
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi lưu!');
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fas fa-save"></i> Lưu Công Việc';
        });
});

// ============================================
// DELETE TASK
// ============================================
function deleteTask(userTaskId) {
    if (!confirm('Bạn có chắc muốn xóa công việc này?')) {
        return;
    }

    fetch('/PersonalTask/DeleteTask', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ userTaskId: parseInt(userTaskId) })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showToast('success', data.message || 'Xóa thành công!');

                // Remove card from DOM
                const card = document.querySelector(`.pt-task-card[data-id="${userTaskId}"]`);
                if (card) {
                    card.style.transition = 'all 0.3s ease';
                    card.style.opacity = '0';
                    card.style.transform = 'scale(0.8)';
                    setTimeout(() => {
                        card.remove();
                        updateColumnCounts();
                    }, 300);
                }
            } else {
                showToast('error', data.message || 'Có lỗi xảy ra!');
            }
        })
        .catch(error => {
            console.error('Error:', error);
            showToast('error', 'Có lỗi xảy ra khi xóa!');
        });
}

// ============================================
// CALENDAR INITIALIZATION
// ============================================
function initializeCalendar() {
    const calendarEl = document.getElementById('calendar');
    if (!calendarEl) return;

    const calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,timeGridDay'
        },
        events: function (info, successCallback, failureCallback) {
            fetch('/PersonalTask/GetCalendarTasks')
                .then(response => response.json())
                .then(data => {
                    successCallback(data);
                })
                .catch(error => {
                    console.error('Error loading calendar:', error);
                    failureCallback(error);
                });
        },
        eventClick: function (info) {
            showEditModal(info.event.id);
        },
        height: 'auto',
        locale: 'vi'
    });

    calendar.render();
}

// ============================================
// TOAST NOTIFICATIONS
// ============================================
// ============================================
// TOAST NOTIFICATIONS
// ============================================
function showToast(type, message) {
    // Remove existing toasts
    const existingToasts = document.querySelectorAll('.pt-toast');
    existingToasts.forEach(toast => toast.remove());

    const toast = document.createElement('div');
    toast.className = `pt-toast pt-toast-${type}`;

    const icon = type === 'success' ? 'fa-check-circle' :
        type === 'error' ? 'fa-exclamation-circle' :
            'fa-info-circle';

    toast.innerHTML = `
        <i class="fas ${icon}"></i>
        <span>${message}</span>
    `;

    document.body.appendChild(toast);

    // Trigger animation
    setTimeout(() => toast.classList.add('show'), 10);

    // Auto remove after 3 seconds
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// ============================================
// FILTER TASKS IN LIST VIEW
// ============================================
function filterTasks(status) {
    const items = document.querySelectorAll('.pt-list-item');
    const filterBtns = document.querySelectorAll('.pt-filter-btn');

    // Update active button
    filterBtns.forEach(btn => {
        if (btn.dataset.filter === status) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Filter items
    items.forEach(item => {
        if (status === 'all' || item.dataset.status === status) {
            item.classList.remove('hidden');
        } else {
            item.classList.add('hidden');
        }
    });
}

// Add toast styles dynamically
const toastStyles = document.createElement('style');
toastStyles.textContent = `
.pt-toast {
    position: fixed;
    top: 20px;
    right: 20px;
    padding: 1rem 1.5rem;
    border-radius: 0.75rem;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    display: flex;
    align-items: center;
    gap: 0.75rem;
    font-weight: 600;
    z-index: 10000;
    opacity: 0;
    transform: translateX(100px);
    transition: all 0.3s ease;
}

.pt-toast.show {
    opacity: 1;
    transform: translateX(0);
}

.pt-toast-success {
    background: #10b981;
    color: white;
}

.pt-toast-error {
    background: #ef4444;
    color: white;
}

.pt-toast-info {
    background: #3b82f6;
    color: white;
}

.pt-toast i {
    font-size: 1.25rem;
}
`;
document.head.appendChild(toastStyles);