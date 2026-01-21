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

- .NET Runtime **8.0 or later**  
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
---

### 1) projectName
Human‑readable name of the project.
Displayed in console logs.

---

### 2) services
A list of all services that kosh will start. Each service entry contains:

| Field     | Req | Description                                                             |
|-----------|-----|-------------------------------------------------------------------------|
| **name**  | Y   | Unique identifier for the service (displayed in console logs)           |
| **type**  | Y   | Service Runner type (defines how the service is started)                |
| **path**  | Y   | Working directory of the service relative to the `koshconfig.yaml` file |
| **args**  | N   | Additional arguments passed to the runner                               |
| **env**   | N   | Environment variables passed to the runner                              |
| **logs**  | N   | Whether logs should be streamed to the terminal                         |

---

### 3) hosts

Defines a local development domains that can be used by reverse proxy. It will insert these domains to the **.hosts** file. On Linux/MacOS it will ask you for the user password to do that and on Windows it will
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

****NOTE:*** `dotnet-run` is currently the only service that supports `globbing` directory or file pattern matching. In the example above
all migration projects that matches the provided pattern will be executed in parallel and execution of the registered services will be
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

Runs `dotnet watch run` with **hot reload** enabled by default. To disable **hot reload** pass the '--no-hot-reload' to the args

---

### 5) dotnet watch Alternative

```yaml
- name: api
  type: dotnet-watch-alt
  path: ./src/apps/KoshTestProject.Api
  args: "--urls http://localhost:6001"
  env:
    ASPNETCORE_ENVIRONMENT: Development
```

Runs **kosh** alternative implementation for the `dotnet watch run`. On some occasions I had an issue with running `dotnet watch run` as a child 
process of a console. Because of that I created an alternative mechanism that uses `dotnet run` and restarts when a change on the project DLL file
is detected. That means, it will restart the service when you rebuild the project after making a change in the code.

---

### 6) Node application

```yaml
- name: frontend-react
  type: node
  path: ./src/apps/kosh-test-project-react
```

Runs a **Node-based** application using the `npm run` command (React, Angular, Next.js, etc.).

***NOTE:** by default (if no **args** are provided) it will run using the **dev** arg like this: `npm run dev` but you can override it using the **args** field.

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

kosh will launch every service defined in koshconfig.yaml and show logs in the same terminal.

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

# 🗑️ Uninstalling kosh

If you ever want to remove the tool:

```bash
dotnet tool uninstall -g kosh
```

---