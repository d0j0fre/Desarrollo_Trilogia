(function (root) {
  "use strict";

  var states = Object.freeze(["None", "NoRecipient", "AlreadyRequested", "Sending", "Unknown", "SmtpAccepted", "Pending"]);
  var uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

  function canContinue(state, eligible) {
    return state === "Pending" || state === "None" && eligible === true;
  }

  function messageFor(state) {
    switch (state) {
      case "None": return "No hay una solicitud previa. Podés crear el primer aviso de este evento.";
      case "Pending": return "El mismo intento está pendiente. Podés continuarlo manualmente.";
      case "NoRecipient": return "No hay destinatario disponible; no se creó un envío.";
      case "AlreadyRequested": return "Este evento ya tiene una solicitud registrada. No se iniciará otra.";
      case "Sending": return "El intento está en curso. Consultá su estado antes de continuar.";
      case "Unknown": return "El resultado no está confirmado. Consultá de nuevo; no se reintentará automáticamente.";
      case "SmtpAccepted": return "El servidor SMTP aceptó el aviso; esto no confirma su entrega al buzón.";
      default: return "No se pudo confirmar el estado. Consultá el recibo antes de continuar.";
    }
  }

  function createController(options) {
    var document = options.document;
    var fetcher = options.fetch;
    var config = options.config || {};
    var toast = document.querySelector("[data-route-dispatch-toast]");

    function notify(text) {
      if (!toast) return;
      toast.textContent = text;
      toast.hidden = false;
    }

    function readSelectors(panel) {
      var routeId = panel.getAttribute("data-route-id") || "";
      var dispatchId = panel.getAttribute("data-dispatch-id") || "";
      var eventId = panel.getAttribute("data-event-id") || "";
      var eventGuid = panel.getAttribute("data-event-guid") || "";
      if (!/^\d+$/.test(routeId) || Number(routeId) <= 0 || !uuidPattern.test(dispatchId)
          || !/^\d+$/.test(eventId) || Number(eventId) <= 0 || !uuidPattern.test(eventGuid)) return null;
      return { routeId: routeId, dispatchId: dispatchId, eventId: eventId, eventGuid: eventGuid };
    }

    function post(url, panel) {
      var selectors = readSelectors(panel);
      var csrf = panel.querySelector('input[name="__RequestVerificationToken"]');
      if (!selectors || !url || !csrf || !csrf.value) throw new Error("invalid-context");
      var body = new URLSearchParams();
      body.set("rutaId", selectors.routeId);
      body.set("dispatchId", selectors.dispatchId);
      body.set("eventId", selectors.eventId);
      body.set("eventGuid", selectors.eventGuid);
      body.set("__RequestVerificationToken", csrf.value);
      return fetcher(url, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
        body: body.toString()
      }).then(function (response) {
        if (!response || response.redirected || response.status === 401 || response.status === 403 || response.status === 404) {
          throw new Error("not-authorized");
        }
        if (!response.ok) throw new Error("unconfirmed");
        return response.json();
      }).then(function (receipt) {
        var state = receipt && (receipt.state || receipt.State);
        if (!receipt || receipt.ok === false || states.indexOf(state) < 0) throw new Error("invalid-receipt");
        return state;
      });
    }

    function init() {
      if (!config.enabled || !document.querySelectorAll || typeof fetcher !== "function") return;
      Array.prototype.forEach.call(document.querySelectorAll("[data-route-dispatch-notice]"), function (panel) {
        var receipt = panel.querySelector("[data-route-dispatch-receipt]");
        var send = panel.querySelector("[data-route-dispatch-notify]");
        var status = panel.querySelector("[data-route-dispatch-notice-status]");
        var eligible = panel.getAttribute("data-notify-eligible") === "true";
        var currentState = null;
        var busy = false;
        if (send) { send.hidden = true; send.disabled = true; }

        function render(state) {
          currentState = states.indexOf(state) >= 0 ? state : null;
          var allowed = canContinue(currentState, eligible);
          if (status) status.textContent = messageFor(currentState);
          if (send) {
            send.hidden = !allowed;
            send.disabled = busy || !allowed;
            send.textContent = currentState === "None" ? "Enviar aviso" : "Continuar mismo intento";
          }
          if (!allowed && document.activeElement === send && status && typeof status.focus === "function") status.focus();
        }

        function setBusy(value, active) {
          busy = value;
          [receipt, send].forEach(function (button) {
            if (!button) return;
            button.disabled = value || button === send && !canContinue(currentState, eligible);
            if (button === active) {
              button.setAttribute("aria-busy", value ? "true" : "false");
              if (value) {
                button.dataset.previousText = button.textContent;
                button.textContent = button === receipt ? "Consultando…" : "Continuando…";
              } else if (button.dataset.previousText) {
                button.textContent = button.dataset.previousText;
                delete button.dataset.previousText;
              }
            }
          });
        }

        function run(url, button) {
          if (busy || button === send && !canContinue(currentState, eligible)) return;
          setBusy(true, button);
          var request;
          try { request = post(url, panel); }
          catch (_) { request = Promise.reject(new Error("invalid-context")); }
          Promise.resolve(request).then(function (state) {
            render(state);
            notify(messageFor(state));
          }).catch(function () {
            currentState = null;
            if (send) { send.hidden = true; send.disabled = true; }
            if (status) status.textContent = "No se confirmó el resultado. Consultá el estado manualmente antes de continuar.";
            if (document.activeElement === send && status && typeof status.focus === "function") status.focus();
            notify("No se confirmó el resultado. No se inició otro envío; consultá el estado manualmente.");
          }).then(function () { setBusy(false, button); });
        }

        if (receipt) receipt.addEventListener("click", function () { run(config.receiptUrl, receipt); });
        if (send) send.addEventListener("click", function () { run(config.notifyUrl, send); });
      });
    }

    return { init: init };
  }

  var api = Object.freeze({ canContinue: canContinue, messageFor: messageFor, createController: createController });
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (root) {
    root.RouteDispatchMailUiInternals = api;
    function start() {
      var config = root.routeDispatchMailConfig;
      if (!config || !config.enabled) return;
      createController({ document: root.document, fetch: root.fetch.bind(root), config: config }).init();
    }
    if (root.document && root.document.readyState === "loading") root.document.addEventListener("DOMContentLoaded", start);
    else if (root.document) start();
  }
})(typeof window !== "undefined" ? window : null);
