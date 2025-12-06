let parsedData = [];
let selectedFile = null;

// ============================================
// Download Template
// ============================================
document.getElementById('downloadTemplateBtn').addEventListener('click', function () {
    const wb = XLSX.utils.book_new();
    const templateData = [
        ['HỌ VÀ TÊN*', 'EMAIL*', 'SỐ ĐIỆN THOẠI*', 'PHÒNG BAN', 'VAI TRÒ'],
        ['Nguyễn Văn A', 'nguyenvana@example.com', '0912345678', 'Phòng IT', 'Staff'],
        ['Trần Thị B', 'tranthib@example.com', '0923456789', 'Phòng Marketing', 'Staff'],
        ['Lê Văn C', 'levanc@example.com', '0934567890', 'Phòng Kế toán', 'Tester']
    ];

    const ws = XLSX.utils.aoa_to_sheet(templateData);
    ws['!cols'] = [{ wch: 20 }, { wch: 30 }, { wch: 15 }, { wch: 20 }, { wch: 15 }];
    XLSX.utils.book_append_sheet(wb, ws, 'Template');
    XLSX.writeFile(wb, 'Template_Import_NhanVien.xlsx');

    Swal.fire({
        icon: 'success',
        title: 'Đã tải xuống!',
        text: 'Vui lòng điền thông tin vào file mẫu',
        confirmButtonColor: '#7A3F30'
    });
});

// ============================================
// File Upload Handling
// ============================================
const dropArea = document.getElementById('dropArea');
const fileInput = document.getElementById('excelFile');
const selectFileBtn = document.getElementById('selectFileBtn');
const changeFileBtn = document.getElementById('changeFileBtn');

selectFileBtn.addEventListener('click', () => fileInput.click());
changeFileBtn.addEventListener('click', () => fileInput.click());

// Drag & Drop
['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
    dropArea.addEventListener(eventName, preventDefaults, false);
});

function preventDefaults(e) {
    e.preventDefault();
    e.stopPropagation();
}

['dragenter', 'dragover'].forEach(eventName => {
    dropArea.addEventListener(eventName, () => {
        dropArea.classList.add('dragover');
    });
});

['dragleave', 'drop'].forEach(eventName => {
    dropArea.addEventListener(eventName, () => {
        dropArea.classList.remove('dragover');
    });
});

dropArea.addEventListener('drop', function (e) {
    const files = e.dataTransfer.files;
    if (files.length > 0) {
        handleFile(files[0]);
    }
});

fileInput.addEventListener('change', function (e) {
    if (e.target.files.length > 0) {
        handleFile(e.target.files[0]);
    }
});

function handleFile(file) {
    if (!file.name.match(/\.(xlsx|xls)$/)) {
        Swal.fire({
            icon: 'error',
            title: 'File không hợp lệ',
            text: 'Vui lòng chọn file Excel (.xlsx hoặc .xls)',
            confirmButtonColor: '#7A3F30'
        });
        return;
    }

    selectedFile = file;

    // Show file info
    document.querySelector('.file-upload-content').style.display = 'none';
    document.getElementById('fileSelected').style.display = 'block';
    document.getElementById('fileName').textContent = file.name;
    document.getElementById('fileSize').textContent = (file.size / 1024).toFixed(2) + ' KB';

    // Parse file
    parseExcelFile(file);
}

function parseExcelFile(file) {
    const reader = new FileReader();

    reader.onload = function (e) {
        try {
            const data = new Uint8Array(e.target.result);
            const workbook = XLSX.read(data, { type: 'array' });
            const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
            const jsonData = XLSX.utils.sheet_to_json(firstSheet, { header: 1 });

            // Skip header row
            const rows = jsonData.slice(1).filter(row => row.length > 0);

            parsedData = rows.map((row, index) => ({
                rowNumber: index + 2,
                fullName: (row[0] || '').toString().trim(),
                email: (row[1] || '').toString().trim(),
                phoneNumber: (row[2] || '').toString().trim(),
                department: (row[3] || '').toString().trim(),
                role: (row[4] || 'Staff').toString().trim(),
                errors: []
            }));

            validateData();
            displayPreview();

        } catch (error) {
            Swal.fire({
                icon: 'error',
                title: 'Lỗi đọc file',
                text: 'Không thể đọc file Excel. Vui lòng kiểm tra lại định dạng',
                confirmButtonColor: '#7A3F30'
            });
        }
    };

    reader.readAsArrayBuffer(file);
}

// ============================================
// Validate Data
// ============================================
function validateData() {
    parsedData.forEach(row => {
        row.errors = [];

        if (!row.fullName) row.errors.push('Thiếu họ tên');
        if (!row.email) row.errors.push('Thiếu email');
        else if (!validateEmail(row.email)) row.errors.push('Email không hợp lệ');
        if (!row.phoneNumber) row.errors.push('Thiếu SĐT');
    });
}

function validateEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

