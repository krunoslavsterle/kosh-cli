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
- restarts services automatically on file changes
- works the same on Linux, macOS, and Windows
- requires minimal configuration

---

# Installation Guide

kosh is distributed as a **.NET global tool** via NuGet.org.  
This makes installation, updates, and removal extremely simple and fully cross‑platform.

## 📦 Prerequisites

You need:

- .NET Runtime **10.0 or later**  
  Download: https://dotnet.microsoft.com/download

Check your version:

```bash
dotnet --version
```

---

## 🚀 Installing kosh

Install the tool globally:

```bash
dotnet tool install -g kosh
```

Check the version

```
kosh version
```

If the command is recognized, you're ready to go.

***NOTE:**

- After installation, ensure your .dotnet/tools directory is in your PATH.
- On most systems, .NET adds this automatically.
- kosh works on Linux, macOS, and Windows.

---

# 📦 Project Configuration - koshconfig.yaml

`koshconfig.yaml` is the central configuration file used by kosh.

It defines:

- the project name
- all services that kosh should start
- local development domains

kosh reads this file on every command execution and uses it to orchestrate your entire development environment.

***NOTE:** Everything in **koshconfig.yaml** is optional except `projectName`

---

## 🧱 File Structure Overview

```yaml
projectName: Kosh Example

services:
  - name: infra
    type: docker-compose
    path: ./devops/local
    logs: none

  - name: gateway
    type: caddy
    logs: error
    path: ./devops/local
    args: "--config Caddyfile"

  - name: core-migration
    type: dotnet-run
    path: ./src/apps/KoshTestProject.Console
    inheritEnv: true

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
    type: npm
    path: ./src/apps/kosh-test-project-react

hosts:
  - domain: kosh-test.api.localhost
  - domain: kosh-test.localhost
```

---

### 1) projectName

Human‑readable name of the project.
Displayed in console logs.

---

### 2) services

A list of all services that kosh will start. Each service entry contains:

| Field          | Req | Description                                                                                                |
|----------------|-----|------------------------------------------------------------------------------------------------------------|
| **name**       | Y   | Unique identifier for the service (displayed in console logs)                                              |
| **type**       | Y   | Service Runner type (defines how the service is started)                                                   |
| **path**       | Y   | Working directory of the service relative to the `koshconfig.yaml` file                                    |
| **args**       | N   | Additional arguments passed to the runner                                                                  |
| **env**        | N   | Environment variables passed to the runner                                                                 |
| **inheritEnv** | N   | Flag indicating should a service inherit environment variables from a global `.env` (**false** by default) |
| **logs**       | N   | Kind of logs that should be streamed to the terminal [none, error, all] (**all** by default)               |
| **manualStart**| N   | If true, the service will not start automatically; it must be started manually (**false** by default)      |

---

### 3) hosts

Defines a local development domains that can be used by reverse proxy. It will insert these domains to the **.hosts**
file. On Linux/MacOS it will ask you for the user password to do that and on Windows it will
open the confirmation window.

---

## 🔧 Service Examples

### 1) Docker Compose service

``` yaml
- name: infra
  type: docker-compose
  path: ./devops/local
  logs: false
```

Runs docker-compose up inside the specified directory and shows only error logs in the console.

Useful for local infrastructure setup.

---

### 2) Caddy reverse proxy

```yaml
- name: gateway
  type: caddy
  logs: false
  path: ./devops/local
  args: "--config Caddyfile"
```

Starts Caddy with a custom configuration file (Caddyfile) that is located in the specified directory.

I like to use it this way because it will handle the local ssl certificates automatically.

---

### 3) dotnet run (one‑off execution)

```yaml
  - name: Migrations
    type: dotnet-run
    path: ./src/apps/KoshTest.*.Migrations
```

Runs `dotnet run` once and will pause with the services execution until it is completed.

Ideal for migrations and similar jobs.

****NOTE:*** `dotnet-run` is currently the only service that supports `globbing` directory or file pattern matching. In
the example above
all migration projects that matches the provided pattern will be executed in parallel and execution of the registered
services will be
stopped until all migrations are completed successfully.

---

### 4) dotnet watch with Hot Reload

```yaml
- name: api
  type: dotnet-watch
  path: ./src/apps/KoshTestProject.Api
  args: "--urls http://localhost:6001"
  env:
    ASPNETCORE_ENVIRONMENT: Development
```

Runs `dotnet watch run` with **hot reload** enabled by default. To disable **hot reload** pass the '--no-hot-reload' to
the args

---

### 5) Node application

