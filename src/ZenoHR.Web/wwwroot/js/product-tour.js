// REQ-OPS-001: ZenoHR interactive product tour engine — guided onboarding walkthrough.
// Pure JS, no external dependencies. Brand-aware, dark-mode compatible, keyboard accessible.

window.ZenoHRTour = (function () {
    'use strict';

    /** @type {HTMLElement|null} */ let overlay = null;
    /** @type {HTMLElement|null} */ let tooltip = null;
    /** @type {HTMLElement|null} */ let progressDots = null;
    /** @type {Array<{selector:string, title:string, description:string}>} */ let steps = [];
    /** @type {number} */ let currentStep = 0;
    /** @type {boolean} */ let isActive = false;

    // ── Brand colors ───────────────────────────────────────────────────────────
    const BRAND = {
        primary: '#1d4777',
        primaryDark: '#4a8fd4',
        accent: '#d4890e',
        accentDark: '#f0a832'
    };

    function isDarkMode() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ||
            document.documentElement.classList.contains('dark');
    }

    // ── DOM helpers ────────────────────────────────────────────────────────────
    function createElement(tag, className, attrs) {
        const el = document.createElement(tag);
        if (className) el.className = className;
        if (attrs) Object.entries(attrs).forEach(([k, v]) => el.setAttribute(k, v));
        return el;
    }

    function removeIfExists(el) {
        if (el && el.parentNode) el.parentNode.removeChild(el);
    }

    // ── Overlay (backdrop with cutout) ─────────────────────────────────────────
    function createOverlay() {
        removeIfExists(overlay);
        overlay = createElement('div', 'zenohr-tour-overlay', {
            'role': 'dialog',
            'aria-modal': 'true',
            'aria-label': 'Product tour'
        });
        document.body.appendChild(overlay);
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) skip();
        });
    }

    function updateOverlayClipPath(targetEl) {
        if (!overlay) return;
        if (!targetEl) {
            overlay.style.clipPath = 'none';
            return;
        }
        const rect = targetEl.getBoundingClientRect();
        const pad = 8;
        const r = 8;
        const x = rect.left - pad;
        const y = rect.top - pad;
        const w = rect.width + pad * 2;
        const h = rect.height + pad * 2;

        // SVG-based clip-path: full viewport minus rounded rect cutout
        overlay.style.clipPath =
            `polygon(evenodd, ` +
            `0% 0%, 100% 0%, 100% 100%, 0% 100%, 0% 0%, ` +
            `${x + r}px ${y}px, ${x + w - r}px ${y}px, ${x + w}px ${y + r}px, ` +
            `${x + w}px ${y + h - r}px, ${x + w - r}px ${y + h}px, ` +
            `${x + r}px ${y + h}px, ${x}px ${y + h - r}px, ${x}px ${y + r}px, ` +
            `${x + r}px ${y}px)`;
    }

    // ── Tooltip ────────────────────────────────────────────────────────────────
    function createTooltip() {
        removeIfExists(tooltip);
        tooltip = createElement('div', 'zenohr-tour-tooltip', {
            'role': 'alertdialog',
            'aria-live': 'polite'
        });
        document.body.appendChild(tooltip);
    }

    function renderTooltip(step, index, total) {
        if (!tooltip) return;
        const dark = isDarkMode();

        tooltip.innerHTML = '';

        // Step counter pill
        const counter = createElement('div', 'zenohr-tour-counter');
        counter.textContent = `Step ${index + 1} of ${total}`;
        tooltip.appendChild(counter);

        // Title
        const title = createElement('h3', 'zenohr-tour-title');
        title.textContent = step.title;
        tooltip.appendChild(title);

        // Description
        const desc = createElement('p', 'zenohr-tour-desc');
        desc.textContent = step.description;
        tooltip.appendChild(desc);

        // Button row
        const btnRow = createElement('div', 'zenohr-tour-buttons');

        const skipBtn = createElement('button', 'zenohr-tour-btn zenohr-tour-btn--skip', {
            'aria-label': 'Skip tour'
        });
        skipBtn.textContent = 'Skip';
        skipBtn.addEventListener('click', skip);
        btnRow.appendChild(skipBtn);

        const navGroup = createElement('div', 'zenohr-tour-nav-group');

        if (index > 0) {
            const prevBtn = createElement('button', 'zenohr-tour-btn zenohr-tour-btn--prev', {
                'aria-label': 'Previous step'
            });
            prevBtn.textContent = 'Previous';
            prevBtn.addEventListener('click', previous);
            navGroup.appendChild(prevBtn);
        }

        const nextBtn = createElement('button', 'zenohr-tour-btn zenohr-tour-btn--next', {
            'aria-label': index === total - 1 ? 'Complete tour' : 'Next step'
        });
        nextBtn.textContent = index === total - 1 ? 'Done' : 'Next';
        nextBtn.addEventListener('click', function () {
            if (index === total - 1) complete();
            else next();
        });
        navGroup.appendChild(nextBtn);

        btnRow.appendChild(navGroup);
        tooltip.appendChild(btnRow);

        // Progress dots
        const dots = createElement('div', 'zenohr-tour-dots');
        for (let i = 0; i < total; i++) {
            const dot = createElement('span', 'zenohr-tour-dot' + (i === index ? ' active' : '') + (i < index ? ' completed' : ''));
            dots.appendChild(dot);
        }
        tooltip.appendChild(dots);

        // Focus the next/done button
        setTimeout(function () { nextBtn.focus(); }, 100);
    }

    function positionTooltip(targetEl) {
        if (!tooltip) return;
        if (!targetEl) {
            // No target — center on screen
            tooltip.style.position = 'fixed';
            tooltip.style.left = '50%';
            tooltip.style.top = '50%';
            tooltip.style.transform = 'translate(-50%, -50%)';
            tooltip.removeAttribute('data-position');
            return;
        }

        const rect = targetEl.getBoundingClientRect();
        const ttRect = tooltip.getBoundingClientRect();
        const pad = 16;
        const arrowSize = 10;
        const vpW = window.innerWidth;
        const vpH = window.innerHeight;

        // Reset
        tooltip.style.transform = '';
        tooltip.removeAttribute('data-position');

        // Decide position: prefer bottom, then top, then right, then left
        let top, left, position;

        // Try bottom
        if (rect.bottom + pad + arrowSize + ttRect.height < vpH) {
            position = 'bottom';
            top = rect.bottom + pad + arrowSize;
            left = rect.left + rect.width / 2 - ttRect.width / 2;
        }
        // Try top
        else if (rect.top - pad - arrowSize - ttRect.height > 0) {
            position = 'top';
            top = rect.top - pad - arrowSize - ttRect.height;
            left = rect.left + rect.width / 2 - ttRect.width / 2;
        }
        // Try right
        else if (rect.right + pad + arrowSize + ttRect.width < vpW) {
            position = 'right';
            top = rect.top + rect.height / 2 - ttRect.height / 2;
            left = rect.right + pad + arrowSize;
        }
        // Fallback: left
        else {
            position = 'left';
            top = rect.top + rect.height / 2 - ttRect.height / 2;
            left = rect.left - pad - arrowSize - ttRect.width;
        }

        // Clamp to viewport
        left = Math.max(12, Math.min(left, vpW - ttRect.width - 12));
        top = Math.max(12, Math.min(top, vpH - ttRect.height - 12));

        tooltip.style.position = 'fixed';
        tooltip.style.top = top + 'px';
        tooltip.style.left = left + 'px';
        tooltip.setAttribute('data-position', position);
    }

    // ── Highlight target ───────────────────────────────────────────────────────
    function highlightTarget(targetEl) {
        // Remove previous highlight
        const prev = document.querySelector('.zenohr-tour-highlight');
        if (prev) prev.classList.remove('zenohr-tour-highlight');

        if (targetEl) {
            targetEl.classList.add('zenohr-tour-highlight');
        }
    }

    function scrollToTarget(targetEl) {
        if (!targetEl) return;
        const rect = targetEl.getBoundingClientRect();
        if (rect.top < 0 || rect.bottom > window.innerHeight) {
            targetEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    // ── Step rendering ─────────────────────────────────────────────────────────
    function showStep(index) {
        if (index < 0 || index >= steps.length) return;
        currentStep = index;
        const step = steps[index];

        const targetEl = step.selector ? document.querySelector(step.selector) : null;

        if (targetEl) {
            scrollToTarget(targetEl);
            // Small delay to let scroll settle
            setTimeout(function () {
                updateOverlayClipPath(targetEl);
                highlightTarget(targetEl);
                renderTooltip(step, index, steps.length);
                positionTooltip(targetEl);
            }, 300);
        } else {
            // No target found — show centered
            updateOverlayClipPath(null);
            highlightTarget(null);
            renderTooltip(step, index, steps.length);
            positionTooltip(null);
        }
    }

    // ── Keyboard handler ───────────────────────────────────────────────────────
    function onKeyDown(e) {
        if (!isActive) return;
        switch (e.key) {
            case 'Escape':
                e.preventDefault();
                skip();
                break;
            case 'Enter':
                e.preventDefault();
                if (currentStep === steps.length - 1) complete();
                else next();
                break;
            case 'ArrowRight':
            case 'ArrowDown':
                e.preventDefault();
                if (currentStep < steps.length - 1) next();
                break;
            case 'ArrowLeft':
            case 'ArrowUp':
                e.preventDefault();
                if (currentStep > 0) previous();
                break;
        }
    }

    // ── Resize handler ─────────────────────────────────────────────────────────
    function onResize() {
        if (!isActive) return;
        showStep(currentStep);
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────
    function cleanup() {
        isActive = false;
        removeIfExists(overlay);
        removeIfExists(tooltip);
        overlay = null;
        tooltip = null;
        const highlighted = document.querySelector('.zenohr-tour-highlight');
        if (highlighted) highlighted.classList.remove('zenohr-tour-highlight');
        document.removeEventListener('keydown', onKeyDown);
        window.removeEventListener('resize', onResize);
        document.body.classList.remove('zenohr-tour-active');
    }

    function fireEvent(name, detail) {
        document.dispatchEvent(new CustomEvent(name, { detail: detail || {} }));
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    function start(tourSteps) {
        if (isActive) cleanup();
        if (!tourSteps || !tourSteps.length) return;

        steps = tourSteps;
        currentStep = 0;
        isActive = true;

        document.body.classList.add('zenohr-tour-active');
        createOverlay();
        createTooltip();
        document.addEventListener('keydown', onKeyDown);
        window.addEventListener('resize', onResize);

        fireEvent('tour:started', { totalSteps: steps.length });
        showStep(0);
    }

    function next() {
        if (!isActive) return;
        if (currentStep < steps.length - 1) {
            showStep(currentStep + 1);
        }
    }

    function previous() {
        if (!isActive) return;
        if (currentStep > 0) {
            showStep(currentStep - 1);
        }
    }

    function skip() {
        fireEvent('tour:skipped', { stoppedAt: currentStep + 1, totalSteps: steps.length });
        cleanup();
    }

    function complete() {
        fireEvent('tour:completed', { totalSteps: steps.length });
        cleanup();
    }

    // ── localStorage helpers for Blazor interop ────────────────────────────────
    function hasTourCompleted(userId, tourId) {
        var key = 'zenohr-tour-' + tourId + '-' + userId;
        return localStorage.getItem(key) === 'completed' || localStorage.getItem(key) === 'skipped';
    }

    function markTourCompleted(userId, tourId, status) {
        var key = 'zenohr-tour-' + tourId + '-' + userId;
        localStorage.setItem(key, status || 'completed');
    }

    // ── Checklist localStorage helpers ─────────────────────────────────────────
    function getChecklistState(userId, role) {
        var key = 'zenohr-checklist-' + role + '-' + userId;
        var raw = localStorage.getItem(key);
        if (!raw) return null;
        try {
            var state = JSON.parse(raw);
            // Expire after 7 days
            if (state.createdAt && (Date.now() - state.createdAt > 7 * 24 * 60 * 60 * 1000)) {
                localStorage.removeItem(key);
                return null;
            }
            return state;
        } catch (_) { return null; }
    }

    function saveChecklistState(userId, role, items, dismissed) {
        var key = 'zenohr-checklist-' + role + '-' + userId;
        var existing = getChecklistState(userId, role);
        localStorage.setItem(key, JSON.stringify({
            items: items,
            dismissed: dismissed || false,
            createdAt: (existing && existing.createdAt) || Date.now()
        }));
    }

    function dismissChecklist(userId, role) {
        var key = 'zenohr-checklist-' + role + '-' + userId;
        localStorage.removeItem(key);
    }

    return {
        start: start,
        next: next,
        previous: previous,
        skip: skip,
        complete: complete,
        hasTourCompleted: hasTourCompleted,
        markTourCompleted: markTourCompleted,
        getChecklistState: getChecklistState,
        saveChecklistState: saveChecklistState,
        dismissChecklist: dismissChecklist,
        isActive: function () { return isActive; }
    };
})();
