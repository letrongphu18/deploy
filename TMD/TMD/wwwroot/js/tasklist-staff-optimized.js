// =====================================================
// TASKLIST STAFF - OPTIMIZED SCRIPT
// Lọc real-time và sắp xếp thông minh
// =====================================================

let userPermissions = null;
let allTesters = [];
let allTasks = []; // Lưu trữ tất cả tasks để sắp xếp

document.addEventListener('DOMContentLoaded', async () => {
	await loadPermissions();
	initializeTasks();
	setupFilters();
	setupModalClose();
});

// ========== KHỞI TẠO VÀ SẮP XẾP TASKS ==========
function initializeTasks()
{
	const taskContainer = document.getElementById('taskList');
	if (!taskContainer) return;

	const taskElements = Array.from(taskContainer.querySelectorAll('.task-item'));

	// Lưu trữ thông tin tasks
	allTasks = taskElements.map(el => {
		const deadlineStr = el.querySelector('[data-deadline]')?.dataset.deadline || '';
		const createdAtStr = el.querySelector('[data-created]')?.dataset.created || '';

		return {
		element: el,
            id: el.dataset.id,
            status: el.dataset.status,
            priority: el.dataset.priority,
            name: el.dataset.name,
            deadline: deadlineStr ? new Date(deadlineStr) : null,
            createdAt: createdAtStr ? new Date(createdAtStr) : new Date(),
            isOverdue: el.querySelector('.badge-overdue') !== null

		};
	});

	// Sắp xếp và hiển thị
	sortAndDisplayTasks();
	calculateStats();
}

// ========== SẮP XẾP THÔNG MINH ==========
function sortAndDisplayTasks()
{
	const now = new Date();
	const threeDaysLater = new Date(now.getTime() + (3 * 24 * 60 * 60 * 1000));

	allTasks.sort((a, b) => {
	// 1. Ưu tiên: Tasks QUÁ HẠN lên đầu
	if (a.isOverdue && !b.isOverdue) return -1;
	if (!a.isOverdue && b.isOverdue) return 1;

	// 2. Ưu tiên: Tasks SẮP ĐẾN HẠN (trong 3 ngày) lên trước
	const aIsUrgent = a.deadline && a.deadline <= threeDaysLater && a.deadline > now;
	const bIsUrgent = b.deadline && b.deadline <= threeDaysLater && b.deadline > now;

	if (aIsUrgent && !bIsUrgent) return -1;
	if (!aIsUrgent && bIsUrgent) return 1;

	// 3. Ưu tiên: Tasks MỚI NHẤT (vừa được giao) lên trước
	if (a.createdAt && b.createdAt)
	{
		const timeDiff = b.createdAt - a.createdAt;
		if (Math.abs(timeDiff) > 24 * 60 * 60 * 1000)
		{ // Chênh lệch > 1 ngày
			return timeDiff;
		}
	}

	// 4. Ưu tiên theo Status (Reopen > InProgress > TODO > Testing > Done)
	const statusPriority = {
			'Reopen': 1,
            'InProgress': 2,
            'TODO': 3,
            'Testing': 4,
            'Done': 5
        };
const aStatusPrio = statusPriority[a.status] || 999;
const bStatusPrio = statusPriority[b.status] || 999;
if (aStatusPrio !== bStatusPrio) return aStatusPrio - bStatusPrio;

// 5. Ưu tiên theo Priority (High > Medium > Low)
const priorityValue = {
			'High': 1,
            'Medium': 2,
            'Low': 3
        };
const aPrio = priorityValue[a.priority] || 999;
const bPrio = priorityValue[b.priority] || 999;
if (aPrio !== bPrio) return aPrio - bPrio;

// 6. Cuối cùng: Sắp xếp theo deadline (gần nhất trước)
if (a.deadline && b.deadline)
{
	return a.deadline - b.deadline;
}
if (a.deadline) return -1;
if (b.deadline) return 1;

return 0;
    });

// Hiển thị lại tasks theo thứ tự đã sắp xếp
const taskContainer = document.getElementById('taskList');
if (taskContainer)
{
	taskContainer.innerHTML = '';
	allTasks.forEach(task => {
		taskContainer.appendChild(task.element);
	});
}
}