```yaml
- name: frontend-react
  type: node
  path: ./src/apps/kosh-test-project-react
```

Runs a **Node-based** application using the `npm run` command (React, Angular, Next.js, etc.).

***NOTE:** by default (if no **args** are provided) it will run using the **dev** arg like this: `npm run dev` but you
can override it using the **args** field.

---

# 🌱 Environment Variables

**kosh** supports three sources of environment variables (all are optional):

- environment variables defined directly in `koshconfig.yaml`
- `.env` file located inside each service’s working directory
- global `.env` file located in the root of the project (applied for services with flag `inheritEnv: true`)

Environment variables from these sources are merged in a deterministic order to ensure predictable behavior.

---

## 1. Environment variables in koshconfig.yaml

Each service can define its own environment variables directly in the configuration:

```yaml
services:
  - name: api
    type: dotnet-watch
    path: ./src/apps/Api
    env:
      ASPNETCORE_ENVIRONMENT: Development
      API_PORT: "6001"

```

***NOTE:** These have the **highest priority** in case you define same variable from multiple different sources.

---

## 2. Service local .env file (applied automatically)

If a service’s working directory contains a `.env` file, **kosh** automatically loads it and applies its variables to
that service:

`src/apps/Api/.env`

These variables are applied **after** environment variables defined in the `koshconfig.yaml` (**only if the variable
does not already exist**).

---

## 3. Global .env file (opt‑in)

If the root directory of your solution contains a .env file, kosh loads it into memory:

```
/.env
/koshconfig.yaml
```

A service will inherit variables from the global `.env` only if it explicitly enables it in `koshconfig.yaml` with a
flag: `inheritEnv: true`

Example:

```yaml
services:
  - name: api
    type: dotnet-watch
    path: ./src/apps/Api
    inheritEnv: true
```

Global `.env` variables have the **lowest priority** and are applied only if the service does not already define a
variable with the same name.

---

# 🚀 Usage Guide (Step‑by‑Step)

## 1. Create and configure koshconfig.yaml

In the root directory of your solution, create a file named:

```
koshconfig.yaml
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

kosh will launch every service defined in koshconfig.yaml and open an **Interactive Terminal Dashboard (TUI)**.

### 🖥️ Interactive Dashboard (TUI) Features

The new dashboard gives you complete control over your running services:

- **Service Status Overview**: Instantly see which services are running, starting, ready, or stopped.
- **Expanded View**: Press `S` to toggle an expanded table showing more details (Group, Port, and ManualStart).
- **Log Management**:
  - Smooth scrolling via Mouse Wheel or Touchpad.
  - Native text selection (`Shift + Drag`).
  - Clear logs instantly with `C`.
- **Command Palette**: Press `:` to open the command input. Available commands:
  - `find <query>`: Pure substring search across all current logs (creates a dedicated view showing only matching logs).
  - `view all`: Reset view to show all logs.
  - *(More commands coming soon!)*
- **Shortcuts**:
  - `C`: Clear Logs
  - `S`: Expand/Compact Services
  - `Q`: Quit
  - `H`: Help Dialog

## 4. Stop all services

To stop everything, simply press:

```
CTRL + C
```

# 🔄 Updating kosh

To update to the latest version:

```bash
dotnet tool update -g kosh
```

---

# 🎮 Demo Project

This repository includes a full-featured demo project located in the `demo` folder. It serves as a practical showcase of everything **kosh** can do. 

The demo orchestrates a complex, multi-service architecture including:
- **Infrastructure (`docker-compose`)**: Background infrastructure and services.
- **Reverse Proxy (`caddy`)**: Gateway handling local SSL certificates and routing.
- **Database Migrations (`dotnet-run`)**: One-off execution scripts that pause other services until completed.
- **Backend API (`dotnet-watch`)**: An ASP.NET Core API with hot-reload enabled.
- **Background Worker (`dotnet-watch`)**: A dotnet worker with the `manualStart` flag enabled.
- **Frontend Clients (`npm`)**: React and Angular applications running simultaneously.

### How to run the Demo

To experience **kosh** in action, clone this repository and follow these steps:

1. Navigate to the `demo` directory:
   ```bash
   cd demo
   ```
2. Run kosh:
   ```bash
   kosh start
   ```

*(Note: Since the demo configures local domains in your `.hosts` file, you may be prompted for an administrator password when Caddy or Kosh initializes the network).*

---

# 🗑️ Uninstalling kosh

If you ever want to remove the tool:

```bash
dotnet tool uninstall -g kosh
```

---