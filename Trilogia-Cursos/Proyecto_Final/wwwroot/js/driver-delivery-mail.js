(function (root) {
    "use strict";

    var states = Object.freeze(["SmtpAccepted", "Pending", "Sending", "Unknown", "AlreadyRequested", "None", "NoEvent"]);

    function canSend(state) {
        return state === "None" || state === "Pending";
    }

    function messageFor(state) {
        switch (state) {
            case "SmtpAccepted": return "El servidor de correo aceptó el aviso; esto no confirma que llegó al buzón.";
            case "Pending": return "El mismo intento sigue pendiente. Podés continuarlo.";
            case "Sending": return "El envío está en curso. Consultá el estado antes de otra acción.";
            case "Unknown": return "No se confirmó el resultado. No se reenviará automáticamente para evitar duplicados.";
            case "AlreadyRequested": return "Hay una solicitud previa para este evento. No se creó otro envío.";
            case "None": return "Esta sesión no tiene un intento para el evento. Podés solicitar el primer aviso.";
            case "NoEvent": return "No hay una actualización confirmada para notificar.";
            default: return "No se pudo confirmar el estado del aviso. Consultá el recibo.";
        }
    }

    function createController(options) {
        var document = options.document;
        var fetcher = options.fetch;
        var config = options.config || {};
        var toast = document.getElementById("driverToast");

        function showMessage(text, isError) {
            if (toast) {
                toast.textContent = text;
                toast.className = "djj-field-toast" + (isError ? " djj-field-toast--error" : "");
                toast.hidden = false;
            }
        }

        function post(url, routeOrderId) {
            var body = new URLSearchParams();
            body.set("rutaPedidoId", routeOrderId);
            body.set("__RequestVerificationToken", config.token || "");
            return fetcher(url, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body: body.toString()
            }).then(function (response) {
                if (response.redirected || response.status === 401 || response.status === 403 || response.status === 404) {
                    var denied = new Error("No se pudo consultar el aviso con la sesión y autorización actuales.");
                    denied.safeMessage = denied.message;
                    throw denied;
                }
                if (!response.ok) throw new Error("No se pudo consultar el aviso.");
                return response.json();
            });
        }

        function init() {
            if (!config.enabled || !document.querySelectorAll) return;
            Array.prototype.forEach.call(document.querySelectorAll("[data-delivery-notice]"), function (panel) {
                var id = panel.getAttribute("data-route-order-id");
                var receipt = panel.querySelector("[data-delivery-receipt]");
                var send = panel.querySelector("[data-delivery-send]");
                var status = panel.querySelector("[data-delivery-notice-status]");
                var currentState = null;
                var busy = false;

                if (send) { send.hidden = true; send.disabled = true; }
                if (status) status.hidden = true;

                function render(state) {
                    currentState = states.indexOf(state) >= 0 ? state : null;
                    if (status) {
                        status.textContent = messageFor(currentState);
                        status.hidden = false;
                    }
                    if (send) {
                        send.hidden = !canSend(currentState);
                        send.disabled = busy || !canSend(currentState);
                        send.textContent = currentState === "Pending" ? "Continuar mismo intento" : "Enviar aviso";
                    }
                }

                function setBusy(value, activeButton) {
                    busy = value;
                    [receipt, send].forEach(function (button) {
                        if (!button) return;
                        button.disabled = value || (button === send && !canSend(currentState));
                        if (button === activeButton) {
                            button.setAttribute("aria-busy", value ? "true" : "false");
                            if (value) button.dataset.previousText = button.textContent;
                            else if (button.dataset.previousText && (button.textContent === "Consultando…" || button.textContent === "Procesando…")) {
                                button.textContent = button.dataset.previousText;
                                delete button.dataset.previousText;
                            }
                            if (value) button.textContent = button === receipt ? "Consultando…" : "Procesando…";
                        }
                    });
                }

                function run(url, button) {
                    if (busy) return;
                    if (button === send && !canSend(currentState)) return;
                    setBusy(true, button);
                    post(url, id).then(function (data) {
                        if (!data || data.ok !== true || states.indexOf(data.state) < 0) {
                            throw new Error("No se pudo confirmar el estado del aviso.");
                        }
                        render(data.state);
                        showMessage(messageFor(data.state), false);
                    }).catch(function (error) {
                        if (status) {
                            status.textContent = error.safeMessage || "No se confirmó el estado del aviso. Consultá el recibo antes de continuar.";
                            status.hidden = false;
                        }
                        if (send) { send.hidden = true; send.disabled = true; }
                        showMessage(error.safeMessage || "No se confirmó el estado del aviso. Consultá el recibo antes de continuar.", true);
                    }).then(function () { setBusy(false, button); });
                }

                if (receipt) receipt.addEventListener("click", function () { run(config.receiptUrl, receipt); });
                if (send) send.addEventListener("click", function () { run(config.notifyUrl, send); });
            });
        }

        return { init: init };
    }

    var api = Object.freeze({ canSend: canSend, messageFor: messageFor, createController: createController });
    if (typeof module !== "undefined" && module.exports) module.exports = api;
    if (root) {
        root.DeliveryMailUiInternals = api;
        function start() {
            var config = root.driverDeliveryMailConfig;
            if (!config || !config.enabled) return;
            createController({ document: root.document, fetch: root.fetch.bind(root), config: config }).init();
        }
        if (root.document && root.document.readyState === "loading") root.document.addEventListener("DOMContentLoaded", start);
        else if (root.document) start();
    }
})(typeof window !== "undefined" ? window : null);