// ============================================
// Display Preview
// ============================================
function displayPreview() {
    const validRows = parsedData.filter(r => r.errors.length === 0);
    const errorRows = parsedData.filter(r => r.errors.length > 0);

    // Stats
    const statsHtml = `
                <div class="stat-card">
                    <div class="stat-value">${parsedData.length}</div>
                    <div class="stat-label">Tổng số dòng</div>
                </div>
                <div class="stat-card">
                    <div class="stat-value" style="color: #16a34a;">${validRows.length}</div>
                    <div class="stat-label">Hợp lệ</div>
                </div>
                <div class="stat-card">
                    <div class="stat-value" style="color: #dc2626;">${errorRows.length}</div>
                    <div class="stat-label">Lỗi</div>
                </div>
            `;
    document.getElementById('previewStats').innerHTML = statsHtml;

    // Table
    let tableHtml = `
                <table class="preview-table">
                    <thead>
                        <tr>
                            <th>STT</th>
                            <th>Họ Tên</th>
                            <th>Email</th>
                            <th>SĐT</th>
                            <th>Phòng Ban</th>
                            <th>Vai Trò</th>
                            <th>Trạng Thái</th>
                        </tr>
                    </thead>
                    <tbody>
            `;

    parsedData.forEach(row => {
        const rowClass = row.errors.length > 0 ? 'error' : '';
        const status = row.errors.length > 0
            ? `<span style="color: #dc2626;">❌ ${row.errors.join(', ')}</span>`
            : '<span style="color: #16a34a;">✓ Hợp lệ</span>';

        tableHtml += `
                    <tr class="${rowClass}">
                        <td>${row.rowNumber}</td>
                        <td>${row.fullName}</td>
                        <td>${row.email}</td>
                        <td>${row.phoneNumber}</td>
                        <td>${row.department || '-'}</td>
                        <td>${row.role || 'Staff'}</td>
                        <td>${status}</td>
                    </tr>
                `;
    });

    tableHtml += '</tbody></table>';
    document.getElementById('previewTable').innerHTML = tableHtml;
    document.getElementById('previewSection').style.display = 'block';
}

// ============================================
// Import Data
// ============================================
document.getElementById('importBtn').addEventListener('click', async function () {
    const validRows = parsedData.filter(r => r.errors.length === 0);

    if (validRows.length === 0) {
        Swal.fire({
            icon: 'error',
            title: 'Không có dữ liệu hợp lệ',
            text: 'Vui lòng kiểm tra lại file Excel',
            confirmButtonColor: '#7A3F30'
        });
        return;
    }

    document.getElementById('previewSection').style.display = 'none';
    document.getElementById('progressSection').style.display = 'block';

    const results = {
        success: [],
        errors: []
    };

    for (let i = 0; i < validRows.length; i++) {
        const row = validRows[i];
        const progress = ((i + 1) / validRows.length * 100).toFixed(0);

        document.getElementById('progressFill').style.width = progress + '%';
        document.getElementById('progressText').textContent = `${i + 1} / ${validRows.length}`;

        try {
            const response = await fetch('/Admin/ImportSingleUser', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(row)
            });

            const result = await response.json();

            if (result.success) {
                results.success.push({ ...row, username: result.username });
            } else {
                results.errors.push({ ...row, error: result.message });
            }
        } catch (error) {
            results.errors.push({ ...row, error: 'Lỗi kết nối server' });
        }

        await new Promise(resolve => setTimeout(resolve, 100));
    }

    displayResults(results);
});

// ============================================
// Display Results
// ============================================
function displayResults(results) {
    document.getElementById('progressSection').style.display = 'none';

    let resultsHtml = `
                <h5><i class="fas fa-check-circle"></i> Kết Quả Import</h5>
                <div class="result-summary">
                    <div class="result-card success">
                        <div class="result-number">${results.success.length}</div>
                        <div class="stat-label">Thành công</div>
                    </div>
                    <div class="result-card error">
                        <div class="result-number">${results.errors.length}</div>
                        <div class="stat-label">Thất bại</div>
                    </div>
                </div>
            `;

    if (results.errors.length > 0) {
        resultsHtml += '<div class="error-list"><strong>Chi tiết lỗi:</strong>';
        results.errors.forEach(err => {
            resultsHtml += `
                        <div class="error-item">
                            <strong>Dòng ${err.rowNumber}:</strong> ${err.fullName} (${err.email})<br>
                            <span style="color: #dc2626;">Lỗi: ${err.error}</span>
                        </div>
                    `;
        });
        resultsHtml += '</div>';
    }

    if (results.success.length > 0) {
        resultsHtml += '<div class="success-list"><strong>Import thành công:</strong>';
        results.success.slice(0, 5).forEach(s => {
            resultsHtml += `
                        <div class="success-item">
                            <strong>${s.fullName}</strong> (${s.email})
                        </div>
                    `;
        });
        if (results.success.length > 5) {
            resultsHtml += `<div class="success-item"><em>... và ${results.success.length - 5} người khác</em></div>`;
        }
        resultsHtml += '</div>';
    }

    document.getElementById('resultsSection').innerHTML = resultsHtml;
    document.getElementById('resultsSection').style.display = 'block';

    Swal.fire({
        icon: results.success.length > 0 ? 'success' : 'error',
        title: 'Hoàn Thành!',
        html: `
                    Import thành công: <strong>${results.success.length}</strong><br>
                    Thất bại: <strong>${results.errors.length}</strong>
                `,
        confirmButtonColor: '#7A3F30'
    });
}

// Cancel
document.getElementById('cancelBtn').addEventListener('click', function () {
    location.reload();
});