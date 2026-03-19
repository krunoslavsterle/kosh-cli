import { api } from "./client";

export function startService(id) {
    console.log(id);
  return api(`/services/${id.value}/start`, { method: "POST" });
}

export function stopService(id) {
    console.log(id);
  return api(`/services/${id.value}/stop`, { method: "POST" });
}
