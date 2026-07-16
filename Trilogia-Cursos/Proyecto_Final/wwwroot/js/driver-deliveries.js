/*
 * CU-082 E2 — Actualización de estado de entrega con soporte offline.
 * Los cambios se encolan en localStorage y se reenvían al recuperar señal.
 * La idempotencia se garantiza en el servidor con un SyncGuid único por cambio.
 */
(function () {
    "use strict";

    var cfg = window.driverDeliveriesConfig || {};
    var QUEUE_KEY = "driverDeliveryQueue";
    var flushing = false;

    function uuid() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }
        // Respaldo RFC4122 v4
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            var r = (Math.random() * 16) | 0;
            var v = c === "x" ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    }

    function getQueue() {
        try { return JSON.parse(localStorage.getItem(QUEUE_KEY)) || []; }
        catch (e) { return []; }
    }

    function setQueue(q) {
        localStorage.setItem(QUEUE_KEY, JSON.stringify(q));
    }

    function enqueue(item) {
        var q = getQueue();
        q.push(item);
        setQueue(q);
        updatePendingBadge();
    }

    function updatePendingBadge() {
        var badge = document.getElementById("pendingSyncBadge");
        if (!badge) return;
        var count = getQueue().length;
        badge.textContent = count;
        badge.parentElement.style.display = count > 0 ? "inline-block" : "none";
    }

    function updateConnectionBadge() {
        var badge = document.getElementById("connectionBadge");
        if (!badge) return;
        if (navigator.onLine) {
            badge.className = "badge badge-success";
            badge.innerHTML = '<i class="fa fa-wifi mr-1"></i>En línea';
        } else {
            badge.className = "badge badge-secondary";
            badge.innerHTML = '<i class="fa fa-plug mr-1"></i>Sin conexión';
        }
    }

    function labelEstado(estado) {
        if (estado === "EnRuta") return "En ruta";
        return estado;
    }

    function setCardState(card, estado, pending) {
        card.setAttribute("data-estado", estado);
        var label = card.querySelector(".delivery-status-label");
        if (label) {
            label.textContent = pending ? (labelEstado(estado) + " (pendiente de sincronizar)") : labelEstado(estado);
        }
        // Deshabilitar acciones si el estado es final
        if (estado === "Entregado" || estado === "Fallido") {
            card.querySelectorAll("[data-action]").forEach(function (b) { b.disabled = true; });
        }
    }

    function postUpdate(item) {
        var body = new URLSearchParams();
        body.append("rutaPedidoId", item.rutaPedidoId);
        body.append("nuevoEstado", item.nuevoEstado);
        body.append("syncGuid", item.syncGuid);
        if (item.motivoFallo) body.append("motivoFallo", item.motivoFallo);
        if (cfg.token) body.append("__RequestVerificationToken", cfg.token);

        return fetch(cfg.updateUrl, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: body.toString()
        }).then(function (resp) {
            if (!resp.ok) throw new Error("HTTP " + resp.status);
            return resp.json();
        });
    }

    function flush() {
        if (flushing) return Promise.resolve();
        if (!navigator.onLine) { updateConnectionBadge(); return Promise.resolve(); }

        flushing = true;
        var appliedOnline = false;

        function step() {
            var q = getQueue();
            if (q.length === 0) {
                flushing = false;
                updatePendingBadge();
                if (appliedOnline) {
                    // Recargar para reflejar el estado autoritativo del servidor.
                    window.location.reload();
                }
                return Promise.resolve();
            }

            var item = q[0];
            return postUpdate(item).then(function (data) {
                var rest = getQueue();
                rest.shift();
                setQueue(rest);
                updatePendingBadge();
                if (data && data.ok) {
                    appliedOnline = true;
                } else if (data && data.message) {
                    // Error de negocio (no reintentable): informar y continuar.
                    showToast(data.message, true);
                }
                return step();
            }).catch(function () {
                // Error de red: conservar la cola y detener el reenvío.
                flushing = false;
                updateConnectionBadge();
                updatePendingBadge();
                return Promise.resolve();
            });
        }

        return step();
    }

    function showToast(message, isError) {
        var box = document.getElementById("driverToast");
        if (!box) { alert(message); return; }
        box.textContent = message;
        box.className = "alert " + (isError ? "alert-danger" : "alert-success");
        box.style.display = "block";
        setTimeout(function () { box.style.display = "none"; }, 4000);
    }

    function handleAction(card, estado, motivo) {
        var item = {
            syncGuid: uuid(),
            rutaPedidoId: parseInt(card.getAttribute("data-rutapedidoid"), 10),
            nuevoEstado: estado,
            motivoFallo: motivo || null
        };
        enqueue(item);
        setCardState(card, estado, !navigator.onLine);
        flush();
        if (!navigator.onLine) {
            showToast("Cambio guardado localmente. Se sincronizará al recuperar señal.", false);
        }
    }

    function init() {
        updateConnectionBadge();
        updatePendingBadge();

        document.querySelectorAll(".delivery-card").forEach(function (card) {
            card.querySelectorAll("[data-action]").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    var action = btn.getAttribute("data-action");
                    if (action === "Fallido") {
                        var box = card.querySelector(".fallido-box");
                        if (box) { box.hidden = false; return; }
                    }
                    if (action === "Fallido-confirm") {
                        var input = card.querySelector(".fallido-motivo");
                        var motivo = input ? input.value.trim() : "";
                        if (!motivo) { if (input) input.focus(); showToast("Indique el motivo del fallo.", true); return; }
                        handleAction(card, "Fallido", motivo);
                        return;
                    }
                    handleAction(card, action, null);
                });
            });
        });

        window.addEventListener("online", function () { updateConnectionBadge(); flush(); });
        window.addEventListener("offline", updateConnectionBadge);

        // Intento inicial de reenvío de pendientes de sesiones anteriores.
        flush();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
