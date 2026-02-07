// Live countdown banner for server-enforced session tickets.
// Banner is positioned below the page header/navbar by computing its bottom.
// Loads with credentials and shows a per-second local countdown synchronized with server.

(function () {
    const sessionRemainingUrl = '/Session/Remaining';
    const keepAliveUrl = '/Session/KeepAlive';
    const pollServerMs = 10000;    // periodic server sync (ms)
    const initialPollMs = 500;     // first quick poll
    const showThresholdSec = 30;   // show banner when remaining <= this

    let remaining = null;
    let localTimer = null;
    let banner = null;

    function computeBannerTop() {
        // Prefer <header>, then .navbar, then first fixed/top element
        const header = document.querySelector('header') ||
                       document.querySelector('.navbar') ||
                       document.querySelector('nav') ||
                       null;
        if (!header) return 0;
        const rect = header.getBoundingClientRect();
        // rect.bottom is relative to viewport; when using fixed banner, top should be rect.bottom px
        return Math.max(0, Math.ceil(rect.bottom));
    }

    function applyBannerPosition() {
        if (!banner) return;
        const top = computeBannerTop();
        banner.style.top = top + 'px';
        // ensure banner does not overlap footer area when window is very small
        banner.style.width = '100%';
    }

    function createBanner() {
        if (banner) return banner;
        banner = document.createElement('div');
        banner.id = 'session-timeout-banner';
        banner.style.position = 'fixed';
        banner.style.left = '0';
        banner.style.right = '0';
        banner.style.zIndex = '2000';
        banner.style.background = '#ffc107';
        banner.style.color = '#000';
        banner.style.padding = '0.6rem 1rem';
        banner.style.display = 'none';
        banner.style.alignItems = 'center';
        banner.style.justifyContent = 'space-between';
        banner.style.boxShadow = '0 2px 6px rgba(0,0,0,0.12)';
        banner.style.boxSizing = 'border-box';
        banner.innerHTML = '<div id="session-timeout-text">Your session will expire in <strong id="session-timeout-seconds">--</strong>s</div>' +
            '<div style="margin-left:1rem">' +
            '<button id="session-keepalive-btn" class="btn btn-sm btn-primary" style="margin-right:0.5rem">Stay signed in</button>' +
            '<button id="session-logout-btn" class="btn btn-sm btn-outline-dark">Sign out</button>' +
            '</div>';
        document.body.appendChild(banner);

        // position correctly now
        applyBannerPosition();

        // Recompute on resize/scroll in case header size/position changes
        window.addEventListener('resize', applyBannerPosition, { passive: true });
        window.addEventListener('scroll', applyBannerPosition, { passive: true });

        document.getElementById('session-keepalive-btn').addEventListener('click', function () {
            fetch(keepAliveUrl, { credentials: 'same-origin' })
                .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
                .then(data => {
                    if (data && data.ok) {
                        setRemaining(data.remaining);
                        hideBannerIfNotNeeded();
                    }
                }).catch(() => { /* ignore */ });
        });

        document.getElementById('session-logout-btn').addEventListener('click', function () {
            window.location.href = '/Account/Logout';
        });

        return banner;
    }

    function showBanner() {
        createBanner();
        applyBannerPosition();
        banner.style.display = 'flex';
    }

    function hideBanner() {
        if (!banner) return;
        banner.style.display = 'none';
    }

    function updateDisplay() {
        const el = document.getElementById('session-timeout-seconds');
        if (!el) return;
        el.textContent = remaining !== null ? String(Math.max(0, remaining)) : '--';
    }

    function tick() {
        if (remaining === null) return;
        if (remaining <= 0) {
            updateDisplay();
            // session expired: redirect to login page
            window.location.href = '/Account/Login';
            return;
        }
        remaining--;
        updateDisplay();
        if (remaining <= showThresholdSec) {
            showBanner();
        }
        if (remaining > showThresholdSec) {
            hideBanner();
        }
    }

    function setRemaining(sec) {
        remaining = typeof sec === 'number' ? Math.max(0, sec) : null;
        updateDisplay();
    }

    function hideBannerIfNotNeeded() {
        if (remaining === null || remaining > showThresholdSec) {
            hideBanner();
        }
    }

    function fetchRemaining() {
        return fetch(sessionRemainingUrl, { credentials: 'same-origin' })
            .then(r => {
                if (!r.ok) throw new Error('network');
                return r.json();
            })
            .then(data => {
                if (data && typeof data.remaining === 'number') {
                    setRemaining(data.remaining);
                }
            })
            .catch(() => {
                // ignore; keep local countdown running if any
            });
    }

    // Start local per-second timer and initial server sync
    function start() {
        // initial quick sync
        setTimeout(() => fetchRemaining().then(() => applyBannerPosition()), initialPollMs);

        // start per-second tick
        if (localTimer) clearInterval(localTimer);
        localTimer = setInterval(tick, 1000);

        // periodic server resync to correct drift
        setInterval(fetchRemaining, pollServerMs);
    }

    // Start only if the script runs in a document with a logged-in user.
    document.addEventListener('DOMContentLoaded', function () {
        fetchRemaining().finally(() => start());
    });
})();