// ========== BỘ LỌC REAL-TIME ==========
function setupFilters()
{
	const filterStatus = document.getElementById('filterStatus');
	const filterPriority = document.getElementById('filterPriority');
	const searchTask = document.getElementById('searchTask');

	if (filterStatus)
	{
		filterStatus.addEventListener('change', filterTasksRealtime);
	}
	if (filterPriority)
	{
		filterPriority.addEventListener('change', filterTasksRealtime);
	}
	if (searchTask)
	{
		// Debounce cho search để tránh lag
		let searchTimeout;
		searchTask.addEventListener('input', () => {
			clearTimeout(searchTimeout);
			searchTimeout = setTimeout(filterTasksRealtime, 300);
		});
	}
}

function filterTasksRealtime()
{
	const statusFilter = document.getElementById('filterStatus')?.value.toLowerCase() || '';
	const priorityFilter = document.getElementById('filterPriority')?.value.toLowerCase() || '';
	const searchTerm = document.getElementById('searchTask')?.value.toLowerCase().trim() || '';

	const emptyFilter = document.getElementById('emptyFilter');
	let visibleCount = 0;

	allTasks.forEach(task => {
		const statusMatch = !statusFilter || task.status.toLowerCase() === statusFilter;
		const priorityMatch = !priorityFilter || task.priority.toLowerCase() === priorityFilter;
		const searchMatch = !searchTerm || task.name.toLowerCase().includes(searchTerm);

		const isVisible = statusMatch && priorityMatch && searchMatch;

		if (isVisible)
		{
			task.element.style.display = 'flex';
			visibleCount++;
		}
		else
		{
			task.element.style.display = 'none';
		}
	});

	// Hiển thị thông báo nếu không có kết quả
	if (emptyFilter)
	{
		if (visibleCount === 0)
		{
			emptyFilter.classList.remove('hidden');
		}
		else
		{
			emptyFilter.classList.add('hidden');
		}
	}

	// Cập nhật stats chỉ với tasks hiển thị
	calculateStatsFiltered(visibleCount);
}

function calculateStatsFiltered(visibleCount)
{
	if (visibleCount === 0) return;

	const visibleTasks = allTasks.filter(task => task.element.style.display !== 'none');

	let done = 0, inProgress = 0;
	visibleTasks.forEach(task => {
		const status = task.status.toLowerCase();
		if (status === 'done')
		{
			done++;
		}
		else if (['inprogress', 'testing', 'reopen'].includes(status))
		{
			inProgress++;
		}
	});

	document.getElementById('statDone').textContent = done;
	document.getElementById('statInProgress').textContent = inProgress;
	document.getElementById('statTotal').textContent = visibleTasks.length;
}

function calculateStats()
{
	let done = 0, inProgress = 0;

	allTasks.forEach(task => {
		const status = task.status.toLowerCase();
		if (status === 'done')
		{
			done++;
		}
		else if (['inprogress', 'testing', 'reopen'].includes(status))
		{
			inProgress++;
		}
	});

	document.getElementById('statDone').textContent = done;
	document.getElementById('statInProgress').textContent = inProgress;
	document.getElementById('statTotal').textContent = allTasks.length;
}

// ========== PERMISSIONS & TESTERS ==========
async function loadPermissions()
{
	try
	{
		const res = await fetch('/Staff/GetUserPermissions');
		const data = await res.json();
		if (data.success)
		{
			userPermissions = data.permissions;
			console.log('✅ Permissions loaded:', userPermissions);

			if (userPermissions.canSendToTesting)
			{
				await loadTesters();
			}
		}
		else
		{
			showError('Không thể tải thông tin quyền: ' + data.message);
		}
	}
	catch (err)
	{
		console.error('Error loading permissions:', err);
		showError('Lỗi khi tải thông tin quyền');
	}
}

