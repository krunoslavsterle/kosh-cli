import { useEffect, useRef } from "react";
import { useLogs } from "../hooks/useLogs";

export function LogViewer() {
  const logEndRef = useRef < HTMLDivElement > null;
  const logs = useLogs();

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [logs, logEndRef]);

  const getLevelColor = (level) => {
    switch (level) {
      case "error":
        return "text-[#f48771]";
      case "warn":
        return "text-[#dcdcaa]";
      case "success":
        return "text-[#4ec9b0]";
      case "info":
      default:
        return "text-[#569cd6]";
    }
  };

  const getLevelBadge = (level) => {
    const badge = level.toUpperCase().padEnd(7);
    return badge;
  };

  return (
    <div className="flex-1 overflow-y-auto px-4 py-2 bg-[#1e1e1e] custom-scrollbar">
      <div className="space-y-0.5">
        {/* {logs.map((log) => (
          <div
            key={log.id}
            className="flex items-start gap-3 text-xs font-mono"
          >
            <span className="text-[#9cdcfe] flex-shrink-0 uppercase">
              [{log.service}]
            </span>
            <span className={`flex-shrink-0 w-16 ${getLevelColor(log.level)}`}>
              {getLevelBadge(log.level)}
            </span>
            <span className="text-[#d4d4d4] flex-1">{log.message}</span>
          </div>
        ))} */}
        <div ref={logEndRef} />
      </div>
    </div>
  );
}
