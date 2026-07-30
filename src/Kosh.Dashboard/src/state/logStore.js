class LogStore {
  logs = [];
  listeners = new Set();

  constructor() {
    this.init();
  }

  async init() {
    // 1) initial fetch (svi logovi od početka)
    // const initial = await api("/logs");
    // this.logs = initial;
    // this.notify();

    // 2) SSE stream za nove logove
    const es = new EventSource("/api/logs/stream");

    es.onmessage = (e) => {
      console.log("RAW SSE DATA:", e.data);
      const data = JSON.parse(e.data);

      if (Array.isArray(data)) {
        // initial logs
        this.logs = data;
        this.notify();
      } else {
        // single log
        this.logs.push(data);
        this.notify();
      }
    };

    es.onerror = (e) => {
      console.error("SSE error: ", e);
      es.close();
    };
  }

  subscribe(fn) {
    this.listeners.add(fn);
    fn(this.logs); // odmah pošalji trenutno stanje
    return () => this.listeners.delete(fn);
  }

  notify() {
    for (const fn of this.listeners) {
      fn(this.logs);
    }
  }
}

export const logStore = new LogStore();
