// JellyShowcase: pulsante "Richiedi" nella scheda dei titoli della Vetrina (client web e app basate sul web).
// Lo stato arriva da /JellyShowcase/Status/{id}; la richiesta parte come l'utente collegato e resta
// in attesa finché un amministratore non la approva in Seerr.
(function () {
    'use strict';

    var STATES = {
        none: { icon: 'add_circle', text: 'Richiedi', action: true },
        declined: { icon: 'add_circle', text: 'Richiedi di nuovo', action: true },
        pending: { icon: 'hourglass_top', text: 'In attesa di approvazione' },
        processing: { icon: 'download', text: 'In download' },
        partial: { icon: 'download', text: 'In parte disponibile' },
        available: { icon: 'check_circle', text: 'Disponibile' }
    };

    var loading = null;

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
        button.dataset.itemId = itemId;
        button.dataset.status = status;
        button.disabled = !state.action;
        button.style.cursor = state.action ? 'pointer' : 'default';
        button.style.background = state.action ? '#00a4dc' : 'rgba(255,255,255,.18)';
        button.title = message || state.text;
        button.innerHTML = '<span class="material-icons" aria-hidden="true">' + state.icon + '</span>'
            + '<span>' + (message || state.text) + '</span>';
    }

    function onClick(event) {
        var button = event.currentTarget;
        var itemId = button.dataset.itemId;
        var container = button.parentNode;
        if (button.disabled || !itemId) return;
        button.disabled = true;
        button.lastChild.textContent = 'Invio…';
        api('POST', 'JellyShowcase/Request/' + itemId).then(function (data) {
            render(container, itemId, data.status || 'pending');
        }).catch(function (response) {
            var fallback = 'Richiesta non riuscita';
            var done = function (msg) {
                render(container, itemId, button.dataset.status, msg || fallback);
                setTimeout(function () { render(container, itemId, button.dataset.status); }, 5000);
            };
            if (response && typeof response.json === 'function') {
                response.json().then(function (body) { done(body && body.message); }, function () { done(); });
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
        }).catch(function () { /* titolo non della Vetrina o server non raggiungibile */ })
            .then(function () { loading = null; });
    }

    document.addEventListener('viewshow', function () { setTimeout(refresh, 300); });
    window.addEventListener('hashchange', function () { setTimeout(refresh, 300); });
    setInterval(refresh, 1500);
})();