async function loadTesters()
{
	try
	{
		const res = await fetch('/Staff/GetTesters');
		const data = await res.json();
		if (data.success && data.testers)
		{
			allTesters = data.testers;
			const select = document.getElementById('testerId');
			if (select)
			{
				select.innerHTML = '<option value="">-- Chọn Tester --</option>';
				data.testers.forEach(t => {
				const opt = document.createElement('option');
				opt.value = t.userId;
				opt.textContent = `${ t.fullName} (${ t.departmentName})`;
				select.appendChild(opt);
			});
		}
	}

	} catch (err) {
	console.error('Error loading testers:', err);
}
}

// ========== MODAL CONTROLS ==========
function openModal(id)
{
	const modal = document.getElementById(id);
	if (modal)
	{
		modal.classList.add('active');
		document.body.style.overflow = 'hidden';
	}
}

function closeModal(id)
{
	const modal = document.getElementById(id);
	if (modal)
	{
		modal.classList.remove('active');
		document.body.style.overflow = '';
	}
}

function setupModalClose()
{
	document.querySelectorAll('.modal').forEach(modal => {
		modal.addEventListener('click', (e) => {
			if (e.target === modal)
			{
				closeModal(modal.id);
			}
		});
	});
}

// ========== VIEW TASK DETAIL ==========
async function viewTask(taskId)
{
	openModal('detailModal');
	const content = document.getElementById('detailContent');
	content.innerHTML = `

		< div class= "loading" >

			< div class= "spinner" ></ div >

			< p class= "mt-2" > Đang tải chi tiết...</p>
        </div>
    `;
try
{
	const res = await fetch(`/ Staff / GetTaskDetail ? taskId =${ taskId}`);
	const data = await res.json();

	if (data.success)
	{
		const t = data.task;
		const statusClass = `status -${ (t.status || 'todo').toLowerCase()}`;
		const priorityClass = `priority -${ (t.priority || 'medium').toLowerCase()}`;

		let reopenSection = '';
		if (t.reopenReason)
		{
			const isCurrentlyReopen = t.status === 'Reopen';
			const labelText = isCurrentlyReopen ? 'Lý do Reopen' : 'Lưu ý từ lần Reopen trước';
			const bgColor = isCurrentlyReopen ? 'rgba(239, 68, 68, 0.05)' : 'rgba(245, 158, 11, 0.05)';
			const borderColor = isCurrentlyReopen ? 'var(--danger)' : 'var(--warning)';
			const textColor = isCurrentlyReopen ? 'var(--danger)' : 'var(--warning)';

			reopenSection = `

					< div class= "form-group" >

						< label class= "form-label" style = "color: ${textColor};" >

							< i class= "fas fa-exclamation-circle" ></ i > ${ labelText}

						</ label >

						< div style = "padding: 1rem; background: ${bgColor}; border-left: 4px solid ${borderColor}; border-radius: var(--radius-md); line-height: 1.6;" >
                            ${ t.reopenReason}
                            ${ !isCurrentlyReopen ? '<br><small style="color: var(--text-secondary); font-style: italic;">💡 Hãy đảm bảo đã sửa đúng vấn đề này trước khi gửi test lại</small>' : ''}

						</ div >

					</ div >
                `;
            }

            content.innerHTML = `

				< div style = "margin-bottom: 1.5rem;" >

					< h4 style = "font-size: 1.375rem; font-weight: 700; margin-bottom: 1rem;" >
                        ${ t.taskName}

					</ h4 >

					< div style = "display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 1rem;" >

						< div class= "status-badge ${statusClass}" >

							< i class= "fas fa-circle" ></ i > ${ getStatusText(t.status)}

						</ div >

						< div class= "priority-badge ${priorityClass}" >

							< i class= "fas fa-flag" ></ i > ${ t.priority}

						</ div >
                        ${ t.isOverdue ? '<span class="badge-overdue"><i class="fas fa-exclamation-triangle"></i> QUÁ HẠN</span>' : ''}

					</ div >

				</ div >


				< div class= "form-group" >

					< label class= "form-label" >< i class= "fas fa-align-left" ></ i > Mô tả </ label >

					< div style = "padding: 1rem; background: var(--light-bg); border-radius: var(--radius-md); line-height: 1.6;" >
                        ${ t.description || '<em style="color: var(--text-secondary);">Không có mô tả</em>'}

					</ div >

				</ div >

                ${ reopenSection}


				< div class= "form-group" >

					< label class= "form-label" >< i class= "fas fa-laptop-code" ></ i > Nền tảng </ label >

					< input type = "text" class= "form-control" value = "${t.platform}" disabled >

				</ div >

                ${
	t.reportLink ? `

				< div class= "form-group" >

					< label class= "form-label" >< i class= "fas fa-link" ></ i > Link báo cáo</label>
                    <a href = "${t.reportLink}" target= "_blank" class= "btn btn-secondary" style = "width: 100%; justify-content: center;" >

						< i class= "fas fa-external-link-alt me-1" ></ i > Mở Link
					</ a >

				</ div >
                ` : ''}

                ${
	t.testerName ? `

				< div class= "form-group" >

					< label class= "form-label" >< i class= "fas fa-user-check" ></ i > Tester phụ trách</label>
                    <input type = "text" class= "form-control" value = "${t.testerName}" disabled >

				</ div >
                ` : ''}


				< div style = "display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;" >

					< div class= "form-group" >

						< label class= "form-label" >< i class= "fas fa-calendar-alt" ></ i > Ngày giao </ label >

						< input type = "text" class= "form-control" value = "${t.createdAt || 'N/A'}" disabled >

					</ div >

					< div class= "form-group" >

						< label class= "form-label" >< i class= "fas fa-hourglass-half" ></ i > Hạn chót </ label >

						< input type = "text" class= "form-control" value = "${t.deadlineStr}" disabled >

					</ div >

				</ div >


				< div class= "form-group" >

					< label class= "form-label" >< i class= "fas fa-clock" ></ i > Cập nhật lần cuối</label>
                    <input type="text" class= "form-control" value = "${t.updatedAt}" disabled >

				</ div >
            `;
        } else
{
	content.innerHTML = `< p style = "color: var(--danger); text-align: center;" >${ data.message}</ p >`;
}
    } catch (err) {
	console.error('Error:', err);
	content.innerHTML = `< p style = "color: var(--danger); text-align: center;" > Lỗi kết nối: ${ err.message}</ p >`;
}
}

