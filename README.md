## Table of Contents

- [Technology Stack](#technology-stack)
- [Features](#features)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Building](#building)
- [Running the Application](#running-the-application)
  - [1. ECU Simulator](#1-ecu-simulator)
  - [2. MAUI UI Application](#2-maui-ui-application)
- [Usage Workflow](#usage-workflow)
- [Architectural Highlights](#architectural-highlights)

## Technology Stack

This project is built on the cutting edge of the .NET ecosystem, prioritizing performance, maintainability, and a modern development experience.

- **.NET 9**: The underlying framework for all projects.
- **C# 13**: Leveraging the latest language features.
- **.NET MAUI**: For the cross-platform user interface, targeting Windows, Android, iOS, and macOS from a single codebase.
- **Blazor Hybrid**: Powers the user interface, allowing for web UI technologies within a native MAUI application shell.
- **Serilog**: For structured and configurable logging across all applications.

## Features

- **Cross-Platform**: Natively compiled application for Windows, Android, and other MAUI targets.
- **Dual Connectivity**: Seamlessly connect to ECUs via USB/Serial or TCP/IP.
- **ECU Simulator**: A full-featured TCP-based simulator for development and testing without physical hardware.
- **Dynamic Definition Loading**: Load and parse ECU definitions directly from `.mefw` or compatible XML files.
- **Real-time Dashboard**: Monitor critical engine parameters with a live-streaming data view.
- **Table Editing**: View and modify 2D and 3D tuning tables (e.g., VE, Ignition) with a color-scaled, interactive UI.
- **Driver Configuration**: Read and write driver parameters.
- **Decoupled Architecture**: Clean separation between UI, business logic, and communication layers.

## Project Structure

The solution is logically divided into several projects:

| Project                       | Description                                                                                                                             |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `ME221CrossApp.UI`              | The main .NET MAUI Blazor Hybrid application. This is the primary user-facing client.                                                   |
| `ME221CrossApp.Services`        | A shared class library containing services for ECU interaction, definition management, and communication protocols.                     |
| `ME221CrossApp.Models`          | Contains the shared data models (records) used across the entire solution, ensuring type safety and consistency.                        |
| `ME221CrossApp.EcuSimulator`    | A .NET 9 console application that simulates an ME221 ECU, listening for TCP connections and responding to protocol messages.            |
| `ME221CrossApp`                 | A .NET 9 console application demonstrating the use of the service layer for command-line based ECU interaction.                         |

## Getting Started

### Prerequisites

1.  **.NET 9 SDK**: Ensure you have the latest .NET 9 SDK installed.
2.  **.NET MAUI Workload**: Install the MAUI workload for .NET 9:
    ```bash
    dotnet workload install maui
    ```
3.  **IDE**: Visual Studio 2022 (latest preview) or JetBrains Rider (latest EAP).

### Building

Clone the repository and build the solution from the root directory:

```bash
git clone <repository-url>
cd ME221CrossApp
dotnet build
```

## Running the Application

### 1. ECU Simulator

For development without a physical ECU, first run the simulator. It will start a TCP server on `127.0.0.1:54321`.

```bash
dotnet run --project ME221CrossApp.EcuSimulator
```

The simulator uses the `ecu_definitions.json` file within its project directory to define its behavior and available objects.

### 2. MAUI UI Application

With the solution open in your IDE, set `ME221CrossApp.UI` as the startup project. Select your desired target (e.g., "Windows Machine", "Android Emulator", a physical Android device) and run the application.

Alternatively, you can run the application for a specific platform via the command line. For example, to run on Windows (adjust TFM if necessary):

```bash
dotnet build -t:Run -f net9.0-windows10.0.19041.0 --project ME221CrossApp.UI
```

## Usage Workflow

1.  **Start the Simulator**: Run the `ME221CrossApp.EcuSimulator` project.
2.  **Start the MAUI App**: Run the `ME221CrossApp.UI` project.
3.  **Load Definitions**:
    - Navigate to the **Settings** page in the UI.
    - Click "Load from .mefw file".
    - Select the `ecu_definitions.json` file located in the `ME221CrossApp.EcuSimulator` project folder. This simulates loading a real definition file.
4.  **Connect to ECU**:
    - Navigate back to the **Home** page.
    - The "Connect to Simulator (Debug)" input should be pre-filled with `127.0.0.1:54321`.
    - Click the "Connect" button.
5.  **View Data**:
    - The dashboard will appear, showing live-streaming data from the simulator.
    - Navigate to the **Tables** page.
    - Select a table from the dropdown to view and edit its data in 2D or 3D.

## Architectural Highlights

- **Communication Multiplexer**: The `CommunicationMux` service allows the application to switch between `ISerialPortCommunicator` and `ITcpPortCommunicator` implementations at runtime, providing a single `IDeviceCommunicator` interface to the rest of the application.
- **Asynchronous Protocol Handling**: The communication layer is fully asynchronous, using `IAsyncEnumerable<T>` and `System.Threading.Channels.Channel<T>` to handle incoming messages without blocking the UI or processing threads.
- **Centralized Connection State**: A singleton `ConnectionStateService` is used to manage and broadcast the application's connection status and ECU information, ensuring all Blazor components are aware of the current state.
- **Blazor Hybrid with Device-Specific Services**: The application uses .NET MAUI's dependency injection to provide platform-specific implementations (e.g., `AndroidDeviceDiscoveryService`, `WindowsDeviceCommunicator`) while sharing the majority of the UI and application logic.
- **Framed Protocol with Checksum**: Communication relies on a custom `[Sync1][Sync2][Size][Message][CRC]` framing protocol, with a Fletcher-16 checksum for data integrity, ensuring robust communication.