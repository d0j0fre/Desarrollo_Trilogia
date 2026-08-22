(() => {
  "use strict";

  const focusableSelector = [
    "a[href]", "button:not([disabled])", "input:not([disabled])", "select:not([disabled])",
    "textarea:not([disabled])", "[tabindex]:not([tabindex='-1'])"
  ].join(",");

  const setPageLocked = (locked) => document.documentElement.toggleAttribute("data-djj-scroll-locked", locked);

  const setupDrawer = (drawer) => {
    const openButton = document.querySelector(`[data-djj-drawer-open][aria-controls="${drawer.id}"]`);
    const closeButtons = drawer.querySelectorAll("[data-djj-drawer-close]");
    const scrim = document.querySelector(`[data-djj-drawer-scrim][aria-controls="${drawer.id}"]`);
    let returnFocus = null;

    const close = () => {
      drawer.dataset.open = "false";
      drawer.setAttribute("aria-hidden", "true");
      openButton?.setAttribute("aria-expanded", "false");
      if (scrim) scrim.hidden = true;
      setPageLocked(false);
      returnFocus?.focus();
    };

    const open = () => {
      returnFocus = document.activeElement;
      drawer.dataset.open = "true";
      drawer.setAttribute("aria-hidden", "false");
      openButton?.setAttribute("aria-expanded", "true");
      if (scrim) scrim.hidden = false;
      setPageLocked(true);
      drawer.querySelector(focusableSelector)?.focus();
    };

    openButton?.addEventListener("click", open);
    closeButtons.forEach((button) => button.addEventListener("click", close));
    scrim?.addEventListener("click", close);
    drawer.addEventListener("keydown", (event) => {
      if (event.key === "Escape") close();
      if (event.key !== "Tab") return;
      const focusable = [...drawer.querySelectorAll(focusableSelector)];
      if (!focusable.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    });
  };

  document.querySelectorAll("[data-djj-drawer]").forEach(setupDrawer);

  const workspaceSidebar = document.querySelector("[data-djj-workspace-sidebar]");
  const workspaceShell = document.querySelector("[data-djj-workspace-shell]");
  const workspaceOpen = document.querySelector("[data-djj-workspace-open]");
  const workspaceScrim = document.querySelector("[data-djj-workspace-scrim]");
  const workspaceCollapse = document.querySelector("[data-djj-workspace-collapse]");

  const closeWorkspace = () => {
    workspaceSidebar?.setAttribute("data-open", "false");
    workspaceOpen?.setAttribute("aria-expanded", "false");
    workspaceSidebar?.setAttribute("aria-hidden", window.matchMedia("(max-width: 63.99rem)").matches ? "true" : "false");
    if (workspaceScrim) workspaceScrim.hidden = true;
    setPageLocked(false);
  };

  workspaceOpen?.addEventListener("click", () => {
    workspaceSidebar?.setAttribute("data-open", "true");
    workspaceOpen.setAttribute("aria-expanded", "true");
    workspaceSidebar?.setAttribute("aria-hidden", "false");
    if (workspaceScrim) workspaceScrim.hidden = false;
    setPageLocked(true);
    workspaceSidebar?.querySelector(focusableSelector)?.focus();
  });
  workspaceScrim?.addEventListener("click", closeWorkspace);
  workspaceSidebar?.addEventListener("keydown", (event) => { if (event.key === "Escape") { closeWorkspace(); workspaceOpen?.focus(); } });

  workspaceCollapse?.addEventListener("click", () => {
    const collapsed = workspaceShell?.toggleAttribute("data-sidebar-collapsed") ?? false;
    workspaceCollapse.setAttribute("aria-expanded", String(!collapsed));
    workspaceCollapse.setAttribute("aria-label", collapsed ? "Expandir navegación" : "Contraer navegación");
  });

  const connection = document.querySelector("[data-djj-connection]");
  const updateConnection = () => {
    if (!connection) return;
    connection.dataset.online = String(navigator.onLine);
    connection.textContent = navigator.onLine ? "En línea" : "Sin conexión";
  };
  window.addEventListener("online", updateConnection);
  window.addEventListener("offline", updateConnection);
  updateConnection();

  document.querySelectorAll("form[data-djj-confirm]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (!window.confirm(form.dataset.djjConfirm)) event.preventDefault();
    });
  });

  const validationSummary = document.querySelector("[data-djj-validation-summary]");
  if (validationSummary?.textContent.trim()) validationSummary.focus();

  document.querySelectorAll("[data-djj-print]").forEach((button) => {
    button.addEventListener("click", () => window.print());
  });

  document.addEventListener("change", (event) => {
    const input = event.target.closest("input[type='file'][data-file-label]");
    if (!input) return;
    const label = document.getElementById(input.dataset.fileLabel);
    if (!label) return;
    const file = input.files?.[0];
    label.textContent = file ? `${file.name} · ${Math.max(1, Math.round(file.size / 1024))} KB` : "Ningún archivo seleccionado";
  });

  document.addEventListener("input", (event) => {
    const input = event.target.closest("[data-expense-money]");
    if (!input) return;
    const form = input.closest("form");
    const output = form?.querySelector("[data-expense-total]");
    if (!output) return;
    const total = [...form.querySelectorAll("[data-expense-money]")].reduce((sum, field) => {
      const value = Number.parseFloat(field.value);
      return Number.isFinite(value) && value > 0 ? sum + value : sum;
    }, 0);
    output.textContent = new Intl.NumberFormat("es-CR", { style: "currency", currency: "CRC" }).format(total);
  });
})();