// ========== UPDATE TASK ==========
async function updateTask(id)
{
	if (!userPermissions)
	{
		showWarning('Vui lòng đợi hệ thống tải thông tin quyền...');
		return;
	}

	try
	{
		const item = document.querySelector(`.task - item[data - id = "${id}"]`);
		const currentStatus = item ? item.dataset.status : 'TODO';
		const currentStatusText = item ? item.querySelector('.status-badge span').textContent : '';

		document.getElementById('updateTaskId').value = id;
		document.getElementById('currentStatus').value = currentStatus;
		document.getElementById('currentStatusText').value = currentStatusText;
		document.getElementById('reportLink').value = '';
		document.getElementById('reportLinkGroup').classList.add('hidden');
		document.getElementById('testerGroup').classList.add('hidden');
		document.getElementById('testerId').value = '';

		const select = document.getElementById('newStatus');
		select.innerHTML = '<option value="">-- Chọn trạng thái --</option>';

		let options = [];
		const isDev = userPermissions.canSendToTesting;

		if (currentStatus === 'Testing' || currentStatus === 'Done')
		{
			if (currentStatus === 'Testing')
			{
				showInfo('Công việc đang ở trạng thái **Chờ test**. Vui lòng đợi Tester phản hồi.');
			}
			else
			{
				showInfo('Công việc đã **Hoàn thành** và không thể thay đổi trạng thái.');
			}
			return;
		}

		if (currentStatus === 'TODO')
		{
			options.push('InProgress');
			if (isDev)
			{
				options.push('Testing');
			}
		}
		else if (currentStatus === 'InProgress')
		{
			if (isDev)
			{
				options.push('Testing');
			}
			options.push('Done');
		}
		else if (currentStatus === 'Reopen')
		{
			options.push('InProgress');
		}

		const statusMap = {
			'InProgress': 'Đang làm',
            'Testing': 'Chờ test',
            'Done': 'Hoàn thành',
            'Reopen': 'Cần sửa lại'
        };

[...new Set(options)].forEach(status => {
	const opt = document.createElement('option');
	opt.value = status;
	opt.textContent = statusMap[status] || status;
	select.appendChild(opt);
});

if (select.options.length <= 1)
{
	showInfo('Không có trạng thái nào hợp lệ để chuyển tiếp công việc này.');
	return;
}

openModal('updateModal');
    } catch (err) {
	console.error('Error:', err);
	showError('Đã xảy ra lỗi khi chuẩn bị cập nhật tiến độ.');
}
}

