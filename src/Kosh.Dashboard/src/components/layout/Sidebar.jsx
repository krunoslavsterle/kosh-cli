import {
  Circle,
  Play,
  Square,
  RotateCw,
  ChevronDown,
  ChevronRight,
} from "lucide-react";

import { useServiceStatus } from "../../hooks/useServiceStatus";
import { stopService, startService } from "../../api/services";

export default function Sidebar({ currentView }) {
  const data = useServiceStatus();

  //   const [expandedGroups, setExpandedGroups] =
  //     useState < Set < string >> new Set(groups.map((g) => g.id));

  //   const toggleGroup = (groupId: string) => {
  //     setExpandedGroups((prev) => {
  //       const next = new Set(prev);
  //       if (next.has(groupId)) {
  //         next.delete(groupId);
  //       } else {
  //         next.add(groupId);
  //       }
  //       return next;
  //     });
  //   };

  const getStatusColor = (status) => {
    switch (status) {
      case "Running":
        return "text-[#4ec9b0]";
      case "Ready":
        return "text-[#569cd6]";
      case "NotStarted":
        return "text-[#6e7681]";
      case "Failed":
        return "text-[#f48771]";
      case "Stopped":
        return "text-[#f48771]";
      case "Starting":
        return "text-[#dcdcaa]";
      default:
        return "text-[#dcdcaa]";
    }
  };

  const getStatusIcon = (status) => {
    return (
      <Circle className={`w-2 h-2 ${getStatusColor(status)} fill-current`} />
    );
  };

  const formatUptime = (seconds) => {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  };

  return (
    <div className="w-72 border-r border-[#3e3e42] bg-[#252526] overflow-y-auto custom-scrollbar">
      <div className="p-3 border-b border-[#3e3e42]">
        <button
          // onClick={() => onViewChange("all")}
          className={`w-full text-left px-3 py-2 text-xs uppercase tracking-wider rounded transition-colors ${
            currentView === "all"
              ? "bg-[#37373d] text-[#ffffff]"
              : "text-[#cccccc] hover:bg-[#2a2d2e]"
          }`}
        >
          All Services
        </button>
      </div>

      <div className="py-2">
        {data?.groups.map((group) => {
          const groupServices = group.services;
          //const isExpanded = expandedGroups.has(group.id);
          const isExpanded = true;
          const runningCount = groupServices.filter(
            (s) => s.status === "Running",
          ).length;

          return (
            <div key={group.id} className="mb-1">
              <div
                className={`px-3 py-2 flex items-center justify-between cursor-pointer hover:bg-[#2a2d2e] ${
                  currentView === group.id ? "bg-[#37373d]" : ""
                }`}
                // onClick={() => onViewChange(group.id)}
              >
                <div className="flex items-center gap-3 flex-1">
                  <button
                    // onClick={(e) => {
                    //   e.stopPropagation();
                    //   toggleGroup(group.id);
                    // }}
                    className="text-[#858585] hover:text-[#cccccc]"
                  >
                    {isExpanded ? (
                      <ChevronDown className="w-3.5 h-3.5" />
                    ) : (
                      <ChevronRight className="w-3.5 h-3.5" />
                    )}
                  </button>

                  <span className="text-xs text-[#cccccc] uppercase tracking-wider">
                    {group.name}
                  </span>

                  <span className="text-[10px] text-[#858585]">
                    ({runningCount}/{groupServices.length})
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  {/* <button
                    onClick={(e) => {
                      e.stopPropagation();
                      onGroupAction(group.id, "start");
                    }}
                    className="p-1 text-[#4ec9b0] hover:bg-[#3e3e42] rounded"
                    title="Start all"
                  >
                    <Play className="w-3 h-3" />
                  </button> */}
                  {/* <button
                    onClick={(e) => {
                      e.stopPropagation();
                      onGroupAction(group.id, "stop");
                    }}
                    className="p-1 text-[#f48771] hover:bg-[#3e3e42] rounded"
                    title="Stop all"
                  >
                    <Square className="w-3 h-3" />
                  </button> */}
                </div>
              </div>

              {isExpanded && (
                <div className="ml-4">
                  {groupServices.map((service) => (
                    <div
                      key={service.id}
                      className={`px-3 py-2 flex items-center justify-between cursor-pointer hover:bg-[#2a2d2e] ${
                        currentView === service.id ? "bg-[#37373d]" : ""
                      }`}
                      //onClick={() => onViewChange(service.id)}
                    >
                      <div className="flex items-center gap-2 flex-1 min-w-0">
                        {getStatusIcon(service.status)}
                        <span className="text-xs text-[#cccccc] truncate">
                          {service.name}
                        </span>
                        {service.status === "running" && (
                          <span className="text-[10px] text-[#858585]">
                            {/* {formatUptime(service.uptime)} */}
                          </span>
                        )}
                      </div>
                      <div className="flex items-center gap-1">
                        {(service.status === "NotStarted" ||
                          service.status === "Failed" ||
                          service.status === "Stopped") && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              startService(service.id);
                            }}
                            className="p-1 text-[#4ec9b0] hover:bg-[#3e3e42] rounded"
                            title="Start"
                          >
                            <Play className="w-3 h-3" />
                          </button>
                        )}
                        {service.status === "Running" && (
                          <>
                            <button
                              onClick={(e) => {
                                e.stopPropagation();
                                stopService(service.id);
                              }}
                              className="p-1 text-[#f48771] hover:bg-[#3e3e42] rounded"
                              title="Stop"
                            >
                              <Square className="w-3 h-3" />
                            </button>
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
