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
    // Bumped whenever polling starts or stops, so a poll that is still in flight doesn't
    // schedule another tick for a loop that has since been stopped or replaced
    let pollGeneration = 0;
    // The last response text for each list, so a poll that brings nothing new doesn't re-render
    let lastEventsText = null;
    let lastRejectionsText = null;
    let knownTopics = new Set();

    // DOM Elements
    const elements = {
        autoRefresh: document.getElementById('autoRefresh'),
        refreshIndicator: document.getElementById('refreshIndicator'),
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
    async function init() {
        setupEventListeners();

        // Start polling once the first fetch is done, so the first poll can't overlap it
        await fetchData();
        if (autoRefreshEnabled) {
            startAutoRefresh();
        }
    }

    function setupEventListeners() {
        elements.autoRefresh.addEventListener('change', handleAutoRefreshToggle);
        elements.closeDetail.addEventListener('click', closeDetailPanel);
        elements.topicFilter.addEventListener('change', handleTopicFilterChange);
        elements.tabEvents.addEventListener('click', () => switchTab('events'));
        elements.tabRejections.addEventListener('click', () => switchTab('rejections'));
        elements.clearBtn.addEventListener('click', clearHistory);

        // One listener per container, so re-rendering the items doesn't mean re-attaching them
        elements.eventsList.addEventListener('click', e => {
            const item = e.target.closest('.event-item');
            if (item) selectEvent(item.dataset.eventId);
        });
        elements.rejectionsList.addEventListener('click', e => {
            const item = e.target.closest('.rejection-item');
            if (item) selectRejection(item.dataset.rejectionId);
        });
        elements.detailContent.addEventListener('click', e => {
            const toggle = e.target.closest('.attempt-toggle');
            if (toggle) toggleAttempts(toggle);
        });

        // Keyboard navigation
        document.addEventListener('keydown', handleKeyDown);
    }

    async function clearHistory() {
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
                lastEventsText = null;
                lastRejectionsText = null;
                closeDetailPanel(); // also re-renders both lists
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
        elements.tabEvents.setAttribute('aria-selected', String(tab === 'events'));
        elements.tabRejections.setAttribute('aria-selected', String(tab === 'rejections'));
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

    // Each poll schedules the next one only after it has finished, so slow polls can't overlap
    function startAutoRefresh() {
        stopAutoRefresh();
        scheduleNextPoll(++pollGeneration);
    }

    function scheduleNextPoll(generation) {
        refreshTimer = setTimeout(async () => {
            await fetchData();
            if (generation === pollGeneration) {
                scheduleNextPoll(generation);
            }
        }, REFRESH_INTERVAL);
    }

    function stopAutoRefresh() {
        pollGeneration++;
        clearTimeout(refreshTimer);
        refreshTimer = null;
    }

    function handleTopicFilterChange() {
        renderEventsList();
        renderRejectionsList();
    }

    function handleKeyDown(e) {
        // Leave the arrow keys to form controls that use them, so they still change the topic
        // filter. Escape still closes the panel, and checkboxes don't use the arrow keys.
        const usesArrowKeys = 'select, textarea, input:not([type="checkbox"])';
        if (e.key !== 'Escape' && e.target instanceof Element && e.target.closest(usesArrowKeys)) return;

        const list = getVisibleItems();
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

            // Read all three responses before rendering, so the topic filter sees this poll's
            // rejections as well as its events
            const eventsText = eventsResponse.ok ? await eventsResponse.text() : null;
            const stats = statsResponse.ok ? await statsResponse.json() : null;
            const rejectionsText = rejectionsResponse.ok ? await rejectionsResponse.text() : null;

            // Only parse and re-render a list when its response has changed since the last poll
            const eventsChanged = eventsText !== null && eventsText !== lastEventsText;
            const rejectionsChanged = rejectionsText !== null && rejectionsText !== lastRejectionsText;

            if (eventsChanged) {
                events = JSON.parse(eventsText);
                lastEventsText = eventsText;
            }

            if (rejectionsChanged) {
                rejections = JSON.parse(rejectionsText);
                lastRejectionsText = rejectionsText;
            }

            if (eventsChanged || rejectionsChanged) {
                updateTopicFilter();
            }

            if (eventsChanged) {
                renderEventsList();
            }

            if (stats) {
                renderStats(stats);
            }

            if (rejectionsChanged) {
                renderRejectionsList();
                updateRejectionCount();
            }
        } catch (error) {
            console.error('Failed to fetch data:', error);
        }
    }

    async function fetchEventDetails(eventId) {
        try {
            const response = await fetch(`${API_BASE}/events/${encodeURIComponent(eventId)}`);
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

    // The events or rejections shown in a tab, narrowed by the topic filter
    function getVisibleItems(tab = activeTab) {
        const items = tab === 'events' ? events : rejections;
        const filterTopic = elements.topicFilter.value;
        return filterTopic ? items.filter(item => item.topicName === filterTopic) : items;
    }

    function renderEventsList() {
        const filteredEvents = getVisibleItems('events');

        if (filteredEvents.length === 0) {
            elements.emptyState.classList.remove('hidden');
            elements.eventsList.innerHTML = '';
            return;
        }

        elements.emptyState.classList.add('hidden');

        elements.eventsList.innerHTML = DOMPurify.sanitize(filteredEvents
            .map(event => renderEventItem(event))
            .join(''));
    }

    function renderRejectionsList() {
        const filteredRejections = getVisibleItems('rejections');

        if (filteredRejections.length === 0) {
            elements.emptyRejectionsState.classList.remove('hidden');
            elements.rejectionsList.innerHTML = '';
            return;
        }

        elements.emptyRejectionsState.classList.add('hidden');

        elements.rejectionsList.innerHTML = DOMPurify.sanitize(filteredRejections
            .map(rejection => renderRejectionItem(rejection))
            .join(''));
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

        // Another item was selected, or the panel was closed, while this request was in flight
        if (selectedEventId !== eventId) return;

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

    // A label/value row in the detail panel. valueHtml must already be escaped.
    function detailRow(label, valueHtml, valueClass = '') {
        const className = valueClass ? `detail-value ${valueClass}` : 'detail-value';
        return `
            <div class="detail-row">
                <span class="detail-label">${label}</span>
                <span class="${className}">${valueHtml}</span>
            </div>
        `;
    }

    function renderEventDetails(event) {
        const html = `
            <div class="detail-section">
                <h4 class="detail-section-title">Event Information</h4>
                ${detailRow('Event ID', escapeHtml(event.id))}
                ${detailRow('Event Type', escapeHtml(event.eventType))}
                ${detailRow('Subject', escapeHtml(event.subject || '-'))}
                ${detailRow('Source', escapeHtml(event.source || '-'))}
                ${detailRow('Event Time', formatDateTime(event.eventTime))}
            </div>

            <div class="detail-section">
                <h4 class="detail-section-title">Diagnostic Info</h4>
                ${detailRow('Received At', formatDateTime(event.receivedAt))}
                ${detailRow('Topic', escapeHtml(event.topicName))}
                ${detailRow('Port', event.topicPort)}
                ${detailRow('Schema', escapeHtml(event.inputSchema))}
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
    }

    function toggleAttempts(toggle) {
        const list = toggle.nextElementSibling;
        if (!list) return;
        const hidden = list.classList.toggle('hidden');
        toggle.setAttribute('aria-expanded', String(!hidden));
        toggle.textContent = hidden
            ? `Show ${toggle.dataset.count} attempts`
            : 'Hide attempts';
    }

    function renderRejectionDetails(rejection) {
        const html = `
            <div class="detail-section">
                <h4 class="detail-section-title">Rejection Details</h4>
                ${detailRow('Status Code', `HTTP ${rejection.statusCode}`, 'rejection-status-code')}
                ${detailRow('Error Message', escapeHtml(rejection.errorMessage), 'detail-value-error')}
                ${detailRow('Rejected At', formatDateTime(rejection.rejectedAt))}
            </div>

            <div class="detail-section">
                <h4 class="detail-section-title">Request Info</h4>
                ${detailRow('Topic', escapeHtml(rejection.topicName))}
                ${detailRow('Port', rejection.topicPort)}
                ${detailRow('Content-Type', escapeHtml(rejection.contentType || '-'))}
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
                    <p class="detail-note">No delivery attempts recorded</p>
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
                ${d.completedAt ? detailRow('Completed', formatDateTime(d.completedAt)) : ''}
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
                <button aria-expanded="false" class="attempt-toggle" data-count="${attempts.length}">Show ${attempts.length} attempt${attempts.length > 1 ? 's' : ''}</button>
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
            'Timeout': 'Timeout',
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

    // Escapes quotes as well as &, < and >, so the result is safe inside attribute values
    // (event ids, for example, can contain any character).
    const HTML_ESCAPES = {'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'};

    function escapeHtml(text) {
        if (text === null || text === undefined) return '';
        return String(text).replace(/[&<>"']/g, c => HTML_ESCAPES[c]);
    }

    // Start the application
    document.addEventListener('DOMContentLoaded', init);
})();
