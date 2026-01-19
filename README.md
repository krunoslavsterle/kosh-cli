# kosh

**kosh** is a lightweight, fast, and developer-friendly tool for running and orchestrating multiple services (
docker-compose, dotnet, node, caddy).  
It was created to solve a simple problem: **smaller projects often need a quick, zero‑friction way to start all their
services at once**, without complex scripts, multiple terminals, or heavy tooling.

---

## ✨ Why kosh exists

Large orchestration tools (like Aspire, Nx, etc.) are powerful but often overkill for smaller projects.  
I needed something that:

- initiates my local development environment (set domains in .hosts file, start docker-compose for infrasturcture, run
  migrations)
- starts all services with one command
- shows logs in a clean, unified console
- restarts services automatically when files change (project rebuilds on .Net)
- works the same on Linux, macOS, and Windows
- requires minimal configuration

---

# Installation Guide

kosh is distributed as a **.NET global tool** via NuGet.org.  
This makes installation, updates, and removal extremely simple and fully cross‑platform.

## 📦 Prerequisites

You need:

- .NET Runtime **8.0 or later**  
  Download: https://dotnet.microsoft.com/download

Check your version:

```bash
dotnet --version
```

## 🚀 Installing kosh

Install the tool globally:

```bash
dotnet tool install -g kosh
```

If the command is recognized, you're ready to go.

## 🔄 Updating kosh

To update to the latest version:

```bash
dotnet tool update -g kosh --prerelease
```

## 🗑️ Uninstalling kosh

If you ever want to remove the tool:

```bash
dotnet tool uninstall -g kosh
```

## 📝 Notes

- After installation, ensure your .dotnet/tools directory is in your PATH.
- On most systems, .NET adds this automatically.
- kosh works on Linux, macOS, and Windows.

# 🚀 Usage Guide (Step‑by‑Step)

## 1. Create and configure koshconfig.yaml

In the root directory of your solution, create a file named:

```
koshconfig.yaml
```

Configure all services manually.
Example:

```yaml
projectName: Kosh Demo Project

services:
  - name: infra 
    type: docker-compose 
    path: ./devops/local
    logs: true
    
  - name: gateway
    type: caddy
    logs: false
    path: ./devops/local
    args: "--config Caddyfile"   
    
  - name: core-migration
    type: dotnet-run
    path: ./src/apps/KoshTestProject.Console 
        
  - name: api
    type: dotnet-watch
    path: ./src/apps/KoshTestProject.Api
    args: "--urls http://localhost:6001"
    env:
      ASPNETCORE_ENVIRONMENT: Development
      
  - name: admin-api
    type: dotnet-watch
    path: ./src/apps/KoshTestProject.Admin.Api
    args: "--urls http://localhost:6002"
    env:
      ASPNETCORE_ENVIRONMENT: Development
      
  - name: frontend-react
    type: node
    path: ./src/apps/kosh-test-project-react
      
hosts:
  - domain: kosh-test.api.localhost
  - domain: kosh-test.localhost
```

## 2. Navigate to the solution root

Open your terminal and move to the directory where koshconfig.yaml is located:

```
cd path/to/your/solution
```

## 3. Start orchestration

Run:

```
kosh start
```

kosh will launch every service defined in koshconfig.yaml and show logs in the same terminal.

## 4. Stop all services

To stop everything, simply press:

```
CTRL + C
```