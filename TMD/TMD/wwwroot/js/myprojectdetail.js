// Tab switching
document.querySelectorAll('.pm-tab').forEach(tab => {
    tab.addEventListener('click', function () {
        const targetTab = this.dataset.tab;

        document.querySelectorAll('.pm-tab').forEach(t => t.classList.remove('active'));
        document.querySelectorAll('.pm-tab-content').forEach(c => c.classList.remove('active'));

        this.classList.add('active');
        document.getElementById(targetTab).classList.add('active');
    });
});

function updateTaskStatus(userTaskId, currentStatus) {
    Swal.fire({
        title: 'Cập nhật tiến độ',
        text: 'Chuyển sang trang My Tasks để cập nhật chi tiết hơn?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#6366f1',
        cancelButtonColor: '#64748b',
        confirmButtonText: '<i class="fas fa-arrow-right"></i> Đến My Tasks',
        cancelButtonText: '<i class="fas fa-times"></i> Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = '@Url.Action("MyTasks", "Staff")';
        }
    });
}

// Dark mode support
if (localStorage.getItem('dark-mode') === 'enabled') {
    document.body.classList.add('staff-dark-mode');
}

// ============================================
// LEADER: ADD TASK MODAL
// ============================================
async function openLeaderAddTaskModal() {
    const members = await getProjectMembersForTask();

    const { value: formValues } = await Swal.fire({
        title: 'Tạo Task mới',
        html: `
                    <div style="text-align: left;">
                        <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Tên Task *</label>
                        <input id="swal-taskname" class="swal2-input" style="width: 100%;" placeholder="Nhập tên task">

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">Mô tả</label>
                        <textarea id="swal-desc" class="swal2-textarea" style="width: 100%;" placeholder="Mô tả chi tiết"></textarea>

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">Platform</label>
                        <input id="swal-platform" class="swal2-input" style="width: 100%;" placeholder="Web, Mobile, API...">

                        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-top: 1rem;">
                            <div>
                                <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Deadline</label>
                                <input type="datetime-local" id="swal-deadline" class="swal2-input" style="width: 100%;">
                            </div>
                            <div>
                                <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Độ ưu tiên</label>
                                <select id="swal-priority" class="swal2-input" style="width: 100%;">
                                    <option value="Low">Thấp</option>
                                    <option value="Medium" selected>Trung bình</option>
                                    <option value="High">Cao</option>
                                </select>
                            </div>
                        </div>

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">
                            Giao cho <small class="text-muted">(Giữ Ctrl để chọn nhiều)</small>
                        </label>
                        <select id="swal-assignees" class="swal2-input" multiple style="width: 100%; height: 120px;">
                            ${members}
                        </select>
                        <small style="color: #6b7280;">💡 Chỉ hiển thị members trong dự án</small>
                    </div>
                `,
        width: '600px',
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: '<i class="fas fa-plus"></i> Tạo Task',
        cancelButtonText: '<i class="fas fa-times"></i> Hủy',
        confirmButtonColor: '#10b981',
        cancelButtonColor: '#64748b',
        preConfirm: () => {
            const taskName = document.getElementById('swal-taskname').value.trim();
            if (!taskName) {
                Swal.showValidationMessage('Vui lòng nhập tên task');
                return false;
            }

            const assignees = Array.from(document.getElementById('swal-assignees').selectedOptions)
                .map(opt => parseInt(opt.value));

            const deadlineValue = document.getElementById('swal-deadline').value;

            return {
                taskName,
                description: document.getElementById('swal-desc').value.trim(),
                platform: document.getElementById('swal-platform').value.trim(),
                deadline: deadlineValue ? new Date(deadlineValue).toISOString() : null,
                priority: document.getElementById('swal-priority').value,
                assignedUserIds: assignees
            };
        }
    });

    if (formValues) {
        await leaderCreateTask(formValues);
    }
}

