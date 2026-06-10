// Azure Event Grid Simulator Dashboard Application

(function () {
    'use strict';

    // Configuration
    const REFRESH_INTERVAL = 3000; // 3 seconds
    const API_BASE = '/dashboard/api';

    // State
    let events = [];
    let rejections = [];
    let selectedEventId = null;
    let selectedRejectionId = null;
    let activeTab = 'events';
    let autoRefreshEnabled = true;
    let refreshTimer = null;
    let knownTopics = new Set();

    // DOM Elements
    const elements = {
        autoRefresh: document.getElementById('autoRefresh'),
        refreshIndicator: document.getElementById('refreshIndicator'),
        statsSection: document.getElementById('statsSection'),
        statTotalReceived: document.getElementById('statTotalReceived'),
        statInHistory: document.getElementById('statInHistory'),
        statDelivered: document.getElementById('statDelivered'),
        statFailed: document.getElementById('statFailed'),
        statPending: document.getElementById('statPending'),
        statRejected: document.getElementById('statRejected'),
        statTopics: document.getElementById('statTopics'),
        topicFilter: document.getElementById('topicFilter'),
        emptyState: document.getElementById('emptyState'),
        emptyRejectionsState: document.getElementById('emptyRejectionsState'),
        eventsList: document.getElementById('eventsList'),
        rejectionsList: document.getElementById('rejectionsList'),
        detailPanel: document.getElementById('detailPanel'),
        detailContent: document.getElementById('detailContent'),
        closeDetail: document.getElementById('closeDetail'),
        tabEvents: document.getElementById('tabEvents'),
        tabRejections: document.getElementById('tabRejections'),
        eventsTab: document.getElementById('eventsTab'),
        rejectionsTab: document.getElementById('rejectionsTab'),
        rejectionCount: document.getElementById('rejectionCount'),
        clearBtn: document.getElementById('clearBtn'),
    };

    // Initialize
    function init() {
        setupEventListeners();
        startAutoRefresh();
        fetchData();
    }

    function setupEventListeners() {
        elements.autoRefresh.addEventListener('change', handleAutoRefreshToggle);
        elements.closeDetail.addEventListener('click', closeDetailPanel);
        elements.topicFilter.addEventListener('change', handleTopicFilterChange);
        elements.tabEvents.addEventListener('click', () => switchTab('events'));
        elements.tabRejections.addEventListener('click', () => switchTab('rejections'));

        if (elements.clearBtn) {
            elements.clearBtn.addEventListener('click', clearHistory);
        } else {
            console.error('Clear button not found!');
        }

        // Keyboard navigation
        document.addEventListener('keydown', handleKeyDown);
    }

    async function clearHistory() {
        console.log('Clear button clicked');
        if (!confirm('Clear all events and rejections?')) return;

        elements.clearBtn.disabled = true;
        elements.clearBtn.textContent = 'Clearing...';

        try {
            const response = await fetch(`${API_BASE}/clear`, {method: 'DELETE'});
            if (response.ok) {
                events = [];
                rejections = [];
                selectedEventId = null;
                selectedRejectionId = null;
                knownTopics = new Set();
                closeDetailPanel();
                renderEventsList();
                renderRejectionsList();
                updateRejectionCount();
                updateTopicFilter();
                await fetchData(); // Refresh stats
            } else {
                console.error('Clear failed:', response.status, await response.text());
            }
        } catch (error) {
            console.error('Failed to clear history:', error);
        } finally {
            elements.clearBtn.disabled = false;
            elements.clearBtn.textContent = 'Clear';
        }
    }

    function switchTab(tab) {
        activeTab = tab;
        closeDetailPanel();

        elements.tabEvents.classList.toggle('active', tab === 'events');
        elements.tabRejections.classList.toggle('active', tab === 'rejections');
        elements.eventsTab.classList.toggle('hidden', tab !== 'events');
        elements.rejectionsTab.classList.toggle('hidden', tab !== 'rejections');
    }

    function handleAutoRefreshToggle() {
        autoRefreshEnabled = elements.autoRefresh.checked;
        if (autoRefreshEnabled) {
            elements.refreshIndicator.classList.remove('paused');
            startAutoRefresh();
        } else {
            elements.refreshIndicator.classList.add('paused');
            stopAutoRefresh();
        }
    }

    function startAutoRefresh() {
        if (refreshTimer) return;
        refreshTimer = setInterval(fetchData, REFRESH_INTERVAL);
    }

    function stopAutoRefresh() {
        if (refreshTimer) {
            clearInterval(refreshTimer);
            refreshTimer = null;
        }
    }

    function handleTopicFilterChange() {
        renderEventsList();
        renderRejectionsList();
    }

    function handleKeyDown(e) {
        const list = activeTab === 'events' ? events : rejections;
        const selectedId = activeTab === 'events' ? selectedEventId : selectedRejectionId;

        if (!list.length) return;

        const currentIndex = list.findIndex(item => item.id === selectedId);

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (currentIndex < list.length - 1) {
                    selectItem(list[currentIndex + 1].id);
                } else if (currentIndex === -1) {
                    selectItem(list[0].id);
                }
                break;
            case 'ArrowUp':
                e.preventDefault();
                if (currentIndex > 0) {
                    selectItem(list[currentIndex - 1].id);
                }
                break;
            case 'Escape':
                closeDetailPanel();
                break;
        }
    }

    function selectItem(id) {
        if (activeTab === 'events') {
            selectEvent(id);
        } else {
            selectRejection(id);
        }
    }

    // API Calls
    async function fetchData() {
        try {
            const [eventsResponse, statsResponse, rejectionsResponse] = await Promise.all([
                fetch(`${API_BASE}/events`),
                fetch(`${API_BASE}/stats`),
                fetch(`${API_BASE}/rejections`),
            ]);

            if (eventsResponse.ok) {
                events = await eventsResponse.json();
                updateTopicFilter();
                renderEventsList();
            }

            if (statsResponse.ok) {
                const stats = await statsResponse.json();
                renderStats(stats);
            }

            if (rejectionsResponse.ok) {
                rejections = await rejectionsResponse.json();
                renderRejectionsList();
                updateRejectionCount();
            }
        } catch (error) {
            console.error('Failed to fetch data:', error);
        }
    }

    async function fetchEventDetails(eventId) {
        try {
            const response = await fetch(`${API_BASE}/events/${eventId}`);
            if (response.ok) {
                return await response.json();
            }
        } catch (error) {
            console.error('Failed to fetch event details:', error);
        }
        return null;
    }

    // Rendering
    function renderStats(stats) {
        elements.statTotalReceived.textContent = formatNumber(stats.totalEventsReceived);
        elements.statInHistory.textContent = formatNumber(stats.eventsInHistory);
        elements.statDelivered.textContent = formatNumber(stats.totalDelivered);
        elements.statFailed.textContent = formatNumber(stats.totalFailed);
        elements.statPending.textContent = formatNumber(stats.totalPending);
        elements.statRejected.textContent = formatNumber(stats.totalRejected);
        elements.statTopics.textContent = formatNumber(stats.topicsActive);
    }

    function updateRejectionCount() {
        elements.rejectionCount.textContent = rejections.length > 0 ? rejections.length : '';
    }

    function updateTopicFilter() {
        const allTopics = new Set([
            ...events.map(e => e.topicName),
            ...rejections.map(r => r.topicName)
        ]);

        // Only update if topics changed
        if (setsEqual(knownTopics, allTopics)) return;

        knownTopics = allTopics;
        const currentValue = elements.topicFilter.value;

        elements.topicFilter.innerHTML = '<option value="">All Topics</option>';

        Array.from(knownTopics).sort().forEach(topic => {
            const option = document.createElement('option');
            option.value = topic;
            option.textContent = topic;
            elements.topicFilter.appendChild(option);
        });

        elements.topicFilter.value = currentValue;
    }

    function setsEqual(a, b) {
        if (a.size !== b.size) return false;
        for (const item of a) {
            if (!b.has(item)) return false;
        }
        return true;
    }

    function renderEventsList() {
        const filterTopic = elements.topicFilter.value;
        const filteredEvents = filterTopic
            ? events.filter(e => e.topicName === filterTopic)
            : events;

        if (filteredEvents.length === 0) {
            elements.emptyState.classList.remove('hidden');
            elements.eventsList.innerHTML = '';
            return;
        }

        elements.emptyState.classList.add('hidden');

        elements.eventsList.innerHTML = DOMPurify.sanitize(filteredEvents
            .map(event => renderEventItem(event))
            .join(''));

        // Attach click handlers
        elements.eventsList.querySelectorAll('.event-item').forEach(item => {
            item.addEventListener('click', () => selectEvent(item.dataset.eventId));
        });
    }

    function renderRejectionsList() {
        const filterTopic = elements.topicFilter.value;
        const filteredRejections = filterTopic
            ? rejections.filter(r => r.topicName === filterTopic)
            : rejections;

        if (filteredRejections.length === 0) {
            elements.emptyRejectionsState.classList.remove('hidden');
            elements.rejectionsList.innerHTML = '';
            return;
        }

        elements.emptyRejectionsState.classList.add('hidden');

        elements.rejectionsList.innerHTML = DOMPurify.sanitize(filteredRejections
            .map(rejection => renderRejectionItem(rejection))
            .join(''));

        // Attach click handlers
        elements.rejectionsList.querySelectorAll('.rejection-item').forEach(item => {
            item.addEventListener('click', () => selectRejection(item.dataset.rejectionId));
        });
    }

    function renderEventItem(event) {
        const isSelected = event.id === selectedEventId;
        const deliveryBadges = renderDeliveryBadges(event.deliveries || []);

        return `
            <div class="event-item ${isSelected ? 'selected' : ''}" data-event-id="${escapeHtml(event.id)}">
                <div class="event-item-header">
                    <span class="event-type">${escapeHtml(event.eventType)}</span>
                    <span class="event-time">${formatTime(event.receivedAt)}</span>
                </div>
                <div class="event-item-meta">
                    <span class="event-topic">${escapeHtml(event.topicName)}</span>
                    ${event.subject ? `<span class="event-subject">${escapeHtml(event.subject)}</span>` : ''}
                </div>
                ${deliveryBadges ? `<div class="event-delivery-status">${deliveryBadges}</div>` : ''}
            </div>
        `;
    }

    function renderRejectionItem(rejection) {
        const isSelected = rejection.id === selectedRejectionId;

        return `
            <div class="rejection-item ${isSelected ? 'selected' : ''}" data-rejection-id="${escapeHtml(rejection.id)}">
                <div class="rejection-item-header">
                    <span class="rejection-error">${escapeHtml(rejection.errorMessage)}</span>
                    <span class="rejection-time">${formatTime(rejection.rejectedAt)}</span>
                </div>
                <div class="rejection-meta">
                    <span class="event-topic">${escapeHtml(rejection.topicName)}</span>
                    <span class="rejection-status-code">HTTP ${rejection.statusCode}</span>
                </div>
            </div>
        `;
    }

    function renderDeliveryBadges(deliveries) {
        if (!deliveries.length) return '';

        return deliveries.map(d => {
            const statusClass = d.status.toLowerCase();
            return `<span class="delivery-badge ${statusClass}">${escapeHtml(d.subscriberName)}: ${formatStatus(d.status)}</span>`;
        }).join('');
    }

    async function selectEvent(eventId) {
        selectedEventId = eventId;
        selectedRejectionId = null;
        renderEventsList();

        const eventDetails = await fetchEventDetails(eventId);
        if (eventDetails) {
            renderEventDetails(eventDetails);
            elements.detailPanel.classList.add('open');
        }
    }

    function selectRejection(rejectionId) {
        selectedRejectionId = rejectionId;
        selectedEventId = null;
        renderRejectionsList();

        const rejection = rejections.find(r => r.id === rejectionId);
        if (rejection) {
            renderRejectionDetails(rejection);
            elements.detailPanel.classList.add('open');
        }
    }

    function closeDetailPanel() {
        selectedEventId = null;
        selectedRejectionId = null;
        elements.detailPanel.classList.remove('open');
        elements.detailContent.innerHTML = DOMPurify.sanitize('<p class="detail-placeholder">Select an event to view details</p>');
        renderEventsList();
        renderRejectionsList();
    }

    function renderEventDetails(event) {
        const html = `
            <div class="detail-section">
                <h4 class="detail-section-title">Event Information</h4>
                <div class="detail-row">
                    <span class="detail-label">Event ID</span>
                    <span class="detail-value">${escapeHtml(event.id)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Event Type</span>
                    <span class="detail-value">${escapeHtml(event.eventType)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Subject</span>
                    <span class="detail-value">${escapeHtml(event.subject || '-')}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Source</span>
                    <span class="detail-value">${escapeHtml(event.source || '-')}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Event Time</span>
                    <span class="detail-value">${formatDateTime(event.eventTime)}</span>
                </div>
            </div>

            <div class="detail-section">
                <h4 class="detail-section-title">Diagnostic Info</h4>
                <div class="detail-row">
                    <span class="detail-label">Received At</span>
                    <span class="detail-value">${formatDateTime(event.receivedAt)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Topic</span>
                    <span class="detail-value">${escapeHtml(event.topicName)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Port</span>
                    <span class="detail-value">${event.topicPort}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Schema</span>
                    <span class="detail-value">${escapeHtml(event.inputSchema)}</span>
                </div>
            </div>

            <div class="detail-section">
                <h4 class="detail-section-title">Payload</h4>
                <div class="payload-container">
                    <pre class="payload-json">${formatJson(event.payloadJson)}</pre>
                </div>
            </div>

            ${renderDeliverySection(event.deliveries || [])}
        `;

        elements.detailContent.innerHTML = DOMPurify.sanitize(html);

        // Attach attempt toggle handlers
        elements.detailContent.querySelectorAll('.attempt-toggle').forEach(toggle => {
            toggle.addEventListener('click', () => {
                const list = toggle.nextElementSibling;
                if (!list) return;
                list.classList.toggle('hidden');
                toggle.textContent = list.classList.contains('hidden')
                    ? `Show ${toggle.dataset.count} attempts`
                    : 'Hide attempts';
            });
        });
    }

    function renderRejectionDetails(rejection) {
        const html = `
            <div class="detail-section">
                <h4 class="detail-section-title">Rejection Details</h4>
                <div class="detail-row">
                    <span class="detail-label">Status Code</span>
                    <span class="detail-value rejection-status-code">HTTP ${rejection.statusCode}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Error Message</span>
                    <span class="detail-value" style="color: var(--color-error);">${escapeHtml(rejection.errorMessage)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Rejected At</span>
                    <span class="detail-value">${formatDateTime(rejection.rejectedAt)}</span>
                </div>
            </div>

            <div class="detail-section">
                <h4 class="detail-section-title">Request Info</h4>
                <div class="detail-row">
                    <span class="detail-label">Topic</span>
                    <span class="detail-value">${escapeHtml(rejection.topicName)}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Port</span>
                    <span class="detail-value">${rejection.topicPort}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">Content-Type</span>
                    <span class="detail-value">${escapeHtml(rejection.contentType || '-')}</span>
                </div>
            </div>

            ${rejection.rawBody ? `
            <div class="detail-section">
                <h4 class="detail-section-title">Request Body</h4>
                <div class="raw-body-container">
                    <pre class="raw-body-content">${escapeHtml(rejection.rawBody)}</pre>
                </div>
            </div>
            ` : ''}
        `;

        elements.detailContent.innerHTML = DOMPurify.sanitize(html);
    }

    function renderDeliverySection(deliveries) {
        if (!deliveries.length) {
            return `
                <div class="detail-section">
                    <h4 class="detail-section-title">Deliveries</h4>
                    <p style="color: var(--color-text-secondary); font-size: 13px;">No delivery attempts recorded</p>
                </div>
            `;
        }

        const deliveryItems = deliveries.map(d => `
            <div class="delivery-item">
                <div class="delivery-item-header">
                    <span class="delivery-subscriber">${escapeHtml(d.subscriberName)}</span>
                    <span class="delivery-badge ${d.status.toLowerCase()}">${formatStatus(d.status)}</span>
                </div>
                <div class="delivery-endpoint">${escapeHtml(d.endpoint)} (${escapeHtml(d.subscriberType)})</div>
                ${d.completedAt ? `<div class="detail-row"><span class="detail-label">Completed</span><span class="detail-value">${formatDateTime(d.completedAt)}</span></div>` : ''}
                ${renderAttempts(d.attempts || [])}
            </div>
        `).join('');

        return `
            <div class="detail-section">
                <h4 class="detail-section-title">Deliveries</h4>
                <div class="delivery-list">${deliveryItems}</div>
            </div>
        `;
    }

    function renderAttempts(attempts) {
        if (!attempts.length) return '';

        const attemptItems = attempts.map(a => `
            <div class="attempt-item">
                <span class="attempt-time">${formatDateTime(a.attemptTime)}</span>
                <span class="status-${a.outcome.toLowerCase()}">${formatStatus(a.outcome)}</span>
                ${a.statusCode ? ` (HTTP ${a.statusCode})` : ''}
                ${a.errorMessage ? `<div class="attempt-error">${escapeHtml(a.errorMessage)}</div>` : ''}
            </div>
        `).join('');

        return `
            <div class="delivery-attempts">
                <button class="attempt-toggle" data-count="${attempts.length}">Show ${attempts.length} attempt${attempts.length > 1 ? 's' : ''}</button>
                <div class="attempt-list hidden">${attemptItems}</div>
            </div>
        `;
    }

    // Utility Functions
    function formatNumber(num) {
        return (num || 0).toLocaleString();
    }

    function formatTime(isoString) {
        if (!isoString) return '-';
        const date = new Date(isoString);
        return date.toLocaleTimeString();
    }

    function formatDateTime(isoString) {
        if (!isoString) return '-';
        const date = new Date(isoString);
        return date.toLocaleString();
    }

    function formatStatus(status) {
        const statusMap = {
            'Pending': 'Pending',
            'InProgress': 'In Progress',
            'Delivered': 'Delivered',
            'Retrying': 'Retrying',
            'Failed': 'Failed',
            'DeadLettered': 'Dead Lettered',
            'Success': 'Success',
            'Failure': 'Failed',
            'Timeout': 'Timeout',
            'Exception': 'Exception',
        };
        return statusMap[status] || status;
    }

    function formatJson(jsonString) {
        if (!jsonString) return '';
        try {
            const parsed = JSON.parse(jsonString);
            return escapeHtml(JSON.stringify(parsed, null, 2));
        } catch {
            return escapeHtml(jsonString);
        }
    }

    function escapeHtml(text) {
        if (text === null || text === undefined) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Start the application
    document.addEventListener('DOMContentLoaded', init);
})();
