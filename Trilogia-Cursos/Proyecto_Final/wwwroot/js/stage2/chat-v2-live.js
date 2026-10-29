'use strict';
(function (root, factory) {
    const Live = factory();
    if (typeof module === 'object' && module.exports) { module.exports = { Live }; return; }
    const element = document.querySelector('[data-djj-chat-v2]');
    if (!element?.dataset.liveUrl || !root.signalR) return;
    const status = element.querySelector('#chat-v2-live-status');
    root.ChatV2Live = new Live(() => new root.signalR.HubConnectionBuilder()
        .withUrl(element.dataset.liveUrl, { transport: root.signalR.HttpTransportType.WebSockets })
        .withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(root.signalR.LogLevel.None).build(),
        text => { if (status) status.textContent = text; },
        () => document.dispatchEvent(new Event('djj:chat-v2-invalidate')));
    window.addEventListener('pagehide', () => root.ChatV2Live.stop(), { once: true });
})(globalThis, () => {
    class Live {
        constructor(factory, status, invalidate) {
            this.factory = factory; this.status = status; this.invalidate = invalidate;
            this.connection = null; this.desired = null; this.subscribed = null;
            this.key = ''; this.version = 0; this.closed = false; this.chain = Promise.resolve();
        }
        select(domain, resource) {
            if (this.closed) return this.chain;
            const selected = domain === 'P' && Number.isInteger(resource) && resource > 0 && resource <= 2147483647 ? resource : null;
            const key = selected === null ? '' : `P:${selected}`;
            if (this.key === key) return this.chain;
            this.key = key; this.desired = selected; this.version++;
            return this.enqueue();
        }
        enqueue() {
            this.chain = this.chain.then(() => this.synchronize()).catch(() => {
                if (!this.closed) this.status('Actualización periódica activa. Tiempo real no disponible.');
            });
            return this.chain;
        }
        async synchronize() {
            if (this.closed) return;
            if (this.desired === null) {
                if (this.connection && this.subscribed !== null) await this.connection.invoke('Unsubscribe');
                this.subscribed = null; return;
            }
            if (!this.connection) {
                const connection = this.factory(); this.connection = connection;
                connection.on('Invalidate', (...args) => { if (!this.closed && this.connection === connection && this.desired !== null && args.length === 0) this.invalidate(); });
                connection.onreconnecting(() => { if (this.connection !== connection) return; this.subscribed = null; this.status('Reconectando. La actualización periódica continúa.'); });
                connection.onreconnected(() => { if (this.connection !== connection) return; this.subscribed = null; void this.enqueue(); });
                connection.onclose(() => { if (this.connection !== connection) return; this.subscribed = null; if (!this.closed) this.status('Actualización periódica activa. Tiempo real no disponible.'); });
                await connection.start();
            }
            if (this.closed || this.desired === null) return;
            const selection = this.desired, version = this.version;
            if (this.subscribed !== selection) await this.connection.invoke('Subscribe', 'P', selection);
            if (!this.closed && version === this.version) {
                this.subscribed = selection; this.status('Tiempo real conectado. Actualización periódica de respaldo activa.');
            }
        }
        retry() {
            if (this.closed || !this.desired) return this.chain;
            if (this.connection?.state === 'Disconnected') this.connection = null;
            return this.enqueue();
        }
        async stop() {
            if (this.closed) return;
            this.closed = true; this.version++; this.desired = null; this.subscribed = null;
            try { await this.connection?.stop(); } catch { /* No data or authentication fallback. */ }
        }
    }
    return Live;
});