// Event listener cho newStatus dropdown
document.getElementById('newStatus')?.addEventListener('change', function() {
	const status = this.value;
	const reportLinkGroup = document.getElementById('reportLinkGroup');
	const testerGroup = document.getElementById('testerGroup');

	if (['InProgress', 'Done'].includes(status))
	{
		reportLinkGroup.classList.remove('hidden');
	}
	else
	{
		reportLinkGroup.classList.add('hidden');
	}

	if (userPermissions?.canSendToTesting && status === 'Testing')
	{
		testerGroup.classList.remove('hidden');
	}
	else
	{
		testerGroup.classList.add('hidden');
	}
});

async function submitUpdate()
{
	const taskId = document.getElementById('updateTaskId').value;
	const status = document.getElementById('newStatus').value;
	const reportLink = document.getElementById('reportLink').value.trim();
	const testerId = document.getElementById('testerId').value;

	if (!status)
	{
		showWarning('Vui lòng chọn trạng thái mới.');
		return;
	}

	if (status === 'Testing' && userPermissions.canSendToTesting && !testerId)
	{
		showWarning('Vui lòng chọn Tester khi chuyển sang trạng thái "Chờ test".');
		return;
	}

	const payload = {
		UserTaskId: parseInt(taskId),
        Status: status,
        ReportLink: reportLink || null,
        TesterId: status === 'Testing' ? (testerId ? parseInt(testerId) : null) : null
    };

showLoading('Đang cập nhật...');

try
{
	const res = await fetch('/Staff/UpdateTaskProgress', {
	method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)

		});

	const data = await res.json();
	if (data.success)
	{
		await showSuccess('Cập nhật thành công!', data.message);
		location.reload();
	}
	else
	{
		showError(data.message);
	}
}
catch (err)
{
	console.error('Error:', err);
	showError('Lỗi kết nối: ' + err.message);
}
}

// ========== HELPER FUNCTIONS ==========
function getStatusText(status)
{
	const map = {
		'TODO': 'Chưa bắt đầu',
        'InProgress': 'Đang làm',
        'Testing': 'Chờ test',
        'Done': 'Hoàn thành',
        'Reopen': 'Cần sửa lại'
    };
return map[status] || status;
}

function showLoading(title = 'Đang xử lý...')
{
	Swal.fire({
	title: title,
        text: 'Vui lòng đợi giây lát',
        allowOutsideClick: false,
        allowEscapeKey: false,
        didOpen: () => Swal.showLoading()

	});
}

async function showSuccess(title, text)
{
	return Swal.fire({
	icon: 'success',
        title: title,
        text: text,
        confirmButtonText: 'OK',
        confirmButtonColor: '#10b981'

	});
}

function showError(message)
{
	Swal.fire({
	icon: 'error',
        title: 'Lỗi',
        text: message,
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#ef4444'

	});
}

function showWarning(message)
{
	Swal.fire({
	icon: 'warning',
        title: 'Cảnh báo',
        text: message,
        confirmButtonText: 'OK',
        confirmButtonColor: '#f59e0b'

	});
}

function showInfo(message)
{
	Swal.fire({
	icon: 'info',
        title: 'Thông báo',
        html: message,
        confirmButtonText: 'OK',
        confirmButtonColor: '#3b82f6'

	});
}