import {
  Cpu,
  MemoryStick,
  Clock,
  Monitor,
  Search,
  PanelLeftClose,
  PanelLeft,
} from "lucide-react";

import { useServiceStatus } from "../../hooks/useServiceStatus";

export default function Header() {
  const data = useServiceStatus();

  const formatUptime = (seconds) => {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    return `${hours.toString().padStart(2, "0")}:${minutes.toString().padStart(2, "0")}:${secs.toString().padStart(2, "0")}`;
  };

  //   const runningCount = services.filter((s) => s.status === "running").length;
  //   const stoppedCount = services.filter((s) => s.status === "stopped").length;
  //   const errorCount = services.filter((s) => s.status === "error").length;

  const runningCount = 3;
  const stoppedCount = 1;
  const errorCount = 0;
  const isServicePanelOpen = true;
  const cpuUsage = data?.systemStatus?.cpuPercentage;
  const memoryUsage = data?.systemStatus?.memoryUsage;
  const uptime = 321;
  const viewMode = "live";

  return (
    <div className="border-b border-[#3e3e42] bg-[#2d2d30]">
      {/* Main Status Bar */}
      <div className="px-4 py-2 flex items-center gap-8 text-xs">
        <button
          //   onClick={onToggleServicePanel}
          className="text-[#cccccc] hover:text-white transition-colors p-1 hover:bg-[#3e3e42] rounded"
          title={
            isServicePanelOpen ? "Hide services panel" : "Show services panel"
          }
        >
          {isServicePanelOpen ? (
            <PanelLeftClose className="w-4 h-4" />
          ) : (
            <PanelLeft className="w-4 h-4" />
          )}
        </button>

        <div className="flex items-center gap-3">
          <span className="text-[#858585] uppercase tracking-wider">
            Services:
          </span>
          <span className="text-[#4ec9b0]">{runningCount} Running</span>
          <span className="text-[#858585]">{stoppedCount} Stopped</span>
          {errorCount > 0 && (
            <span className="text-[#f48771]">{errorCount} Error</span>
          )}
        </div>

        <div className="h-4 w-px bg-[#3e3e42]" />

        <div className="flex items-center gap-2">
          <Cpu className="w-4 h-4 text-[#569cd6]" />
          <span className="text-[#9cdcfe]">CPU:</span>
          <span className="text-[#d4d4d4] font-semibold">
            {cpuUsage?.toFixed(1)}%
          </span>
          <div className="w-20 h-2 bg-[#3e3e42] border border-[#555555] ml-1">
            <div
              className="h-full bg-[#569cd6] transition-all duration-300"
              style={{ width: `${cpuUsage}%` }}
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <MemoryStick className="w-4 h-4 text-[#569cd6]" />
          <span className="text-[#9cdcfe]">MEM:</span>
          <span className="text-[#d4d4d4] font-semibold">
            {memoryUsage?.toFixed(0)} MB
          </span>
          <div className="w-20 h-2 bg-[#3e3e42] border border-[#555555] ml-1">
            <div
              className="h-full bg-[#569cd6] transition-all duration-300"
              style={{
                width: `${(memoryUsage / data?.systemStatus.totalMemory) * 100}%`,
              }}
            />
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Clock className="w-4 h-4 text-[#569cd6]" />
          <span className="text-[#9cdcfe]">UPTIME:</span>
          <span className="text-[#d4d4d4] font-semibold">
            {formatUptime(uptime)}
          </span>
        </div>

        <div className="ml-auto flex items-center gap-4">
          <div className="flex items-center gap-2 border border-[#3e3e42] rounded">
            <button
              //  onClick={() => onViewModeChange("live")}
              className={`flex items-center gap-2 px-3 py-1 text-xs uppercase tracking-wider transition-colors ${
                viewMode === "live"
                  ? "bg-[#007acc] text-white"
                  : "text-[#cccccc] hover:text-white"
              }`}
            >
              <Monitor className="w-3.5 h-3.5" />
              Live
            </button>
            <button
              // onClick={() => onViewModeChange("search")}
              className={`flex items-center gap-2 px-3 py-1 text-xs uppercase tracking-wider transition-colors ${
                viewMode === "search"
                  ? "bg-[#007acc] text-white"
                  : "text-[#cccccc] hover:text-white"
              }`}
            >
              <Search className="w-3.5 h-3.5" />
              Search
            </button>
          </div>
          <span className="text-[#858585]">KOSH v1.0.0</span>
        </div>
      </div>
    </div>
  );
}
