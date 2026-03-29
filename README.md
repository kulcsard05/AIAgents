## 🛠️ Prerequisites & Setup

This project requires **Ollama** to run local AI models. Follow the steps below to get started:

### 1. Install Ollama
Choose the command for your Operating System:

* **Windows (PowerShell):**
    ```powershell
    irm [https://ollama.com/install.ps1](https://ollama.com/install.ps1) | iex
    ```
* **Linux:**
    ```bash
    curl -fsSL [https://ollama.com/install.sh](https://ollama.com/install.sh) | sh
    ```
* **macOS:**
    ```bash
    curl -fsSL [https://ollama.com/install.sh](https://ollama.com/install.sh) | sh
    ```
* **Manual Download:** If the scripts fail, download the installer directly from [ollama.com/download](https://ollama.com/download).

### 2. Download and Run the Model
Once installed, you need to pull the **Llama 3.1** model (approximately 4.7 GB). Open your terminal and run:

```bash
  ollama run llama3.1
