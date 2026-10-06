// JellyShowcase: "Request" button on discover items in the web client (and web-based apps).
// The status comes from /JellyShowcase/Status/{id}; the request is made as the logged-in user and
// stays pending until an administrator approves it in Seerr.
// Texts follow the Jellyfin UI language (Italian and English, English for any other language).
(function () {
    'use strict';

    var TEXTS = {
        en: {
            none: 'Request', declined: 'Request again', pending: 'Pending approval',
            processing: 'Downloading', partial: 'Partially available', available: 'Available',
            sending: 'Sending…',
            notShowcase: 'This item is not in the discover library.',
            notLinked: 'Your user is not linked to Seerr: ask the administrator.',
            failed: 'Request failed: you may have reached your request limit. Try again later.'
        },
        it: {
            none: 'Richiedi', declined: 'Richiedi di nuovo', pending: 'In attesa di approvazione',
            processing: 'In download', partial: 'In parte disponibile', available: 'Disponibile',
            sending: 'Invio…',
            notShowcase: 'Questo titolo non è nella Vetrina.',
            notLinked: 'Il tuo utente non è collegato a Seerr: chiedi all\'amministratore.',
            failed: 'Richiesta non riuscita: forse hai raggiunto il limite di richieste. Riprova più tardi.'
        }
    };

    var STATES = {
        none: { icon: 'add_circle', action: true },
        declined: { icon: 'add_circle', action: true },
        pending: { icon: 'hourglass_top' },
        processing: { icon: 'download' },
        partial: { icon: 'download' },
        available: { icon: 'check_circle' }
    };

    var loading = null;

    function t(key) {
        var lang = (document.documentElement.getAttribute('lang') || navigator.language || 'en').toLowerCase();
        var texts = TEXTS[lang.split('-')[0]] || TEXTS.en;
        return texts[key] || TEXTS.en[key] || key;
    }

    function currentItemId() {
        var m = window.location.hash.match(/[?&]id=([0-9a-f]{32}|[0-9a-f-]{36})/i);
        return m ? m[1].replace(/-/g, '').toLowerCase() : null;
    }

    function buttonContainer() {
        var page = document.querySelector('.itemDetailPage:not(.hide)');
        return page ? page.querySelector('.mainDetailButtons') : null;
    }

    function api(method, path) {
        return window.ApiClient.ajax({ type: method, url: window.ApiClient.getUrl(path), dataType: 'json' });
    }

    function render(container, itemId, status, message) {
        var state = STATES[status];
        var button = container.querySelector('.jellyShowcaseRequest');
        if (!state) {
            if (button) button.remove();
            return;
        }
        if (!button) {
            button = document.createElement('button');
            button.type = 'button';
            button.className = 'jellyShowcaseRequest';
            button.style.cssText = 'display:inline-flex;align-items:center;gap:.4em;margin:.3em .6em .3em 0;'
                + 'padding:.45em 1.1em;border:0;border-radius:2em;font:inherit;font-weight:600;cursor:pointer;'
                + 'color:#fff;background:rgba(255,255,255,.18)';
            button.addEventListener('click', onClick);
            container.insertBefore(button, container.firstChild);
        }
        var text = message || t(status);
        button.dataset.itemId = itemId;
        button.dataset.status = status;
        button.disabled = !state.action;
        button.style.cursor = state.action ? 'pointer' : 'default';
        button.style.background = state.action ? '#00a4dc' : 'rgba(255,255,255,.18)';
        button.title = text;
        button.innerHTML = '<span class="material-icons" aria-hidden="true">' + state.icon + '</span><span></span>';
        button.lastChild.textContent = text;
    }

    function onClick(event) {
        var button = event.currentTarget;
        var itemId = button.dataset.itemId;
        var container = button.parentNode;
        if (button.disabled || !itemId) return;
        button.disabled = true;
        button.lastChild.textContent = t('sending');
        api('POST', 'JellyShowcase/Request/' + itemId).then(function (data) {
            render(container, itemId, data.status || 'pending');
        }).catch(function (response) {
            var done = function (code) {
                render(container, itemId, button.dataset.status, t(code || 'failed'));
                setTimeout(function () { render(container, itemId, button.dataset.status); }, 5000);
            };
            if (response && typeof response.json === 'function') {
                response.json().then(function (body) { done(body && body.code); }, function () { done(); });
            } else {
                done();
            }
        });
    }

    function refresh() {
        var itemId = currentItemId();
        var container = buttonContainer();
        if (!itemId || !container || !window.ApiClient) return;
        var existing = container.querySelector('.jellyShowcaseRequest');
        if ((existing && existing.dataset.itemId === itemId) || loading === itemId) return;
        if (existing) existing.remove();
        loading = itemId;
        api('GET', 'JellyShowcase/Status/' + itemId).then(function (data) {
            if (currentItemId() === itemId && data && data.showcase) {
                render(container, itemId, data.status);
            }
        }).catch(function () { /* not a discover item, or server unreachable */ })
            .then(function () { loading = null; });
    }

    document.addEventListener('viewshow', function () { setTimeout(refresh, 300); });
    window.addEventListener('hashchange', function () { setTimeout(refresh, 300); });
    setInterval(refresh, 1500);
})();