// ============================================
// LEADER: EDIT TASK MODAL
// ============================================
async function openLeaderEditTaskModal(taskId) {
    // Lấy thông tin task hiện tại
    const taskResponse = await fetch(`/Staff/LeaderGetTask?taskId=${taskId}`);
    const taskData = await taskResponse.json();

    if (!taskData.success) {
        Swal.fire('Lỗi', taskData.message, 'error');
        return;
    }

    const task = taskData.task;
    const members = await getProjectMembersForTask();

    const { value: formValues } = await Swal.fire({
        title: 'Sửa Task',
        html: `
                    <div style="text-align: left;">
                        <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Tên Task *</label>
                        <input id="swal-taskname" class="swal2-input" style="width: 100%;" 
                               value="${task.taskName}" placeholder="Nhập tên task">

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">Mô tả</label>
                        <textarea id="swal-desc" class="swal2-textarea" style="width: 100%;" 
                                  placeholder="Mô tả chi tiết">${task.description}</textarea>

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">Platform</label>
                        <input id="swal-platform" class="swal2-input" style="width: 100%;" 
                               value="${task.platform}" placeholder="Web, Mobile, API...">

                        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-top: 1rem;">
                            <div>
                                <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Deadline</label>
                                <input type="datetime-local" id="swal-deadline" class="swal2-input" 
                                       value="${task.deadline}" style="width: 100%;">
                            </div>
                            <div>
                                <label style="display: block; margin-bottom: 0.5rem; font-weight: 600;">Độ ưu tiên</label>
                                <select id="swal-priority" class="swal2-input" style="width: 100%;">
                                    <option value="Low" ${task.priority === 'Low' ? 'selected' : ''}>Thấp</option>
                                    <option value="Medium" ${task.priority === 'Medium' ? 'selected' : ''}>Trung bình</option>
                                    <option value="High" ${task.priority === 'High' ? 'selected' : ''}>Cao</option>
                                </select>
                            </div>
                        </div>

                        <label style="display: block; margin-top: 1rem; margin-bottom: 0.5rem; font-weight: 600;">
                            Giao cho <small class="text-muted">(Giữ Ctrl để chọn nhiều)</small>
                        </label>
                        <select id="swal-assignees" class="swal2-input" multiple style="width: 100%; height: 120px;">
                            ${members}
                        </select>
                    </div>
                `,
        width: '600px',
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: '<i class="fas fa-save"></i> Lưu thay đổi',
        cancelButtonText: '<i class="fas fa-times"></i> Hủy',
        confirmButtonColor: '#f59e0b',
        cancelButtonColor: '#64748b',
        didOpen: () => {
            // Chọn sẵn assigned users
            const selectEl = document.getElementById('swal-assignees');
            task.assignedUserIds.forEach(uid => {
                const option = selectEl.querySelector(`option[value="${uid}"]`);
                if (option) option.selected = true;
            });
        },
        preConfirm: () => {
            const taskName = document.getElementById('swal-taskname').value.trim();
            if (!taskName) {
                Swal.showValidationMessage('Vui lòng nhập tên task');
                return false;
            }

            const assignees = Array.from(document.getElementById('swal-assignees').selectedOptions)
                .map(opt =></parameter >
                    <parameter name="new_str">                    const assignees = Array.from(document.getElementById('swal-assignees').selectedOptions)
        .map(opt => parseInt(opt.value));
                        const deadlineValue = document.getElementById('swal-deadline').value;

                        return {
                            taskId: taskId,
                        taskName,
                        description: document.getElementById('swal-desc').value.trim(),
                        platform: document.getElementById('swal-platform').value.trim(),
                        deadline: deadlineValue ? new Date(deadlineValue).toISOString() : null,
                        priority: document.getElementById('swal-priority').value,
                        assignedUserIds: assignees
                        };
                    }
                });

                        if (formValues) {
                            await leaderUpdateTask(formValues);
                }
            }

                        // ============================================
                        // LEADER: DELETE TASK
                        // ============================================
                        async function leaderDeleteTask(taskId, taskName) {
                const result = await Swal.fire({
                            title: 'Xác nhận xóa',
                        html: `Bạn có chắc muốn xóa task:<br><strong>"${taskName}"</strong>?<br><br><small style="color: #ef4444;">⚠️ Hành động này không thể hoàn tác!</small>`,
                            icon: 'warning',
                            showCancelButton: true,
                            confirmButtonColor: '#ef4444',
                            cancelButtonColor: '#64748b',
                            confirmButtonText: '<i class="fas fa-trash"></i> Xóa task',
                            cancelButtonText: '<i class="fas fa-times"></i> Hủy'
                });

                            if (!result.isConfirmed) return;

                            Swal.fire({
                                title: 'Đang xóa...',
                            html: '<i class="fas fa-spinner fa-spin"></i> Vui lòng đợi',
                            allowOutsideClick: false,
                            showConfirmButton: false
                });

                            try {
                    const response = await fetch('/Staff/LeaderDeleteTask', {
                                method: 'POST',
                            headers: {'Content-Type': 'application/json' },
                            body: JSON.stringify({taskId})
                    });

                            const result = await response.json();

                            if (result.success) {
                                await Swal.fire({
                                    icon: 'success',
                                    title: 'Thành công!',
                                    html: result.message,
                                    confirmButtonColor: '#10b981',
                                    timer: 2000
                                });
                            location.reload();
                    } else {
                                Swal.fire({
                                    icon: 'error',
                                    title: 'Lỗi!',
                                    text: result.message,
                                    confirmButtonColor: '#ef4444'
                                });
                    }
                } catch (error) {
                                Swal.fire({
                                    icon: 'error',
                                    title: 'Lỗi!',
                                    text: 'Có lỗi xảy ra: ' + error.message,
                                    confirmButtonColor: '#ef4444'
                                });
                }
            }

                            async function getProjectMembersForTask() {
                try {
                    const projectId = @ViewBag.Project.ProjectId;
                            const activeMembers = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(
                            ViewBag.ActiveMembers?.Cast<dynamic>()
                    .Where(m => m.Member.UserId != ViewBag.Project.LeaderId)
                    .Select(m => new
                                {
                                    userId = m.Member.UserId,
                                    fullName = m.Member.User.FullName,
                                    departmentName = m.Member.User?.Department?.DepartmentName ?? "N/A"
                                })
                                .ToList() ?? new List<object>()
                                    ));

                                    return activeMembers
                        .map(m => `<option value="${m.userId}">${m.fullName} (${m.departmentName})</option>`)
                                    .join('');
                } catch (error) {
                                        console.error('Error getting members:', error);
                                    return '<option value="">Không có thành viên nào</option>';
                }
            }

                                    async function leaderCreateTask(data) {
                const projectId = @ViewBag.Project.ProjectId;

                                    Swal.fire({
                                        title: 'Đang tạo task...',
                                    html: '<i class="fas fa-spinner fa-spin"></i> Vui lòng đợi',
                                    allowOutsideClick: false,
                                    showConfirmButton: false
                });

                                    try {
                    const response = await fetch('/Staff/LeaderCreateTask', {
                                        method: 'POST',
                                    headers: {'Content-Type': 'application/json' },
                                    body: JSON.stringify({
                                        ...data,
                                        projectId: projectId
                        })
                    });

                                    const result = await response.json();

                                    if (result.success) {
                                        await Swal.fire({
                                            icon: 'success',
                                            title: 'Thành công!',
                                            html: result.message,
                                            confirmButtonColor: '#10b981',
                                            timer: 2000
                                        });
                                    location.reload();
                    } else {
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'Lỗi!',
                                            text: result.message,
                                            confirmButtonColor: '#ef4444'
                                        });
                    }
                } catch (error) {
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'Lỗi!',
                                            text: 'Có lỗi xảy ra: ' + error.message,
                                            confirmButtonColor: '#ef4444'
                                        });
                }
            }

                                    async function leaderUpdateTask(data) {
                                        Swal.fire({
                                            title: 'Đang cập nhật...',
                                            html: '<i class="fas fa-spinner fa-spin"></i> Vui lòng đợi',
                                            allowOutsideClick: false,
                                            showConfirmButton: false
                                        });

                                    try {
                    const response = await fetch('/Staff/LeaderUpdateTask', {
                                        method: 'POST',
                                    headers: {'Content-Type': 'application/json' },
                                    body: JSON.stringify(data)
                    });

                                    const result = await response.json();

                                    if (result.success) {
                                        await Swal.fire({
                                            icon: 'success',
                                            title: 'Thành công!',
                                            html: result.message,
                                            confirmButtonColor: '#10b981',
                                            timer: 2000
                                        });
                                    location.reload();
                    } else {
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'Lỗi!',
                                            text: result.message,
                                            confirmButtonColor: '#ef4444'
                                        });
                    }
                } catch (error) {
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'Lỗi!',
                                            text: 'Có lỗi xảy ra: ' + error.message,
                                            confirmButtonColor: '#ef4444'
                                        });
                }
            }