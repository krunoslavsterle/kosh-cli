// copy-build.js
const fs = require("fs");
const path = require("path");

// Paths
const dashboardDist = path.join(__dirname, "src", "Kosh.Dashboard", "dist");
const apiDashboard = path.join(__dirname, "src", "Kosh.Api", "dashboard");

// Helper: delete folder recursively
function deleteFolderRecursive(folderPath) {
    if (fs.existsSync(folderPath)) {
        fs.rmSync(folderPath, { recursive: true, force: true });
        console.log(`Deleted: ${folderPath}`);
    }
}

// Helper: copy folder recursively
function copyRecursive(src, dest) {
    if (!fs.existsSync(dest)) {
        fs.mkdirSync(dest, { recursive: true });
    }

    for (const item of fs.readdirSync(src)) {
        const srcPath = path.join(src, item);
        const destPath = path.join(dest, item);

        if (fs.lstatSync(srcPath).isDirectory()) {
            copyRecursive(srcPath, destPath);
        } else {
            fs.copyFileSync(srcPath, destPath);
        }
    }
}

// 1) Ensure dist exists
if (!fs.existsSync(dashboardDist)) {
    console.error("❌ ERROR: dist/ folder not found. Did you run `npm run build` in Kosh.Dashboard?");
    process.exit(1);
}

// 2) Delete old dashboard
deleteFolderRecursive(apiDashboard);

// 3) Copy new build
copyRecursive(dashboardDist, apiDashboard);

console.log("✅ Dashboard build copied to Kosh.Api/dashboard");
