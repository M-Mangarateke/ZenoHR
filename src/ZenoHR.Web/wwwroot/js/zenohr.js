// ZenoHR JavaScript interop
// REQ-SEC-002: theme, sidebar, chart, lucide interop for Blazor Server

window.zenohr = {
    setTheme: function(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('zenohr-theme', theme);
    },
    getTheme: function() {
        return localStorage.getItem('zenohr-theme') || 'light';
    },
    initTheme: function() {
        const t = localStorage.getItem('zenohr-theme') || 'light';
        document.documentElement.setAttribute('data-theme', t);
        return t;
    },
    toggleSidebar: function() {
        const sidebar = document.getElementById('sidebar');
        const wrapper = document.getElementById('mainWrapper');
        if (sidebar) sidebar.classList.toggle('collapsed');
    },
    initLucide: function() {
        if (window.lucide) lucide.createIcons();
    },
    // REQ-HR-004: Trigger browser file download from base64 content (payslip PDFs, CSV exports)
    downloadFile: function(filename, contentType, base64Content) {
        const link = document.createElement('a');
        link.download = filename;
        link.href = `data:${contentType};base64,${base64Content}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },
    renderPayrollChart: function(canvasId, labels, data, isDark) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;
        if (ctx._chart) ctx._chart.destroy();
        const blue = isDark ? '#4a8fd4' : '#1d4777';
        const gridColor = isDark ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.04)';
        const textColor = isDark ? '#94a3b8' : '#64748b';
        const grad = ctx.getContext('2d').createLinearGradient(0, 0, 0, 180);
        grad.addColorStop(0, isDark ? 'rgba(74,143,212,0.4)' : 'rgba(29,71,119,0.15)');
        grad.addColorStop(1, 'rgba(29,71,119,0)');
        ctx._chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Payroll (R)',
                    data: data,
                    borderColor: blue,
                    backgroundColor: grad,
                    borderWidth: 2.5,
                    pointBackgroundColor: blue,
                    pointRadius: 4,
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: { label: c => ` R ${(c.raw/1000000).toFixed(2)}M` },
                        backgroundColor: isDark ? '#1e293b' : '#fff',
                        titleColor: isDark ? '#f1f5f9' : '#0f172a',
                        bodyColor: isDark ? '#94a3b8' : '#64748b',
                        borderColor: isDark ? '#334155' : '#e2e8f0',
                        borderWidth: 1, padding: 10, cornerRadius: 8
                    }
                },
                scales: {
                    x: { grid: { color: gridColor }, ticks: { color: textColor, font: { family: "'JetBrains Mono', monospace", size: 11 } }, border: { display: false } },
                    y: { grid: { color: gridColor }, ticks: { color: textColor, font: { family: "'JetBrains Mono', monospace", size: 11 }, callback: v => 'R ' + (v/1000000).toFixed(1)+'M' }, border: { display: false } }
                }
            }
        });
    }
};
