import Header from "../components/layout/Header";
import Sidebar from "../components/layout/Sidebar";
import { LogViewer } from "../components/LogViewer";

export default function HomeView() {
  return (
    <div className="h-screen flex flex-col bg-[#1e1e1e] text-[#d4d4d4] font-mono text-md">
      <Header></Header>
      <div className="flex-1 flex overflow-hidden">
        <Sidebar currentView={"all"}></Sidebar>

        <LogViewer />
      </div>
    </div>
  );
}
