import { useEffect, useState } from "react";
import { logStore } from "../state/logStore";

export function useLogs() {
  const [logs, setLogs] = useState(logStore.logs);

  // useEffect(() => {
  //   return logStore.subscribe(setLogs);
  // }, []);

  return logs;
}
