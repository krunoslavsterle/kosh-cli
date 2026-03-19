import { api } from "../api/client";

class ServiceStore {
    data = null;
    listeners = new Set();

    constructor() {
        this.startPolling();
    }

    async startPolling() {
        const fetchStatus = async () => {
            try {
                const snapshot = await api("/status");
                this.data = snapshot;
                this.notify();
            } catch (err) {
                console.error("Status polling error:", err);
            }
        };

        // initial fetch
        await fetchStatus();

        // poll every 2 seconds
        this.interval = setInterval(fetchStatus, 2000);
    }

    subscribe(fn) {
        this.listeners.add(fn);
        fn(this.data); // send current state immediately
        return () => this.listeners.delete(fn);
    }

    notify() {
        for (const fn of this.listeners) {
            fn(this.data);
        }
    }
}

export const serviceStore = new ServiceStore();
